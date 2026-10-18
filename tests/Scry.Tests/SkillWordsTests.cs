using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class SkillWordsTests
    {
        // A skill the game has goes by its name. A skill a mod adds has no name in the game's
        // list, only a number, and its mod gives it a name under the game's own word for a
        // skill ("$skill_" and that number), as the game's skill screen looks it up.

        private static string Localize(string token)
        {
            var words = new Dictionary<string, string> { ["$skill_1795429391"] = "Ranching", ["$skill_swords"] = "Schwerter" };
            return words.TryGetValue(token, out var word) ? word : "";
        }

        [Fact]
        public void AGameSkillGoesByItsName()
        {
            Assert.Equal("Swords", SkillWords.Name("Swords", Localize));
            Assert.Equal("Elemental magic", SkillWords.Name("ElementalMagic", Localize));
        }

        [Fact]
        public void AModsSkillGoesByTheNameItsModGivesIt()
        {
            Assert.Equal("Ranching", SkillWords.Name("1795429391", Localize));
        }

        [Fact]
        public void AModsSkillWithNoNameIsStillTold()
        {
            Assert.Equal("a skill a mod adds", SkillWords.Name("-48213", Localize));
            Assert.Equal("a skill a mod adds", SkillWords.Name("48213", null));
        }

        [Fact]
        public void ASkillsNameIsLookedUpAsTheGameLooksItUp()
        {
            // SkillsDialog and SE_Stats.GetTooltipString: "$skill_" and the skill's name in small letters.
            Assert.Equal("$skill_woodcutting", SkillWords.Token("WoodCutting"));
            Assert.Equal("$skill_1795429391", SkillWords.Token("1795429391"));
        }

        [Fact]
        public void NoSkillIsNone()
        {
            Assert.Null(SkillWords.Name("", Localize));
            Assert.Null(SkillWords.Name(null, Localize));
        }

        // A figure at your own skill reads faint under the game's, labelled with the skill and
        // the level the game works with: what your next swing deals, not what the item lists.

        [Fact]
        public void AFigureAtYourSkillSaysWhichSkillAndLevel()
        {
            Assert.Equal("at Swords 34", SkillWords.At("Swords", 34f));
            Assert.Equal("at Wood Cutting 0", SkillWords.At("Wood Cutting", 0f));
            Assert.Equal("at Blocking 20: 15", SkillWords.Yours("at Blocking 20", "15"));
            Assert.Null(SkillWords.Yours("at Blocking 20", null));
        }

        [Fact]
        public void YourHitsAreARangeOfWholeNumbers()
        {
            // The game rolls each hit between two ends and tells them rounded, as its own tooltip does (HitData.DamageTypes.GetTooltipString).
            Assert.Equal("25–41", SkillWords.Range(24.97f, 41.47f));
            Assert.Equal("55", SkillWords.Range(55f, 55.2f));
            Assert.Equal("25–41 slash, 5–8 fire", SkillWords.DamageRange(new[] { ("fire", 10f), ("slash", 55f), ("pierce", 0f) }, 0.454f, 0.754f));
            Assert.Null(SkillWords.DamageRange(new[] { ("slash", 0f) }, 0.454f, 0.754f));
        }

        [Fact]
        public void AnAttackAtYourSkillLinesUpUnderItsLineInTheTable()
        {
            // The table's columns: the attack, damage, knockback, stagger and costs; stagger is the attack's own multiplier, which the skill leaves.
            var line = SkillWords.AttackLine("at Swords 34", new[] { ("slash", 55f) }, 40f, (0.454f, 0.754f), "8.9 stamina");
            Assert.Equal(new[] { "at Swords 34", "25–41 slash", "18–30", "", "8.9 stamina" }, line);
            var bare = SkillWords.AttackLine("at Swords 34", new[] { ("slash", 0f) }, 0f, (0.454f, 0.754f), null);
            Assert.Equal(new[] { "at Swords 34", "–", "–", "", "–" }, bare);
        }

        [Fact]
        public void AComboAtYourSkillHasALineForEachSwing()
        {
            // As the table's own lines (CombatWords.WeaponCombo): the last swing deals its multiple and knocks back 1.2 times.
            var lines = SkillWords.AttackLines("at Swords 34", 3, new[] { ("slash", 55f) }, 40f, 2f, (0.454f, 0.754f), "8.9 stamina");
            Assert.Equal(3, lines.Count);
            Assert.Equal(new[] { "at Swords 34", "25–41 slash", "18–30", "", "8.9 stamina" }, lines[0]);
            Assert.Equal(lines[0], lines[1]);
            Assert.Equal(new[] { "at Swords 34", "50–83 slash", "22–36", "", "8.9 stamina" }, lines[2]);
            Assert.Single(SkillWords.AttackLines("at Swords 34", 1, new[] { ("slash", 55f) }, 40f, 1f, (0.454f, 0.754f), "8.9 stamina"));
        }

        [Fact]
        public void AnAxeOnATreeIsToldByTheSkillTreesTake()
        {
            Assert.Equal("on trees, at Wood Cutting 12", SkillWords.OnTrees("at Wood Cutting 12"));
            // Only what it deals: a tree is pushed by nothing, and the attack's cost is the axe's own.
            var lines = SkillWords.TreeLines("on trees, at Wood Cutting 10", 2, new[] { ("slash", 30f), ("chop", 40f) }, 2f, (0.31f, 0.61f));
            Assert.Equal(new[] { "on trees, at Wood Cutting 10", "12–24 chop, 9–18 slash", "", "", "" }, lines[0]);
            Assert.Equal(new[] { "on trees, at Wood Cutting 10", "25–49 chop, 19–37 slash", "", "", "" }, lines[1]);
        }

        [Fact]
        public void AFigureTheSkillMovesTellsBothEnds()
        {
            Assert.Equal("1.5 m, 2.5 m at the top", SkillWords.Span(Numbers.Metres(1.5f, 1), Numbers.Metres(2.5f, 1)));
            Assert.Equal("7.7 m/s", SkillWords.Speed(7.7175f));
            Assert.Equal("6.4 a second", SkillWords.PerSecond(6.36f));
        }

        [Fact]
        public void ACreatureSeesYouSneakingNearerTheDarkerItIs()
        {
            // Player.UpdateStealth: its sight times your stealth, from the dark to full light.
            Assert.Equal("sneaking, 8.5 m in the dark to 18 m in full light", SkillWords.Sneaking(8.5f, 18f));
        }

        [Fact]
        public void ABonusSaysHowMuchAndHowLikely()
        {
            Assert.Equal("1 more at times: never at no skill, 25% at the top", SkillWords.BonusSpan(1, 0.25f));
            Assert.Equal("7.5% for 1 more", SkillWords.Bonus(1, 0.075f));
        }

        [Fact]
        public void AStaffTellsHowManySummonsItKeepsAtEachLevel()
        {
            // SpawnAbility.Spawn: the step whose level the skill reaches sets how many it keeps at once.
            Assert.Equal("1, then 2 at level 20, 3 at 40, 4 at 60 and 5 at 80", SkillWords.SummonSteps(new[] { 0, 20, 40, 60, 80 }, new[] { 1, 2, 3, 4, 5 }));
            Assert.Equal("1, then 3 at level 80", SkillWords.SummonSteps(new[] { 0, 80 }, new[] { 1, 3 }));
            Assert.Equal("3", SkillWords.SummonSteps(new[] { 0 }, new[] { 3 }));
            Assert.Equal("none, then 2 at level 10", SkillWords.SummonSteps(new[] { 10 }, new[] { 2 }));
            Assert.Equal("×1, and ×0.02 more for each level", SkillWords.SummonsHit(0.02f));
            Assert.Equal("2", SkillWords.SummonsAt(new[] { 0, 20, 40 }, new[] { 1, 2, 3 }, 34f));
            Assert.Equal("none", SkillWords.SummonsAt(new[] { 10 }, new[] { 2 }, 5f));
            Assert.Equal("1", SkillWords.SummonsAt(new[] { 0, 20, 40 }, new[] { 1, 2, 3 }, 5f));
        }

        [Fact]
        public void TheTableByQualityHasYourHitsAndBlockAtEachQuality()
        {
            // ItemData.GetDamage and GetBaseBlockPower add so much a level; the skill's share works on each.
            var damage = SkillWords.DamageByQuality("at Swords 34", new[] { ("slash", 55f, 6f) }, 3, (0.454f, 0.754f));
            Assert.Equal(new[] { "at Swords 34", "25–41 slash", "28–46 slash", "30–51 slash" }, damage);
            Assert.Null(SkillWords.DamageByQuality("at Swords 34", new[] { ("slash", 55f, 0f) }, 3, (0.454f, 0.754f)));
            Assert.Null(SkillWords.DamageByQuality("at Swords 34", new[] { ("slash", 55f, 6f) }, 1, (0.454f, 0.754f)));
            Assert.Equal(new[] { "at Blocking 20", "11", "16.5", "22" }, SkillWords.BlockByQuality("at Blocking 20", 10f, 5f, 3, 20f));
            Assert.Null(SkillWords.BlockByQuality("at Blocking 20", 10f, 0f, 3, 20f));
        }

        [Fact]
        public void AParryTellsWhatItHoldsBackAtYourBlocking()
        {
            Assert.Equal("22 held back on a parry", SkillWords.Parry(22f));
        }

        [Fact]
        public void AFishTellsWhatReelingItInCosts()
        {
            // FishingFloat.FixedUpdate: the fish's own drain, more while it pulls away, times its level.
            Assert.Equal("1 stamina a second, 2 while it pulls away, each times its level", SkillWords.Reeling(1f, 2f, true));
            Assert.Equal("0.6 stamina a second, 1.2 while it pulls away", SkillWords.Reeling(0.6f, 1.2f, false));
        }

        [Fact]
        public void ASummonTellsWhoseSkillItTrainsAndHowHardItHitsForYou()
        {
            // Character.RaiseSkill: a tamed creature's hits raise its owner's skill by its share.
            Assert.Equal("its owner's Blood Magic, 50% of a rise each", SkillWords.TrainsOwner("Blood Magic", 0.5f));
            Assert.Equal("summoned at Blood Magic 30", SkillWords.SummonedAt("at Blood Magic 30"));
            // Character.GetRandomSkillFactor for anything but a player: the square root of an even roll from 0.75 to 1, times its strength.
            Assert.Equal(new[] { "summoned at Blood Magic 30", "28–32 slash", "", "", "" },
                SkillWords.SummonedLine("summoned at Blood Magic 30", new[] { ("slash", 20f) }, 1.6f));
        }

        [Fact]
        public void AnArcheryTargetTellsWhatItTrains()
        {
            // ArcheryTarget: a hit raises the skill of what was shot, more the nearer its middle.
            Assert.Equal("the skill of what hits it, more the nearer its middle", SkillWords.TargetTrains(1f));
            Assert.Equal("the skill of what hits it, more the nearer its middle, ×2 as much", SkillWords.TargetTrains(2f));
        }

        [Fact]
        public void YouRunFasterAtYourRun()
        {
            Assert.Equal("runs 7.7 m/s", SkillWords.Runs(7.7175f));
        }

        [Fact]
        public void TheFaintLinesSayWhatTheyLeaveOut()
        {
            Assert.Equal("What it comes to at your Swords level now, with what effects add to the level. What you wear and other effects change it further.", SkillWords.YoursTip("Swords"));
        }
    }
}
