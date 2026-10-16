using Xunit;

namespace Scry.Tests
{
    public class RoomFocusTests
    {
        [Fact]
        public void TheRoomGoneIntoStandsWholeItsLevelHalfDimmedBelowFaintAboveAway()
        {
            const float ground = -10f;

            Assert.Equal(RoomLook.Whole, RoomFocus.Look(true, ground, ground));
            Assert.Equal(RoomLook.Near, RoomFocus.Look(false, -11.5f, ground));
            Assert.Equal(RoomLook.Near, RoomFocus.Look(false, -8.5f, ground));
            Assert.Equal(RoomLook.Below, RoomFocus.Look(false, -11.6f, ground));
            Assert.Equal(RoomLook.Away, RoomFocus.Look(false, -8.4f, ground));
        }

        [Fact]
        public void ARoomsGroundIsItsOwnMainFloorElseWhereItIsWalkedInto()
        {
            // Its rays found its main floor; else its lowest doorway; else the foot of its box.
            Assert.Equal(-3.2f, RoomFocus.Ground(-3.2f, new[] { -2f, -5f }, -6f), 3);
            Assert.Equal(-5f, RoomFocus.Ground(null, new[] { -2f, -5f }, -6f), 3);
            Assert.Equal(-6f, RoomFocus.Ground(null, new float[0], -6f), 3);
        }

        [Fact]
        public void TheRoomIsCutAtHeadHeightOverItsGroundOrJustUnderItsCeiling()
        {
            Assert.Equal(-10f + PlaceView.CutAboveFloor, RoomFocus.Cut(-10f, null), 3);
            Assert.Equal(-10f + PlaceView.CutAboveFloor, RoomFocus.Cut(-10f, -2f), 3);
            Assert.Equal(-8.2f - PlaceView.UnderCeiling, RoomFocus.Cut(-10f, -8.2f), 3);
            // Never so low that what stands on the ground is cut through.
            Assert.True(RoomFocus.Cut(-10f, -9.5f) >= -10f + 1.4f - 0.001f);
        }
    }
}
