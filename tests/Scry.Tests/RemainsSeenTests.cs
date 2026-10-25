using Xunit;

namespace Scry.Tests
{
    /// <summary>A creature's remains seen on any game name the creature, where one creature alone leaves them.</summary>
    public class RemainsSeenTests
    {
        [Fact]
        public void RemainsOneCreatureLeavesNameIt()
        {
            Assert.Equal("Troll", RemainsSeen.CreatureOf("Troll_ragdoll", r => r == "Troll_ragdoll" ? new[] { "Troll" } : null));
        }

        [Fact]
        public void RemainsNoCreatureOrTwoLeaveNameNone()
        {
            Assert.Null(RemainsSeen.CreatureOf("Troll_ragdoll", r => null));
            Assert.Null(RemainsSeen.CreatureOf("Troll_ragdoll", r => new string[0]));
            Assert.Null(RemainsSeen.CreatureOf("Draugr_ragdoll", r => new[] { "Draugr", "Draugr_Ranged" }));
            Assert.Null(RemainsSeen.CreatureOf(null, r => new[] { "Troll" }));
        }
    }
}
