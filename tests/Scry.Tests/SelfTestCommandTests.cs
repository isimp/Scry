using System;
using Xunit;

namespace Scry.Tests
{
    public class SelfTestCommandTests
    {
        // With the self-test installed, /scry selftest starts a quick run, through a few of the
        // places the long parts go through; deep goes through every one, full also reads every
        // location again, failed runs again the parts that failed last, list writes every part's
        // name, stop stops it, and any other words run the parts whose names hold them all.
        // Anything not starting with selftest is left to Scry, as a search or its own words. The
        // marker file starts a run by the same words.

        private static SelfTestRequest Of(string typed) => SelfTestCommand.Of(typed);

        [Theory]
        [InlineData("selftest")]
        [InlineData("SelfTest")]
        [InlineData("  selftest ")]
        public void SelftestStartsAQuickRun(string typed)
        {
            var asked = Of(typed);
            Assert.Equal(SelfTestAsk.Start, asked.Ask);
            Assert.Equal(RunScope.Quick, asked.Scope);
            Assert.True(asked.Sampled);
        }

        [Theory]
        [InlineData("selftest stop")]
        [InlineData("SELFTEST STOP")]
        [InlineData("selftest   stop")]
        public void SelftestStopStopsIt(string typed) => Assert.Equal(SelfTestAsk.Stop, Of(typed).Ask);

        [Theory]
        [InlineData("selftest full", "Full")]
        [InlineData("SelfTest FULL", "Full")]
        [InlineData("selftest deep", "Deep")]
        [InlineData("selftest failed", "Failed")]
        public void AWordOfItsOwnPicksHowMuchRuns(string typed, string scope)
        {
            var asked = Of(typed);
            Assert.Equal(SelfTestAsk.Start, asked.Ask);
            Assert.Equal(scope, asked.Scope.ToString());
            Assert.False(asked.Sampled);
        }

        [Fact]
        public void ListWritesThePartsNames() => Assert.Equal(SelfTestAsk.List, Of("selftest list").Ask);

        [Theory]
        [InlineData("selftest compendium box", new[] { "compendium", "box" })]
        [InlineData("selftest  Plan  BOX ", new[] { "plan", "box" })]
        [InlineData("selftest stop now", new[] { "stop", "now" })]
        public void OtherWordsRunThePartsNamedSo(string typed, string[] words)
        {
            var asked = Of(typed);
            Assert.Equal(SelfTestAsk.Start, asked.Ask);
            Assert.Equal(RunScope.Named, asked.Scope);
            Assert.Equal(words, asked.Words);
            Assert.False(asked.Sampled);
        }

        [Theory]
        [InlineData("", "Quick")]
        [InlineData(null, "Quick")]
        [InlineData("full", "Full")]
        [InlineData(" FULL\r\n", "Full")]
        [InlineData("deep", "Deep")]
        [InlineData("plan box", "Named")]
        [InlineData("stop", "Quick")]
        [InlineData("list", "Quick")]
        public void TheMarkerFileStartsARunByTheSameWords(string text, string scope)
        {
            var asked = SelfTestCommand.OfMarker(text);
            Assert.Equal(SelfTestAsk.Start, asked.Ask);
            Assert.Equal(scope, asked.Scope.ToString());
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("selftesting")]
        [InlineData("self test")]
        [InlineData("troll")]
        [InlineData("stop")]
        public void AnythingElseIsLeftToScry(string typed) => Assert.Equal(SelfTestAsk.None, Of(typed).Ask);

        [Fact]
        public void AFewOfManyAreSpreadOverThemAndAFewOfAFewAreTheFirst()
        {
            var many = new[] { "a", "b", "c", "d", "e", "f", "g", "h", "i", "j" };
            Assert.Equal(new[] { "a", "c", "e", "g", "i" }, SelfTestSample.Spread(many, 5));
            Assert.Equal(many, SelfTestSample.Spread(many, 20));
            Assert.Equal(new[] { "a", "b" }, SelfTestSample.First(many, 2));
            Assert.Equal(new[] { "a" }, SelfTestSample.First(new[] { "a" }, 2));
        }
    }
}
