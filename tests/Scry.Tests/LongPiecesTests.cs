using Xunit;

namespace Scry.Tests
{
    public class LongPiecesTests
    {
        [Fact]
        public void OnlyPiecesOverTheLimitAreKeptAndToldSlowestFirst()
        {
            var pieces = new LongPieces(8);
            pieces.Add("the prefab Troll", 12.4);
            pieces.Add("the prefab Boar", 3);
            pieces.Add("the prefab Fader", 31);

            Assert.Equal("Scry's catalog pieces over 8 ms (2): the prefab Fader 31, the prefab Troll 12.", pieces.Line("Scry's catalog pieces"));
        }

        [Fact]
        public void APieceJustAtTheLimitIsNotLong()
        {
            var pieces = new LongPieces(8);
            pieces.Add("Crypt2 creatures", 8);

            Assert.Null(pieces.Line("Scry's location steps"));
        }

        [Fact]
        public void TheLineNamesTheSlowestTwentyFiveAndCountsThemAll()
        {
            var pieces = new LongPieces(8);
            for (var i = 1; i <= 30; i++) pieces.Add("piece " + i, 10 + i);

            var line = pieces.Line("Scry's location steps");
            Assert.StartsWith("Scry's location steps over 8 ms (30): piece 30 40, piece 29 39, ", line);
            Assert.EndsWith("piece 6 16.", line);
            Assert.DoesNotContain("piece 5 ", line);
        }

        [Fact]
        public void NothingLongMeansNoLine()
        {
            Assert.Null(new LongPieces(8).Line("Scry's catalog pieces"));
        }
    }
}
