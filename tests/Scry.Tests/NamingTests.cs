using Xunit;

namespace Scry.Tests
{
    public class NamingTests
    {
        // Scry's text is English whatever language the PC is set to, its numbers with it: a
        // count's thousands by commas, a fraction's point a point.

        // A list of names reads as a sentence: "A", "A and B", "A, B and C".

        [Fact]
        public void AListReadsAsASentence()
        {
            Assert.Equal("", Naming.Joined(new string[0]));
            Assert.Equal("wood", Naming.Joined(new[] { "wood" }));
            Assert.Equal("wood and stone", Naming.Joined(new[] { "wood", "stone" }));
            Assert.Equal("wood, stone and resin", Naming.Joined(new[] { "wood", "stone", "resin" }));
        }

        // An amount or a length keeps up to two decimals and its thousands by commas.

        [Fact]
        public void AmountsAndLengthsKeepTwoDecimalsAndTheirThousands()
        {
            Assert.Equal("1,234.5", Naming.Amount(1234.5f));
            Assert.Equal("0.25", Naming.Amount(0.25f));
            Assert.Equal("3", Naming.Amount(3f));
            Assert.Equal("1,500 m", Naming.Metres(1500f));
        }

        [Fact]
        public void NumbersReadTheSameWhateverThePcsLanguage()
        {
            var was = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
                Assert.Equal("1,234", Naming.Count(1234));
                Assert.Equal("12", Naming.Count(12));
                Assert.Equal("2.5", Naming.Number(2.5f));
                Assert.Equal("0.4%", DropWords.Share(0.004f));
                Assert.Equal("1,200 within 10 m, 3 within 40 m", SpawnWords.SpawnerCaps(1200, 10f, 3, 40f));
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = was;
            }
        }

        [Theory]
        [InlineData("m_startEffects", "Start")]
        [InlineData("m_stopEffects", "Stop")]
        [InlineData("m_tickEffect", "Tick")]
        [InlineData("m_breakEffects", "Break")]
        [InlineData("m_healthUpgradeEffect", "Health upgrade")]
        public void AStatusEffectsEffectListIsOfferedUnderAPlainName(string field, string shown)
        {
            Assert.Equal(shown, Naming.EffectListLabel(field));
        }

        [Theory]
        [InlineData("m_staminaRegenMultiplier", "Stamina regen multiplier")]
        [InlineData("m_addMaxCarryWeight", "Add max carry weight")]
        [InlineData("m_speed", "Speed")]
        [InlineData("m_raiseSkill", "Raise skill")]
        public void AFieldIsShownUnderAPlainName(string field, string shown)
        {
            Assert.Equal(shown, Naming.FieldLabel(field));
        }

        [Theory]
        [InlineData("<color=orange>Lord Reto</color>", "Lord Reto")]
        [InlineData("<b>Bold</b> and <i>plain</i>", "Bold and plain")]
        [InlineData("<size=20><color=#ff0000>Big</color></size>", "Big")]
        [InlineData("Less < more > still", "Less < more > still")]
        [InlineData("Troll", "Troll")]
        public void ANameIsShownWithoutTheMarkupAModPutInIt(string raw, string shown)
        {
            Assert.Equal(shown, Naming.Plain(raw));
        }

        [Theory]
        [InlineData("m_effects")]
        [InlineData("")]
        [InlineData(null)]
        public void AListWithNoNameOfItsOwnIsJustCalledEffect(string field)
        {
            Assert.Equal("Effect", Naming.EffectListLabel(field));
        }
    
        [Theory]
        [InlineData("m_fireworkItemList", "m_fireworksEffects", "Fireworks")]
        [InlineData("m_projectileHitEffects", "m_effect", "Projectile hit")]
        [InlineData("m_projectileHitEffects", "m_effects", "Projectile hit")]
        public void AListInsideTheGameDataOfAFieldGoesByItsOwnNameElseByTheField(string outer, string inner, string shown)
        {
            Assert.Equal(shown, Naming.NestedListLabel(outer, inner));
        }

        [Theory]
        [InlineData("m_fireworkItemList", "m_fireworksEffects", "Blue fireworks", "Blue fireworks: fireworks")]
        [InlineData("m_projectileHitEffects", "m_effect", "Arrow", "Arrow: projectile hit")]
        [InlineData("m_projectileHitEffects", "m_effect", "", "Projectile hit")]
        public void OneOfSeveralListsInTheGameDataIsNamedAfterWhatItIsFor(string outer, string inner, string of, string shown)
        {
            Assert.Equal(shown, Naming.NestedListLabel(outer, inner, of));
        }

        [Theory]
        [InlineData(40f, "40 s")]
        [InlineData(119f, "119 s")]
        [InlineData(120f, "2 min")]
        [InlineData(90f * 60f, "90 min")]
        [InlineData(1500f, "25 min")]
        [InlineData(7200f, "2 h")]
        [InlineData(9000f, "2.5 h")]
        public void ATimeIsToldInTheLargestUnitThatReadsWell(float seconds, string shown)
        {
            Assert.Equal(shown, Naming.Duration(seconds));
        }

        [Theory]
        [InlineData(12.25f, "12.25")]
        [InlineData(2.5f, "2.5")]
        [InlineData(12f, "12")]
        [InlineData(11.9999f, "12")]
        [InlineData(0.333f, "0.33")]
        [InlineData(-5f, "-5")]
        public void NumbersAreWrittenOneWayEverywhere(float value, string shown)
        {
            // Up to two decimals, none for a whole number: a creature's attack and an item's facts agree.
            Assert.Equal(shown, Naming.Number(value));
        }

        [Theory]
        [InlineData(3000f, 3600f, "50–60 min")]
        [InlineData(20f, 40f, "20–40 s")]
        [InlineData(90f, 180f, "90 s to 3 min")]
        [InlineData(600f, 600f, "10 min")]
        public void ARangeOfTimesSharesItsUnitWhereItCan(float least, float most, string shown)
        {
            Assert.Equal(shown, Naming.DurationRange(least, most));
        }
    }
}
