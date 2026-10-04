using System.Globalization;
using Xunit;

namespace Scry.Tests
{
    public class NumbersTests
    {
        // Every number Scry shows reads one way: English whatever language the PC is set to,
        // its thousands by commas, a fraction's point a point, rounded half away from zero.

        [Theory]
        [InlineData(0, "0")]
        [InlineData(12, "12")]
        [InlineData(1234, "1,234")]
        [InlineData(-5000, "-5,000")]
        [InlineData(1234567L, "1,234,567")]
        public void ACountHasItsThousandsByCommas(long value, string shown)
        {
            Assert.Equal(shown, Numbers.Count(value));
        }

        [Fact]
        public void ASignedCountSaysItsSign()
        {
            // A step button reads "+10" or "-1".
            Assert.Equal("+10", Numbers.Count(10, signed: true));
            Assert.Equal("-1", Numbers.Count(-1, signed: true));
            Assert.Equal("0", Numbers.Count(0, signed: true));
            Assert.Equal("+1,000", Numbers.Count(1000, signed: true));
        }

        [Theory]
        [InlineData(255, 2, "FF")]
        [InlineData(10, 2, "0A")]
        [InlineData(0, 2, "00")]
        [InlineData(4096, 4, "1000")]
        public void ACodeInHexHasItsDigits(long value, int digits, string shown)
        {
            // A colour read off a picture: "#0AFF00".
            Assert.Equal(shown, Numbers.Hex(value, digits));
        }

        [Theory]
        [InlineData(5000f, "5,000")]
        [InlineData(1234.5f, "1,234.5")]
        [InlineData(12.25f, "12.25")]
        [InlineData(12f, "12")]
        [InlineData(11.9999f, "12")]
        [InlineData(0.333f, "0.33")]
        [InlineData(-5f, "-5")]
        public void AnAmountKeepsUpToTwoDecimalsAndItsThousands(float value, string shown)
        {
            Assert.Equal(shown, Numbers.Amount(value));
        }

        [Theory]
        [InlineData(2.25, 1, "2.3")]
        [InlineData(2.35, 1, "2.4")]
        [InlineData(-2.25, 1, "-2.3")]
        [InlineData(2.5, 0, "3")]
        [InlineData(1500.04, 1, "1,500")]
        [InlineData(0.0004, 3, "0")]
        [InlineData(-0.001, 2, "0")]
        [InlineData(2.675, 2, "2.68")]
        public void AnAmountRoundsHalfAwayFromZeroToTheDecimalsAsked(double value, int decimals, string shown)
        {
            Assert.Equal(shown, Numbers.Amount(value, decimals));
        }

        [Theory]
        [InlineData(1.5, 2, "1.50")]
        [InlineData(1234.5, 1, "1,234.5")]
        [InlineData(3, 1, "3.0")]
        [InlineData(0.125, 2, "0.13")]
        public void AFixedReadoutAlwaysShowsItsDecimals(double value, int decimals, string shown)
        {
            // A clip's time or a slider's value keeps its width as it changes.
            Assert.Equal(shown, Numbers.Fixed(value, decimals));
        }

        [Theory]
        [InlineData(0.25, 0, false, "25%")]
        [InlineData(0.125, 1, false, "12.5%")]
        [InlineData(0.005, 0, false, "1%")]
        [InlineData(12.5, 0, false, "1,250%")]
        [InlineData(0.05, 0, true, "+5%")]
        [InlineData(-0.05, 0, true, "-5%")]
        [InlineData(0, 0, true, "0%")]
        public void AShareReadsAsAPercentage(double share, int decimals, bool signed, string shown)
        {
            Assert.Equal(shown, Numbers.Percent(share, decimals, signed));
        }

        [Fact]
        public void ASignedAmountSaysItsSign()
        {
            Assert.Equal("+2.5", Numbers.Amount(2.5, 2, signed: true));
            Assert.Equal("-1,000", Numbers.Amount(-1000, 2, signed: true));
            Assert.Equal("0", Numbers.Amount(0, 2, signed: true));
        }

