using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class RoomCardTests
    {
        // The card is compact: its creatures and its chests and pickups fold to their counts
        // until opened, as a page's sections do, and its page is a link on its name's line.

        [Fact]
        public void AFoldedListOfTheCardSaysHowManyItHoldsAndThatItOpens()
        {
            Assert.Equal("Creatures in this roll 3  ▸", RoomCardWords.ListHeading(RoomCardWords.Creatures, 3, open: false));
            Assert.Equal("Chests and pickups 12  ▾", RoomCardWords.ListHeading(RoomCardWords.Loot, 12, open: true));
            Assert.Equal(RoomCardWords.CreaturesTip + "\nOpen the list", RoomCardWords.ListTip(RoomCardWords.CreaturesTip, open: false));
            Assert.Equal(RoomCardWords.LootTip + "\nFold the list away to its count", RoomCardWords.ListTip(RoomCardWords.LootTip, open: true));
        }

        [Fact]
        public void AListWhoseEveryLineWasLeftOutSaysOnlyHowManyAreNotYetMet()
        {
            // Five creatures rolled, none met: the list says "5 not yet met", not also that the roll put none there.
            Assert.Equal(new[] { "5 not yet met" }, RoomCardWords.ListLines(new string[0], CompendiumWords.NotYetMet(null, 5, alone: true)));
            Assert.Equal(new[] { "5 not yet seen together" }, RoomCardWords.ListLines(new string[0], CompendiumWords.LeftOutOf(null, 5, alone: true)));
            // Some listed, the rest counted as more.
            Assert.Equal(new[] { "Draugr ×2", "3 more not yet met" }, RoomCardWords.ListLines(new[] { "Draugr ×2" }, CompendiumWords.NotYetMet(null, 3)));
            // Nothing rolled at all.
            Assert.Equal(new[] { RoomCardWords.None }, RoomCardWords.ListLines(new string[0], null));
        }

        [Fact]
        public void TheCardsPageIsALinkOnItsNamesLine()
        {
            Assert.Equal("Its page", RoomCardWords.PageLink);
        }

        [Fact]
        public void TheCardSaysHowFarBelowTheEntranceTheRoomIsHowManyRoomsInAndHowManyWays()
        {
            Assert.Equal("12 m below the entrance  ·  4 rooms in  ·  3 ways", RoomCardWords.Where(-12.4f, 4, 3));
        }

        [Fact]
        public void ARoomAboveTheEntranceSaysSo()
        {
            Assert.Equal("3 m above the entrance  ·  1 room in  ·  1 way", RoomCardWords.Where(3.2f, 1, 1));
        }

        [Fact]
        public void ARoomWithinHalfAMetreOfTheEntrancesGroundIsLevelWithIt()
        {
            Assert.Equal("Level with the entrance  ·  2 rooms in  ·  2 ways", RoomCardWords.Where(-0.49f, 2, 2));
            Assert.Equal("1 m below the entrance  ·  2 rooms in  ·  2 ways", RoomCardWords.Where(-0.5f, 2, 2));
        }

        [Fact]
        public void TheEntranceItselfSaysItIsTheEntrance()
        {
            Assert.Equal("The entrance  ·  2 ways", RoomCardWords.Where(0f, 0, 2));
        }

        [Fact]
        public void ARoomNoWayReachesFromTheEntranceLeavesOutHowManyRoomsIn()
        {
            Assert.Equal("5 m below the entrance  ·  1 way", RoomCardWords.Where(-5f, -1, 1));
        }

        [Fact]
        public void TheCreaturesOfTheRollAreCountedByNameTheMostFirst()
        {
            var lines = RoomCardWords.Counted(new[] { "Skeleton", "Draugr", "Draugr", "Ghost", "Draugr", "Skeleton" });
            Assert.Equal(new[] { "Draugr ×3", "Skeleton ×2", "Ghost" }, lines);
        }

        [Fact]
        public void EachCreatureLineOpensThePageOfTheCreaturesItCounts()
        {
            var lines = RoomCardWords.CountedPages(new[] { ("Skeleton", "skeleton"), ("Draugr", "draugr"), ("Draugr", "draugr"), ("Ghost", "ghost") });
            Assert.Equal(new[] { ("Draugr ×2", "draugr"), ("Ghost", "ghost"), ("Skeleton", "skeleton") }, lines);
            // Two of one name from two pages: the line opens the first one's.
            Assert.Equal(new[] { ("Draugr ×2", "draugr") }, RoomCardWords.CountedPages(new[] { ("Draugr", "draugr"), ("Draugr", "draugr_elite") }));
        }

        [Fact]
        public void EachChestOrPickupLineOpensThePageOfWhatItCounts()
        {
            var lines = RoomCardWords.OddsPages(new[] { ("Chest", "chest", 0.6f), ("Chest", "chest", 0.6f), ("Amber", "amber", 0.3f) });
            Assert.Equal(new[] { ("Chest ×2", "60% each", "chest"), ("Amber", "30%", "amber") }, lines);
        }

        [Fact]
        public void CreaturesAsManyAsEachOtherComeInTheOrderOfTheirNames()
        {
            Assert.Equal(new[] { "Ghost", "rat" }, RoomCardWords.Counted(new[] { "rat", "Ghost" }));
        }

        [Fact]
        public void ASectionWithNothingInItSaysSo()
        {
            Assert.Empty(RoomCardWords.Counted(new string[0]));
            Assert.Empty(RoomCardWords.Odds(new (string, float)[0]));
            Assert.Equal("None in this roll", RoomCardWords.None);
        }

        [Fact]
        public void EachChestOrPickupSaysHowLikelyARoomOfItsKindHoldsIt()
        {
            Assert.Equal(new[] { ("Chest", "60%") }, RoomCardWords.Odds(new[] { ("Chest", 0.6f) }));
        }

        [Fact]
        public void OneAlwaysThereSaysAlways()
        {
            Assert.Equal(new[] { ("Chest", "always") }, RoomCardWords.Odds(new[] { ("Chest", 1f) }));
        }

        [Fact]
        public void SeveralOfAKindAtTheSameOddsAreCountedTogether()
        {
            var lines = RoomCardWords.Odds(new[] { ("Chest", 0.6f), ("Chest", 0.3f), ("Chest", 0.6f) });
            Assert.Equal(new[] { ("Chest ×2", "60% each"), ("Chest", "30%") }, lines);
        }

        [Fact]
        public void CopiesOfOneThingNumberedApartAreCountedAsOne()
        {
            // A room's copies keep the number Unity gives each, "Morkhalla Eye (4)"; the card counts them as one thing.
            var lines = RoomCardWords.Odds(new[] { ("Morkhalla Eye (4)", 0.18f), ("Morkhalla Eye (1)", 0.18f), ("Morkhalla Eye Gemstone (6)", 0.5f) });
            Assert.Equal(new[] { ("Morkhalla Eye Gemstone", "50%"), ("Morkhalla Eye ×2", "18% each") }, lines);
            Assert.Equal(new[] { "Draugr ×2" }, RoomCardWords.Counted(new[] { "Draugr (1)", "Draugr" }));
        }

        [Fact]
        public void WhatIsAlwaysThereComesFirstThenTheLikeliestThenByName()
        {
            var lines = RoomCardWords.Odds(new[] { ("Bone pile", 0.3f), ("Chest", 1f), ("Amber", 0.3f), ("Coins", 0.5f) });
            Assert.Equal(new[] { "Chest", "Coins", "Amber", "Bone pile" }, lines.Select(l => l.Name));
        }

        [Fact]
        public void ALineReadsAsItsNameThenItsOddsWhereItHasAny()
        {
            Assert.Equal("Chest  60%", RoomCardWords.Row("Chest", "60%"));
            Assert.Equal("Draugr ×3", RoomCardWords.Row("Draugr ×3", null));
        }

        [Fact]
        public void AWayOnTheSameLevelIsItsRoomsName()
        {
            Assert.Equal("Corridor", RoomCardWords.Way("Corridor", 1.5f, false));
            Assert.Equal("Corridor", RoomCardWords.Way("Corridor", -1.5f, false));
        }

        [Fact]
        public void AWayUpOrDownSaysHowFar()
        {
            Assert.Equal("Stairs, 2 m up", RoomCardWords.Way("Stairs", 1.6f, false));
            Assert.Equal("Stairs, 6 m up", RoomCardWords.Way("Stairs", 6.3f, false));
            Assert.Equal("Hall, 4 m down", RoomCardWords.Way("Hall", -4f, false));
        }

        [Fact]
        public void AWayToADeadEndSaysSo()
        {
            Assert.Equal("Tomb, 3 m down, dead end", RoomCardWords.Way("Tomb", -3f, true));
            Assert.Equal("Tomb, dead end", RoomCardWords.Way("Tomb", 0f, true));
        }

        [Fact]
        public void ARoomsFloorsAreItsGroundAndHowFarEachOtherIsFromIt()
        {
            Assert.Equal("Ground", RoomCardWords.Floor(2f, 2f));
            Assert.Equal("10 m up", RoomCardWords.Floor(12.3f, 2f));
            Assert.Equal("3 m down", RoomCardWords.Floor(-1f, 2f));
        }

        [Fact]
        public void ARoomsOwnStoreysAreItsFloorsTheDungeonsAreLevels()
        {
            // "Level" names the dungeon's levels (the plan, the breadcrumb, the ruler); a room's own storeys are floors.
            Assert.Equal("Floors in this room", RoomCardWords.Floors);
            Assert.Equal("Cut at head height over this floor", RoomCardWords.FloorTip);
        }

        [Fact]
        public void ALongListKeepsWhatFitsAndCountsTheRest()
        {
            var five = new List<string> { "a", "b", "c", "d", "e" };
            Assert.Equal(new[] { "a", "b", "and 3 more" }, RoomCardWords.Fit(five, 3));
            Assert.Equal(five, RoomCardWords.Fit(five, 5));
            Assert.Equal(new[] { "and 5 more" }, RoomCardWords.Fit(five, 1));
            Assert.Empty(RoomCardWords.Fit(five, 0));
        }

        // The ways on from a room, as the card lists them.

        private static PlacedRoom Room(string name, float x, float y, int order, params Vec3[] doorways)
        {
            var shape = new RoomShape { Name = name, Size = new Vec3(4f, 4f, 4f) };
            var position = new Vec3(x, y, 0f);
            foreach (var at in doorways) shape.Doorways.Add(new Doorway { Position = at - position });
            return new PlacedRoom { Room = shape, Position = position, PlaceOrder = order };
        }

        private static Vec3 At(float x, float y) => new Vec3(x, y, 0f);

        /// <summary>An entrance, a hall behind it at a crossroads, and from the hall stairs up to a gallery and a tomb down, a dead end.</summary>
        private static (DungeonWays Ways, PlacedRoom Entrance, PlacedRoom Hall, PlacedRoom Stairs, PlacedRoom Tomb) Crossroads()
        {
            var entrance = Room("Entrance", 0f, 0f, 0, At(2f, 0f));
            var hall = Room("Hall", 4f, 0f, 1, At(2f, 0f), At(6f, 0f), At(4f, 2f));
            var stairs = Room("Stairs", 8f, 6f, 2, At(6f, 0f), At(10f, 6f));
            var gallery = Room("Gallery", 12f, 6f, 3, At(10f, 6f));
            var tomb = Room("Tomb", 4f, -5f, 2, At(4f, 2f));
            return (DungeonWays.Of(new[] { entrance, hall, stairs, gallery, tomb }), entrance, hall, stairs, tomb);
        }

        [Fact]
        public void TheWaysOnFromARoomComeNearestTheEntranceFirst()
        {
            var (ways, _, hall, _, _) = Crossroads();
            var on = RoomCard.WaysOn(ways, hall, r => r.Position.Y, r => r.Room.Name);
            Assert.Equal(new[] { "Entrance", "Stairs, 6 m up", "Tomb, 5 m down, dead end" }, on.Select(w => w.Words));
        }

        [Fact]
        public void EachWayOnGoesToItsRoom()
        {
            var (ways, entrance, hall, stairs, tomb) = Crossroads();
            var on = RoomCard.WaysOn(ways, hall, r => r.Position.Y, r => r.Room.Name);
            Assert.Equal(new[] { entrance, stairs, tomb }, on.Select(w => w.To));
        }

        [Fact]
        public void UpAndDownAreMeasuredFromTheRoomGoneInto()
        {
            var (ways, _, _, stairs, _) = Crossroads();
            Assert.Equal(new[] { "Hall, 6 m down", "Gallery, dead end" }, RoomCard.WaysOn(ways, stairs, r => r.Position.Y, r => r.Room.Name).Select(w => w.Words));
        }

        [Fact]
        public void TheEntranceIsNoDeadEndThoughOneWayLeadsFromIt()
        {
            var (ways, entrance, hall, _, _) = Crossroads();
            Assert.Equal(new[] { "Hall" }, RoomCard.WaysOn(ways, entrance, r => r.Position.Y, r => r.Room.Name).Select(w => w.Words));
            Assert.Equal("Entrance", RoomCard.WaysOn(ways, hall, r => r.Position.Y, r => r.Room.Name)[0].Words);
        }
    }
}
