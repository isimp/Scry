using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MachineFactsTests
    {
        // A machine's page tells what it does, line by line, in the order a player reads it.

        private static string Value(MachineFacts facts, string label) => MachineWords.Pairs(facts).Where(p => p.Label == label).Select(p => p.Value).SingleOrDefault();

        private static string[] Labels(MachineFacts facts) => MachineWords.Pairs(facts).Select(p => p.Label).ToArray();

        [Fact]
        public void ABallistaTellsWhatItHoldsWhomItShootsAndHowOften()
        {
            var facts = new MachineFacts { Turret = new TurretFacts { MaxAmmo = 20, Enemies = true, Range = 30f, Cooldown = 2f } };
            Assert.Equal(new[] { "Holds", "Shoots", "Shoots every" }, Labels(facts));
            Assert.Equal("20 shots", Value(facts, "Holds"));
            Assert.Equal(MachineWords.Shoots(true, false, false, 30f), Value(facts, "Shoots"));
            Assert.Equal("2 s", Value(facts, "Shoots every"));
        }

        [Fact]
        public void GivenTrophiesABallistaShootsOnlyTheirCreatures()
        {
            Assert.Equal("Given these trophies, up to 3 at once, it shoots only their creatures", MachineWords.TrophiesTitle(3));
        }

        [Fact]
        public void ATrapTellsOnWhomItSpringsHowSoonItRearmsAndWhatItDoes()
        {
            var facts = new MachineFacts { Trap = new TrapFacts { Players = true, Rearm = 5f, Damage = new[] { ("pierce", 40f) }, Staggers = true } };
            Assert.Equal(new[] { "Springs on", "Rearms after", "Damage", "Staggers" }, Labels(facts));
            Assert.Equal(MachineWords.Springs(false, true), Value(facts, "Springs on"));
            Assert.Equal("5 s", Value(facts, "Rearms after"));
            Assert.Equal("40 pierce", Value(facts, "Damage"));
            Assert.Equal("whoever it hits", Value(facts, "Staggers"));
        }

        [Fact]
        public void ATrapThatDoesNoDamageAndDoesNotStaggerSaysNeither()
        {
            var facts = new MachineFacts { Trap = new TrapFacts { Enemies = true, Rearm = 5f } };
            Assert.Equal(new[] { "Springs on", "Rearms after" }, Labels(facts));
        }

        [Fact]
        public void AShipTellsTheAshlandsSeasAndCapsizedOnlyWhereItHurts()
        {
            var hurt = new MachineFacts { Ship = new ShipFacts { AshlandsReady = false, CapsizedDamage = 10f, CapsizedEvery = 1f } };
            Assert.Equal(new[] { "Ashlands seas", "Capsized" }, Labels(hurt));
            Assert.Equal(MachineWords.Ashlands(false), Value(hurt, "Ashlands seas"));
            var unhurt = new MachineFacts { Ship = new ShipFacts { AshlandsReady = true } };
            Assert.Equal(new[] { "Ashlands seas" }, Labels(unhurt));
        }

        [Fact]
        public void ACartTellsWhatItWeighs()
        {
            var facts = new MachineFacts { Cart = new CartFacts { Mass = 20f, LoadShare = 0.5f } };
            Assert.Equal(MachineWords.CartWeight(20f, 0.5f), Value(facts, "Weighs"));
        }

        [Fact]
        public void ACatapultTellsWhatItLoadsHowManyAtATimeAndWhomElse()
        {
            var facts = new MachineFacts { Catapult = new CatapultFacts { ListExcludes = true, Types = new[] { "trophies" }, MaxLoad = 3, ThrowsWhoStandsThere = true } };
            Assert.Equal(new[] { "Loads", "At a time", "Also throws" }, Labels(facts));
            Assert.Equal(MachineWords.Loads(true, new[] { "trophies" }, false), Value(facts, "Loads"));
            Assert.Equal("up to 3", Value(facts, "At a time"));
            Assert.Equal("whoever stands where it is loaded", Value(facts, "Also throws"));
        }

        [Fact]
        public void ACatapultThatLoadsOneAtATimeSaysNothingOfIt()
        {
            var facts = new MachineFacts { Catapult = new CatapultFacts { MaxLoad = 1 } };
            Assert.Equal(new[] { "Loads" }, Labels(facts));
        }

        [Fact]
        public void NoMachineTellsNothing() => Assert.Empty(Labels(new MachineFacts()));
    }
}
