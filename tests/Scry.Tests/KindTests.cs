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
        public void ANestIsASpawnerThoughItBreaksIntoDrops()
        {
            // A greydwarf nest spawns creatures; it is found with the other spawners, in a kind of their own.
            var nest = new PrefabTraits { HasDestructible = true, HasDrops = true, HasSpawner = true, IsNest = true, HasRenderer = true, HasSolidCollider = true };
            Assert.Equal(Kind.Spawner, Kinds.Of(nest));
            Assert.Equal(Kind.Spawner, Kinds.Of(new PrefabTraits { HasSpawner = true }));
            // A spawner a mod makes buildable is a piece, as anything built is.
            Assert.Equal(Kind.Piece, Kinds.Of(new PrefabTraits { HasSpawner = true, HasPiece = true, HasRenderer = true }));
        }

        [Fact]
        public void SpawnersAreGroupedIntoNestsAndSpawnPoints()
        {
            var nests = Groups.Spawner(new PrefabTraits { HasSpawner = true, IsNest = true });
            var points = Groups.Spawner(new PrefabTraits { HasSpawner = true });
            Assert.Equal("Nests", nests.Name);
            Assert.Equal("Spawn points", points.Name);
            Assert.True(nests.Order < points.Order);
            Assert.Equal("Spawners", Kinds.Label(Kind.Spawner));
            Assert.Equal("spawner", Kinds.TermWord(Kind.Spawner));
            Assert.True(Search.KindMatches(Kind.Spawner, "spawners"));
        }

        [Fact]
        public void ACropYouPlantIsAPieceThoughItGrows()
        {
            // Planted with the cultivator, it is built like any other piece.
            var crop = new PrefabTraits { HasPiece = true, HasResource = true, HasPlant = true, HasRenderer = true };

            Assert.Equal(Kind.Piece, Kinds.Of(crop));
        }

        [Fact]
        public void ATreeRockOrBushAModMadeBuildableIsStillAResource()
        {
            // MoreVanillaBuildPrefabs adds a Piece to hundreds of the game's own prefabs; an oak
            // is still chopped, a rock mined and a bush picked.
            var oak = new PrefabTraits { HasPiece = true, HasResource = true, HasRenderer = true, HasSolidCollider = true };
            var vein = new PrefabTraits { HasPiece = true, HasDestructible = true, BreaksIntoResource = true, HasRenderer = true };

            Assert.Equal(Kind.Resource, Kinds.Of(oak));
            Assert.Equal(Kind.Resource, Kinds.Of(vein));
        }

        [Fact]
        public void ResourcesAreGroupedByHowTheyAreGathered()
        {
            Assert.Equal(ResourceGroup.Trees, Kinds.GroupOf(new PrefabTraits { HasResource = true, IsTree = true, HasDestructible = true, HasDrops = true }));
            Assert.Equal(ResourceGroup.Logs, Kinds.GroupOf(new PrefabTraits { HasResource = true, IsLog = true }));
            Assert.Equal(ResourceGroup.RocksAndOre, Kinds.GroupOf(new PrefabTraits { HasResource = true, IsMined = true }));
            Assert.Equal(ResourceGroup.RocksAndOre, Kinds.GroupOf(new PrefabTraits { HasDestructible = true, BreaksIntoResource = true }));
            Assert.Equal(ResourceGroup.Plants, Kinds.GroupOf(new PrefabTraits { HasResource = true, HasPlant = true, IsPicked = true }));
            Assert.Equal(ResourceGroup.BushesAndPickables, Kinds.GroupOf(new PrefabTraits { HasResource = true, IsPicked = true, HasDestructible = true }));
            Assert.Equal(ResourceGroup.Other, Kinds.GroupOf(new PrefabTraits { HasDestructible = true, HasDrops = true }));
        }

        [Fact]
        public void OnlyResourcesHaveAGroup()
        {
            Assert.Equal(ResourceGroup.None, Kinds.GroupOf(new PrefabTraits { HasCharacter = true }));
            Assert.Equal(ResourceGroup.None, Kinds.GroupOf(new PrefabTraits { HasPiece = true, HasResource = true, HasPlant = true }));
        }

        [Fact]
        public void ABuiltPieceThatDropsWhatItIsMadeOfIsStillAPiece()
        {
            var crate = new PrefabTraits { HasPiece = true, HasDestructible = true, HasDrops = true, HasRenderer = true };

            Assert.Equal(Kind.Piece, Kinds.Of(crate));
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
