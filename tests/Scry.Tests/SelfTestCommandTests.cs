using Xunit;

namespace Scry.Tests
{
    public class SelfTestCommandTests
    {
        // With the self-test installed, /scry selftest starts it and /scry selftest stop stops
        // it; anything else typed after /scry is left to Scry, as a search or its own words.

        [Theory]
        [InlineData("selftest")]
        [InlineData("SelfTest")]
        [InlineData("  selftest ")]
        public void SelftestStartsTheSelfTest(string typed) => Assert.Equal(SelfTestAsk.Start, SelfTestCommand.Of(typed));

        [Theory]
        [InlineData("selftest stop")]
        [InlineData("SELFTEST STOP")]
        [InlineData("selftest   stop")]
        public void SelftestStopStopsIt(string typed) => Assert.Equal(SelfTestAsk.Stop, SelfTestCommand.Of(typed));

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("selftesting")]
        [InlineData("self test")]
        [InlineData("selftest now")]
        [InlineData("selftest stop now")]
        [InlineData("troll")]
        [InlineData("stop")]
        public void AnythingElseIsLeftToScry(string typed) => Assert.Equal(SelfTestAsk.None, SelfTestCommand.Of(typed));
    }
}
