using Xunit;

namespace Scry.Tests
{
    public class IdTests
    {
        // The ids Scry makes of what it reads, made one way each in the model: a member's path,
        // a status effect as the owner of what it plays, the field marks of what something is
        // played in, and the trigger a chain attack starts with.

        [Fact]
        public void AMemberIsNamedByWhatItIsIn() => Assert.Equal("m_attack.m_hitEffect", Naming.MemberPath("m_attack", "m_hitEffect"));

        [Fact]
        public void AStatusEffectAsAnOwnerIsToldFromAnyOther()
        {
            var owner = Provenance.StatusEffect("Rested");
            Assert.Equal("status effect Rested", owner);
            Assert.True(Provenance.IsStatusEffect(owner));
            Assert.False(Provenance.IsStatusEffect("Troll"));
            Assert.False(Provenance.IsStatusEffect(null));
        }

        [Fact]
        public void AFieldIsMarkedByWhatItIsOn()
        {
            Assert.Equal("ui:m_clickEffect", Groups.FieldOf("m_clickEffect", onInterface: true, onStatusEffect: false));
            Assert.Equal("se:m_startEffects", Groups.FieldOf("m_startEffects", onInterface: false, onStatusEffect: true));
            Assert.Equal("m_hitEffect", Groups.FieldOf("m_hitEffect", onInterface: false, onStatusEffect: false));
            Assert.Equal("se:spawned", Groups.StatusEffectSpawned);
        }

        [Fact]
        public void AChainAttackStartsWithItsFirstStep() => Assert.Equal("swing_axe0", ClipAttacks.ChainStart("swing_axe"));
    }
}