        [Fact]
        public void ALengthIsInMetres()
        {
            Assert.Equal("1,500 m", Numbers.Metres(1500f));
            Assert.Equal("2.5 m", Numbers.Metres(2.5f));
            Assert.Equal("2 m", Numbers.Metres(2.04f, 1));
        }

        [Theory]
        [InlineData(3, 3, "3")]
        [InlineData(1, 4, "1–4")]
        [InlineData(1000, 2000, "1,000–2,000")]
        [InlineData(5, 2, "5")]
        public void ARangeOfCountsSaysOneNumberWhenItIsOne(long least, long most, string shown)
        {
            Assert.Equal(shown, Numbers.CountRange(least, most));
        }

        [Theory]
        [InlineData(40f, "40 s")]
        [InlineData(119f, "119 s")]
        [InlineData(120f, "2 min")]
        [InlineData(90f * 60f, "90 min")]
        [InlineData(1500f, "25 min")]
        [InlineData(7200f, "2 h")]
        [InlineData(9000f, "2.5 h")]
        [InlineData(9216f, "2.6 h")]
        [InlineData(3600f * 1500f, "1,500 h")]
        public void ATimeIsToldInTheLargestUnitThatReadsWell(float seconds, string shown)
        {
            Assert.Equal(shown, Numbers.Duration(seconds));
        }

        [Theory]
        [InlineData(3000f, 3600f, "50–60 min")]
        [InlineData(20f, 40f, "20–40 s")]
        [InlineData(90f, 180f, "90 s to 3 min")]
        [InlineData(600f, 600f, "10 min")]
        public void ARangeOfTimesSharesItsUnitWhereItCan(float least, float most, string shown)
        {
            Assert.Equal(shown, Numbers.DurationRange(least, most));
        }

        [Theory]
        [InlineData(0f, "0:00")]
        [InlineData(65.4f, "1:05")]
        [InlineData(3599.9f, "59:59")]
        [InlineData(3600f, "60:00")]
        public void APlayingTimeReadsAsMinutesAndSeconds(float seconds, string shown)
        {
            Assert.Equal(shown, Numbers.Clock(seconds));
        }

        [Fact]
        public void APointReadsAsItsFigures()
        {
            Assert.Equal("(1.50, -2.00)", Numbers.Point(1.5, -2, 2));
            Assert.Equal("(1,234.5, 0.0, 3.0)", Numbers.Point(1234.5, 0, 3, 1));
        }

        [Fact]
        public void NumbersReadTheSameWhateverThePcsLanguage()
        {
            var was = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.Equal("1,234", Numbers.Count(1234));
                Assert.Equal("2.5", Numbers.Amount(2.5f));
                Assert.Equal("1,234.50", Numbers.Fixed(1234.5, 2));
                Assert.Equal("-12.5%", Numbers.Percent(-0.125, 1, signed: true));
                Assert.Equal("2.5 h", Numbers.Duration(9000f));
                Assert.Equal("0.4%", DropWords.Share(0.004f));
                Assert.Equal("1,200 within 10 m, 3 within 40 m", SpawnWords.SpawnerCaps(1200, 10f, 3, 40f));
                CultureInfo.CurrentCulture = new CultureInfo("sv-SE");
                Assert.Equal("-5,000", Numbers.Count(-5000));
            }
            finally
            {
                CultureInfo.CurrentCulture = was;
            }
        }

        [Fact]
        public void AMultiplierReadsAsTimesItsAmount()
        {
            Assert.Equal("×1.5", Numbers.Times(1.5));
            Assert.Equal("×2", Numbers.Times(2));
            Assert.Equal("×1,234.5", Numbers.Times(1234.5));
            Assert.Equal("×1.25", Numbers.Times(1.254));
            Assert.Equal("×1.3", Numbers.Times(1.254, 1));
        }

        [Fact]
        public void AMultiplierReadoutKeepsItsDecimals() => Assert.Equal("×1.50", Numbers.TimesFixed(1.5, 2));
    }
}
