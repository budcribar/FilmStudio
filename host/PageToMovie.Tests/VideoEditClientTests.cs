using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PageToMovie.Core.Models;
using PageToMovie.Core.Options;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

/// <summary>
/// Prompt-based clip edit (xAI /v1/videos/edits) — catalog wiring, the no-key IsConfigured
/// contract, and the file_id-first-with-base64-fallback submit logic (mocked HTTP, no network —
/// see LiveApi/README.md for the paid-call opt-in convention). CreditsSceneVideoGenGuardApiTests.cs
/// established the fakes-mode job-pipeline pattern this session; VideoEditApiTests.cs applies it
/// to the whole video-edit job end-to-end. This file is the fast, isolated unit-test layer under
/// both — it proves the fallback branch itself, not just that the job eventually succeeds.
/// </summary>
public class VideoEditClientTests : IDisposable
{
    private readonly string _root;
    private readonly ProjectStore _store;
    private readonly ProjectTelemetryService _telemetry;

    public VideoEditClientTests()
    {
        SupportedModelCatalog.ReloadCatalog();
        _root = Path.Combine(Path.GetTempPath(), "fs-video-edit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "projects"));
        _store = new ProjectStore(Options.Create(new PageToMovieOptions { WorkspaceRoot = _root }));
        _telemetry = new ProjectTelemetryService(_store, NullLogger<ProjectTelemetryService>.Instance);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { /* ignore */ }
    }

    [Fact]
    public void VideoEdit_empty_model_throws_instead_of_inventing_default()
    {
        Assert.Throws<InvalidOperationException>(
            () => SupportedModelCatalog.ResolveOrDefault(null, ModelCapability.VideoEdit));
    }

    [Fact]
    public void VideoEdit_catalog_default_resolves_grok_imagine_video_edit()
    {
        var id = SupportedModelCatalog.DefaultModelIdForCapability("video-edit");
        Assert.Equal("grok-imagine-video-edit", id);

        var m = SupportedModelCatalog.Find("grok-imagine-video-edit", ModelCapability.VideoEdit);
        Assert.NotNull(m);
        Assert.Equal(ModelProviderFamily.Xai, m!.Provider);
        Assert.Equal("grok", m.ProviderId);
        Assert.Contains("XAI_API_KEY", m.RequiredEnvKeys);
        Assert.Equal(8.7, m.MaxEditInputDurationSeconds);
    }

    [Fact]
    public void VideoEdit_models_are_not_mixed_into_the_general_video_capability()
    {
        // Edit output inherits the source clip's duration/resolution (not independently
        // configurable) — it must never show up as a pickable fresh text-to-video model.
        var video = SupportedModelCatalog.ForCapability(ModelCapability.Video, enabledOnly: false);
        Assert.DoesNotContain(video, e => e.Id.Contains("video-edit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GrokVideoEditClient_reports_not_configured_without_an_xai_key()
    {
        using var http = new HttpClient();
        var client = new GrokVideoEditClient(
            http, Options.Create(new PageToMovieOptions()), _telemetry, NullLogger<GrokVideoEditClient>.Instance);

        var had = Environment.GetEnvironmentVariable("XAI_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("XAI_API_KEY", null);
            Assert.False(client.IsConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XAI_API_KEY", had);
        }
    }

    /// <summary>Records every POST body sent to videos/edits and can simulate the file_id
    /// attempt being rejected, to prove the client's fallback logic without real network.</summary>
    private sealed class StubGrokEditHandler : HttpMessageHandler
    {
        public List<string> EditRequestBodies { get; } = new();
        public bool RejectFileId { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("videos/edits"))
            {
                var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
                EditRequestBodies.Add(body);
                if (RejectFileId && body.Contains("\"file_id\""))
                {
                    return new HttpResponseMessage(HttpStatusCode.BadRequest)
                    { Content = new StringContent("{\"error\":\"file not found\"}", Encoding.UTF8, "application/json") };
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{\"request_id\":\"req-1\"}", Encoding.UTF8, "application/json") };
            }
            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"status\":\"done\",\"video\":{\"url\":\"https://fake.example/result.mp4\"}}",
                        Encoding.UTF8, "application/json"),
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private GrokVideoEditClient BuildClient(StubGrokEditHandler handler)
    {
        var http = new HttpClient(handler);
        var opts = Options.Create(new PageToMovieOptions { GrokTimeoutSeconds = 30, GrokPollSeconds = 0 });
        return new GrokVideoEditClient(http, opts, _telemetry, NullLogger<GrokVideoEditClient>.Instance);
    }

    [Fact]
    public async Task EditClipAsync_with_a_valid_file_id_submits_file_id_only_once()
    {
        var handler = new StubGrokEditHandler();
        var client = BuildClient(handler);
        var videoPath = Path.Combine(_root, "clip.mp4");
        await File.WriteAllBytesAsync(videoPath, new byte[] { 1, 2, 3 });

        var had = Environment.GetEnvironmentVariable("XAI_API_KEY");
        Environment.SetEnvironmentVariable("XAI_API_KEY", "test-key");
        try
        {
            var url = await client.EditClipAsync(
                videoPath, "make it red", sourceFileId: "file-abc", model: "grok-imagine-video-edit");
            Assert.Equal("https://fake.example/result.mp4", url);
        }
        finally { Environment.SetEnvironmentVariable("XAI_API_KEY", had); }

        Assert.Single(handler.EditRequestBodies);
        Assert.Contains("\"file_id\":\"file-abc\"", handler.EditRequestBodies[0]);
    }

    [Fact]
    public async Task EditClipAsync_falls_back_to_upload_when_the_file_id_is_rejected()
    {
        var handler = new StubGrokEditHandler { RejectFileId = true };
        var client = BuildClient(handler);
        var videoPath = Path.Combine(_root, "clip.mp4");
        await File.WriteAllBytesAsync(videoPath, new byte[] { 1, 2, 3, 4 });

        var had = Environment.GetEnvironmentVariable("XAI_API_KEY");
        Environment.SetEnvironmentVariable("XAI_API_KEY", "test-key");
        try
        {
            var url = await client.EditClipAsync(
                videoPath, "make it red", sourceFileId: "expired-file-id", model: "grok-imagine-video-edit");
            Assert.Equal("https://fake.example/result.mp4", url);
        }
        finally { Environment.SetEnvironmentVariable("XAI_API_KEY", had); }

        // First attempt used the file_id and was rejected; the client transparently retried with
        // a base64 upload of the local file rather than propagating the failure to the caller.
        Assert.Equal(2, handler.EditRequestBodies.Count);
        Assert.Contains("\"file_id\":\"expired-file-id\"", handler.EditRequestBodies[0]);
        Assert.Contains("\"url\":\"data:video/mp4;base64,", handler.EditRequestBodies[1]);
    }

    [Fact]
    public async Task EditClipAsync_without_a_file_id_goes_straight_to_upload()
    {
        var handler = new StubGrokEditHandler();
        var client = BuildClient(handler);
        var videoPath = Path.Combine(_root, "clip.mp4");
        await File.WriteAllBytesAsync(videoPath, new byte[] { 5, 6, 7 });

        var had = Environment.GetEnvironmentVariable("XAI_API_KEY");
        Environment.SetEnvironmentVariable("XAI_API_KEY", "test-key");
        try
        {
            var url = await client.EditClipAsync(
                videoPath, "make it red", sourceFileId: null, model: "grok-imagine-video-edit");
            Assert.Equal("https://fake.example/result.mp4", url);
        }
        finally { Environment.SetEnvironmentVariable("XAI_API_KEY", had); }

        Assert.Single(handler.EditRequestBodies);
        Assert.Contains("\"url\":\"data:video/mp4;base64,", handler.EditRequestBodies[0]);
    }
}
