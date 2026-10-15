using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class HitToldTests
    {
        // The game's popup on a hit shows one kind of landing, the one most of the hit's damage
        // took (HitData.ApplyResistance's significant modifier): the types it speaks for are those
        // carrying damage that landed so, each as the creature takes it.

        [Fact]
        public void AHitOfOneTypeTellsHowThatTypeLands()
        {
            Assert.Equal(new[] { "Blunt" }, HitTold.Types(new[] { ("Blunt", 30f, Degree.Weak) }, Degree.Weak));
            Assert.Equal(new[] { "Slash" }, HitTold.Types(new[] { ("Slash", 30f, Degree.Normal) }, Degree.Normal));
        }

        [Fact]
        public void AMixedHitTellsOnlyTheTypesThatLandedAsThePopupSaid()
        {
            var arrow = new[] { ("Pierce", 20f, Degree.Normal), ("Fire", 40f, Degree.VeryWeak), ("Frost", 0f, Degree.Weak) };

            Assert.Equal(new[] { "Fire" }, HitTold.Types(arrow, Degree.Weak));
            Assert.Equal(new[] { "Pierce" }, HitTold.Types(arrow, Degree.Normal));
        }

        [Fact]
        public void EveryDegreeOfResistingOrBeingWeakIsOneKindOfLanding()
        {
            var hit = new[] { ("Blunt", 10f, Degree.SlightlyResistant), ("Slash", 10f, Degree.VeryResistant), ("Pierce", 10f, Degree.SlightlyWeak), ("Spirit", 10f, Degree.Immune) };

            Assert.Equal(new[] { "Blunt", "Slash" }, HitTold.Types(hit, Degree.Resistant));
            Assert.Equal(new[] { "Pierce" }, HitTold.Types(hit, Degree.Weak));
            Assert.Equal(new[] { "Spirit" }, HitTold.Types(hit, Degree.Immune));
        }

        [Fact]
        public void DamageTheCreatureIgnoresTellsNothing()
        {
            Assert.Empty(HitTold.Types(new[] { ("Chop", 30f, Degree.Ignore) }, Degree.Normal));
            Assert.Empty(HitTold.Types(new[] { ("Chop", 30f, Degree.Ignore) }, Degree.Ignore));
        }
    }
}
