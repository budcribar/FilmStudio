using PageToMovie.Web.Services;
using Xunit;

namespace PageToMovie.Tests;

public class SceneCutProcessEndpointTests
{
    [Fact]
    public void ProcessSceneCutResponse_StoresPhaseResultsCorrectly()
    {
        var resp = new EngineApiClient.ProcessSceneCutResponse(
            DialogueResult: null,
            MusicResult: null);

        Assert.Null(resp.DialogueResult);
        Assert.Null(resp.MusicResult);
    }
}