using PageToMovie.Engine;
using Xunit;

namespace PageToMovie.Tests;

public class SpeakBatchPathTests
{
    [Fact]
    public void RevoiceAudioRelativePath_default_mp3()
    {
        Assert.Equal(
            "assets/audio/revoice/scene_01_clip_02.mp3",
            MediaRegistryService.RevoiceAudioRelativePath(1, 2));
    }

    [Fact]
    public void RevoiceAudioRelativePath_custom_ext()
    {
        Assert.Equal(
            "assets/audio/revoice/scene_03_clip_04.wav",
            MediaRegistryService.RevoiceAudioRelativePath(3, 4, ".wav"));
    }
}
