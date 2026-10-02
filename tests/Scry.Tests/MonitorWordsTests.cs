using Xunit;

namespace Scry.Tests
{
    public class MonitorWordsTests
    {
        // The monitor's figures: milliseconds to two places under ten, one above; memory in
        // bytes, kilobytes, megabytes or gigabytes, whichever reads best.

        [Fact]
        public void MillisecondsAreToTwoPlacesUnderTenAndOneAbove()
        {
            Assert.Equal("0.42", MonitorWords.Ms(0.4213));
            Assert.Equal("0.00", MonitorWords.Ms(0));
            Assert.Equal("9.99", MonitorWords.Ms(9.99));
            Assert.Equal("12.3", MonitorWords.Ms(12.34));
        }

        [Fact]
        public void MemoryReadsInTheUnitThatFits()
        {
            Assert.Equal("512 B", MonitorWords.Bytes(512));
            Assert.Equal("14 KB", MonitorWords.Bytes(14 * 1024));
            Assert.Equal("6.2 MB", MonitorWords.Bytes((long)(6.2 * 1024 * 1024)));
            Assert.Equal("3.4 GB", MonitorWords.Bytes((long)(3.4 * 1024 * 1024 * 1024)));
            Assert.Equal("0 B", MonitorWords.Bytes(-5));
        }
    }
}
