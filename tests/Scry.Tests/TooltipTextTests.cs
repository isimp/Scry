using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class TooltipTextTests
    {
        // An item's tooltip as the game shows it with its mods, each line marked where a mod added
        // it: a line whose label the game's own tooltip lacks.

        [Fact]
        public void ATooltipReadsAsPlainLinesWithoutItsMarkup()
        {
            var lines = TooltipText.Lines("A sturdy pack.\n\n\n<color=orange>Weight</color>: <b>4</b>\r\n  Armour: <color=#ffa500ff>1</color>  \n\n");

            Assert.Equal(new[] { "A sturdy pack.", "", "Weight: 4", "Armour: 1" }, lines);
            Assert.Empty(TooltipText.Lines(null));
            Assert.Empty(TooltipText.Lines("\n \n"));
        }

        [Fact]
        public void ALineWhoseLabelTheGamesOwnTooltipLacksIsAMods()
        {
            const string game = "A sturdy pack.\n\nWeight: 4\nDurability: 200/200";
            const string modded = "A sturdy pack.\n\nWeight: 4\nDurability: 200/200\nArmour: 1\nAdded by Adventure Backpacks";

            var marked = TooltipText.Marked(modded, game);

            Assert.Equal(new[] { false, false, false, false, true, true }, marked.Select(m => m.FromMods));
            Assert.Equal("Armour: 1", marked[4].Line);
        }

        [Fact]
        public void ALineAModOnlyRewordedStaysTheGames()
        {
            // A tooltip mod that colours and adds to the game's own lines changes none of their labels.
            const string game = "Weight: 4\nArmour: 10";
            const string modded = "<color=yellow>Weight</color>: 4\nArmour: 10 (+2 per level)";

            Assert.All(TooltipText.Marked(modded, game), m => Assert.False(m.FromMods));
        }

        [Fact]
        public void LabelsAreComparedAsWordsCaseAside() =>
            Assert.False(TooltipText.Marked("ARMOUR : 12", "Armour: 10").Single().FromMods);

        [Fact]
        public void WithoutTheGamesOwnTooltipNothingIsMarked() =>
            Assert.All(TooltipText.Marked("Weight: 4\nArmour: 1", null), m => Assert.False(m.FromMods));

        [Fact]
        public void ABlankLineIsNeverAMods() =>
            Assert.False(TooltipText.Marked("Weight: 4\n\nArmour: 1", "Weight: 4").Single(m => m.Line.Length == 0).FromMods);

        [Fact]
        public void WhetherAnyLineIsAMods()
        {
            Assert.True(TooltipText.AnyFromMods(TooltipText.Marked("Weight: 4\nArmour: 1", "Weight: 4")));
            Assert.False(TooltipText.AnyFromMods(TooltipText.Marked("Weight: 4", "Weight: 4")));
        }

        // How it is told.

        [Fact]
        public void TheNoteNamesTheModsHookingTheTooltip()
        {
            Assert.Equal("Lines with a bar beside them come from mods hooking the tooltip: Adventure Backpacks, MyLittleUI.", TooltipWords.FromMods(new[] { "Adventure Backpacks", "MyLittleUI" }));
            Assert.Equal("Lines with a bar beside them come from mods hooking the tooltip.", TooltipWords.FromMods(new string[0]));
        }

        [Fact]
        public void ALevelChipNamesItsQuality() => Assert.Equal("Level 3", TooltipWords.Level(3));
    }
}
