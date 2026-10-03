using Xunit;

namespace Scry.Tests
{
    public class YieldTests
    {
        // A game day lasts 1,200 s (EnvMan.m_dayLengthSec).
        private const float Day = 1200f;

        [Fact]
        public void ABushThatGrowsBackWithinADaySaysHowManyItGivesADay()
        {
            // One every 5 minutes: four in a day of 20.
            Assert.Equal("about 4 a day", Yield.PerDay(1, 5f, Day));
        }

        [Fact]
        public void ABushThatTakesLongerThanADaySaysEveryHowManyDays()
        {
            // Raspberries grow back in 300 minutes, 15 days of 20 minutes.
            Assert.Equal("3 every 15 days", Yield.PerDay(3, 300f, Day));
        }

        [Fact]
        public void AFractionOfADayIsRounded()
        {
            Assert.Equal("1 every 1.5 days", Yield.PerDay(1, 30f, Day));
        }

        [Fact]
        public void SomethingThatNeverGrowsBackHasNoYield()
        {
            Assert.Null(Yield.PerDay(1, 0f, Day));
            Assert.Null(Yield.PerDay(0, 5f, Day));
            Assert.Null(Yield.PerDay(1, 5f, 0f));
        }
    }
}
