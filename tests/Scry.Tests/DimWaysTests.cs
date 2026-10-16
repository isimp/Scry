using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class DimWaysTests
    {
        private static Dictionary<DimWay, float?> Kept(float? tint, float? colours, float? light, float? plain) => new Dictionary<DimWay, float?>
        {
            { DimWay.Tint, tint }, { DimWay.Colours, colours }, { DimWay.Light, light }, { DimWay.Plain, plain },
        };

        [Fact]
        public void EachPlaceIsToldWithWhatEachWayOfDimmingLeavesOfItsBrightness()
        {
            Assert.Equal("Frost Caves: its tint 93%, every colour 80%, light at 30% 35%, one plain dark material 20%",
                DimWays.Line("Frost Caves", Kept(0.93f, 0.8f, 0.35f, 0.2f)));
        }

        [Fact]
        public void AWayThatCouldNotBeReadIsToldSo()
        {
            Assert.Equal("The Hole: its tint 49%, every colour not read, light at 30% 35%, one plain dark material not read",
                DimWays.Line("The Hole", new Dictionary<DimWay, float?> { { DimWay.Tint, 0.49f }, { DimWay.Colours, null }, { DimWay.Light, 0.35f } }));
        }

        [Fact]
        public void OnlyTheWaysDarkEnoughInEveryPlaceAreNamed()
        {
            var places = new[]
            {
                Kept(0.93f, 0.8f, 0.35f, 0.2f),
                Kept(0.49f, 0.45f, 0.62f, 0.25f),
                Kept(0.71f, 0.5f, 0.3f, 0.6f),
            };

            Assert.Equal(new[] { DimWay.Plain }, DimWays.DarkEverywhere(places));
            Assert.Equal("dark enough in every place: one plain dark material", DimWays.Summary(places));
        }

        [Fact]
        public void AWayNotReadInSomePlaceIsNotDarkEverywhere()
        {
            var places = new[]
            {
                Kept(0.5f, 0.5f, 0.3f, 0.2f),
                new Dictionary<DimWay, float?> { { DimWay.Tint, 0.5f }, { DimWay.Colours, 0.5f }, { DimWay.Light, null } },
            };

            Assert.Equal(new[] { DimWay.Tint, DimWay.Colours }, DimWays.DarkEverywhere(places));
        }

        [Fact]
        public void WithNoWayDarkEnoughEverywhereTheSummarySaysSo()
        {
            var places = new[] { Kept(0.93f, 0.9f, 0.7f, 0.65f) };

            Assert.Empty(DimWays.DarkEverywhere(places));
            Assert.Equal("no way of dimming is dark enough in every place", DimWays.Summary(places));
        }

        [Fact]
        public void WithNoPlaceReadNoWayIsDarkEverywhere()
        {
            Assert.Empty(DimWays.DarkEverywhere(new Dictionary<DimWay, float?>[0]));
        }
    }
}
