using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlaceContentsTests
    {
        // A location's parts are rolled when the game first builds its zone: each RandomSpawn at
        // its chance, each RandomObject picking one of its objects by weight (ZoneSystem.SpawnLocation).

        // A dungeon's or camp's own prefab holds little; what is in it is in its rooms. Its page
        // counts each thing across its kinds of room: in how many of them it is, the most
        // widespread first, then by name.

        private static List<PlacePart> Room(params string[] prefabs) => prefabs.Select(p => new PlacePart { Prefab = p, Count = 1, Chance = 1f }).ToList();

        [Fact]
        public void WhatARoomsHoldIsCountedByTheKindsOfRoomItIsIn()
        {
            var across = PlaceParts.Across(new[] { Room("TreasureChest_forestcrypt", "Spawner_Skeleton"), Room("Spawner_Skeleton", "Pickable_ForestCryptRemains"), Room("Spawner_Skeleton") });

            Assert.Equal(new[] { ("Spawner_Skeleton", 3), ("Pickable_ForestCryptRemains", 1), ("TreasureChest_forestcrypt", 1) }, across.ToArray());
        }

        [Fact]
        public void AThingTwiceInOneRoomCountsThatRoomOnce()
        {
            var room = Room("Spawner_Skeleton");
            room.Add(new PlacePart { Prefab = "Spawner_Skeleton", Count = 2, Chance = 0.5f });

            Assert.Equal(new[] { ("Spawner_Skeleton", 1) }, PlaceParts.Across(new[] { room }).ToArray());
        }

        [Fact]
        public void RoomsNotReadAddNothing()
        {
            Assert.Empty(PlaceParts.Across(new List<PlacePart>[] { null, new List<PlacePart>() }));
        }

        [Theory]
        [InlineData(1, "in 1 kind of room")]
        [InlineData(5, "in 5 kinds of room")]
        public void HowWidespreadIsToldInKindsOfRoom(int rooms, string told)
        {
            Assert.Equal(told, PlaceParts.InRooms(rooms));
        }

        [Fact]
        public void PartsOfOnePrefabAtOneChanceAreCountedTogether()
        {
            var parts = PlaceParts.Group(new[] { ("MushroomYellow", 0.2f), ("MushroomYellow", 0.2f), ("MushroomYellow", 0.2f), ("Spawner_Skeleton", 0.33f) });

            Assert.Equal(2, parts.Count);
            Assert.Equal(3, parts.Single(p => p.Prefab == "MushroomYellow").Count);
        }

        [Fact]
        public void TheSamePrefabAtAnotherChanceIsAPartOfItsOwn()
        {
            var parts = PlaceParts.Group(new[] { ("SurtlingCoreStand", 0.55f), ("SurtlingCoreStand", 0.757f), ("SurtlingCoreStand", 0.55f) });

            Assert.Equal(new[] { (1, 0.757f), (2, 0.55f) }, parts.Select(p => (p.Count, p.Chance)).ToArray());
        }

        [Fact]
        public void WhatIsAlwaysThereComesFirstThenTheLikeliest()
        {
            var parts = PlaceParts.Group(new[] { ("B", 0.2f), ("Spawner", 1f), ("A", 0.5f), ("Chest", 1f) });

            Assert.Equal(new[] { "Chest", "Spawner", "A", "B" }, parts.Select(p => p.Prefab).ToArray());
        }

        [Fact]
        public void ChancesThatDifferOnlyByRoundingAreOne()
        {
            var parts = PlaceParts.Group(new[] { ("A", 0.33f), ("A", 0.3300001f) });

            Assert.Single(parts);
        }

        [Theory]
        [InlineData(1, 1f, "")]
        [InlineData(2, 1f, "2")]
        [InlineData(1, 0.33f, "33%")]
        [InlineData(6, 0.2f, "6, 20% each")]
        [InlineData(1, 0.004f, "0.4%")]
        public void APartSaysHowManyAndHowLikely(int count, float chance, string words)
        {
            Assert.Equal(words, PlaceParts.Amount(count, chance));
        }

        // RandomObject.GetWeightedObject: a roll from 0 to the sum of every entry's weight, an
        // entry without an object included, picks the first entry whose running weight reaches it.

        [Fact]
        public void APickTakesTheFirstObjectWhoseRunningWeightReachesTheRoll()
        {
            var weights = new[] { 1f, 3f };

            Assert.Equal(0, PlaceParts.Pick(weights, 0.0));
            Assert.Equal(0, PlaceParts.Pick(weights, 0.25));
            Assert.Equal(1, PlaceParts.Pick(weights, 0.26));
            Assert.Equal(1, PlaceParts.Pick(weights, 1.0));
        }

        [Fact]
        public void AnEntryWithoutAnObjectTakesItsShareOfThePicks()
        {
            Assert.Equal(new[] { 0.25f, 0.75f }, PlaceParts.Shares(new[] { 1f, 3f }));
            Assert.Equal(new[] { 0f, 0f }, PlaceParts.Shares(new[] { 0f, 0f }));
        }

        // A place's parts are told by what each is: chests and pickups first, then what is mined,
        // felled or broken, then the rest (spawners, altars, stations), building pieces last.

        [Fact]
        public void AChestOrAPickupIsLootWhateverElseItIs()
        {
            Assert.Equal(PartRole.Loot, PlaceParts.RoleOf(new PartTraits { Container = true, Built = true }));
            Assert.Equal(PartRole.Loot, PlaceParts.RoleOf(new PartTraits { Pickup = true, Breaks = true }));
            Assert.Equal(PartRole.Loot, PlaceParts.RoleOf(new PartTraits { Container = true, Used = true, Spawns = true }));
        }

        [Fact]
        public void ASpawnerOrSomethingUsedIsTheRestEvenWhenBuilt()
        {
            Assert.Equal(PartRole.Other, PlaceParts.RoleOf(new PartTraits { Spawns = true, Built = true, Breaks = true }));
            Assert.Equal(PartRole.Other, PlaceParts.RoleOf(new PartTraits { Used = true, Built = true, Gathered = true }));
        }

        [Fact]
        public void WhatIsMinedOrFelledIsGatheredEvenWhenBuilt()
        {
            Assert.Equal(PartRole.Gather, PlaceParts.RoleOf(new PartTraits { Gathered = true, Built = true }));
        }

        [Fact]
        public void ABuildingPieceIsBuiltEvenWhenItBreaks()
        {
            Assert.Equal(PartRole.Built, PlaceParts.RoleOf(new PartTraits { Built = true }));
            Assert.Equal(PartRole.Built, PlaceParts.RoleOf(new PartTraits { Built = true, Breaks = true }));
        }

        [Fact]
        public void WhatOnlyBreaksIsGatheredAndAnythingElseIsTheRest()
        {
            Assert.Equal(PartRole.Gather, PlaceParts.RoleOf(new PartTraits { Breaks = true }));
            Assert.Equal(PartRole.Other, PlaceParts.RoleOf(new PartTraits()));
        }

        [Theory]
        [InlineData(PartRole.Loot, false, "Chests and pickups")]
        [InlineData(PartRole.Gather, false, "To gather")]
        [InlineData(PartRole.Other, false, "Holds")]
        [InlineData(PartRole.Built, false, "Built of")]
        [InlineData(PartRole.Loot, true, "Chests and pickups in its rooms")]
        [InlineData(PartRole.Gather, true, "To gather in its rooms")]
        [InlineData(PartRole.Other, true, "Its rooms hold")]
        [InlineData(PartRole.Built, true, "Its rooms are built of")]
        public void EachKindOfPartHasItsRow(PartRole role, bool rooms, string title)
        {
            Assert.Equal(title, PlaceParts.Title(role, rooms));
        }

        [Fact]
        public void TheRowsGoChestsThenGatheringThenTheRestThenBuilding()
        {
            Assert.Equal(new[] { PartRole.Loot, PartRole.Gather, PartRole.Other, PartRole.Built }, PlaceParts.Roles);
        }

        // Roll again shows a place otherwise only where the game rolls: a RandomSpawn between
        // never and always, or a RandomObject with two or more entries it can pick.

        [Fact]
        public void APlaceWhoseRollsAllComeOutOneWayHasNothingToRollAgain()
        {
            Assert.False(PlaceParts.LeftToChance(new[] { 100f, 0f }, new[] { new[] { 1f }, new[] { 2f, 0f } }));
            Assert.False(PlaceParts.LeftToChance(new float[0], new float[0][]));
        }

        [Fact]
        public void ASpawnBetweenNeverAndAlwaysOrAPickOfTwoCanComeOutOtherwise()
        {
            Assert.True(PlaceParts.LeftToChance(new[] { 100f, 50f }, new float[0][]));
            Assert.True(PlaceParts.LeftToChance(new float[0], new[] { new[] { 1f, 3f } }));
        }
    }
}
