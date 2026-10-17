using Xunit;

namespace Scry.Tests
{
    public class StageWordsTests
    {
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
        public void TheCutsTwoChipsSayWhichWayEachCutsAndWhatGoes()
        {
            // Named apart from the plan's Above and the camera's Side.
            Assert.Equal("Top cut", StageWords.CutChip(side: false));
            Assert.Equal("Side cut", StageWords.CutChip(side: true));
            Assert.Equal("Cut from above: the roof off over a floor or the room gone into", StageWords.CutTip(side: false));
            Assert.Equal("Cut from the side: the half of every room facing you taken away", StageWords.CutTip(side: true));
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
        public void TheRoomsRoofAndRulerSayTheyStayInTheRoom()
        {
            Assert.Contains("click to put its roof on", StageWords.RoomRoofTip(on: false));
            Assert.Contains("click to take it off", StageWords.RoomRoofTip(on: true));
            Assert.Equal("Roof on", StageWords.RoomRoofOn);
            Assert.Contains("stays in the room", StageWords.RoomRulerTip(aboveTop: false));
            Assert.Equal("Click to put the room's roof on", StageWords.RoomRulerTip(aboveTop: true));
        }
    }
}
