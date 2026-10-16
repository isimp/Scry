using Xunit;

namespace Scry.Tests
{
    public class RoomLevelsTests
    {
        /// <summary>A room's own rays as read: the ground they were cast over, and each height with the room to stand there, in square metres.</summary>
        private static FloorPatch Room(float ground, params (float Height, float Room)[] levels)
        {
            var patch = new FloorPatch { Ground = ground };
            foreach (var (height, room) in levels)
            {
                patch.Bands[(int)System.Math.Round(height / FloorFinder.Band)] = new FloorPatch.Band { Room = room, Sum = height, Count = 1, Landed = true };
            }
            return patch;
        }

        [Fact]
        public void ATallHallsSecondStoreyIsALevelOfItsOwn()
        {
            // As Mörkhalla's tall halls were read: two storeys ten metres apart, over a thousand square metres each.
            Assert.Equal(new[] { 0f, 10f }, RoomLevels.Of(Room(3000f, (0f, 1500f), (10f, 1100f)), 0f));
        }

        [Fact]
        public void StepsLandingsAndALowPlatformAreNoLevels()
        {
            // Nearer the ground than head height, or too small to stand about on.
            var room = Room(200f, (0f, 120f), (1.1f, 4f), (-2.9f, 22f), (2f, 34f), (17.8f, 17f));

            Assert.Equal(new[] { 0f }, RoomLevels.Of(room, 0f));
        }

        [Fact]
        public void OfTwoLevelsNearerThanHeadHeightTheBiggerStays()
        {
            // As a cave's slope room was read: a level high up, with a smaller one a few metres under it.
            var room = Room(600f, (0f, 177f), (21.7f, 61f), (23.8f, 168f), (25.6f, 41f), (35.9f, 34f));

            Assert.Equal(new[] { 0f, 23.8f, 35.9f }, RoomLevels.Of(room, 0f));
        }

        [Fact]
        public void ASliverOfAVastRoomAndGroundNoRayLandedOnAreNoLevels()
        {
            // 40 m2 is no level of a 5,000 m2 room's own; a band only its neighbours' rays reach is none either.
            Assert.Equal(new[] { 0f }, RoomLevels.Of(Room(5000f, (0f, 2000f), (10f, 40f)), 0f));
            var room = Room(300f, (0f, 120f));
            room.Bands[(int)System.Math.Round(8f / FloorFinder.Band)] = new FloorPatch.Band { Room = 90f, Sum = 8f, Count = 1, Landed = false };
            Assert.Equal(new[] { 0f }, RoomLevels.Of(room, 0f));
        }

        [Fact]
        public void ALevelUnderTheGroundIsOneToo()
        {
            Assert.Equal(new[] { -12.1f, 0f }, RoomLevels.Of(Room(300f, (0f, 85f), (-12.1f, 45f)), 0f));
        }

        [Fact]
        public void ARoomWithNoFlatGroundOfItsOwnHasOnlyWhereItIsWalkedInto()
        {
            // A sloping tunnel: its ground is its lowest doorway, its rays found nothing big enough.
            Assert.Equal(new[] { -7f }, RoomLevels.Of(Room(150f, (-7.4f, 2f)), -7f));
            Assert.Equal(new[] { -7f }, RoomLevels.Of(null, -7f));
        }
    }
}
