using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class GuideWordsTests
    {
        // The compendium's guide (the ? in its box) reads in a glance before it explains: each
        // heading opens with one short line saying what it is for; the three rules stand side by
        // side as a table; and examples show what a page says, in the very lines a page shows, so
        // they never drift from it.

        [Fact]
        public void EachHeadingOpensWithOneShortLineSayingWhatItIsFor()
        {
            Assert.NotEmpty(GuideWords.Sections);
            foreach (var section in GuideWords.Sections)
            {
                Assert.False(string.IsNullOrEmpty(section.Caption));
                Assert.False(string.IsNullOrEmpty(section.Lead), section.Caption + " has no line saying what it is for");
                Assert.True(section.Lead.Length <= 80, section.Caption + "'s first line runs long: " + section.Lead);
                Assert.DoesNotContain(". ", section.Lead);
            }
        }

        [Fact]
        public void TheGuideTellsWhatEachLookOfDungeonMarkMeans()
        {
            // The marks' words and the dungeon records' docs send the player here for it.
            var marks = GuideWords.Sections.Single(s => s.Caption == "Dungeon marks");
            foreach (var look in new[] { "solid", "ticked", "faint", "hollow", "glyph" }) Assert.Contains(look, marks.Words);
        }

        [Fact]
        public void TheThreeRulesStandSideBySideACellForEach()
        {
            var rules = GuideWords.Sections.First();
            Assert.Equal("Off, Loose and Strict", rules.Caption);
            Assert.Equal(new[] { "Off", "Loose", "Strict" }, rules.Table.Columns);
            Assert.All(rules.Table.Rows, row => Assert.Equal(3, row.Cells.Length));
            // Off shows everything; Strict keeps back what Loose shows.
            var numbers = rules.Table.Rows.Single(r => r.Row.Contains("numbers"));
            Assert.Equal(numbers.Cells[0], numbers.Cells[1]);
            Assert.NotEqual(numbers.Cells[1], numbers.Cells[2]);
        }

        [Fact]
        public void TheExamplesSayWhatAPageSaysInItsOwnLines()
        {
            var rules = GuideWords.Sections.First();
            var said = rules.Examples.SelectMany(e => e.Lines).Select(l => l.Said).ToList();
            Assert.Contains(CompendiumWords.MetOf(3, 6, false), said);
            Assert.Contains(CompendiumWords.MeetLine("loot", 1), said);
            Assert.Contains(CompendiumWords.FoundOutCount(20, 35, false), said);
            Assert.Contains("how many of each", said);
            Assert.All(rules.Examples, e => Assert.False(string.IsNullOrEmpty(e.Title)));
            // Under Loose a drop never met is counted, not named; under Strict a number kept back says what shows it.
            Assert.Equal("Meet 1 more item it drops", CompendiumWords.MeetLine("loot", 1));
        }
    }
}
