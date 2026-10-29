using Xunit;

namespace Scry.Tests
{
    public class PageStagingTests
    {
        // A page opened for the first time answers the click at once and lays its parts out over
        // the next frames, a few milliseconds of each, rather than holding one frame for all of them.

        private sealed class Clock
        {
            public double Ms;
            public double Now() => Ms;
        }

        /// <summary>One drawing of the page: each part costs its milliseconds when laid; which were laid.</summary>
        private static bool[] Pass(PageStaging staging, Clock clock, bool repaint, params double[] costs) => PassAfter(staging, clock, repaint, 0, costs);

        /// <summary>The same, in a frame whose other work of Scry's already took <paramref name="spentMs"/>.</summary>
        private static bool[] PassAfter(PageStaging staging, Clock clock, bool repaint, double spentMs, params double[] costs)
        {
            staging.StartPass(repaint, spentMs);
            var laid = new bool[costs.Length];
            for (var i = 0; i < costs.Length; i++)
            {
                laid[i] = staging.Lay();
                if (laid[i]) clock.Ms += costs[i];
            }
            staging.EndPass();
            return laid;
        }

        [Fact]
        public void APageNotStagedLaysEverythingAtOnce()
        {
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);

            Assert.Equal(new[] { true, true, true }, Pass(staging, clock, true, 20, 20, 20));
            Assert.False(staging.Active);
        }

        [Fact]
        public void EachRepaintLaysAtLeastOnePartAndMoreWhileItsShareLasts()
        {
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();

            // The first part alone takes more than the share: it is laid, the rest wait.
            Assert.Equal(new[] { true, false, false, false }, Pass(staging, clock, true, 20, 2, 2, 2));
            // Laid parts are drawn again for nothing new; two cheap ones fit the share, the third waits.
            Assert.Equal(new[] { true, true, true, false }, Pass(staging, clock, true, 0, 3, 3, 3));
            Assert.True(staging.Active);
            Assert.Equal(new[] { true, true, true, true }, Pass(staging, clock, true, 0, 0, 0, 3));
            // A repaint that held nothing back ends the staging.
            Assert.False(staging.Active);
        }

        [Fact]
        public void EventsBetweenRepaintsMeetThePageAsLastDrawn()
        {
            // A click is handled against what was shown: only a repaint lays more.
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();
            Pass(staging, clock, true, 7, 1, 1);

            Assert.Equal(new[] { true, false, false }, Pass(staging, clock, false, 1, 1, 1));
            Assert.Equal(new[] { true, false, false }, Pass(staging, clock, false, 1, 1, 1));
            Assert.True(staging.Active);
        }

        [Fact]
        public void ARepaintWhoseLaidPartsAlreadyTakeItsShareStillLaysOneMore()
        {
            // So a page whose laid parts are slow to draw still gets to its end.
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();
            Pass(staging, clock, true, 1, 20, 1);

            // The two laid parts take 20 ms to draw again, past the share: the third is laid all the same.
            Assert.Equal(new[] { true, true, true }, Pass(staging, clock, true, 10, 10, 1));
            Assert.False(staging.Active);
        }

        [Fact]
        public void AFrameWhoseOtherWorkAlreadyTookItsShareLaysNothingNewAndTheNextLaysOneWhatever()
        {
            // The frame after a click makes the stage's copy and films it: the page waits a frame
            // rather than adding to it, and goes on in the next, however busy that is.
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();
            Pass(staging, clock, true, 20, 1, 1);

            Assert.Equal(new[] { true, false, false }, PassAfter(staging, clock, true, 30.0, 1, 1, 1));
            Assert.True(staging.Active);
            Assert.Equal(new[] { true, true, false }, PassAfter(staging, clock, true, 30.0, 1, 7, 1));
            // A frame with little other work lays as many as its share holds again.
            Assert.Equal(new[] { true, true, true }, PassAfter(staging, clock, true, 2.0, 0, 0, 1));
            Assert.False(staging.Active);
        }

