using Xunit;

namespace Scry.Tests
{
    public class MapMarksTests
    {
        // On Valheim's large map, each dungeon been in has a small mark just above its entrance,
        // placed as the game places its pins: a world point to a point of the map's texture, then
        // into the window of it the large map shows.

        private static readonly (float XMin, float YMin, float Width, float Height) Whole = (0f, 0f, 1f, 1f);

        [Fact]
        public void AMarkStandsWhereTheGameWouldPinItsEntrance()
        {
            // A 256 texel map of 64 m texels: the world's middle in the map's middle.
            Assert.True(MapMarks.Place(new Vec3(0f, 30f, 0f), 64f, 256, Whole, (1000f, 800f), out var middle));
            Assert.Equal(500f, middle.X, 3);
            Assert.Equal(400f, middle.Y, 3);
            Assert.True(MapMarks.Place(new Vec3(64f * 64f, 0f, -64f * 32f), 64f, 256, Whole, (1000f, 800f), out var off));
            Assert.Equal(750f, off.X, 3);
            Assert.Equal(300f, off.Y, 3);
        }

        [Fact]
        public void TheLargeMapsWindowMovesAndScalesIt()
        {
            // Zoomed to the quarter round the middle.
            var window = (0.375f, 0.375f, 0.25f, 0.25f);
            Assert.True(MapMarks.Place(new Vec3(0f, 0f, 0f), 64f, 256, window, (1000f, 800f), out var middle));
            Assert.Equal(500f, middle.X, 3);
            Assert.Equal(400f, middle.Y, 3);
            Assert.True(MapMarks.Place(new Vec3(64f * 16f, 0f, 0f), 64f, 256, window, (1000f, 800f), out var right));
            Assert.Equal(750f, right.X, 3);
        }

        [Fact]
        public void AnEntranceOutsideTheWindowHasNoMark()
        {
            var window = (0.375f, 0.375f, 0.25f, 0.25f);
            Assert.False(MapMarks.Place(new Vec3(64f * 64f, 0f, 0f), 64f, 256, window, (1000f, 800f), out _));
            Assert.False(MapMarks.Place(new Vec3(0f, 0f, -64f * 64f), 64f, 256, window, (1000f, 800f), out _));
        }

        [Fact]
        public void AMarkIsAsBigAsAPinAndStandsJustAboveOneAtTheEntrance()
        {
            Assert.Equal(48f, MapMarks.Size(48f), 3);
            Assert.Equal(48f, MapMarks.Lift(48f), 3);
        }

        [Fact]
        public void AMarkTakesAClickOnlyWhereThePointerStayedOnItNotADragOfTheMap()
        {
            // The game calls a press under a quarter second a click however far it moved (Minimap.OnMapLeftUp):
            // a quick drag of the map let go over a mark is no click of the mark's.
            Assert.True(MapMarks.Clicked((100f, 100f), (104f, 98f), 48f));
            Assert.True(MapMarks.Clicked((100f, 100f), (100f, 124f), 48f));
            Assert.False(MapMarks.Clicked((100f, 100f), (100f, 125f), 48f));
            Assert.False(MapMarks.Clicked((10f, 10f), (300f, 200f), 48f));
        }

        [Fact]
        public void AMarksWordsUnderItNameTheDungeonAndHowFarItWasWalked()
        {
            // As the game names a pin under it, shown while the pointer rests on the mark.
            Assert.Equal("Burial Chambers\n<size=80%><color=#F5B857>6 of 18 rooms walked</color></size>", DungeonMapWords.MarkLabel("Burial Chambers", 6, 18, "F5B857"));
        }
    }
}
