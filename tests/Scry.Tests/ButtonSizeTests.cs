using Xunit;

namespace Scry.Tests
{
    public class ButtonSizeTests
    {
        [Fact]
        public void AButtonDrawnBoldWhileOpenIsWideEnoughForItsBoldLabel()
        {
            // The compendium's button is drawn in the bold style while its box is open.
            Assert.Equal(130f, ButtonSize.Fitting(110f, 120f, 10f), 3);
        }

        [Fact]
        public void AButtonWhoseLabelIsWiderPlainIsWideEnoughForThat()
        {
            Assert.Equal(130f, ButtonSize.Fitting(120f, 110f, 10f), 3);
        }

        [Fact]
        public void AButtonKeepsOneWidthOpenOrNot()
        {
            // One width for both looks, so the button does not jump as its box opens.
            var width = ButtonSize.Fitting(98f, 104f, 8f);
            Assert.True(width >= 98f + 8f && width >= 104f + 8f);
            Assert.Equal(112f, width, 3);
        }
    }
}
