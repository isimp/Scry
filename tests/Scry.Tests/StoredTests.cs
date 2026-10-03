using System.Globalization;
using Xunit;

namespace Scry.Tests
{
    public class StoredTests
    {
        // What Scry writes into its own files and keys reads back the same on any PC: plain
        // digits, no thousands, a point for a fraction.

        [Fact]
        public void ACountIsWrittenPlainAndReadsBack()
        {
            Assert.Equal("12345", Stored.Count(12345));
            Assert.Equal("-7", Stored.Count(-7));
            Assert.True(Stored.TryCount("12345", out var count));
            Assert.Equal(12345, count);
        }

        [Fact]
        public void ANumberIsWrittenWithAPointAndReadsBack()
        {
            Assert.Equal("1234.5", Stored.Number(1234.5f));
            Assert.Equal("0.85", Stored.Number(0.854f));
            Assert.Equal("3", Stored.Number(3f));
            Assert.Equal("1234.6", Stored.Number(1234.56f, 1));
            Assert.True(Stored.TryNumber("1234.5", out var number));
            Assert.Equal(1234.5f, number);
        }

        [Theory]
        [InlineData("00e9", 0xE9)]
        [InlineData("FFFF", 0xFFFF)]
        [InlineData("0041", 0x41)]
        public void HexDigitsReadAsTheirCode(string text, int code)
        {
            Assert.True(Stored.TryHex(text, out var read));
            Assert.Equal(code, read);
        }

        [Theory]
        [InlineData("")]
        [InlineData("00g9")]
        [InlineData("-1")]
        [InlineData(null)]
        public void WhatIsNoHexIsRefused(string text)
        {
            Assert.False(Stored.TryHex(text, out _));
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("1,234")]
        [InlineData("12 ")]
        [InlineData(null)]
        public void WhatIsNoCountIsRefused(string text)
        {
            Assert.False(Stored.TryCount(text, out _));
        }

        [Theory]
        [InlineData("")]
        [InlineData("x")]
        [InlineData("1,5")]
        [InlineData("NaN")]
        [InlineData("Infinity")]
        [InlineData(null)]
        public void WhatIsNoNumberIsRefused(string text)
        {
            Assert.False(Stored.TryNumber(text, out _));
        }

        [Fact]
        public void AFileWrittenOnAGermanPcReadsTheSameAnywhere()
        {
            var was = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var written = Stored.Number(0.75f);
                Assert.Equal("0.75", written);
                Assert.Equal("12345", Stored.Count(12345));
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.True(Stored.TryNumber(written, out var read));
                Assert.Equal(0.75f, read);
            }
            finally
            {
                CultureInfo.CurrentCulture = was;
            }
        }
    }
}
