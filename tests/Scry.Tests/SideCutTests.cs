using System;
using Xunit;

namespace Scry.Tests
{
    public class SideCutTests
    {
        [Fact]
        public void TheSideCutFacesAwayFromTheCameraLevel()
        {
            // Looking down at a slant toward +Z, the cut stands upright and faces +Z.
            var facing = SideCut.Facing(new Vec3(0f, -0.6f, 0.8f), new Vec3(0f, 0.8f, 0.6f));

            Assert.Equal(0f, facing.X, 4);
            Assert.Equal(0f, facing.Y, 4);
            Assert.Equal(1f, facing.Z, 4);
        }

        [Fact]
        public void LookingStraightDownTheCutFacesTheWayTheTopOfThePictureIs()
        {
            var facing = SideCut.Facing(new Vec3(0f, -1f, 0f), new Vec3(1f, 0f, 0f));

            Assert.Equal(1f, facing.X, 4);
            Assert.Equal(0f, facing.Y, 4);
            Assert.Equal(0f, facing.Z, 4);
        }

        [Fact]
        public void TheHalfFacingTheCameraIsTakenAwayAndTheFarHalfKept()
        {
            var through = new Vec3(10f, 0f, 10f);
            var facing = SideCut.Facing(new Vec3(0.6f, -0.5f, 0.6f), new Vec3(0f, 1f, 0f));

            Assert.True(SideCut.Keeps(new Vec3(12f, -40f, 12f), through, facing));
            Assert.False(SideCut.Keeps(new Vec3(8f, 5f, 8f), through, facing));
            // On the cut itself is kept.
            Assert.True(SideCut.Keeps(through, through, facing));
            Assert.Equal(1f, (float)Math.Sqrt(facing.X * facing.X + facing.Z * facing.Z), 4);
        }
    }
}
