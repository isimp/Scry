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
