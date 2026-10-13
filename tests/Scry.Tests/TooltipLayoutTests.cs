using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class TooltipLayoutTests
    {
        // An item's tooltip laid out as a card: its description first, labelled lines as a label
        // beside its value, a line ending in a colon heading the lines under it, other lines as
        // text, blank lines as dividers, and each run of lines a mod added kept together.

        private static List<(string Line, bool FromMods)> Game(params string[] lines) => lines.Select(l => (l, false)).ToList();

        private const string Pack = "An arctic backpack, fit for long treks through the mountains.";

        [Fact]
        public void TheItemsDescriptionLeadsWithNoDividerUnderIt()
        {
            var parts = TooltipLayout.Parts(Game(Pack, "", "Weight: 5.0"), Pack);

            Assert.Equal(new[] { TooltipPartKind.Description, TooltipPartKind.Row }, parts.Select(p => p.Kind));
            Assert.Equal(Pack, parts[0].Value);
        }

        [Fact]
        public void ADescriptionOfSeveralLinesLeadsWhole()
        {
            var parts = TooltipLayout.Parts(Game("A pack.", "Fit for treks.", "", "Weight: 5.0"), "A pack.\n<i>Fit for treks.</i>");

            Assert.Equal(new[] { TooltipPartKind.Description, TooltipPartKind.Description, TooltipPartKind.Row }, parts.Select(p => p.Kind));
        }

        [Fact]
        public void WithoutTheDescriptionFirstTheLinesAreToldAsTheyCome()
        {
            // An item with no description, or one a mod put something before.
            Assert.Equal(TooltipPartKind.Text, TooltipLayout.Parts(Game("One-handed", "Weight: 1.0"), "").First().Kind);
            Assert.Equal(TooltipPartKind.Text, TooltipLayout.Parts(Game("Crafted by a mod", Pack), Pack).First().Kind);
            Assert.Equal(TooltipPartKind.Row, TooltipLayout.Parts(Game("Weight: 5.0"), Pack).Single().Kind);
            // A tooltip shorter than the description holds none of it.
            Assert.Equal(TooltipPartKind.Text, TooltipLayout.Parts(Game("A pack."), "A pack.\nFit for treks.").Single().Kind);
        }

        [Fact]
        public void ALabelledLineIsItsLabelBesideItsValue()
        {
            var row = TooltipLayout.Parts(Game("Max carry weight: +20"), "").Single();

            Assert.Equal(TooltipPartKind.Row, row.Kind);
            Assert.Equal("Max carry weight", row.Label);
            Assert.Equal("+20", row.Value);
            Assert.Equal("", row.Extra);
        }

        [Fact]
        public void AFiguresBracketedAsideIsToldApartFromIt()
        {
            var speed = TooltipLayout.Parts(Game("Movement speed: -15% (Total:-10%)"), "").Single();
            Assert.Equal("Movement speed", speed.Label);
            Assert.Equal("-15%", speed.Value);
            Assert.Equal("(Total:-10%)", speed.Extra);

            var slash = TooltipLayout.Parts(Game("Slash: 55 (19-35)"), "").Single();
            Assert.Equal(("55", "(19-35)"), (slash.Value, slash.Extra));

            var nested = TooltipLayout.Parts(Game("Value: 5 (10 (stack) total)"), "").Single();
            Assert.Equal(("5", "(10 (stack) total)"), (nested.Value, nested.Extra));
        }

        [Fact]
        public void AValueWhollyInBracketsOrWithBracketsInsideStaysWhole()
        {
            var whole = TooltipLayout.Parts(Game("Effect: (none)"), "").Single();
            Assert.Equal(("(none)", ""), (whole.Value, whole.Extra));

            var inside = TooltipLayout.Parts(Game("Set effect (3 parts): Troll leather"), "").Single();
            Assert.Equal(("Set effect (3 parts)", "Troll leather", ""), (inside.Label, inside.Value, inside.Extra));

            var unclosed = TooltipLayout.Parts(Game("Range: 5 (more)x"), "").Single();
            Assert.Equal(("5 (more)x", ""), (unclosed.Value, unclosed.Extra));
        }

        [Fact]
        public void ALineEndingInAColonHeadsTheLinesUnderIt()
        {
            var parts = TooltipLayout.Parts(Game("Recycle Yield:", "1x Wolf Fur Cape"), "");

            Assert.Equal(new[] { TooltipPartKind.Heading, TooltipPartKind.Text }, parts.Select(p => p.Kind));
            Assert.Equal("Recycle Yield", parts[0].Value);
            Assert.Equal("1x Wolf Fur Cape", parts[1].Value);
        }

        [Fact]
        public void ALineWithoutAShortLabelIsText()
        {
            Assert.Equal(TooltipPartKind.Text, TooltipLayout.Parts(Game("Recipe for Arctic Sherpa Pack not known., Recipe requires Forge of level 1"), "").Single().Kind);
            Assert.Equal(TooltipPartKind.Text, TooltipLayout.Parts(Game(": 5"), "").Single().Kind);
            Assert.Equal(TooltipPartKind.Text, TooltipLayout.Parts(Game(":"), "").Single().Kind);
            // A label with nothing after it but spaces is no row with an empty value.
            Assert.Equal(TooltipPartKind.Text, TooltipLayout.Parts(Game("Weight:   "), "").Single().Kind);
            // A sentence with a colon late in it is not a label.
            Assert.Equal(TooltipPartKind.Text, TooltipLayout.Parts(Game("Hold the attack button to charge it up, then let go: it flies"), "").Single().Kind);
            Assert.Equal(TooltipPartKind.Row, TooltipLayout.Parts(Game("Recipe requires the forge at level two: yes"), "").Single().Kind);
            // A web address is not a label and its value.
            Assert.Equal(TooltipPartKind.Text, TooltipLayout.Parts(Game("https://thunderstore.io"), "").Single().Kind);
        }

        [Fact]
        public void BlankLinesBetweenGroupsAreDividers()
        {
            var parts = TooltipLayout.Parts(Game("Weight: 5.0", "", "Max carry weight: +20"), "");

            Assert.Equal(new[] { TooltipPartKind.Row, TooltipPartKind.Divider, TooltipPartKind.Row }, parts.Select(p => p.Kind));
        }

        [Fact]
        public void EachLineKeepsWhetherAModAddedIt()
        {
            var lines = new List<(string, bool)> { ("Weight: 5.0", false), ("Armour: 1", true), ("Recycle Yield:", true), ("1x Wolf Fur Cape", true) };

            Assert.Equal(new[] { false, true, true, true }, TooltipLayout.Parts(lines, "").Select(p => p.FromMods));
        }

        [Fact]
        public void ADividerWithinAModsRunIsTheRunsAndOneBesideTheGamesLinesIsNot()
        {
            var lines = new List<(string, bool)> { ("Quality: 1", false), ("", false), ("Recycle Yield:", true), ("", false), ("Adventure Backpacks", true), ("", false), ("Weight: 5.0", false) };

            var dividers = TooltipLayout.Parts(lines, "").Where(p => p.Kind == TooltipPartKind.Divider).Select(p => p.FromMods);

            Assert.Equal(new[] { false, true, false }, dividers);
        }

        [Fact]
        public void TheModsRunsAreFoundAcrossTheirDividers()
        {
            var lines = new List<(string, bool)> { ("Weight: 5.0", false), ("Armour: 1", true), ("", false), ("Movement speed: -15%", false), ("", false), ("Recycle Yield:", true), ("", false), ("Adventure Backpacks", true) };

            var runs = TooltipLayout.ModRuns(TooltipLayout.Parts(lines, ""));

            Assert.Equal(new[] { (1, 1), (5, 7) }, runs);
        }

        // How it is told.

        [Fact]
        public void TheQualitiesAreNumberedFromOne() => Assert.Equal(new[] { "1", "2", "3", "4" }, TooltipWords.Qualities(4));

        [Fact]
        public void TheLegendSaysWhatTheTintMeansAndIntroducesTheModsWhereAnyAreNamed()
        {
            Assert.Equal("Tinted lines come from the mods hooking the tooltip:", TooltipWords.Legend(true));
            Assert.Equal("Tinted lines come from mods hooking the tooltip.", TooltipWords.Legend(false));
        }
    }
}
