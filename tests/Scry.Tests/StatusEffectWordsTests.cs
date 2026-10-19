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

        [Theory]
        [InlineData("m_healthOverTimeInterval", "Heals every")]
        [InlineData("m_healthOverTimeDuration", "Heals over")]
        [InlineData("m_staminaOverTimeDuration", "Gives stamina over")]
        [InlineData("m_modifyAttackSkill", "Stronger attacks with")]
        [InlineData("m_damageModifier", "Their damage")]
        [InlineData("m_raiseSkill", "Trains faster")]
        [InlineData("m_raiseSkillModifier", "Trains faster by")]
        [InlineData("m_attributes", "Also")]
        [InlineData("m_effectsOnlyOnPlayer", "Shows on")]
        [InlineData("m_staminaDrainPerSec", "Drains stamina")]
        [InlineData("m_staminaOverTimeIsFraction", "Stamina given as")]
        [InlineData("m_absorbDamageWorldLevel", "Absorbs more per world level")]
        [InlineData("m_absorbDamagePerSkillLevel", "Absorbs more per skill level")]
        [InlineData("m_levelUpSkillOnBreak", "Trains when it breaks")]
        [InlineData("m_ttlPerItemLevel", "Lasts per quality")]
        [InlineData("m_windMovementModifier", "Speed with the wind behind")]
        [InlineData("m_windRunStaminaModifier", "Run stamina with the wind behind")]
        [InlineData("m_tickInterval", "Ticks every")]
        [InlineData("m_healthPerTick", "Health each tick")]
        [InlineData("m_removeInterval", "Throws up a food every")]
        [InlineData("m_delay", "Takes hold after")]
        // A setting with no words of its own reads as its name in words.
        [InlineData("m_runStaminaUsage", "Run stamina usage")]
        public void ASettingTheGameDoesNotTellReadsInPlayersWords(string field, string label) => Assert.Equal(label, StatusEffectWords.Label(field));

        [Fact]
        public void BesideItsButtonAStatusEffectSaysItShowsOnlyItsLook()
        {
            Assert.Equal("only the look, never the effect", StatusEffectWords.OnlyTheLook);
            Assert.Equal("nothing of its own to see or hear", StatusEffectWords.NoLook);
        }

        [Fact]
        public void ASettingsValueReadsAsWhatItDoes()
        {
            Assert.Equal("×1.2", StatusEffectWords.Shown("m_damageModifier", 1.2f, "1.2"));
            Assert.Equal("+50%", StatusEffectWords.Shown("m_raiseSkillModifier", 0.5f, "0.5"));
            Assert.Equal("every skill", StatusEffectWords.Shown("m_raiseSkill", "All", "All"));
            Assert.Equal("every skill", StatusEffectWords.Shown("m_modifyAttackSkill", "All", "All"));
            Assert.Equal("Bows", StatusEffectWords.Shown("m_modifyAttackSkill", "Bows", "Bows"));
            Assert.Equal("12", StatusEffectWords.Shown("m_health", 12f, "12"));
            Assert.Equal("players only", StatusEffectWords.Shown("m_effectsOnlyOnPlayer", true, "Yes"));
            Assert.Equal("2.5 a second", StatusEffectWords.Shown("m_staminaDrainPerSec", 2.5f, "2.5"));
            Assert.Equal("a share of your most stamina", StatusEffectWords.Shown("m_staminaOverTimeIsFraction", true, "Yes"));
            Assert.Equal("60 s", StatusEffectWords.Shown("m_ttlPerItemLevel", 60f, "60"));
            Assert.Equal("60 s", StatusEffectWords.Shown("m_ttlPerItemLevel", 60, "60"));
            // Full at a wind of full strength straight behind, none against it (SE_Stats.ModifySpeed, ModifyRunStaminaDrain).
            Assert.Equal("up to +15%, the wind full behind you", StatusEffectWords.Shown("m_windMovementModifier", 0.15f, "0.15"));
            Assert.Equal("up to -50%, the wind full behind you", StatusEffectWords.Shown("m_windRunStaminaModifier", -0.5f, "-0.5"));
            Assert.Equal("+5", StatusEffectWords.Shown("m_healthPerTick", 5f, "5"));
            Assert.Equal("3 damage", StatusEffectWords.Shown("m_healthPerTick", -3f, "-3"));
        }

        [Fact]
        public void WhatAnEffectMarksACharacterWithReadsAsWhatTheGameDoesForIt()
        {
            // StatusEffect.StatusAttribute, read where the game asks for each (Ship.IsWindControllActive,
            // Tameable, ImpactEffect); cold resistance it asks for nowhere.
            Assert.Equal("the wind blows the way your ship sails", StatusEffectWords.Shown("m_attributes", "SailingPower", "Sailing power"));
            Assert.Equal("tames animals near you faster; falling and rolling things hit twice as hard",
                StatusEffectWords.Shown("m_attributes", "TamingBoost, DoubleImpactDamage", "Taming boost, double impact damage"));
            Assert.Equal("a mark of cold resistance the game itself never reads", StatusEffectWords.Shown("m_attributes", "ColdResistance", "Cold resistance"));
            // One a mod adds reads as its name in words.
            Assert.Equal("Glowing skin", StatusEffectWords.Shown("m_attributes", "GlowingSkin", "Glowing skin"));
        }
    }
}
