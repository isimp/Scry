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
            Assert.Equal(-3.2f, RoomFocus.Ground(-3.2f, 50f, new[] { -2f, -5f }, -6f), 3);
            Assert.Equal(-5f, RoomFocus.Ground(null, 0f, new[] { -2f, -5f }, -6f), 3);
            Assert.Equal(-6f, RoomFocus.Ground(null, 0f, new float[0], -6f), 3);
        }

        [Fact]
        public void AFloorWithLessRoomThanALevelNeedsIsNoGroundSoTheRoomIsWalkedIntoAtItsLowestDoorway()
        {
            // A tunnel's scrap of flat rock, 2 m2 below its lower doorway, is no floor to stand on.
            Assert.Equal(-5f, RoomFocus.Ground(-7f, 2f, new[] { 5f, -5f }, -9f), 3);
            Assert.Equal(-8.5f, RoomFocus.Ground(-8.5f, RoomLevels.LeastRoom, new[] { 5f, -5f }, -9f), 3);
            Assert.True(RoomFocus.RealFloor(-8.5f, RoomLevels.LeastRoom));
            Assert.False(RoomFocus.RealFloor(-7f, 2f));
            Assert.False(RoomFocus.RealFloor(null, 100f));
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

        // ----- The rooms above coming back as the cut rises -----

        [Fact]
        public void ARoomAboveComesBackHalfDimmedOnceTheCutRisesPastItsGround()
        {
            foreach (var over in new[] { true, false })
            {
                Assert.Equal(RoomLook.Away, RoomFocus.WithCut(RoomLook.Away, ground: 8f, cut: 8f + RoomFocus.BackOver - 0.1f, from: 2.5f, over));
                Assert.Equal(RoomLook.Near, RoomFocus.WithCut(RoomLook.Away, ground: 8f, cut: 8f + RoomFocus.BackOver + 0.1f, from: 2.5f, over));
            }
        }

        [Fact]
        public void ARoomAboveOverTheRoomGoneIntoStaysAwayWhileTheCutStandsWhereTheRoomWasFirstCut()
        {
            // A shaft is cut a head's height over its highest doorway, past the ground of a room
            // standing over it there: that room stays away until the cut is raised from there.
            Assert.Equal(RoomLook.Away, RoomFocus.WithCut(RoomLook.Away, ground: 18f, cut: 20.5f, from: 20.5f, over: true));
            Assert.Equal(RoomLook.Away, RoomFocus.WithCut(RoomLook.Away, ground: 18f, cut: 20.5f + RoomFocus.BackOver - 0.1f, from: 20.5f, over: true));
            Assert.Equal(RoomLook.Near, RoomFocus.WithCut(RoomLook.Away, ground: 18f, cut: 20.5f + RoomFocus.BackOver + 0.1f, from: 20.5f, over: true));
        }

        [Fact]
        public void ARoomAboveBesideTheRoomGoneIntoStandsHalfDimmedOnceItsFloorIsUnderTheCut()
        {
            // A corridor 1.9 m up beside the room hides nothing of it: it stands as soon as the cut
            // is half a metre over its floor, wherever the room was first cut.
            Assert.Equal(RoomLook.Near, RoomFocus.WithCut(RoomLook.Away, ground: 1.9f, cut: 2.5f, from: 2.5f, over: false));
            Assert.Equal(RoomLook.Away, RoomFocus.WithCut(RoomLook.Away, ground: 1.9f, cut: 2.5f, from: 2.5f, over: true));
        }

        [Fact]
        public void ARoomAboveBesideTheRoomGoneIntoWhoseFloorIsOverTheCutStaysAway()
        {
            // Only what is under its floor would show: rock, or nothing.
            Assert.Equal(RoomLook.Away, RoomFocus.WithCut(RoomLook.Away, ground: 2.2f, cut: 2.5f, from: 2.5f, over: false));
        }

        [Fact]
        public void OpenedByLevelsARoomOfAHigherLevelIsPutAwayOnlyWhereItWouldHideTheLevel()
        {
            // Over a room of the level, by its box seen from above, it would cover it.
            Assert.True(RoomFocus.HidesFromAbove(over: true, ground: 1f, cut: 2.5f));
            // Beside the level, its floor not half a metre under the cut: only its underside would show.
            Assert.True(RoomFocus.HidesFromAbove(over: false, ground: 2.2f, cut: 2.5f));
            Assert.True(RoomFocus.HidesFromAbove(over: false, ground: 2f, cut: 2.5f));
            // Beside the level, its floor under the cut: it stands, cut with the level.
            Assert.False(RoomFocus.HidesFromAbove(over: false, ground: 1.9f, cut: 2.5f));
        }

        [Fact]
        public void WithTheRoomsRoofOnEveryRoomAboveIsBack()
        {
            Assert.Equal(RoomLook.Near, RoomFocus.WithCut(RoomLook.Away, ground: 120f, cut: float.PositiveInfinity, from: 2.5f, over: true));
        }

        [Fact]
        public void TheCutChangesNoOtherLook()
        {
            foreach (var look in new[] { RoomLook.Whole, RoomLook.Near, RoomLook.Below })
            {
                Assert.Equal(look, RoomFocus.WithCut(look, ground: -20f, cut: 0f, from: -30f, over: false));
                Assert.Equal(look, RoomFocus.WithCut(look, ground: -20f, cut: float.PositiveInfinity, from: -30f, over: true));
            }
        }

        // ----- The plan box's switch for the rooms below, on the plan from above -----

        [Fact]
        public void SwitchedOffThePlanFromAboveHidesTheRoomsBelowTheLevelLookedAt()
        {
            Assert.True(RoomFocus.HiddenBelow(ground: -10f, level: 0f));
            Assert.False(RoomFocus.HiddenBelow(ground: -RoomFocus.SameLevel + 0.1f, level: 0f));
            Assert.False(RoomFocus.HiddenBelow(ground: 6f, level: 0f));
        }

        [Fact]
        public void WithNoLevelLookedAtThePlanHidesNothing()
        {
            Assert.False(RoomFocus.HiddenBelow(ground: -100f, level: null));
        }
    }
}
