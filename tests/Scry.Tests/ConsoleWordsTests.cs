using Xunit;

namespace Scry.Tests
{
    public class ConsoleWordsTests
    {
        // What the console says back to /scry: each reply led by Scry's name, so it is told apart
        // from the game's own lines.

        [Fact]
        public void EveryReplyIsLedByScrysName()
        {
            Assert.Equal("Scry: previews cleared.", ConsoleWords.Reply(ConsoleWords.Cleared));
            Assert.Equal("Scry: the self-test is off; SelfTest under Diagnostics in Scry's settings turns it on.", ConsoleWords.Reply(ConsoleWords.SelfTestOff));
        }

        [Fact]
        public void TheMonitorSaysWhetherItIsOn()
        {
            Assert.Equal("Scry: the resource monitor is on.", ConsoleWords.Monitor(on: true));
            Assert.Equal("Scry: the resource monitor is off.", ConsoleWords.Monitor(on: false));
        }
    }
}
