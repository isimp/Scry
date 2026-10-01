using Xunit;

namespace Scry.Tests
{
    public class MachineTests
    {
        // A ballista (Turret) shoots what it may within its sight; a trap (Trap) springs on
        // whom it may; a ship (Ship) burns in the Ashlands' seas unless built for them and is
        // hurt while capsized; a cart (Vagon) weighs its own weight and part or all of its load;
        // a catapult (Catapult.CanItemBeLoaded) loads by item type.

        [Fact]
        public void ABallistaShootsWhatItMay()
        {
            Assert.Equal("enemies, players and tame creatures within 30 m", MachineWords.Shoots(true, true, true, 30f));
            Assert.Equal("enemies within 30 m", MachineWords.Shoots(true, false, false, 30f));
            Assert.Equal("nothing", MachineWords.Shoots(false, false, false, 30f));
        }

        [Fact]
        public void ATrapSpringsOnWhomItMay()
        {
            Assert.Equal("enemies and players", MachineWords.Springs(true, true));
            Assert.Equal("enemies", MachineWords.Springs(true, false));
            Assert.Equal("players", MachineWords.Springs(false, true));
            Assert.Equal("nothing", MachineWords.Springs(false, false));
        }

        [Fact]
        public void AShipBurnsInTheAshlandsSeasUnlessBuiltForThem()
        {
            Assert.Equal("sails them unharmed", MachineWords.Ashlands(true));
            Assert.Equal("their boiling water burns it", MachineWords.Ashlands(false));
            Assert.Equal("takes 20 damage every 5 s", MachineWords.Capsized(20f, 5f));
            Assert.Null(MachineWords.Capsized(0f, 5f));
        }

        [Fact]
        public void ACartWeighsItselfAndItsLoad()
        {
            Assert.Equal("20 empty, and all it carries", MachineWords.CartWeight(20f, 1f));
            Assert.Equal("20 empty, and half of what it carries", MachineWords.CartWeight(20f, 0.5f));
            Assert.Equal("20 empty, and 25% of what it carries", MachineWords.CartWeight(20f, 0.25f));
            Assert.Equal("20, whatever it carries", MachineWords.CartWeight(20f, 0f));
        }

        [Fact]
        public void ACatapultLoadsByType()
        {
            Assert.Equal("anything it can hold but weapons and armour", MachineWords.Loads(true, new[] { "weapons", "armour" }, true));
            Assert.Equal("only ammunition and trophies", MachineWords.Loads(false, new[] { "ammunition", "trophies" }, false));
            Assert.Equal("anything it can hold", MachineWords.Loads(true, new string[0], true));
            Assert.Equal("anything", MachineWords.Loads(true, new string[0], false));
            Assert.Equal("nothing", MachineWords.Loads(false, new string[0], false));
        }
    }
}
