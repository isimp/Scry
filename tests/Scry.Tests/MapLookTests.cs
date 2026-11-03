using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MapLookTests
    {
        // How Show on map sets the rest of the map back and draws its own marks, as the player
        // chooses it in the settings' Map section.

        [Fact]
        public void YourOtherPinsStandAtFullAThirdOrHidden()
        {
            Assert.Equal(1f, MapLook.PinsShare(OtherPinsShown.Full));
            Assert.Equal(1f / 3f, MapLook.PinsShare(OtherPinsShown.Third), 4);
            Assert.Equal(0f, MapLook.PinsShare(OtherPinsShown.Hidden));
            // At full nothing is dimmed at all.
            Assert.False(MapLook.Dims(OtherPinsShown.Full));
            Assert.True(MapLook.Dims(OtherPinsShown.Third));
            Assert.True(MapLook.Dims(OtherPinsShown.Hidden));
        }

        [Fact]
        public void TheMapIsDarkenedNotAtAllByHalfOrStrongly()
        {
            Assert.Equal(0f, MapLook.Veil(MapDarkening.Off));
            Assert.Equal(SeenOnMap.MapDarkened, MapLook.Veil(MapDarkening.Half));
            Assert.True(MapLook.Veil(MapDarkening.Strong) > MapLook.Veil(MapDarkening.Half) && MapLook.Veil(MapDarkening.Strong) < 1f,
                "strongly, the map still shows under it");
        }

        [Fact]
        public void AMarksSizeIsOneOfTheDotsThreeAndAValueBetweenGoesToTheNearest()
        {
            // The dungeon marks' size is chosen as the dots' is; a size kept from before snaps to the nearest of the three.
            Assert.Equal(new[] { 0.7f, 1f, 1.4f }, MapLook.MarkScales);
            Assert.Equal(0.7f, MapLook.NearestMarkScale(0.8f));
            Assert.Equal(0.7f, MapLook.NearestMarkScale(0.5f));
            Assert.Equal(1f, MapLook.NearestMarkScale(1.1f));
            Assert.Equal(1.4f, MapLook.NearestMarkScale(2f));
            Assert.Equal(MapLook.MarkScales, new[] { DotSizeChoice.Small, DotSizeChoice.Normal, DotSizeChoice.Large }.Select(MapLook.DotScale));
        }

        [Fact]
        public void DotsAreSmallerOrLargerThanNormal()
        {
            Assert.Equal(1f, MapLook.DotScale(DotSizeChoice.Normal));
            Assert.True(MapLook.DotScale(DotSizeChoice.Small) < 1f);
            Assert.True(MapLook.DotScale(DotSizeChoice.Large) > 1f);
            // Small zoomed out is still bigger than the dot the first build drew, a fifth of a pin.
            Assert.True(SeenOnMap.DotSize(48f, SeenOnMap.FarthestZoom) * MapLook.DotScale(DotSizeChoice.Small) > 48f * 0.2f * 2f);
        }
    }
}
