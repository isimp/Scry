using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ProjectileWordsTests
    {
        // A projectile's page tells its own numbers: what a hit does, how it flies, whether it can
        // be blocked or dodged, and what it leaves; an attack that fires it gives it the attack's.

        private static ProjectileFacts Plain() => new ProjectileFacts { Blockable = true, Dodgeable = true };

        private static string Value(ProjectileFacts facts, string label) => ProjectileWords.Pairs(facts).Where(p => p.Label == label).Select(p => p.Value).SingleOrDefault();

        [Fact]
        public void OneFiredByAnAttackSaysItTakesTheAttacksFiguresAndSpeed()
        {
            Assert.StartsWith("Fired by an attack, it takes that attack's damage", ProjectileWords.Description(true));
            Assert.Equal("Its speed comes from whatever launches it.", ProjectileWords.Description(false));
        }

        [Fact]
        public void ItsOwnDamageIsToldWhereItHasAny()
        {
            var facts = Plain();
            facts.Damage = new[] { ("fire", 20f), ("blunt", 0f) };
            Assert.Equal(CombatWords.Damage(facts.Damage), Value(facts, "Own damage"));
            Assert.Null(Value(Plain(), "Own damage"));
        }

        [Fact]
        public void OneWhoseSpawnDealsTheDamageHasNoneOfItsOwn()
        {
            var facts = Plain();
            facts.Damage = new[] { ("fire", 20f) };
            facts.SpawnDealsTheDamage = true;
            Assert.Equal("none; what it spawns on hit deals the damage", Value(facts, "Own damage"));
        }

        [Fact]
        public void AnAreaHitTellsHowFarItReaches()
        {
            var facts = Plain();
            facts.AreaOfEffect = 4f;
            Assert.Equal("everything within 4 m of where it lands", Value(facts, "Hits"));
            Assert.Null(Value(Plain(), "Hits"));
        }

        [Theory]
        [InlineData(true, true, "blocked or dodged")]
        [InlineData(true, false, "blocked")]
        [InlineData(false, true, "dodged")]
        [InlineData(false, false, "neither blocked nor dodged")]
        public void ItSaysWhetherItCanBeBlockedOrDodged(bool blockable, bool dodgeable, string said)
        {
            var facts = new ProjectileFacts { Blockable = blockable, Dodgeable = dodgeable };
            Assert.Equal(said, Value(facts, "Can be"));
        }

        [Fact]
        public void ItsStatusEffectGoesToItsPage()
        {
            var facts = Plain();
            facts.OnHit = "Burning";
            facts.OnHitKey = "se:Burning";
            var pair = ProjectileWords.Pairs(facts).Single(p => p.Label == "On hit");
            Assert.Equal("Burning", pair.Value);
            Assert.Equal("se:Burning", pair.Link);
        }

        [Fact]
        public void ItTellsHowItFlies()
        {
            var facts = Plain();
            facts.FliesFor = 5f;
            facts.Gravity = 9.81f;
            facts.Drag = 0.5f;
            Assert.Equal("5 s", Value(facts, "Flies for"));
            Assert.Equal(Numbers.Amount(9.81f) + " m/s²", Value(facts, "Falls"));
            Assert.Equal("drag 0.5", Value(facts, "Slows"));
        }

        [Fact]
        public void OneWithoutGravityFliesStraightAndOneWithoutDragDoesNotSlow()
        {
            Assert.Equal("no, it flies straight", Value(Plain(), "Falls"));
            Assert.Null(Value(Plain(), "Slows"));
            Assert.Null(Value(Plain(), "Flies for"));
        }

        [Theory]
        [InlineData(3, "up to 3 times")]
        [InlineData(98, "up to 98 times")]
        [InlineData(99, "yes")]
        public void ItBouncesUpToANumberOfTimesOrWithoutEnd(int most, string said)
        {
            var facts = Plain();
            facts.Bounces = true;
            facts.MaxBounces = most;
            Assert.Equal(said, Value(facts, "Bounces"));
        }

        [Fact]
        public void ItTellsWhatItLeavesAndHowOftenItSpawns()
        {
            var facts = Plain();
            facts.StaysAfterHit = true;
            facts.StaysFor = 30f;
            facts.LeavesTheWeapon = true;
            facts.SpawnsOnHit = true;
            facts.SpawnOnHitChance = 0.25f;
            Assert.Equal("stays where it struck for 30 s", Value(facts, "After a hit"));
            Assert.Equal("the weapon that threw it, where it lands", Value(facts, "Leaves"));
            Assert.Equal("25% of the time", Value(facts, "Spawns on hit"));

            // Spawning every time is no odds to tell.
            facts.SpawnOnHitChance = 1f;
            Assert.Null(Value(facts, "Spawns on hit"));
        }

        [Fact]
        public void APlainProjectileTellsOnlyWhatEveryOneHas()
        {
            // No damage, area, knockback, effect, flight time, drag, bounce, stay or leavings to tell.
            Assert.Equal(new[] { "Can be", "Falls" }, ProjectileWords.Pairs(Plain()).Select(p => p.Label));
        }

        [Fact]
        public void GravityEitherWayIsTold()
        {
            var facts = Plain();
            facts.Gravity = -2f;
            Assert.Equal(Numbers.Amount(-2f) + " m/s²", Value(facts, "Falls"));
        }

        [Fact]
        public void TheLinesComeInTheOrderAPlayerReadsThem()
        {
            var facts = new ProjectileFacts
            {
                Damage = new[] { ("pierce", 10f) }, AreaOfEffect = 2f, Knockback = 30f, Blockable = true, OnHit = "Wet", OnHitKey = "se:Wet",
                FliesFor = 4f, Gravity = 5f, Drag = 1f, Bounces = true, MaxBounces = 2, StaysAfterHit = true, StaysFor = 10f,
                LeavesTheWeapon = true, SpawnsOnHit = true, SpawnOnHitChance = 0.5f,
            };
            Assert.Equal(new[] { "Own damage", "Hits", "Knockback", "Can be", "On hit", "Flies for", "Falls", "Slows", "Bounces", "After a hit", "Leaves", "Spawns on hit" },
                ProjectileWords.Pairs(facts).Select(p => p.Label));
        }
    }
}
