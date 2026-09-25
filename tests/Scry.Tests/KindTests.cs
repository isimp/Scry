using Xunit;

namespace Scry.Tests
{
    public class KindTests
    {
        [Fact]
        public void ACreatureIsPreviewedAsACreature()
        {
            var troll = new PrefabTraits { HasCharacter = true, HasRenderer = true, HasSolidCollider = true };

            Assert.Equal(Kind.Creature, Kinds.Of(troll));
        }

        [Fact]
        public void AProjectileIsNotMistakenForTheEffectItLooksLike()
        {
            var arrow = new PrefabTraits { HasProjectile = true, HasRenderer = true, HasParticles = true };

            Assert.Equal(Kind.Projectile, Kinds.Of(arrow));
        }

        [Fact]
        public void SomethingYouCanPickUpIsAnItem()
        {
            var wood = new PrefabTraits { HasItemDrop = true, HasRenderer = true, HasSolidCollider = true };

            Assert.Equal(Kind.Item, Kinds.Of(wood));
        }

        [Fact]
        public void SomethingYouCanBuildIsAPiece()
        {
            var wall = new PrefabTraits { HasPiece = true, HasRenderer = true, HasSolidCollider = true };

            Assert.Equal(Kind.Piece, Kinds.Of(wall));
        }

        [Fact]
        public void SomethingYouCanOnlyHearIsASound()
        {
            var sfx = new PrefabTraits { HasAudio = true, FromEffectList = true };

            Assert.Equal(Kind.Sound, Kinds.Of(sfx));
        }

        [Fact]
        public void AnEffectYouSeeAndHearIsAnEffect()
        {
            var vfx = new PrefabTraits { HasParticles = true, HasAudio = true };

            Assert.Equal(Kind.Effect, Kinds.Of(vfx));
        }

        [Fact]
        public void ParticlesWithNothingSolidAreAnEffect()
        {
            var sparks = new PrefabTraits { HasParticles = true, HasRenderer = true };

            Assert.Equal(Kind.Effect, Kinds.Of(sparks));
        }

        [Fact]
        public void AMeshOnlyEverPlayedFromAnEffectListIsAnEffect()
        {
            var shatter = new PrefabTraits { HasRenderer = true, FromEffectList = true };

            Assert.Equal(Kind.Effect, Kinds.Of(shatter));
        }

        [Fact]
        public void ATreeOrARockIsOther()
        {
            var rock = new PrefabTraits { HasRenderer = true, HasSolidCollider = true };
            var smokingRock = new PrefabTraits { HasRenderer = true, HasSolidCollider = true, HasParticles = true };

            Assert.Equal(Kind.Other, Kinds.Of(rock));
            Assert.Equal(Kind.Other, Kinds.Of(smokingRock));
        }

        [Fact]
        public void AStatusEffectIsAStatusEffect()
        {
            Assert.Equal(Kind.StatusEffect, Kinds.Of(new PrefabTraits { IsStatusEffect = true }));
        }

        [Fact]
        public void AnInvisibleSilentObjectHasNothingToPreview()
        {
            var controller = new PrefabTraits { HasSolidCollider = true };

            Assert.True(Kinds.IsEmpty(controller));
        }

        [Fact]
        public void AnythingThatDrawsLightsOrSoundsHasSomethingToPreview()
        {
            Assert.False(Kinds.IsEmpty(new PrefabTraits { HasRenderer = true }));
            Assert.False(Kinds.IsEmpty(new PrefabTraits { HasParticles = true }));
            Assert.False(Kinds.IsEmpty(new PrefabTraits { HasLight = true }));
            Assert.False(Kinds.IsEmpty(new PrefabTraits { HasAudio = true }));
        }

        [Fact]
        public void AStatusEffectAlwaysHasItsCardToShow()
        {
            Assert.False(Kinds.IsEmpty(new PrefabTraits { IsStatusEffect = true }));
        }
    }
}
