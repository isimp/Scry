using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class SkillPageTests
    {
        // A skill's page tells what it changes at no skill, at yours and at the top, each line by
        // the game's own rule for it (SkillMath) and only where something the game reads uses it.

        private static string[] Line(SkillInputs inputs, float level, string what) =>
            SkillPageWords.Changes(inputs, level).FirstOrDefault(l => l[0] == what);

        [Fact]
        public void TheTableComparesNoSkillYoursAndTheTop()
        {
            Assert.Equal("What it changes", SkillPageWords.ChangesHeading);
            Assert.Equal("By level", SkillPageWords.ChangesTitle);
            Assert.Equal(new[] { "", "At 0", "You, 34", "At 100" }, SkillPageWords.ChangesColumns(34f));
            Assert.Equal(new[] { "", "At 0", "You, 120", "At 100" }, SkillPageWords.ChangesColumns(120.6f));
        }

        [Fact]
        public void AWeaponSkillTellsWhatAHitDealsAndWhatAttacksCost()
        {
            var swords = new SkillInputs { Name = "Swords", Hits = true, Costs = true };
            // Knockback is rolled with the damage (Attack.DoMeleeAttack: m_pushForce times the same share).
            Assert.Equal(new[] { "Damage and knockback of a hit", "25–55%", "45–75%", "85–100%" }, Line(swords, 34f, "Damage and knockback of a hit"));
            // The ends are clamped before the roll (Skills.GetRandomSkillFactor), so the top's average is 92.5%, 2.3 times no skill's.
            Assert.Equal(new[] { "Average hit", "40%", "60.4%, ×1.5", "92.5%, ×2.3" }, Line(swords, 34f, "Average hit"));
            Assert.Equal(new[] { "Cost of an attack", "100%", "89%", "67%" }, Line(swords, 34f, "Cost of an attack"));
            // A skill nothing hits with tells neither, and attacks that cost nothing tell no cost.
            Assert.Empty(SkillPageWords.Changes(new SkillInputs { Name = "Swords" }, 34f));
            Assert.Null(Line(new SkillInputs { Name = "Swords", Hits = true }, 34f, "Cost of an attack"));
        }

        [Fact]
        public void WoodCuttingTellsAnAxesHitsOnTrees()
        {
            var wood = new SkillInputs { Name = "WoodCutting", OnTrees = true };
            Assert.Equal(new[] { "An axe's hit on a tree", "25–55%", "31–61%", "85–100%" }, Line(wood, 10f, "An axe's hit on a tree"));
        }

        [Fact]
        public void ABowsDrawIsInSecondsWhenEveryBowTakesAsLong()
        {
            var bows = new SkillInputs { Name = "Bows", Hits = true, DrawDrains = true };
            bows.DrawTimes.Add(2.5f);
            Assert.Equal(new[] { "Full draw", "2.5 s", "1.8 s", "0.5 s" }, Line(bows, 34f, "Full draw"));
            Assert.Equal(new[] { "Drain while drawing", "100%", "89%", "67%" }, Line(bows, 34f, "Drain while drawing"));
            bows.DrawTimes.Add(3f);
            Assert.Equal(new[] { "Full draw, of the bow's time", "100%", "73%", "20%" }, Line(bows, 34f, "Full draw, of the bow's time"));
            // Bows that drain nothing while drawn tell no drain.
            Assert.Null(Line(new SkillInputs { Name = "Bows", Hits = true }, 34f, "Drain while drawing"));
        }

        [Fact]
        public void ACrossbowsReloadIsInSecondsWhenEveryOneTakesAsLong()
        {
            var crossbows = new SkillInputs { Name = "Crossbows", Hits = true };
            crossbows.ReloadTimes.Add(3.5f);
            Assert.Equal(new[] { "Reloading", "3.5 s", "2.9 s", "1.8 s" }, Line(crossbows, 34f, "Reloading"));
            crossbows.ReloadTimes.Add(1.9f);
            Assert.Equal(new[] { "Reloading, of the weapon's time", "100%", "83%", "50%" }, Line(crossbows, 34f, "Reloading, of the weapon's time"));
        }

        [Fact]
        public void BlockingTellsWhatABlockHoldsBack()
        {
            Assert.Equal(new[] { "Block", "100%", "110%", "150%" }, Line(new SkillInputs { Name = "Blocking" }, 20f, "Block"));
        }

        [Fact]
        public void RunningTellsSpeedAndStaminaFromThePlayersOwnFigures()
        {
            var run = new SkillInputs { Name = "Run", RunSpeed = 7f, RunDrain = 8f };
            Assert.Equal(new[] { "Running speed", "7 m/s", "7.7 m/s", "8.8 m/s" }, Line(run, 41f, "Running speed"));
            Assert.Equal(new[] { "Stamina: running", "8 a second", "6.4 a second", "4 a second" }, Line(run, 41f, "Stamina: running"));
        }

        [Fact]
        public void TheOtherWaysOfMovingTellTheirOwnFigures()
        {
            Assert.Equal(new[] { "Stamina: swimming", "6 a second", "4.5 a second", "3 a second" }, Line(new SkillInputs { Name = "Swim", SwimAtNone = 6f, SwimAtTop = 3f }, 50f, "Stamina: swimming"));
            Assert.Equal(new[] { "Jump strength", "100%", "120%", "140%" }, Line(new SkillInputs { Name = "Jump" }, 50f, "Jump strength"));
            // Height grows with the square of the jump's speed: 8 m/s against a pull of 20 m/s each second rises 1.6 m, at the
            // top 3.1 m (Character.Jump); the game's physics, lowering the speed before each step's move, every 0.02 s,
            // leaves 1.5 m and 3 m, as a real jump rises.
            Assert.Equal(new[] { "Jump height, about", "1.6 m", "2.3 m", "3.1 m" }, Line(new SkillInputs { Name = "Jump", JumpForce = 8f, Gravity = 20f }, 50f, "Jump height, about"));
            Assert.Equal(new[] { "Jump height, about", "1.5 m", "2.2 m", "3 m" }, Line(new SkillInputs { Name = "Jump", JumpForce = 8f, Gravity = 20f, PhysicsStep = 0.02f }, 50f, "Jump height, about"));
            Assert.Null(Line(new SkillInputs { Name = "Jump", JumpForce = 8f, Gravity = 0f }, 50f, "Jump height, about"));
            Assert.Equal(new[] { "Stamina: dodging", "15", "11.3", "7.5" }, Line(new SkillInputs { Name = "Dodge", Dodge = 15f }, 50f, "Stamina: dodging"));
            Assert.Equal(new[] { "Mount's running speed", "100%", "110%", "125%" }, Line(new SkillInputs { Name = "Ride" }, 40f, "Mount's running speed"));
            Assert.Equal(new[] { "Mount's stamina", "100%", "80%", "50%" }, Line(new SkillInputs { Name = "Ride" }, 40f, "Mount's stamina"));
        }

        [Fact]
        public void SneakingTellsItsDrainAndHowFarCreaturesSeeYou()
        {
            var sneak = new SkillInputs { Name = "Sneak", Sneak = 5f };
            Assert.Equal(new[] { "Stamina: sneaking", "5 a second", "3.1 a second", "1.3 a second" }, Line(sneak, 25f, "Stamina: sneaking"));
            Assert.Equal(new[] { "Seen in the dark", "50%", "43%", "20%" }, Line(sneak, 25f, "Seen in the dark"));
            Assert.Equal(new[] { "Seen in full light", "100%", "90%", "60%" }, Line(sneak, 25f, "Seen in full light"));
        }

        [Fact]
        public void FishingTellsItsDrainsAndReeling()
        {
            var fishing = new SkillInputs { Name = "Fishing", HookedAtNone = 1f, HookedAtTop = 0.2f, PullAtTop = 0.2f, LineAtNone = 2f, LineAtTop = 6f };
            Assert.Equal(new[] { "Stamina: with a fish on", "1 a second", "0.6 a second", "0.2 a second" }, Line(fishing, 50f, "Stamina: with a fish on"));
            Assert.Equal(new[] { "Stamina: reeling in", "100%", "60%", "20%" }, Line(fishing, 50f, "Stamina: reeling in"));
            Assert.Equal(new[] { "Reeling in", "2 m/s", "4 m/s", "6 m/s" }, Line(fishing, 50f, "Reeling in"));
        }

        [Fact]
        public void MakingTellsTimeAndBonusesOnlyWhereTheyApply()
        {
            var crafting = new SkillInputs { Name = "Crafting", Crafts = true, CraftMostSaved = 0.6f, CraftBonus = 0.25f, BuildStamina = true, WearSaved = 0.5f };
            Assert.Equal(new[] { "Making time", "100%", "70%", "40%" }, Line(crafting, 50f, "Making time"));
            Assert.Equal(new[] { "Bonus: when making", "0%", "12.5%", "25%" }, Line(crafting, 50f, "Bonus: when making"));
            Assert.Equal(new[] { "Building stamina", "100%", "75%", "50%" }, Line(crafting, 50f, "Building stamina"));
            Assert.Equal(new[] { "Tool wear placing", "100%", "75%", "50%" }, Line(crafting, 50f, "Tool wear placing"));
            Assert.Null(Line(crafting, 50f, "Bonus: taking food off"));

            // Cooking's serving tray wears less, though no build table trains it.
            var cooking = new SkillInputs { Name = "Cooking", CookBonus = 0.25f, WearSaved = 0.5f };
            Assert.Equal(new[] { "Bonus: taking food off", "0%", "7.5%", "25%" }, Line(cooking, 30f, "Bonus: taking food off"));
            Assert.Equal(new[] { "Tool wear placing", "100%", "85%", "50%" }, Line(cooking, 30f, "Tool wear placing"));
            Assert.Null(Line(cooking, 30f, "Building stamina"));
            var farming = new SkillInputs { Name = "Farming", PickBonus = 0.25f, ScytheAtNone = 1.5f, ScytheAtTop = 2.5f };
            Assert.Equal(new[] { "Bonus: picking", "0%", "7.5%", "25%" }, Line(farming, 30f, "Bonus: picking"));
            Assert.Equal(new[] { "Scythe reach", "1.5 m", "1.8 m", "2.5 m" }, Line(farming, 30f, "Scythe reach"));
        }

        [Fact]
        public void MagicTellsShieldsAndSummonsByTheWholeLevel()
        {
            var blood = new SkillInputs { Name = "BloodMagic", ShieldListed = 200f, ShieldPerLevel = 5f, SummonPerLevel = 0.02f };
            blood.Summons.Add(("Dead Raiser", new[] { 0, 20, 40, 60, 80 }, new[] { 1, 2, 3, 4, 5 }));
            Assert.Equal(new[] { "Shield absorbs", "200", "350", "700" }, Line(blood, 30f, "Shield absorbs"));
            Assert.Equal(new[] { "Dead Raiser, at once", "1", "2", "5" }, Line(blood, 30f, "Dead Raiser, at once"));
            Assert.Equal(new[] { "Summons' hits", "×1", "×1.6", "×3" }, Line(blood, 30f, "Summons' hits"));
            // Past the top they still grow, by the whole level.
            Assert.Equal(new[] { "Shield absorbs", "200", "800", "700" }, Line(blood, 120f, "Shield absorbs"));
            // A staff whose first step keeps more than one tells its own count from the start.
            var elemental = new SkillInputs { Name = "ElementalMagic", SummonPerLevel = 0.02f };
            elemental.Summons.Add(("Staff of the Wild", new[] { 0, 20, 40, 60, 80 }, new[] { 2, 3, 4, 5, 6 }));
            Assert.Equal(new[] { "Staff of the Wild, at once", "2", "3", "6" }, Line(elemental, 30f, "Staff of the Wild, at once"));
        }

        [Fact]
        public void YourLevelSaysWhatEffectsAdd()
        {
            Assert.Equal("34", SkillPageWords.Level(34f, 0f));
            Assert.Equal("54, 20 of it from effects", SkillPageWords.Level(54f, 20f));
            Assert.Equal("100, the top", SkillPageWords.Level(100f, 0f));
            Assert.Equal("120, 20 of it from effects", SkillPageWords.Level(120f, 20f));
        }

        [Fact]
        public void YourLevelOpensThePageAndStandsOnItsCard()
        {
            Assert.Equal("Level", SkillPageWords.LevelLabel);
            Assert.Equal("To level 35", SkillPageWords.ToNextLabel(34f));
            Assert.Equal("Your level 34", SkillPageWords.Card(34f, 0f));
            Assert.Equal("Your level 54, 20 of it from effects", SkillPageWords.Card(54f, 20f));
        }

        [Fact]
        public void EffectsSayHowMuchTheyAddOrSpeed()
        {
            Assert.Equal("+20", SkillPageWords.AddsLevels(20f));
            Assert.Equal("-5", SkillPageWords.AddsLevels(-5f));
            Assert.Equal("+50%", SkillPageWords.RisesFaster(0.5f));
        }

        [Fact]
        public void TheWayToTheNextLevelIsToldInWhatRaisesIt()
        {
            // 62% of 104.03 kept at level 34; a melee hit on a creature gives 1.5, rested half as much again.
            Assert.Equal("62% there, about 27 hits on creatures, 18 rested", SkillPageWords.ToNext("Swords", 34f, 64.5f, 1f, 1.5f));
            // Running rises 0.2 a second with stamina left.
            Assert.Equal("38% there, about 7.1 min of running, 4.7 min rested", SkillPageWords.ToNext("Run", 41f, 51.7f, 0.2f, 1.5f));
            Assert.Null(SkillPageWords.ToNext("Swords", 100f, 0f, 1f, 1.5f));
            // A skill a mod adds rises however its mod says.
            Assert.Equal("62% there", SkillPageWords.ToNext("1795429391", 34f, 64.5f, 1f, 1.5f));
        }

        [Fact]
        public void TheWayToTheNextLevelCountsHitsWithTheWeaponInHand()
        {
            // A Staff of Frost's bolt rises a fifth of a usual hit (its attack's m_raiseSkillAmount 0.2):
            // 39.53 left of 104.03 at level 34, 0.2 a hit, 0.3 rested.
            Assert.Equal("62% there, about 198 hits on creatures with Staff of Frost, 132 rested",
                SkillPageWords.ToNext("ElementalMagic", 34f, 64.5f, 1f, 1.5f, ("Staff of Frost", 0.2f)));
            // Without one in hand, the skill's usual hit.
            Assert.Equal("62% there, about 40 hits on creatures, 27 rested", SkillPageWords.ToNext("ElementalMagic", 34f, 64.5f, 1f, 1.5f));
        }

        [Fact]
        public void AnAttackRisesHalfAsMuchAgainOnlyAsASwing()
        {
            // A swing hitting a creature rises half as much again (Attack.DoMeleeAttack), an area attack or a projectile as it is.
            Assert.Equal(1.5f, SkillMath.RisePerHit("Horizontal", 1f));
            Assert.Equal(1.5f, SkillMath.RisePerHit("Vertical", 1f));
            Assert.Equal(1f, SkillMath.RisePerHit("Area", 1f));
            Assert.Equal(0.2f, SkillMath.RisePerHit("Projectile", 0.2f));
        }

        [Fact]
        public void AWeaponTrainingItsSkillSlowerThanUsualSaysSoOnItsChip()
        {
            Assert.Equal("×0.2 a hit", SkillPageWords.RiseShare("ElementalMagic", 0.2f));
            // A sledge's area attack against a swing's half again.
            Assert.Equal("×0.67 a hit", SkillPageWords.RiseShare("Clubs", 1f));
            Assert.Null(SkillPageWords.RiseShare("Swords", 1.5f));
            Assert.Null(SkillPageWords.RiseShare("Run", 1f));
        }

        [Fact]
        public void EachSkillSaysWhatRaisesIt()
        {
            Assert.Equal("Hitting creatures with what trains it, once a swing however many it hits", SkillPageWords.RisesBy("Swords"));
            // Anything that breaks but a tree or a creature trains a pickaxe (m_skillHitType Default): rocks, ore, building pieces.
            Assert.Equal("Hitting rocks, ore and other things that break, a tree or a creature aside, with a pickaxe", SkillPageWords.RisesBy("Pickaxes"));
            Assert.Equal("Hitting creatures with what trains it; your summons' hits, half as much, for the one they follow; a shield it cast breaking, for the one it shielded", SkillPageWords.RisesBy("BloodMagic"));
            Assert.Equal("Hitting creatures with what trains it, or an archery target, up to a third as much at its middle", SkillPageWords.RisesBy("Bows"));
            Assert.Equal("Each jump with the stamina for it", SkillPageWords.RisesBy("Jump"));
            Assert.Equal("Each second reeling in, twice as much with a fish on", SkillPageWords.RisesBy("Fishing"));
            Assert.Equal("Putting food on a cooking station and taking it off, cooked or burnt, and making things where it speeds the making", SkillPageWords.RisesBy("Cooking"));
            Assert.Equal("Making things where it speeds the making, each of several made at once; repairing, by how worn it was; building, each piece taken down cancelling the next one built, up to 20", SkillPageWords.RisesBy("Crafting"));
            Assert.Equal("Picking crops and wild plants, by hand or with a scythe, each plant once, and planting with the cultivator", SkillPageWords.RisesBy("Farming"));
            Assert.Equal("Each second running with stamina left", SkillPageWords.RisesBy("Run"));
            Assert.Equal("Each second sneaking, a tenth as much with no creature near enough to notice you", SkillPageWords.RisesBy("Sneak"));
            Assert.Null(SkillPageWords.RisesBy("1795429391"));
        }

        [Fact]
        public void ADeathTakesAShareUnlessYouDiedJustBefore()
        {
            // Skills.OnDeath with the Player prefab's share; Player.OnDeath only after the hard death's wait.
            Assert.Equal("Loses 5% of its level and the way to the next, unless your last death was less than 10 min before", SkillPageWords.OnDeath(0.05f, 600f));
        }

        [Fact]
        public void WhatASkillLeavesAloneIsSaidWherePlayersExpectOtherwise()
        {
            Assert.Equal("Digging the ground", SkillPageWords.NotChanged("Pickaxes").Value.Label);
            Assert.Equal("the same at every level; the skill is mining rocks and ore", SkillPageWords.NotChanged("Pickaxes").Value.Text);
            Assert.Null(SkillPageWords.NotChanged("Run"));
        }
    }
}
