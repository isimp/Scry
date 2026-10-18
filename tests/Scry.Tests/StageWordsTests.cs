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
            // Every notch steps: Shift and the wheel, Page Up and Down, the wheel on the ruler or the plan; the ruler's drag cuts anywhere.
            Assert.Contains("Shift and the wheel, Page Up and Down, the wheel on the ruler or the plan: the next floor or level", sheet);
            Assert.Contains("Drag on the ruler: cut anywhere", sheet);
            Assert.DoesNotContain("move the cut", sheet);
        }

        [Fact]
        public void InARoomTheKeySheetSaysHowToComeBackOut()
        {
            var sheet = StageWords.KeySheet(cut: true, inRoom: true);
            Assert.Contains("Esc", sheet);
            Assert.Contains("back button", sheet);
            Assert.Contains("Shift and the wheel, Page Up and Down: the room's next mark; past its ends, the next level", sheet);
            // The roof button puts the room's own roof on, it does not leave the room.
            Assert.DoesNotContain("roof button to come back out", sheet);
        }

        [Fact]
        public void InsideADungeonTheKeySheetSaysAClickPicksARoomAndADoubleClickGoesIn()
        {
            foreach (var inRoom in new[] { false, true })
            {
                var sheet = StageWords.KeySheet(cut: true, inRoom: inRoom, rooms: true);
                Assert.Contains("Click a room: pick it, Esc letting go of it", sheet);
                Assert.Contains("Double-click a room or Enter: go into it", sheet);
            }
            Assert.DoesNotContain("Click a room", StageWords.KeySheet(cut: true, inRoom: false));
        }

        [Fact]
        public void TheKeySheetsMarkSaysWhatItShows()
        {
            Assert.Equal("?", StageWords.KeysMark);
        }

        // A room of a dungeon's example under the mouse peeks: its name, where it is, what this
        // copy rolled in it, and what a click and a double-click do.

        [Fact]
        public void ARoomPeeksWithWhereItIsWhatItHoldsAndThatAClickPicksIt()
        {
            Assert.Equal("Burial chamber\n12 m below the entrance  ·  4 rooms in  ·  3 ways\nCreatures 2  ·  Chests and pickups 5\nClick to pick it, double-click to go in",
                StageWords.RoomPeek("Burial chamber", "12 m below the entrance  ·  4 rooms in  ·  3 ways", creatures: 2, chests: 5, goneInto: false));
        }

        [Fact]
        public void TheRoomGoneIntoPeeksWithHowToComeBackOut()
        {
            // As the key sheet says: Esc, the breadcrumb's level or the mouse's back button.
            Assert.EndsWith("\nEsc, the breadcrumb's level or the mouse's back button comes back out", StageWords.RoomPeek("Burial chamber", "Entrance", creatures: 0, chests: 0, goneInto: true));
            Assert.Contains("Esc, the breadcrumb's level or the mouse's back button", StageWords.KeySheet(cut: true, inRoom: true));
        }

        [Fact]
        public void AWayOnOfTheCardSaysAClickGoesIntoItsRoom()
        {
            Assert.Equal("Shrine hole 01, 2 m up\nClick to go in", StageWords.GoIntoTip("Shrine hole 01, 2 m up"));
        }

        [Fact]
        public void ARoomPickedSaysADoubleClickGoesIn()
        {
            Assert.Equal("Go in", StageWords.GoInLink);
            Assert.Equal("Goes into the room picked, as a double-click on it does", StageWords.GoInTip);
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
        public void TheRoomsRulerSaysItsMarksStepAndPastItsEndsTheNextLevel()
        {
            Assert.Equal("Roof on", StageWords.RoomRoofOn);
            Assert.Contains("past its ends, the next level", StageWords.RoomRulerTip(aboveTop: false));
            Assert.DoesNotContain("stays in the room", StageWords.RoomRulerTip(aboveTop: false));
            Assert.Equal("Click to put the room's roof on", StageWords.RoomRulerTip(aboveTop: true));
        }
    }
}
