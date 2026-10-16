using Xunit;

namespace Scry.Tests
{
    public class ShaderTallyTests
    {
        [Fact]
        public void EachShaderIsToldWithHowManyOfItsPartsTheDimmingReached()
        {
            var tally = new ShaderTally();
            tally.Add("Standard", true, new string[0]);
            tally.Add("Standard", true, new string[0]);
            tally.Add("Custom/Piece", true, new[] { "_Color" });
            tally.Add("Custom/Piece", false, new[] { "_TintColor" });
            tally.Add("Custom/Piece", false, new[] { "_TintColor" });

            Assert.Equal(new[]
            {
                "Custom/Piece: 3 parts, 1 dimmed, 2 left as they were, its colours _TintColor",
                "Standard: 2 parts, all dimmed",
            }, tally.Lines());
            Assert.Equal(2, tally.Left);
        }

        [Fact]
        public void AShaderTheDimmingNeverReachesNamesTheColoursItTakes()
        {
            // The colours it does take tell which the dimming could darken instead.
            var tally = new ShaderTally();
            tally.Add("Custom/Rock", false, new[] { "_SnowColor", "_MossColor" });
            tally.Add("Custom/Rock", false, new[] { "_MossColor", "_SnowColor", "_EdgeColor" });

            Assert.Equal(new[] { "Custom/Rock: 2 parts, none dimmed, its colours _SnowColor, _MossColor, _EdgeColor" }, tally.Lines());
        }

        [Fact]
        public void AShaderWithNoColoursSaysSo()
        {
            var tally = new ShaderTally();
            tally.Add("Custom/Vegetation", false, new string[0]);

            Assert.Equal(new[] { "Custom/Vegetation: 1 part, none dimmed, no colours" }, tally.Lines());
        }

        [Fact]
        public void TheShadersLeavingMostPartsAsTheyWereComeFirst()
        {
            var tally = new ShaderTally();
            tally.Add("D", true, new string[0]);
            tally.Add("B", false, new string[0]);
            tally.Add("C", false, new string[0]);
            tally.Add("C", false, new string[0]);
            tally.Add("A", true, new string[0]);

            var lines = tally.Lines();

            Assert.StartsWith("C:", lines[0]);
            Assert.StartsWith("B:", lines[1]);
            Assert.StartsWith("A:", lines[2]);
            Assert.StartsWith("D:", lines[3]);
        }

        [Fact]
        public void NothingAddedTellsNothing()
        {
            var tally = new ShaderTally();

            Assert.Empty(tally.Lines());
            Assert.Equal(0, tally.Left);
        }
    }
}
