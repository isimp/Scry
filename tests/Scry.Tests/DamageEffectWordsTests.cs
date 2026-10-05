using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DamageEffectWordsTests
    {
        // The status effects a damage type puts on whatever it hits say what they really do, from
        // their own settings (the game's 1.0.16 values here): fire and spirit dealt over a few
        // seconds rather than at once, poison over a time that grows with it, frost slowing for a
        // time that grows with the share of health it took, and lightning's mark changing nothing.

        private static string Value(System.Collections.Generic.List<FactPair> pairs, string label) => pairs.Single(p => p.Label == label).Value;

        [Fact]
        public void BurningDealsFireOverItsTimeNotAtOnce()
        {
            var burning = DamageEffectWords.OverTime("fire", lasts: 5f, every: 1f);
            Assert.Equal("Dealt over 5 s in 5 even hits, one a second, not at once: what a hit leaves after resistances and armour", Value(burning, "Fire damage"));
            Assert.Equal("Adds to what is left, over 5 s again", Value(burning, "More fire"));
            // Each hit at least 0.2, or none of it is dealt.
            Assert.Equal("A hit leaving under 1 fire damage deals none of it", Value(burning, "Too little"));
        }

        [Fact]
        public void SpiritIsDealtTheSameWayInItsOwnHits()
        {
            var spirit = DamageEffectWords.OverTime("spirit", lasts: 3f, every: 0.5f);
            Assert.Equal("Dealt over 3 s in 6 even hits, one every 0.5 s, not at once: what a hit leaves after resistances and armour", Value(spirit, "Spirit damage"));
            Assert.Equal("A hit leaving under 1.2 spirit damage deals none of it", Value(spirit, "Too little"));
        }

        [Fact]
        public void BurningIsPutOutByWetAndKeepsYouFromResting()
        {
            var fire = DamageEffectWords.Fire();
            Assert.Equal("Puts it out 6 times as fast, the rest of its damage lost", Value(fire, "Wet"));
            Assert.Equal("No resting, and Wet wears off 51 times as fast", Value(fire, "While it burns"));

            // Burning says so; the spirit effect, of the same kind, has nothing to do with Wet.
            Assert.Contains(DamageEffectWords.Burns(spirit: false, 5f, 1f), p => p.Label == "Wet");
            Assert.Contains(DamageEffectWords.Burns(spirit: false, 5f, 1f), p => p.Label == "Fire damage");
            Assert.DoesNotContain(DamageEffectWords.Burns(spirit: true, 3f, 0.5f), p => p.Label == "Wet");
            Assert.Contains(DamageEffectWords.Burns(spirit: true, 3f, 0.5f), p => p.Label == "Spirit damage");
        }

        [Fact]
        public void PoisonLastsLongerTheMoreOfItThereIsLongestOnYou()
        {
            var poison = DamageEffectWords.Poison(baseLasts: 1f, perDamagePlayer: 5f, perDamage: 1f, power: 0.5f, every: 1f);
            Assert.Equal("Dealt over time in even hits, one a second, not at once: what a hit leaves after resistances and armour", Value(poison, "Poison damage"));
            Assert.Equal("On you 1 s and the square root of 5 times the damage; on a creature 1 s and the square root of the damage", Value(poison, "How long"));
            Assert.Equal("Only a stronger hit starts it over; a weaker one adds nothing while it lasts", Value(poison, "More poison"));

            // 20 poison: 1 + √100 = 11 s on you, 11 hits of 1.8; 1 + √20 = 5.5 s on a creature, 5 hits of 4.
            var lines = DamageEffectWords.PoisonLines(1f, 5f, 1f, 0.5f, 1f, new[] { 20f });
            Assert.Equal(new[] { "20", "11 s", "1.8", "5.5 s", "4" }, Assert.Single(lines));
            Assert.Equal(new[] { "Poison", "On you", "Each second", "On a creature", "Each second" }, DamageEffectWords.PoisonColumns);
            Assert.Equal(4, DamageEffectWords.PoisonLines(1f, 5f, 1f, 0.5f, 1f).Count);
        }

        [Fact]
        public void APoisonOrFrostLastsAsLongAsEachHitSays()
        {
            // Their own time is none: each hit sets it.
            Assert.Equal("By the poison a hit leaves", DamageEffectWords.PoisonLasts);
            Assert.Equal("By the share of health a hit takes", DamageEffectWords.FrostLasts);
        }

        [Fact]
        public void FrostHitsAtOnceThenSlowsForTheShareOfHealthItTook()
        {
            var frost = DamageEffectWords.Frost(freezePlayer: 15f, freezeCreature: 50f, leastSpeed: 0.1f);
            Assert.Equal("Dealt at once, then it slows", Value(frost, "Frost damage"));
            Assert.Equal("To 10% of the speed at first, coming back by its end", Value(frost, "Slows"));
            Assert.Equal("15 s on you, 50 s on a creature, times the share of health the hit took", Value(frost, "How long"));
            Assert.Equal("Starts it over where it would last longer than is left", Value(frost, "A stronger hit"));

            // A hit of a tenth of its health: 1.5 s on you, 5 s on a creature.
            var times = DamageEffectWords.FrostTimeLines(15f, 50f);
            Assert.Equal(new[] { "10%", "1.5 s", "5 s" }, times[0]);
            Assert.Equal(new[] { "100%", "15 s", "50 s" }, times.Last());
        }

        [Fact]
        public void FrostSlowsWhatIsWeakToItHarderAndWhatResistsItNotAtAll()
        {
            // The game's multipliers by how frost is taken: none for immune, a quarter to three
            // quarters for resisting (which are not slowed at all), more than one for weak.
            var multipliers = new[]
            {
                (Degree.Immune, 0f), (Degree.VeryResistant, 0.25f), (Degree.Resistant, 0.5f), (Degree.SlightlyResistant, 0.75f),
                (Degree.SlightlyWeak, 1.25f), (Degree.Weak, 1.5f), (Degree.VeryWeak, 1.75f),
            };
            var lines = DamageEffectWords.FrostSlowLines(0.1f, multipliers);
            Assert.Equal(new[]
            {
                "Normal: 10%", "Slightly weak: 8%", "Weak: 6.7%", "Very weak: 5.7%", "Resisting it at all: not slowed",
            }, lines.Select(l => l[0] + ": " + l[1]));
            Assert.Equal(new[] { "Its frost", "Slowed at first to" }, DamageEffectWords.FrostSlowColumns);
        }

        [Fact]
        public void LightningsMarkChangesNothingItself()
        {
            var lightning = DamageEffectWords.Lightning(lasts: 3f);
            Assert.Equal("Dealt at once", Value(lightning, "Lightning damage"));
            Assert.Equal("Changes nothing itself: it marks the hit for 3 s", Value(lightning, "This effect"));
        }
    }
}
