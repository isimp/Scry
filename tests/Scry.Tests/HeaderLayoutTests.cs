using Xunit;

namespace Scry.Tests
{
    public class HeaderLayoutTests
    {
        // The switch between all, the game's own and mods' things stands at the end of the
        // header's first row in the full view, where the search leaves room for it; in the
        // compact view it follows the buttons under the search, on a row of its own only where
        // it does not fit beside them.

        [Fact]
        public void InTheFullViewTheOriginSwitchEndsTheFirstRowWhateverTheRounding()
        {
            // The buttons before it are summed in floats, and can end a hair past where its room starts.
            var (x, nextRow) = HeaderLayout.OriginSwitch(compact: false, afterButtons: 800.0001f, width: 200f, rowLeft: 12f, rowRight: 1000f);
            Assert.False(nextRow);
            Assert.Equal(800f, x);
        }

        [Fact]
        public void InTheCompactViewItTakesARowOfItsOwnOnlyWhereItDoesNotFit()
        {
            Assert.Equal((300f, false), HeaderLayout.OriginSwitch(true, 300f, 200f, 12f, 500f));
            Assert.Equal((300.25f, false), HeaderLayout.OriginSwitch(true, 300.25f, 200f, 12f, 500f));
            Assert.Equal((12f, true), HeaderLayout.OriginSwitch(true, 301f, 200f, 12f, 500f));
        }
    }
}
