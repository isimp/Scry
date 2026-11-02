using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The settings card's sliders move in small steps within what the config file allows; the
    /// View box's speed offers a few steps, a value written in the config file between two
    /// lighting the nearest; each reads as a player says it.
    /// </summary>
    public class SettingsWordsTests
    {
        private static SettingRow Slider(string key) => SettingsCatalog.Sections.SelectMany(s => s.Rows).Single(r => r.Control == SettingControl.Slider && r.Keys[0] == key);

        [Fact]
        public void AValueBetweenTwoStepsLightsTheNearest()
        {
            Assert.Equal(1, SettingsWords.Nearest(StageWords.SpinSpeeds, 14f));
            Assert.Equal(2, SettingsWords.Nearest(StageWords.SpinSpeeds, 30f));
        }

        [Fact]
        public void ASliderMovesInSmallStepsWithinWhatTheConfigAllows()
        {
            var size = Slider("PanelScale");
            Assert.Equal(1.05f, SettingsCatalog.Stepped(size, 1.04f), 3);
            Assert.Equal(1.25f, SettingsCatalog.Stepped(size, 1.26f), 3);
            Assert.Equal(0.6f, SettingsCatalog.Stepped(size, 0.1f), 3);
            Assert.Equal(2f, SettingsCatalog.Stepped(size, 5f), 3);
            // The dungeon marks' size goes in tenths of a pin, from half to twice one.
            var marks = Slider("DungeonMarkSize");
            Assert.Equal(1f, SettingsCatalog.Stepped(marks, 1.04f), 3);
            Assert.Equal(1.3f, SettingsCatalog.Stepped(marks, 1.26f), 3);
            Assert.Equal(0.5f, SettingsCatalog.Stepped(marks, 0.1f), 3);
            Assert.Equal(2f, SettingsCatalog.Stepped(marks, 5f), 3);
            // The recent list in whole entries; a tip's wait any value, in twentieths of a second.
            Assert.Equal(31f, SettingsCatalog.Stepped(Slider("RecentCount"), 30.6f), 3);
            Assert.Equal(2f, SettingsCatalog.Stepped(Slider("TooltipDelay"), 2.01f), 3);
        }

        [Fact]
        public void EachStepReadsAsAPlayerSaysIt()
        {
            Assert.Equal("14°/s", StageWords.SpinSpeedName(14f));
        }

        [Fact]
        public void TheSetupNamesTheCharacterAndOffersEveryRule()
        {
            Assert.Equal("Compendium for Ragna", SettingsWords.SetupCompendium("Ragna"));
            Assert.Equal("Compendium for your character", SettingsWords.SetupCompendium(""));
            Assert.Equal(new[] { CompendiumRule.Off, CompendiumRule.Loose, CompendiumRule.Strict }, SettingsWords.SetupRules.Select(r => r.Rule));
        }
    }
}
