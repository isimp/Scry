using Xunit;

namespace Scry.Tests
{
    public class AfterQuietTests
    {
        // A camp's rooms come in one by one, each changing what the stage's ground holds; the
        // grass is scattered again once they have stopped coming for a moment, once, rather than
        // for each room.

        [Fact]
        public void WorkIsDueOnceChangesHaveStoppedForTheWait()
        {
            var wait = new AfterQuiet(0.3f);
            wait.Changed(10f);

            Assert.False(wait.Due(10.1f));
            Assert.True(wait.Due(10.3f));
        }

        [Fact]
        public void AnotherChangeMeanwhileWaitsAgainFromIt()
        {
            var wait = new AfterQuiet(0.3f);
            wait.Changed(10f);
            wait.Changed(10.2f);

            Assert.False(wait.Due(10.4f));
            Assert.True(wait.Due(10.5f));
        }

        [Fact]
        public void ItIsDueOnlyOnceForEachSettling()
        {
            var wait = new AfterQuiet(0.3f);
            wait.Changed(10f);

            Assert.True(wait.Due(11f));
            Assert.False(wait.Due(12f));
            wait.Changed(13f);
            Assert.True(wait.Due(13.5f));
        }

        [Fact]
        public void WithNoChangeNothingIsDue()
        {
            var wait = new AfterQuiet(0.3f);
            Assert.False(wait.Due(100f));
        }

        [Fact]
        public void DoneAtOnceItIsNoLongerDue()
        {
            // The ground laid afresh for another entry scatters its grass at once.
            var wait = new AfterQuiet(0.3f);
            wait.Changed(10f);
            wait.Done();

            Assert.False(wait.Due(11f));
        }
    }
}
