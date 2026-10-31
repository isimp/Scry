using Xunit;

namespace Scry.Tests
{
    public class TreasureRuleTests
    {
        [Fact]
        public void ATreasureGivesItsOwnItemWhereItHasOneWhateverItsRandomList()
        {
            // As the game picks: a random item only where the treasure names no item of its own.
            Assert.False(TreasureRule.GivesRandom(hasItem: true, randomItems: 3));
            Assert.True(TreasureRule.GivesRandom(hasItem: false, randomItems: 3));
            Assert.True(TreasureRule.GivesRandom(hasItem: false, randomItems: 1));
            Assert.False(TreasureRule.GivesRandom(hasItem: false, randomItems: 0));
        }
    }
}
