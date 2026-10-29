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
        private static bool[] Pass(PageStaging staging, Clock clock, bool repaint, params double[] costs)
        {
            staging.StartPass(repaint);
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