        [Fact]
        public void TheFramesOtherWorkCountsAgainstItsShare()
        {
            // Four milliseconds of the frame went to Scry's other work: two parts fit in what is left.
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();

            Assert.Equal(new[] { true, true, false, false, false }, PassAfter(staging, clock, true, 4.0, 1, 1, 1, 1, 1));
        }

        [Fact]
        public void TheClicksOwnFrameLaysNothingWhenOtherWorkFilledIt()
        {
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();

            Assert.Equal(new[] { false, false }, PassAfter(staging, clock, true, 30.0, 20, 1));
            Assert.Equal(new[] { true, false }, PassAfter(staging, clock, true, 30.0, 20, 1));
        }

        [Fact]
        public void APartWorkedOutOverSeveralFramesWaitsUntilItIsDoneAndTheRestWithIt()
        {
            // The facts are worked out a few steps a frame: their part is laid only once they are done.
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();
            staging.StartPass(true);
            Assert.True(staging.Lay());
            Assert.True(staging.NotYet());
            Assert.True(staging.HeldFirst);
            Assert.False(staging.Lay());
            staging.EndPass();
            Assert.True(staging.Active);

            // Next frame it is asked again, done this time, and the part after it follows.
            staging.StartPass(true);
            Assert.True(staging.Lay());
            Assert.True(staging.Lay());
            staging.EndPass();
            Assert.False(staging.Active);
        }

        [Fact]
        public void APartLaidBeforeCannotBeTakenBack()
        {
            // Facts that went stale after their part was laid are worked out whole instead.
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();
            Pass(staging, clock, true, 1, 20, 1);
            staging.StartPass(true);
            Assert.True(staging.Lay());

            Assert.False(staging.NotYet());
        }

        [Fact]
        public void WhetherThereIsRoomLeftCountsTheFramesOtherWorkAndWhatWasLaid()
        {
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();
            staging.StartPass(true, 4.0);
            Assert.True(staging.HasRoom());
            clock.Ms += 2.5;
            Assert.False(staging.HasRoom());
        }

        [Fact]
        public void OnlyARepaintEndsTheLayingOut()
        {
            // An event drawing fewer parts, a section folded meanwhile, holds none back, but the
            // page shown still waits on its parts.
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();
            Pass(staging, clock, true, 10, 1, 1);

            Pass(staging, clock, false, 1);

            Assert.True(staging.Active);
        }

        [Fact]
        public void APartAfterOneHeldBackWaitsToo()
        {
            // Parts are laid in the page's order, so nothing below a waiting part moves up into its place.
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();

            var laid = Pass(staging, clock, true, 7, 0, 0);

            Assert.Equal(new[] { true, false, false }, laid);
        }

        [Fact]
        public void PartsThatAppearOnceAnEarlierOneIsLaidJoinTheLine()
        {
            // The facts, once worked out, show the topics they hold: more parts than the page had.
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();
            Pass(staging, clock, true, 20, 0);

            Assert.Equal(new[] { true, true, true, false, false }, Pass(staging, clock, true, 0, 2, 4, 3, 3));
            Assert.Equal(0.6, staging.Share, 3);
        }

        [Fact]
        public void HowFarItHasGotIsToldAsAShareOfThePartsLastSeen()
        {
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();
            Assert.Equal(0, staging.Share, 3);
            Pass(staging, clock, true, 10, 1, 1, 1);
            Assert.Equal(0.25, staging.Share, 3);
            staging.Stop();
            Assert.Equal(1, staging.Share, 3);
            Assert.False(staging.Active);
        }

        [Fact]
        public void WhichPartIsLaidNextIsKnownToSayWhatTheWaitIsFor()
        {
            var clock = new Clock();
            var staging = new PageStaging(clock.Now, budgetMs: 6);
            staging.Begin();
            staging.StartPass(true);
            staging.Lay();
            clock.Ms += 10;

            Assert.False(staging.HeldAny);
            Assert.False(staging.Lay());
            Assert.True(staging.HeldFirst);
            Assert.True(staging.HeldAny);
            Assert.False(staging.Lay());
            Assert.False(staging.HeldFirst);
            // A new drawing starts with nothing held.
            staging.StartPass(true);
            Assert.False(staging.HeldAny);
        }
    }
}
