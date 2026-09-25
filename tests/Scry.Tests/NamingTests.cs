using Xunit;

namespace Scry.Tests
{
    public class NamingTests
    {
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
        [InlineData("m_effects")]
        [InlineData("")]
        [InlineData(null)]
        public void AListWithNoNameOfItsOwnIsJustCalledEffect(string field)
        {
            Assert.Equal("Effect", Naming.EffectListLabel(field));
        }
    }
}
