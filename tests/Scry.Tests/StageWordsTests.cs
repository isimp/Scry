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
        public void ACampsRoomStillGoesToItsPage()
        {
            Assert.Equal("Tent\nClick to go to it", StageWords.RoomTip("Tent", true));
        }
    }
}
