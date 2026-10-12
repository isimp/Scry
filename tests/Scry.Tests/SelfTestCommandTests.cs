using Xunit;

namespace Scry.Tests
{
    public class SelfTestCommandTests
    {
        // With the self-test installed, /scry selftest starts a quick run, /scry selftest full
        // one of every part, and /scry selftest stop stops it; anything else typed after /scry is
        // left to Scry, as a search or its own words. The marker file starts a quick run, or a
        // full one where it says so.

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
        [InlineData("selftest full")]
        [InlineData("SelfTest FULL")]
        [InlineData(" selftest   full ")]
        public void SelftestFullStartsEveryPart(string typed) => Assert.Equal(SelfTestAsk.StartFull, SelfTestCommand.Of(typed));

        [Theory]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("anything", false)]
        [InlineData("full", true)]
        [InlineData(" FULL\r\n", true)]
        public void TheMarkerFileStartsAQuickRunOrAFullOneWhereItSaysSo(string text, bool full) =>
            Assert.Equal(full ? SelfTestAsk.StartFull : SelfTestAsk.Start, SelfTestCommand.OfMarker(text));

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("selftesting")]
        [InlineData("self test")]
        [InlineData("selftest now")]
        [InlineData("selftest stop now")]
        [InlineData("selftest full now")]
        [InlineData("troll")]
        [InlineData("stop")]
        public void AnythingElseIsLeftToScry(string typed) => Assert.Equal(SelfTestAsk.None, SelfTestCommand.Of(typed));
    }
}
