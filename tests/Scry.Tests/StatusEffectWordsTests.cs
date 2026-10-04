using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class StatusEffectWordsTests
    {
        // A status effect's page tells how long it lasts and, in the game's own words where its
        // tooltip has them, what it changes; each setting differing from a fresh one is told too.

        [Fact]
        public void AnEffectTellsHowLongItLastsOrThatItHasNoLimitOfItsOwn()
        {
            Assert.Equal("30 s", StatusEffectWords.Lasts(30f));
            Assert.Equal("no time limit of its own", StatusEffectWords.Lasts(0f));
        }

        private static string[] Pairs(string tooltip, string intro = "") =>
            StatusEffectWords.TooltipPairs(tooltip, intro, "Duration", "Damage modifier").Select(p => p.ToString()).ToArray();

        [Fact]
        public void TheGamesTooltipLinesBecomeLines()
        {
            Assert.Equal(new[] { "Health regen: +20%", "Stamina regen: +10%" }, Pairs("Health regen: +20%\nStamina regen: +10%"));
        }

        [Fact]
        public void ASkillLineIsTheSkillAndWhatItAdds()
        {
            Assert.Equal(new[] { "Swords: +15" }, Pairs("Swords +15"));
        }

        [Fact]
        public void TheDescriptionItStartsWithIsLeftOutAsThePageShowsIt()
        {
            Assert.Equal(new[] { "Health regen: +20%" }, Pairs("Rested and well fed.\n\nHealth regen: +20%", "Rested and well fed."));
        }

        [Fact]
        public void ItsDurationAndResistancesAreLeftOutAsTheyAreToldApart()
        {
            Assert.Equal(new[] { "Speed: -15%" }, Pairs("Duration: 10m\nDamage modifier: Fire\nSpeed: -15%"));
        }

        [Fact]
        public void LinesWithNothingToTellAreLeftOut()
        {
            Assert.Equal(new[] { "Speed: -15%" }, Pairs("\r\n  \nAlone\nSpeed: -15%\r\n"));
            Assert.Empty(Pairs(""));
            Assert.Empty(Pairs(null));
        }

        [Fact]
        public void ALineWithoutALabelOrAValueTellsNothing()
        {
            Assert.Empty(Pairs("Ends with:"));
            Assert.Empty(Pairs(": nothing"));
        }

        [Fact]
        public void ADescriptionOverSeveralLinesIsLeftOutWhateverItsLineEnds()
        {
            Assert.Equal(new[] { "Health regen: +20%" }, Pairs("Rested.\r\nWell fed.\r\nHealth regen: +20%", "Rested.\r\nWell fed."));
        }

        [Theory]
        [InlineData("m_timeScale", false)]
        [InlineData("m_cooldown", true)]
        [InlineData("m_tickInterval", true)]
        [InlineData("m_effectDuration", true)]
        [InlineData("m_damageTime", true)]
        [InlineData("m_healthRegenMultiplier", false)]
        [InlineData("m_cooldownModifier", false)]
        [InlineData("m_speed", false)]
        public void ASettingHoldingATimeIsToldWithItsUnit(string field, bool time) => Assert.Equal(time, StatusEffectWords.IsTime(field));
    }
}
