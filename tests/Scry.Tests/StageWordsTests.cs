using Xunit;

namespace Scry.Tests
{
    public class StageWordsTests
    {
        // ----- The stage's key sheet, behind its ? in the corner -----

        [Fact]
        public void TheKeySheetListsWhatTheStageAnswersToForAModel()
        {
            var sheet = StageWords.KeySheet(cut: false, inRoom: false);
            Assert.Contains("Drag: turn", sheet);
            Assert.Contains("Right-drag: move", sheet);
            Assert.Contains("Wheel: zoom", sheet);
            Assert.Contains("Double-click: frame it again", sheet);
            Assert.DoesNotContain("Page Up", sheet);
        }

        [Fact]
        public void OpenedTheKeySheetAddsTheFloorsAndTheCut()
        {
            var sheet = StageWords.KeySheet(cut: true, inRoom: false);
            Assert.Contains("Page Up and Down", sheet);
            Assert.Contains("Shift and the wheel: move the cut", sheet);
        }

        [Fact]
        public void InARoomTheKeySheetSaysHowToComeBackOut()
        {
            var sheet = StageWords.KeySheet(cut: true, inRoom: true);
            Assert.Contains("Esc", sheet);
            Assert.Contains("back button", sheet);
            Assert.Contains("past its ends", sheet);
            // The roof button puts the room's own roof on, it does not leave the room.
            Assert.DoesNotContain("roof button to come back out", sheet);
        }

        [Fact]
        public void TheKeySheetsMarkSaysWhatItShows()
        {
            Assert.Equal("?", StageWords.KeysMark);
        }

        [Fact]
        public void ARoomOfADungeonsExampleSaysAClickGoesIntoIt()
        {
            Assert.Equal("Burial chamber\nClick to go in", StageWords.GoIntoTip("Burial chamber", inside: false));
        }

        [Fact]
        public void TheRoomGoneIntoSaysHowToComeBackOut()
        {
            Assert.Equal("Burial chamber\nEsc or Whole dungeon comes back out", StageWords.GoIntoTip("Burial chamber", inside: true));
        }

        [Fact]
        public void TheCutControlNamesItsPartsAndSaysWhatEachCuts()
        {
            Assert.Equal("Cut", StageWords.CutLabel);
            Assert.Equal("Closed", StageWords.CutModeName(CutMode.Closed));
            Assert.Equal("Top", StageWords.CutModeName(CutMode.Top));
            Assert.Equal("Side", StageWords.CutModeName(CutMode.Side));
            Assert.Equal("Closed: the roof on", StageWords.CutModeTip(CutMode.Closed, inRoom: false));
            Assert.Equal("Cut from above: the roof off over a floor or level", StageWords.CutModeTip(CutMode.Top, inRoom: false));
            Assert.Equal("Cut from the side: the half of every room facing you taken away", StageWords.CutModeTip(CutMode.Side, inRoom: false));
        }

        [Fact]
        public void InARoomTheCutControlActsOnTheRoomAndStaysInIt()
        {
            Assert.Equal("Closed: the room's roof on, the rooms above it back, the room still gone into", StageWords.CutModeTip(CutMode.Closed, inRoom: true));
            Assert.Equal("Cut from above: the room's roof off at head height over its floor", StageWords.CutModeTip(CutMode.Top, inRoom: true));
        }

        [Fact]
        public void ACampsRoomStillGoesToItsPage()
        {
            Assert.Equal("Tent\nClick to go to it", StageWords.RoomTip("Tent", true));
        }

        // ----- The whole dungeon opened by the plan's levels -----

        [Fact]
        public void AMarkOfTheRulerNamesItsLevelByItsDepthAlone()
        {
            // However many cuts a level is filmed in, its mark is the level.
            Assert.Equal("Level -17 m", StageWords.LevelMark("-17 m"));
            Assert.Equal("Level +1 m", StageWords.LevelMark("+1 m"));
        }

        // ----- The ruler in a room gone into -----

        [Fact]
        public void TheRoomsRulerNamesTheRoomsAboveOnAMarkByHowManyAndHowFarUp()
        {
            Assert.Equal("1 room, 8 m up", StageWords.RoomsAboveMark(1, 8.2f));
            Assert.Equal("3 rooms, 20 m up", StageWords.RoomsAboveMark(3, 19.6f));
            Assert.Equal("3 rooms, 20 m up: click to cut over them, bringing them back", StageWords.RoomsAboveTip("3 rooms, 20 m up"));
        }

        [Fact]
        public void TheRoomsRulerSaysItStaysInTheRoom()
        {
            Assert.Equal("Roof on", StageWords.RoomRoofOn);
            Assert.Contains("stays in the room", StageWords.RoomRulerTip(aboveTop: false));
            Assert.Equal("Click to put the room's roof on", StageWords.RoomRulerTip(aboveTop: true));
        }
    }
}
