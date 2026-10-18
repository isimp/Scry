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
        public void TheFaintLinesSayWhatTheyLeaveOut()
        {
            Assert.Equal("What it comes to at your Swords level now, with what effects add to the level. What you wear and other effects change it further.", SkillWords.YoursTip("Swords"));
        }
    }
}
