using System.Net;
using System.Text.Json;
using PageToMovie.Core.Models;
using PageToMovie.Web.Services;
using Xunit;

namespace PageToMovie.Tests;

public class ClientVideoStitchServiceTests
{
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public async Task CollectSceneMediaUrlsAsync_PrefersIndividualClipsOverComposites_PreventsDuplication()
    {
        // Arrange: scene 1 has both a composite AND individual clips on disk
        var projectId = "test-project";
        var sceneDetailJson = JsonSerializer.Serialize(new
        {
            ok = true,
            scene = new
            {
                sceneNumber = 1,
                compositeExists = true,
                clips = new[]
                {
                    new { clipNumber = 1, onDisk = true },
                    new { clipNumber = 2, onDisk = true }
                }
            }
        });

        var handler = new FakeHttpMessageHandler(req =>
        {
            if (req.RequestUri?.AbsolutePath.Contains("/scenes/1") == true)
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sceneDetailJson, System.Text.Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var engineClient = new EngineApiClient(httpClient);
        var stitchService = new ClientVideoStitchService(null!, engineClient);

        var sceneSummaries = new List<SceneSummary>
        {
            new() { SceneNumber = 1, CompositeExists = true, ClipsOnDisk = 2 }
        };

        // Act
        var urls = await stitchService.CollectSceneMediaUrlsAsync(projectId, new[] { 1 }, sceneSummaries, staleScenes: null);

        // Assert: MUST return individual clips ONLY (2 clip URLs), and 0 composite URLs to avoid duplication
        Assert.Equal(2, urls.Count);
        Assert.Contains("scenes/1/clips/1/video", urls[0]);
        Assert.Contains("scenes/1/clips/2/video", urls[1]);
        Assert.DoesNotContain(urls, u => u.Contains("/composite"));
    }

    [Fact]
    public async Task CollectSceneMediaUrlsAsync_StrictlyOrdersScenesAndClipsSequentially()
    {
        // Arrange: scene 1 and scene 2 requested out-of-order
        var projectId = "test-project";
        var handler = new FakeHttpMessageHandler(req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (path.Contains("/scenes/1"))
            {
                var s1 = JsonSerializer.Serialize(new
                {
                    ok = true,
                    scene = new
                    {
                        sceneNumber = 1,
                        clips = new[] { new { clipNumber = 1, onDisk = true } }
                    }
                });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(s1) };
            }
            if (path.Contains("/scenes/2"))
            {
                var s2 = JsonSerializer.Serialize(new
                {
                    ok = true,
                    scene = new
                    {
                        sceneNumber = 2,
                        clips = new[] { new { clipNumber = 1, onDisk = true } }
                    }
                });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(s2) };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var engineClient = new EngineApiClient(httpClient);
        var stitchService = new ClientVideoStitchService(null!, engineClient);

        // Act: Pass scenes out of order [2, 1]
        var urls = await stitchService.CollectSceneMediaUrlsAsync(projectId, new[] { 2, 1 }, null, null);

        // Assert: Must be strictly sorted [scene 1, then scene 2]
        Assert.Equal(2, urls.Count);
        Assert.Contains("scenes/1/clips/1/video", urls[0]);
        Assert.Contains("scenes/2/clips/1/video", urls[1]);
    }

    [Fact]
    public async Task CollectSceneMediaUrlsAsync_FallsBackToComposite_WhenNoIndividualClipsExist()
    {
        // Arrange: scene 1 has no individual clips, but composite exists
        var projectId = "test-project";
        var handler = new FakeHttpMessageHandler(req =>
        {
            var s = JsonSerializer.Serialize(new
            {
                ok = true,
                scene = new
                {
                    sceneNumber = 1,
                    compositeExists = true,
                    clips = Array.Empty<object>()
                }
            });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(s) };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
        var engineClient = new EngineApiClient(httpClient);
        var stitchService = new ClientVideoStitchService(null!, engineClient);

        var sceneSummaries = new List<SceneSummary>
        {
            new() { SceneNumber = 1, CompositeExists = true, ClipsOnDisk = 0 }
        };

        // Act
        var urls = await stitchService.CollectSceneMediaUrlsAsync(projectId, new[] { 1 }, sceneSummaries, null);

        // Assert: Must fall back to composite URL
        Assert.Single(urls);
        Assert.Contains("scenes/1/composite", urls[0]);
    }
}
