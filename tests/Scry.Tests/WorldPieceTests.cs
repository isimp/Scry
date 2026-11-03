using Xunit;

namespace Scry.Tests
{
    public class WorldPieceTests
    {
        // A piece the world placed, as the Dvergr's crates, lanterns and walls in their camps and
        // the goblins' fences, is learned by hitting it, as a rock or a tree is. A piece a player
        // built tells its damage when hit but is learned by its recipe, as before.

        [Fact]
        public void APieceTheWorldPlacedIsLearnedByHittingIt()
        {
            Assert.True(WorldPiece.LearnedByHit(creator: 0L));
        }

        [Fact]
        public void APieceAPlayerBuiltIsNotLearnedByHittingIt()
        {
            Assert.False(WorldPiece.LearnedByHit(creator: 1234567L));
        }
    }
}
