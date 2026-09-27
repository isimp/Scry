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
        public void TreesRocksOreBushesAndPlantsAreResources()
        {
            // What you chop, mine, pick or grow: the game's TreeBase, TreeLog, MineRock, Pickable, Plant.
            var oak = new PrefabTraits { HasResource = true, HasRenderer = true, HasSolidCollider = true };
            var raspberries = new PrefabTraits { HasResource = true, HasRenderer = true };

            Assert.Equal(Kind.Resource, Kinds.Of(oak));
            Assert.Equal(Kind.Resource, Kinds.Of(raspberries));
        }

        [Fact]
        public void SomethingThatBreaksIntoDropsIsAResource()
        {
            var rock = new PrefabTraits { HasDestructible = true, HasDrops = true, HasRenderer = true, HasSolidCollider = true };
            var pot = new PrefabTraits { HasDestructible = true, HasRenderer = true, HasSolidCollider = true };

            Assert.Equal(Kind.Resource, Kinds.Of(rock));
            Assert.Equal(Kind.Other, Kinds.Of(pot));
        }

        [Fact]
        public void AVeinThatBreaksIntoOreIsAResourceThoughItDropsNothingItself()
        {
            // A silver vein is a shell that, struck, turns into the vein that is mined (silvervein_frac).
            var vein = new PrefabTraits { HasDestructible = true, BreaksIntoResource = true, HasRenderer = true, HasSolidCollider = true };

            Assert.Equal(Kind.Resource, Kinds.Of(vein));
        }

        [Fact]
        public void ANestIsNoResourceThoughItBreaksIntoDrops()
        {
            // A greydwarf nest spawns creatures; it is found with the other spawners.
            var nest = new PrefabTraits { HasDestructible = true, HasDrops = true, HasSpawner = true, HasRenderer = true, HasSolidCollider = true };

            Assert.Equal(Kind.Other, Kinds.Of(nest));
        }

        [Fact]
        public void WhatAPrefabIsInTheGameOutranksBeingAResource()
        {
            // A piece can grow or be picked (a planted crop, a beehive); it is still built.
            var crop = new PrefabTraits { HasPiece = true, HasResource = true, HasRenderer = true };

            Assert.Equal(Kind.Piece, Kinds.Of(crop));
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
