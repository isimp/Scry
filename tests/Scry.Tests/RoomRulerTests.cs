using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class RoomRulerTests
    {
        // In a room gone into, the floor ruler is the room's: from its lowest level up to the top
        // of the rooms above it, a tick for each of its own levels and for each level of the rooms
        // above, the cut kept between them.

        [Fact]
        public void TheRulerOfARoomSpansItsLowestLevelToTheTopOfTheRoomsAboveIt()
        {
            var ruler = RoomRuler.Of(ground: 0f, levels: new[] { 0f, 3f }, top: 7f, above: new[] { (8f, 16f) });
            Assert.Equal(0f, ruler.Low, 3);
            Assert.Equal(16f, ruler.High, 3);
            Assert.Equal(0f + FloorCut.LeastOver, ruler.Least, 3);
            // Its own levels are named from its ground, as the card's chips are.
            Assert.Equal(0f, ruler.Ground, 3);
        }

        [Fact]
        public void WithNoRoomAboveItReachesTheRoomsOwnTop()
        {
            var ruler = RoomRuler.Of(ground: 0f, levels: new[] { 0f }, top: 7f, above: new (float, float)[0]);
            Assert.Equal(7f, ruler.High, 3);
        }

        [Fact]
        public void ALevelOfTheRoomUnderItsGroundLowersTheRuler()
        {
            // Mörkhalla's tall floors have a level ten metres under the ground their rays found.
            var ruler = RoomRuler.Of(ground: 0f, levels: new[] { -10f, 0f }, top: 7f, above: new (float, float)[0]);
            Assert.Equal(-10f, ruler.Low, 3);
            Assert.Equal(-10f + FloorCut.LeastOver, ruler.Least, 3);
        }

        [Fact]
        public void ItHasATickForEachOwnLevelAndEachLevelOfTheRoomsAboveFromTheBottomUp()
        {
            var ruler = RoomRuler.Of(ground: 0f, levels: new[] { 3f, 0f }, top: 7f, above: new[] { (8f, 16f) });
            Assert.Equal(new[] { 0f, 3f, 8f }, ruler.Ticks.Select(t => t.Height).ToArray());
            Assert.Equal(new[] { true, true, false }, ruler.Ticks.Select(t => t.Own).ToArray());
            Assert.Equal(1, ruler.Ticks[2].Rooms);
        }

        [Fact]
        public void RoomsAboveWithinALevelOfEachOtherShareATickThatCountsThem()
        {
            var ruler = RoomRuler.Of(ground: 0f, levels: new[] { 0f }, top: 7f, above: new[] { (8.9f, 14f), (8f, 12f), (15f, 20f) });
            var above = ruler.Ticks.Where(t => !t.Own).ToList();
            Assert.Equal(new[] { 8f, 15f }, above.Select(t => t.Height).ToArray());
            Assert.Equal(new[] { 2, 1 }, above.Select(t => t.Rooms).ToArray());
            Assert.Equal(20f, ruler.High, 3);
        }

        [Fact]
        public void ARoomAboveOnALevelOfTheRoomsOwnAddsNoTickOfItsOwn()
        {
            var ruler = RoomRuler.Of(ground: 0f, levels: new[] { 0f, 3f }, top: 7f, above: new[] { (3.5f, 9f) });
            Assert.Equal(new[] { 0f, 3f }, ruler.Ticks.Select(t => t.Height).ToArray());
            Assert.Equal(9f, ruler.High, 3);
        }

        [Fact]
        public void AFoundRoomWithNoLevelsReadHasItsGroundForALevel()
        {
            var ruler = RoomRuler.Of(ground: -4f, levels: new float[0], top: 2f, above: new (float, float)[0]);
            Assert.Single(ruler.Ticks);
            Assert.Equal(-4f, ruler.Ticks[0].Height, 3);
            Assert.True(ruler.Ticks[0].Own);
        }

        [Fact]
        public void TheTickLitIsTheHighestTheCutStandsOver()
        {
            var ruler = RoomRuler.Of(ground: 0f, levels: new[] { 0f, 3f }, top: 7f, above: new[] { (8f, 16f) });
            Assert.Equal(0, ruler.TickUnder(2.5f));
            Assert.Equal(1, ruler.TickUnder(5.5f));
            Assert.Equal(2, ruler.TickUnder(10f));
            Assert.Equal(0, ruler.TickUnder(0f));
            Assert.Equal(-1, ruler.TickUnder(-1f));
        }
    }
}
