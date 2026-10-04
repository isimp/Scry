using Xunit;

namespace Scry.Tests
{
    public class NamingTests
    {
        // A list of names reads as a sentence: "A", "A and B", "A, B and C".

        [Fact]
        public void AListReadsAsASentence()
        {
            Assert.Equal("", Naming.Joined(new string[0]));
            Assert.Equal("wood", Naming.Joined(new[] { "wood" }));
            Assert.Equal("wood and stone", Naming.Joined(new[] { "wood", "stone" }));
            Assert.Equal("wood, stone and resin", Naming.Joined(new[] { "wood", "stone", "resin" }));
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

        [Fact]
        public void ARowOfChipsIsTitledWithHowManyItHolds()
        {
            Assert.Equal("Lives here (1,234)", Naming.Counted("Lives here", 1234));
            Assert.Equal("Needs (0)", Naming.Counted("Needs", 0));
        }

        [Fact]
        public void SomethingFoundMoreThanOnceSaysHowManyTimes()
        {
            Assert.Equal("Collider ×1,234", Naming.Repeated("Collider", 1234));
            Assert.Equal("Collider", Naming.Repeated("Collider", 1));
        }
    }
}
