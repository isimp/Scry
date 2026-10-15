using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class YouTests
    {
        // A page opens with what the character itself did with the thing: the game's own counts
        // (kills, things made and picked up, foods eaten, things picked, pieces built) and how it
        // came to know it, in a box of its own titled "You".

        [Fact]
        public void TheGamesCountsForTheCharacterAreToldEachWhereThereIsOne()
        {
            var counts = new CharacterCounts { Killed = 47, Built = 1, Eaten = 12, Picked = 30, Made = 3, PickedUp = 120 };

            var lines = CompendiumWords.You(counts, null);

            Assert.Equal(new[]
            {
                ("Killed", "47 times"), ("Built", "once"), ("Eaten", "12 times"), ("Picked", "30 times"), ("Made", "3"), ("Picked up", "120"),
            }, lines);
        }

        [Fact]
        public void HowItCameToBeKnownFollowsTheCounts()
        {
            var lines = CompendiumWords.You(new CharacterCounts { Killed = 2 }, "day 12, fighting it in the Black Forest");

            Assert.Equal(("Came to know it", "day 12, fighting it in the Black Forest"), lines.Last());
            Assert.Empty(CompendiumWords.You(new CharacterCounts(), null));
            Assert.Single(CompendiumWords.You(null, "with Troll"));
        }

        [Fact]
        public void HowItCameToBeKnownIsTheWayKeptOrWhatBroughtItAlongOrTheFloor()
        {
            Assert.Equal("day 12, fighting it in the Black Forest", CompendiumWords.CameLine(CameBy.Fought, 12, "Black Forest", owner: "Troll", byFloor: true, known: true));
            Assert.Equal("with Troll", CompendiumWords.CameLine(null, -1, null, owner: "Troll", byFloor: true, known: true));
            Assert.Equal("shown by the floor", CompendiumWords.CameLine(null, -1, null, owner: null, byFloor: true, known: true));
            Assert.Equal("before this book was kept", CompendiumWords.CameLine(null, -1, null, owner: null, byFloor: false, known: true));
            Assert.Null(CompendiumWords.CameLine(null, -1, null, owner: null, byFloor: false, known: false));
        }
    }
}
