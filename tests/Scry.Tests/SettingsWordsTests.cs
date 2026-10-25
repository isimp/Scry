using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The settings card's slider sets the panel's size in small steps within what the config file
    /// allows; the wait before a tip and the View box's speed offer a few steps, a value written in
    /// the config file between two lighting the nearest; each reads as a player says it.
    /// </summary>
    public class SettingsWordsTests
    {
        [Fact]
        public void AValueBetweenTwoStepsLightsTheNearest()
        {
            Assert.Equal(1, SettingsWords.Nearest(SettingsWords.TipWaits, 0.5f));
            Assert.Equal(0, SettingsWords.Nearest(SettingsWords.TipWaits, 0f));
            Assert.Equal(2, SettingsWords.Nearest(SettingsWords.TipWaits, 3f));
            Assert.Equal(1, SettingsWords.Nearest(StageWords.SpinSpeeds, 14f));
            Assert.Equal(2, SettingsWords.Nearest(StageWords.SpinSpeeds, 30f));
        }

        [Fact]
        public void TheSizeSliderMovesInSmallStepsWithinWhatTheConfigAllows()
        {
            Assert.Equal(1.05f, SettingsWords.SizeStep(1.04f), 3);
            Assert.Equal(1.25f, SettingsWords.SizeStep(1.26f), 3);
            Assert.Equal(0.6f, SettingsWords.SizeStep(0.1f), 3);
            Assert.Equal(2f, SettingsWords.SizeStep(5f), 3);
            // The dungeon marks' size goes in tenths of a pin, from half to twice one.
            Assert.Equal(1f, SettingsWords.MarkSizeStep(1.04f), 3);
            Assert.Equal(1.3f, SettingsWords.MarkSizeStep(1.26f), 3);
            Assert.Equal(0.5f, SettingsWords.MarkSizeStep(0.1f), 3);
            Assert.Equal(2f, SettingsWords.MarkSizeStep(5f), 3);
            Assert.Equal("1.3", SettingsWords.SizeName(SettingsWords.MarkSizeStep(1.26f)));
        }

        [Fact]
        public void EachStepReadsAsAPlayerSaysIt()
        {
            Assert.Equal("1.0", SettingsWords.SizeName(1f));
            Assert.Equal("1.25", SettingsWords.SizeName(1.25f));
            Assert.Equal("0.9", SettingsWords.SizeName(0.9f));
            Assert.Equal("14°/s", StageWords.SpinSpeedName(14f));
            Assert.Equal("Now", SettingsWords.TipWaitName(0f));
            Assert.Equal("0.35 s", SettingsWords.TipWaitName(0.35f));
            Assert.Equal("1 s", SettingsWords.TipWaitName(1f));
        }

        [Fact]
        public void TheQuestionNamesTheCharacterWhereItHasAName()
        {
            Assert.Equal("Keep a compendium for Ragna?", SettingsWords.WelcomeTitle("Ragna"));
            Assert.Equal("Keep a compendium for your character?", SettingsWords.WelcomeTitle(""));
        }
    }
}
