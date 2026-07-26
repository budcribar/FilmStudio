using Xunit;

namespace PageToMovie.Tests
{
    public class ClipPromptCompareViewerTests
    {
        [Fact]
        public void VersionPrompts_CanBeCompared()
        {
            var promptA = "Buster looking at candle";
            var promptB = "Buster looking at candle with dramatic shadows";

            Assert.NotEqual(promptA, promptB);
            Assert.Contains(promptA, promptB);
        }
    }
}
