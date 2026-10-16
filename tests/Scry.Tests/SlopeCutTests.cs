using System;
using Xunit;

namespace Scry.Tests
{
    public class SlopeCutTests
    {
        private static Vec3 At(float x, float y, float z) => new Vec3(x, y, z);

        /// <summary>The Hole's crossroads tube: two doorways low on one side, one high on the other, 10 m up.</summary>
        private static readonly Vec3[] Tube = { At(-7.5f, -5f, 7.5f), At(7.5f, 5f, -7.5f), At(-15f, -5f, 0f) };

        [Fact]
        public void ATunnelWithNoRealFloorIsCutAlongItsSlopeAHeadsHeightOverTheWayThroughIt()
        {
            var cut = SlopeCut.Of(Tube, realFloor: false);
            Assert.NotNull(cut);
            var plane = cut.Value;
            Assert.True(plane.Sloping);
            // A head's height over the middle of its lowest doorways, and over its highest.
            Assert.Equal(-2.5f, plane.HeightAt(-11.25f, 3.75f), 3);
            Assert.Equal(7.5f, plane.HeightAt(7.5f, -7.5f), 3);
            // Halfway along, halfway up.
            Assert.Equal(2.5f, plane.HeightAt(-1.875f, -1.875f), 3);
        }

        [Fact]
        public void DoorwaysWithinAMetreOfTheLowestAreAllItsLowEnd()
        {
            // Two low doorways half a metre apart in height: the slope starts over the middle of both.
            var plane = SlopeCut.Of(new[] { At(-10f, -5f, 0f), At(-10f, -5.5f, 4f), At(10f, 5f, 2f) }, realFloor: false).Value;
            Assert.Equal(-2.75f, plane.HeightAt(-10f, 2f), 3);
        }

        [Fact]
        public void SidewaysAcrossTheSlopeTheCutKeepsItsHeight()
        {
            var plane = SlopeCut.Of(Tube, realFloor: false).Value;
            // Square to the way from the low doorways to the high one.
            var along = new[] { 18.75f, -11.25f };
            var side = new[] { -along[1], along[0] };
            var length = (float)Math.Sqrt(side[0] * side[0] + side[1] * side[1]);
            Assert.Equal(2.5f, plane.HeightAt(-1.875f + side[0] / length * 5f, -1.875f + side[1] / length * 5f), 3);
        }

        [Fact]
        public void TheCutKeepsTheTunnelsFloorAndTakesItsRoofAway()
        {
            var plane = SlopeCut.Of(Tube, realFloor: false).Value;
            Assert.True(plane.Keeps(At(-1.875f, 0.5f, -1.875f)));
            Assert.False(plane.Keeps(At(-1.875f, 4f, -1.875f)));
        }

        [Fact]
        public void AShaftIsCutFlatAHeadsHeightOverItsHighestDoorwaysSoItIsLookedDown()
        {
            // The caves' hole: doorways all round its foot and all round its top, nearly straight above each other.
            var shaft = new[] { At(0f, -12f, 6f), At(0f, -12f, -6f), At(-6f, -12f, 0f), At(0f, 5.8f, 6f), At(0f, 5.8f, -6f), At(6f, 5.8f, 0f) };
            var plane = SlopeCut.Of(shaft, realFloor: false).Value;
            Assert.False(plane.Sloping);
            Assert.Equal(8.3f, plane.HeightAt(0f, 0f), 3);
            Assert.Equal(8.3f, plane.HeightAt(-6f, 6f), 3);
        }

        [Fact]
        public void AStairwayIsCutAlongItsGentleSlope()
        {
            // The sunken crypts' stairs: 3.5 m down over 16 m.
            var plane = SlopeCut.Of(new[] { At(7.8f, -2.5f, 0f), At(-8f, -6f, 0f) }, realFloor: false).Value;
            Assert.True(plane.Sloping);
            Assert.Equal(-3.5f, plane.HeightAt(-8f, 0f), 3);
            Assert.Equal(0f, plane.HeightAt(7.8f, 0f), 3);
        }

        [Fact]
        public void ARoomWithItsDoorwaysWithinAStoreyOrARealFloorKeepsItsCutOverItsGround()
        {
            Assert.Null(SlopeCut.Of(new[] { At(0f, -2f, 0f), At(5f, -1f, 0f) }, realFloor: false));
            Assert.Null(SlopeCut.Of(Tube, realFloor: true));
            Assert.Null(SlopeCut.Of(new[] { At(0f, -2f, 0f) }, realFloor: false));
        }

        [Fact]
        public void TheSlopeMovesUpAndDownWithTheCutMovedByHand()
        {
            var plane = SlopeCut.Of(Tube, realFloor: false).Value.Raised(1.5f);
            Assert.Equal(-1f, plane.HeightAt(-11.25f, 3.75f), 3);
            Assert.Equal(9f, plane.HeightAt(7.5f, -7.5f), 3);
        }
    }
}
