using Xunit;

namespace Scry.Tests
{
    public class DimColourTests
    {
        [Fact]
        public void ADimmedColourKeepsItsShareOfEachChannelAndAllOfItsSeeThrough()
        {
            // A snow colour, a tint and a glow alike: each darkened, none made more see-through.
            var (r, g, b, a) = DimColour.Darker(0.9f, 0.8f, 0.5f, 0.4f);

            Assert.Equal(0.9f * DimColour.Share, r, 4);
            Assert.Equal(0.8f * DimColour.Share, g, 4);
            Assert.Equal(0.5f * DimColour.Share, b, 4);
            Assert.Equal(0.4f, a, 4);
        }

        [Fact]
        public void AGlowBrighterThanWhiteIsDarkenedByTheSameShare()
        {
            var (r, _, _, _) = DimColour.Darker(4f, 0f, 0f, 1f);

            Assert.Equal(4f * DimColour.Share, r, 4);
        }

        [Fact]
        public void BlackStaysBlack()
        {
            Assert.Equal((0f, 0f, 0f, 1f), DimColour.Darker(0f, 0f, 0f, 1f));
        }

        [Fact]
        public void ADimmedPartKeepsAboutAThirdOfItsColour()
        {
            Assert.Equal(0.3f, DimColour.Share, 3);
        }
    }
}
