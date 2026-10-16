using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DimLookTests
    {
        /// <summary>A stage picture as each pixel's brightness: the backdrop at <paramref name="backdrop"/>, the rooms' pixels (the first <paramref name="rooms"/>) at <paramref name="room"/>.</summary>
        private static float[] Picture(int pixels, int rooms, float backdrop, float room) =>
            Enumerable.Range(0, pixels).Select(i => i < rooms ? room : backdrop).ToArray();

        [Fact]
        public void RoomsDimmedToAThirdOfTheirLightReadAsAThird()
        {
            var empty = Picture(100, 0, 0.1f, 0f);
            var whole = Picture(100, 40, 0.1f, 0.6f);
            var dimmed = Picture(100, 40, 0.1f, 0.2f);

            var (pixels, share) = DimLook.Read(empty, whole, dimmed);

            Assert.Equal(40, pixels);
            Assert.Equal(1f / 3f, share.Value, 3);
            Assert.True(DimLook.DarkEnough(share));
        }

        [Fact]
        public void DimmingThatChangesNothingReadsAsNoDarkerAndFails()
        {
            // Rooms whose shader takes no tint look as they were.
            var empty = Picture(100, 0, 0.1f, 0f);
            var whole = Picture(100, 40, 0.1f, 0.6f);

            var (pixels, share) = DimLook.Read(empty, whole, whole);

            Assert.Equal(40, pixels);
            Assert.Equal(1f, share.Value, 3);
            Assert.False(DimLook.DarkEnough(share));
        }

        [Fact]
        public void RoomsKeepingMoreThanSixtyPercentOfTheirBrightnessDoNotLookDimmed()
        {
            Assert.True(DimLook.DarkEnough(0.6f));
            Assert.False(DimLook.DarkEnough(0.61f));
        }

        [Fact]
        public void TheBackdropAroundTheRoomsIsLeftOutOfTheReading()
        {
            // Most of the picture is the backdrop, the same in all three: only the rooms' pixels count.
            var empty = Picture(1000, 0, 0.3f, 0f);
            var whole = Picture(1000, 10, 0.3f, 0.8f);
            var dimmed = Picture(1000, 10, 0.3f, 0.24f);

            var (pixels, share) = DimLook.Read(empty, whole, dimmed);

            Assert.Equal(10, pixels);
            Assert.Equal(0.3f, share.Value, 3);
        }

        [Fact]
        public void HalfTheRoomsDarkenedReadsByTheMiddlePixelNotTheMean()
        {
            // A few bright lights left on in dimmed rooms do not pull the reading up.
            var empty = Picture(10, 0, 0f, 0f);
            var whole = Picture(10, 10, 0f, 0.5f);
            var dimmed = Picture(10, 10, 0f, 0.1f);
            dimmed[0] = 1f;
            dimmed[1] = 1f;

            var (_, share) = DimLook.Read(empty, whole, dimmed);

            Assert.Equal(0.2f, share.Value, 3);
        }

        [Fact]
        public void PixelsTooDarkWhenWholeAreLeftOut()
        {
            // A shadow nearly black when whole tells nothing of how much darker dimming makes it.
            var empty = Picture(10, 0, 0.1f, 0f);
            var whole = Picture(10, 10, 0.1f, 0.01f);
            whole[0] = 0.5f;
            var dimmed = Picture(10, 10, 0.1f, 0.01f);
            dimmed[0] = 0.25f;

            var (pixels, share) = DimLook.Read(empty, whole, dimmed);

            Assert.Equal(1, pixels);
            Assert.Equal(0.5f, share.Value, 3);
        }

        [Fact]
        public void WithNothingOfTheRoomsInThePictureThereIsNoReading()
        {
            var empty = Picture(50, 0, 0.1f, 0f);

            var (pixels, share) = DimLook.Read(empty, empty, empty);

            Assert.Equal(0, pixels);
            Assert.Null(share);
            Assert.False(DimLook.DarkEnough(share));
        }

        [Fact]
        public void PicturesOfDifferentSizesHaveNoReading()
        {
            var whole = Enumerable.Repeat(0.5f, 4).ToArray();
            var dimmed = Enumerable.Repeat(0.25f, 5).ToArray();

            var (pixels, share) = DimLook.Read(new float[4], whole, dimmed);

            Assert.Equal(0, pixels);
            Assert.Null(share);
        }

        [Fact]
        public void BrightnessWeighsGreenMostAndBlueLeast()
        {
            Assert.Equal(1f, DimLook.Brightness(255, 255, 255), 3);
            Assert.Equal(0f, DimLook.Brightness(0, 0, 0), 3);
            Assert.True(DimLook.Brightness(0, 255, 0) > DimLook.Brightness(255, 0, 0));
            Assert.True(DimLook.Brightness(255, 0, 0) > DimLook.Brightness(0, 0, 255));
        }
    }
}
