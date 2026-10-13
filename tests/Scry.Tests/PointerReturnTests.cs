using Xunit;

namespace Scry.Tests
{
    public class PointerReturnTests
    {
        // Looking around with the right mouse button holds the cursor in the middle of the window;
        // let go, the pointer is back where the look began, not left in the middle.

        [Fact]
        public void LetGoTheRightButtonThePointerIsBackWhereTheLookBegan()
        {
            var pointer = new PointerReturn();
            pointer.Frame(looking: true, open: true, 300f, 200f);
            pointer.Frame(looking: true, open: true, 960f, 540f);
            pointer.Frame(looking: false, open: true, 960f, 540f);

            Assert.True(pointer.TakeDue(out var x, out var y));
            Assert.Equal(300f, x);
            Assert.Equal(200f, y);
        }

        [Fact]
        public void ThePointerIsPutBackOnceOnly()
        {
            var pointer = new PointerReturn();
            pointer.Frame(looking: true, open: true, 300f, 200f);
            pointer.Frame(looking: false, open: true, 960f, 540f);
            pointer.TakeDue(out _, out _);
            pointer.Frame(looking: false, open: true, 500f, 100f);

            Assert.False(pointer.TakeDue(out _, out _));
        }

        [Fact]
        public void WithoutLookingThePointerStaysWhereItIs()
        {
            var pointer = new PointerReturn();
            pointer.Frame(looking: false, open: true, 300f, 200f);
            pointer.Frame(looking: false, open: true, 400f, 250f);

            Assert.False(pointer.TakeDue(out _, out _));
        }

        [Fact]
        public void APanelClosedWhileLookingLeavesTheCursorToTheGame()
        {
            // Closed, the game holds the cursor for play; putting it anywhere would fight that.
            var pointer = new PointerReturn();
            pointer.Frame(looking: true, open: true, 300f, 200f);
            pointer.Frame(looking: false, open: false, 960f, 540f);

            Assert.False(pointer.TakeDue(out _, out _));
        }

        [Fact]
        public void APanelClosedBeforeThePointerWasPutBackLeavesItToo()
        {
            var pointer = new PointerReturn();
            pointer.Frame(looking: true, open: true, 300f, 200f);
            pointer.Frame(looking: false, open: true, 960f, 540f);
            pointer.Frame(looking: false, open: false, 960f, 540f);

            Assert.False(pointer.TakeDue(out _, out _));
        }

        [Fact]
        public void ALookBegunAgainBeforeThePointerWasPutBackReturnsWhereTheNewOneBegan()
        {
            var pointer = new PointerReturn();
            pointer.Frame(looking: true, open: true, 300f, 200f);
            pointer.Frame(looking: false, open: true, 960f, 540f);
            pointer.Frame(looking: true, open: true, 700f, 400f);
            Assert.False(pointer.TakeDue(out _, out _));
            pointer.Frame(looking: false, open: true, 960f, 540f);

            Assert.True(pointer.TakeDue(out var x, out var y));
            Assert.Equal(700f, x);
            Assert.Equal(400f, y);
        }
    }
}
