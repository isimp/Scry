using Xunit;

namespace Scry.Tests
{
    public class HeaderSlidersTests
    {
        // The header's two small sliders, each opened from its icon: the selection's volume, from
        // silent to twice the game's own in steps of 5%, and its size on a curve from a tenth to
        // ten times, so both ends are as easy to reach, caught at its own size near the middle.

        [Fact]
        public void TheSpeedSliderGoesFromStillToThreeTimesCaughtAtItsOwn()
        {
            // Its own speed sits a third of the way along, and a share near it is caught there.
            Assert.Equal(0f, HeaderSliders.SpeedAt(0f), 3);
            Assert.Equal(3f, HeaderSliders.SpeedAt(1f), 3);
            Assert.Equal(1f, HeaderSliders.SpeedAt(1f / 3f), 3);
            Assert.Equal(1f, HeaderSliders.SpeedAt(1f / 3f + 0.008f), 3);
            Assert.Equal(1.5f, HeaderSliders.SpeedAt(0.5f), 3);
            // Dragged past either end of the track, it stays at that end.
            Assert.Equal(3f, HeaderSliders.SpeedAt(1.2f), 3);
            Assert.Equal(0f, HeaderSliders.SpeedAt(-0.1f), 3);
            Assert.Equal(0.5f, HeaderSliders.SpeedShare(1.5f), 3);
            Assert.Equal("×1.5", HeaderSliders.SpeedLabel(1.5f));
            Assert.Equal("Animation speed ×1.0: click to change it", HeaderSliders.SpeedTip(1f));
        }

        [Fact]
        public void TheVolumeSliderGoesFromSilentToTwiceInStepsOfFive()
        {
            Assert.Equal(0f, HeaderSliders.VolumeAt(0f), 3);
            Assert.Equal(2f, HeaderSliders.VolumeAt(1f), 3);
            Assert.Equal(1f, HeaderSliders.VolumeAt(0.5f), 3);
            // 0.512 of the way is 102.4%, nearest to 100%; 0.52 is 104%, nearest to 105%.
            Assert.Equal(1f, HeaderSliders.VolumeAt(0.512f), 3);
            Assert.Equal(1.05f, HeaderSliders.VolumeAt(0.52f), 3);
            // Past either end is the end.
            Assert.Equal(0f, HeaderSliders.VolumeAt(-0.3f), 3);
            Assert.Equal(2f, HeaderSliders.VolumeAt(1.4f), 3);
        }

        [Fact]
        public void TheVolumeSliderStandsWhereTheVolumeIs()
        {
            Assert.Equal(0.5f, HeaderSliders.VolumeShare(1f), 3);
            Assert.Equal(1f, HeaderSliders.VolumeShare(2f), 3);
            Assert.Equal("100%", HeaderSliders.VolumeLabel(1f));
            Assert.Equal("35%", HeaderSliders.VolumeLabel(0.35f));
        }

        [Fact]
        public void TheSizeSliderRunsOnACurveFromATenthToTenTimes()
        {
            Assert.Equal(0.1f, HeaderSliders.SizeAt(0f), 3);
            Assert.Equal(10f, HeaderSliders.SizeAt(1f), 2);
            Assert.Equal(1f, HeaderSliders.SizeAt(0.5f), 3);
            // A quarter of the way is a third of the size (10 to the power of -0.5).
            Assert.Equal(0.316f, HeaderSliders.SizeAt(0.25f), 3);
            Assert.Equal(0.25f, HeaderSliders.SizeShare(0.31623f), 3);
            Assert.Equal(0.1f, HeaderSliders.SizeAt(-1f), 3);
        }

        [Fact]
        public void NearTheMiddleTheSizeIsItsOwn()
        {
            Assert.Equal(1f, HeaderSliders.SizeAt(0.505f), 3);
            Assert.Equal(1f, HeaderSliders.SizeAt(0.495f), 3);
            Assert.NotEqual(1f, HeaderSliders.SizeAt(0.53f));
            Assert.Equal("\u00d71.00", HeaderSliders.SizeLabel(1f));
            Assert.Equal("\u00d72.50", HeaderSliders.SizeLabel(2.5f));
        }
    }
}
