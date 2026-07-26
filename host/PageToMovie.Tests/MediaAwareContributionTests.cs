using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class MediaAwareContributionTests
{
    private static string MakeTempDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), prefix + "_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task ComputeDiffAsync_Extracts_MediaClips_And_Calculates_Hashes()
    {
        var targetDir = MakeTempDir("ptm_media_diff_target");
        var originDir = MakeTempDir("ptm_media_diff_origin");
        try
        {
            // Create target blueprint with scene 1 clip 1
            var blueprintJson = @"{
                ""scenes"": [
                    {
                        ""scene_index"": 1,
                        ""veo_clips"": [
                            {
                                ""clip_index"": 1,
                                ""relative_path"": ""assets/video/scene_01_clip_01.mp4"",
                                ""video_url"": ""https://cdn.example.com/clip1.mp4""
                            }
                        ]
                    }
                ]
            }";
            await File.WriteAllTextAsync(Path.Combine(targetDir, "blueprint.clips.grok.json"), blueprintJson);
            await File.WriteAllTextAsync(Path.Combine(originDir, "blueprint.clips.grok.json"), blueprintJson);

            // Write clip video file in target directory
            var clipDir = Path.Combine(targetDir, "assets", "video");
            Directory.CreateDirectory(clipDir);
            var clipFile = Path.Combine(clipDir, "scene_01_clip_01.mp4");
            var clipBytes = Encoding.UTF8.GetBytes("fake_video_bytes_scene1");
            await File.WriteAllBytesAsync(clipFile, clipBytes);
            var expectedHash = Convert.ToHexString(SHA256.HashData(clipBytes)).ToLowerInvariant();

            var service = new ProjectContributionService(NullLogger<ProjectContributionService>.Instance);
            var diff = await service.ComputeDiffAsync("fork1", "orig1", targetDir, originDir);

            Assert.NotNull(diff.MediaClips);
            Assert.Single(diff.MediaClips);

            var clip = diff.MediaClips[0];
            Assert.Equal(1, clip.SceneIndex);
            Assert.Equal(1, clip.ClipIndex);
            Assert.Equal(expectedHash, clip.Sha256);
            Assert.Equal("CdnAvailable", clip.Status); // Not in origin, CDN url exists
        }
        finally
        {
            if (Directory.Exists(targetDir)) try { Directory.Delete(targetDir, true); } catch { }
            if (Directory.Exists(originDir)) try { Directory.Delete(originDir, true); } catch { }
        }
    }

    [Fact]
    public async Task SyncContributionMediaAsync_Syncs_Missing_Clips_Via_Local_Proxy_And_Verifies_Hash()
    {
        var targetDir = MakeTempDir("ptm_media_sync_target");
        var originDir = MakeTempDir("ptm_media_sync_origin");
        try
        {
            var clipBytes = Encoding.UTF8.GetBytes("fake_clip_data_12345");
            var expectedHash = Convert.ToHexString(SHA256.HashData(clipBytes)).ToLowerInvariant();

            // Write target blueprint referencing clip 1
            var blueprintJson = $"{{\n  \"scenes\": [\n    {{\n      \"scene_index\": 1,\n      \"veo_clips\": [\n        {{\n          \"clip_index\": 1,\n          \"sha256\": \"{expectedHash}\",\n          \"relative_path\": \"assets/video/scene_01_clip_01.mp4\"\n        }}\n      ]\n    }}\n  ]\n}}";
            await File.WriteAllTextAsync(Path.Combine(targetDir, "blueprint.clips.grok.json"), blueprintJson);

            // Write clip video file in target
            var targetClipDir = Path.Combine(targetDir, "assets", "video");
            Directory.CreateDirectory(targetClipDir);
            await File.WriteAllBytesAsync(Path.Combine(targetClipDir, "scene_01_clip_01.mp4"), clipBytes);

            var service = new ProjectContributionService(NullLogger<ProjectContributionService>.Instance);
            var result = await service.SyncContributionMediaAsync(targetDir, originDir);

            Assert.Equal(1, result.SyncedCount);
            Assert.Equal(1, result.LocalCopyCount);
            Assert.Equal(1, result.VerifiedCount);
            Assert.Empty(result.Errors);

            // Verify origin now has the clip file with matching content
            var originClipPath = Path.Combine(originDir, "assets", "video", "scene_01_clip_01.mp4");
            Assert.True(File.Exists(originClipPath));
            var copiedBytes = await File.ReadAllBytesAsync(originClipPath);
            Assert.Equal(clipBytes, copiedBytes);
        }
        finally
        {
            if (Directory.Exists(targetDir)) try { Directory.Delete(targetDir, true); } catch { }
            if (Directory.Exists(originDir)) try { Directory.Delete(originDir, true); } catch { }
        }
    }

    [Fact]
    public async Task SyncContributionMediaAsync_Downloads_From_Provider_CDN_With_Hash_Verification()
    {
        var targetDir = MakeTempDir("ptm_cdn_sync_target");
        var originDir = MakeTempDir("ptm_cdn_sync_origin");
        try
        {
            var clipBytes = Encoding.UTF8.GetBytes("cdn_video_content_abc");
            var expectedHash = Convert.ToHexString(SHA256.HashData(clipBytes)).ToLowerInvariant();

            var blueprintJson = $"{{\n  \"scenes\": [\n    {{\n      \"scene_index\": 1,\n      \"veo_clips\": [\n        {{\n          \"clip_index\": 1,\n          \"sha256\": \"{expectedHash}\",\n          \"video_url\": \"https://mock.cdn.test/clip.mp4\",\n          \"relative_path\": \"assets/video/scene_01_clip_01.mp4\"\n        }}\n      ]\n    }}\n  ]\n}}";
            await File.WriteAllTextAsync(Path.Combine(targetDir, "blueprint.clips.grok.json"), blueprintJson);

            // Mock HttpClient responding with clipBytes
            var handler = new MockCdnHttpMessageHandler(clipBytes);
            using var http = new HttpClient(handler);

            var service = new ProjectContributionService(NullLogger<ProjectContributionService>.Instance);
            var result = await service.SyncContributionMediaAsync(targetDir, originDir, httpClient: http);

            Assert.Equal(1, result.SyncedCount);
            Assert.Equal(1, result.CdnDownloadCount);
            Assert.Equal(1, result.VerifiedCount);

            var originClipPath = Path.Combine(originDir, "assets", "video", "scene_01_clip_01.mp4");
            Assert.True(File.Exists(originClipPath));
        }
        finally
        {
            if (Directory.Exists(targetDir)) try { Directory.Delete(targetDir, true); } catch { }
            if (Directory.Exists(originDir)) try { Directory.Delete(originDir, true); } catch { }
        }
    }

    private sealed class MockCdnHttpMessageHandler : HttpMessageHandler
    {
        private readonly byte[] _bytes;
        public MockCdnHttpMessageHandler(byte[] bytes) => _bytes = bytes;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_bytes)
            };
            return Task.FromResult(res);
        }
    }
}
