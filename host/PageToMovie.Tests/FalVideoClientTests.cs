using PageToMovie.Core.Models;
using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class FalVideoClientTests
{
    [Fact]
    public void SupportedModelCatalog_ContainsHunyuanVideo()
    {
        var entry = SupportedModelCatalog.Find("hunyuan-video", ModelCapability.Video);
        Assert.NotNull(entry);
        Assert.Equal("HunyuanVideo (Fal.ai)", entry.DisplayName);
        Assert.Equal(ModelProviderFamily.Fal, entry.Provider);
        Assert.Contains(SupportedModelCatalog.FalApiKeyEnv, entry.RequiredEnvKeys);
        Assert.True(entry.SupportsVideoContinue);
        Assert.True(entry.SupportsReferenceImages);
    }

    [Fact]
    public void ParseTaggedRequestId_ParsesFalPrefix()
    {
        var (provider, id) = MultiProviderVideoClient.ParseTaggedRequestId("fal:req_123456789");
        Assert.Equal(ModelProviderFamily.Fal, provider);
        Assert.Equal("req_123456789", id);
    }

    [Fact]
    public void InferProviderFromDownloadUrl_RecognizesFalDomains()
    {
        Assert.Equal(ModelProviderFamily.Fal, MultiProviderVideoClient.InferProviderFromDownloadUrl("https://fal.media/files/monkey/abc.mp4"));
        Assert.Equal(ModelProviderFamily.Fal, MultiProviderVideoClient.InferProviderFromDownloadUrl("https://queue.fal.run/fal-ai/hunyuan-video/123.mp4"));
    }
}
