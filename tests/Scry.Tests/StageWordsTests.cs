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
            Assert.Equal("Burial chamber\nEsc or the roof button comes back out", StageWords.GoIntoTip("Burial chamber", inside: true));
        }

        [Fact]
        public void TheCutsTwoChipsSayWhichWayEachCutsAndWhatGoes()
        {
            Assert.Equal("Above", StageWords.CutChip(side: false));
            Assert.Equal("Side", StageWords.CutChip(side: true));
            Assert.Equal("Cut from above: the roof off over a floor or the room gone into", StageWords.CutTip(side: false));
            Assert.Equal("Cut from the side: the half of every room facing you taken away", StageWords.CutTip(side: true));
        }

        [Fact]
        public void ACampsRoomStillGoesToItsPage()
        {
            Assert.Equal("Tent\nClick to go to it", StageWords.RoomTip("Tent", true));
        }
    }
}
