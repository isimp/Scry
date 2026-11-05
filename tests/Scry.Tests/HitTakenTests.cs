using System;
using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// What a creature's hit does to you, as the game takes it (<c>Character.RPC_Damage</c>): the
    /// world's scale first, then your resistances type by type (<c>HitData.ApplyResistance</c>),
    /// then your armour on all of it together (<c>HitData.ApplyArmor</c>); the game's flat damage
    /// past both. Figures from the game's data: a Seeker's claw 120 pierce, its landing 30 pierce
    /// and 200 frost, a full carapace set at quality 4 with a feather cape, 118 armour.
    /// </summary>
    public class HitTakenTests
    {
        private static float Normal(string type) => 1f;

        [Fact]
        public void AHitMoreThanTwiceYourArmourLosesYourArmour()
        {
            Assert.Equal(122f, HitTaken.ArmourCut(240f, 118f), 3);
            // At twice your armour the two ways meet.
            Assert.Equal(118f, HitTaken.ArmourCut(236f, 118f), 3);
        }

        [Fact]
        public void ASmallerHitIsCutTheHarderTheSmallerItIs()
        {
            // A Seeker's claw against 118: 120² ÷ (4 × 118).
            Assert.Equal(30.508f, HitTaken.ArmourCut(120f, 118f), 2);
            Assert.Equal(5.297f, HitTaken.ArmourCut(50f, 118f), 2);
            Assert.Equal(0f, HitTaken.ArmourCut(0f, 118f));
            // No armour cuts nothing.
            Assert.Equal(120f, HitTaken.ArmourCut(120f, 0f));
        }

        [Fact]
        public void ArmourWorksOnAllOfAHitTogether()
        {
            // A Seeker's landing: 230 together against 118, so the frost, most of it, shares the cut.
            var landing = new[] { ("pierce", 30f), ("frost", 200f) };
            Assert.Equal(230f * 230f / (4f * 118f), HitTaken.Taken(landing, 1f, Normal, 118f), 2);
        }

        [Fact]
        public void YourResistancesCutEachTypeBeforeYourArmour()
        {
            // Resistant to frost: 30 pierce and 100 frost reach the armour.
            var landing = new[] { ("pierce", 30f), ("frost", 200f) };
            float FrostResisted(string type) => type == "frost" ? HitTaken.Factor("Resistant") : 1f;
            Assert.Equal(130f * 130f / (4f * 118f), HitTaken.Taken(landing, 1f, FrostResisted, 118f), 2);
        }

        [Fact]
        public void TheGamesFlatDamagePassesResistancesAndArmour()
        {
            var hit = new[] { ("true", 50f), ("pierce", 120f) };
            float Immune(string type) => 0f;
            Assert.Equal(50f + 30.508f, HitTaken.Taken(hit, 1f, Normal, 118f), 2);
            Assert.Equal(50f, HitTaken.Taken(hit, 1f, Immune, 118f), 2);
        }

        [Fact]
        public void ChopAndPickaxeAreResistedButNotArmoured()
        {
            // A player ignores both; were one taken, armour would not cut it.
            var ram = new[] { ("blunt", 100f), ("chop", 100f), ("pickaxe", 100f) };
            float Ignores(string type) => type == "chop" || type == "pickaxe" ? HitTaken.Factor("Ignore") : 1f;
            Assert.Equal(100f * 100f / (4f * 118f), HitTaken.Taken(ram, 1f, Ignores, 118f), 2);
            Assert.Equal(100f * 100f / (4f * 118f) + 200f, HitTaken.Taken(ram, 1f, Normal, 118f), 2);
        }

        [Fact]
        public void TheWorldsScaleComesFirst()
        {
            // Hits half again as hard: 180 reaches the armour, past twice 80, so 80 comes off.
            var claw = new[] { ("pierce", 120f) };
            Assert.Equal(100f, HitTaken.Taken(claw, 1.5f, Normal, 80f), 2);
        }

        [Fact]
        public void EachModifierCutsAsTheGameCutsIt()
        {
            var factors = new Dictionary<string, float>
            {
                ["Normal"] = 1f, ["SlightlyResistant"] = 0.75f, ["Resistant"] = 0.5f, ["VeryResistant"] = 0.25f,
                ["SlightlyWeak"] = 1.25f, ["Weak"] = 1.5f, ["VeryWeak"] = 2f, ["Immune"] = 0f, ["Ignore"] = 0f,
            };
            foreach (var (name, factor) in factors) Assert.Equal(factor, HitTaken.Factor(name));
            // One the game does not know cuts nothing.
            Assert.Equal(1f, HitTaken.Factor("Unknown"));
        }

        [Fact]
        public void EachStarAddsHalfAgain()
        {
            Assert.Equal(1f, HitTaken.StarScale(0));
            Assert.Equal(1.5f, HitTaken.StarScale(1));
            Assert.Equal(2f, HitTaken.StarScale(2));
        }

        [Fact]
        public void YouTakeReadsAsTheRangeOfAHitsRoll()
        {
            // A creature's hit rolls between 87 and 100 percent of it (SkillMath.CreatureHitShare).
            var claw = new[] { ("pierce", 120f) };
            var (least, most) = HitTaken.Range(claw, 1f, Normal, 118f);
            Assert.Equal(HitTaken.ArmourCut(120f * SkillMath.CreatureHitShare.Least, 118f), least, 3);
            Assert.Equal(30.508f, most, 2);
            Assert.Equal("23–31", CombatWords.YouTake(least, most));
            Assert.Equal("31", CombatWords.YouTake(30.6f, 30.6f));
            Assert.Equal("0", CombatWords.YouTake(0f, 0f));
        }

        [Fact]
        public void TheAttacksTableKeepsWhatYouTakeBackWithItsDamage()
        {
            var columns = CombatWords.WithYouTake(CombatWords.AttackColumnsFor(onHit: false));
            Assert.Equal(new[] { "Attack", "Damage", "How", "Reach", "Every", "You take" }, columns);
            Assert.Contains(Array.IndexOf(columns, "You take"), CombatWords.AttackKeptCells(columns));
        }

        [Fact]
        public void TheLineUnderTheTableSaysHowMuchHarderTheWorldMakesAHit()
        {
            // A server on hard combat makes creatures hit half again as hard, so what you take can
            // be more than the damage the table gives; the line says so.
            Assert.Equal("You take: at your 6 armour and what you resist now, creatures hitting ×1.5 as hard here", CombatWords.YouTakeAt(6f, 1.5f));
            Assert.Equal("You take: at your 118 armour and what you resist now", CombatWords.YouTakeAt(118f, 1f));
            Assert.Equal("You take: at no armour, with what you resist now, creatures hitting ×0.75 as hard here", CombatWords.YouTakeAt(0f, 0.75f));
        }

        [Fact]
        public void TheArmourRowSaysWhatYourArmourDoesToAHit()
        {
            Assert.Equal("you wear 118: a hit of 100 does 21 to you, one of 300 does 182", CombatWords.ArmourYours(118f));
            Assert.Equal("you wear none: a hit does all of itself to you", CombatWords.ArmourYours(0f));
        }
    }
}
