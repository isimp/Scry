using Xunit;

namespace Scry.Tests
{
    public class SeekWatchTests
    {
        // A point sought in a sound while it is paused is kept and played from on going on. A
        // streamed clip (music) reads the point back at once yet can start from its beginning, so
        // after going on the point is watched until the sound has played on 0.3 s past it, and set
        // again, a tenth of a second apart at the most, while it is before it; two seconds at most.

        [Fact]
        public void APointIsKeptWithinTheClipShortOfItsEnd()
        {
            Assert.Equal(3f, SeekWatch.Clamp(3f, 10f));
            Assert.Equal(0f, SeekWatch.Clamp(-1f, 10f));
            Assert.Equal(9.95f, SeekWatch.Clamp(12f, 10f), 4);
            Assert.Equal(0f, SeekWatch.Clamp(1f, 0.02f));
        }

        [Fact]
        public void APointSoughtWhilePausedIsKeptAndOneWhilePlayingIsNot()
        {
            var seek = new SeekWatch();

            seek.Sought(4f, paused: true);
            Assert.Equal(4f, seek.WhilePaused);

            seek.Sought(5f, paused: false);
            Assert.Null(seek.WhilePaused);
        }

        [Fact]
        public void GoingOnPlaysFromThePointSoughtAndWatchesIt()
        {
            var seek = new SeekWatch();
            seek.Sought(4f, paused: true);

            Assert.Equal(4f, seek.GoOn(now: 10f));
            Assert.Null(seek.WhilePaused);
            Assert.Equal(4f, seek.Watching);
        }

        [Fact]
        public void GoingOnWithNothingSoughtWatchesNothing()
        {
            var seek = new SeekWatch();

            Assert.Null(seek.GoOn(now: 10f));
            Assert.Null(seek.Watching);
            Assert.Equal(SeekStep.None, seek.Step(0f, true, 10.5f));
        }

        [Fact]
        public void WhileNotPlayingItWaits()
        {
            var seek = Watching(4f, now: 10f);

            Assert.Equal(SeekStep.Waiting, seek.Step(0f, false, 10.5f));
            Assert.Equal(4f, seek.Watching);
        }

        [Fact]
        public void PlayedOnPastThePointItHeld()
        {
            var seek = Watching(4f, now: 10f);

            Assert.Equal(SeekStep.Waiting, seek.Step(4.29f, true, 10.3f));
            Assert.Equal(SeekStep.Held, seek.Step(4.3f, true, 10.4f));
            Assert.Null(seek.Watching);
        }

        [Fact]
        public void StartedOverFromItsBeginningThePointIsSetAgainNoSoonerThanATenthApart()
        {
            var seek = Watching(4f, now: 10f);

            Assert.Equal(SeekStep.Waiting, seek.Step(0.05f, true, 10.05f));
            Assert.Equal(SeekStep.SetAgain, seek.Step(0.12f, true, 10.12f));
            Assert.Equal(SeekStep.Waiting, seek.Step(0.15f, true, 10.2f));
            Assert.Equal(SeekStep.SetAgain, seek.Step(0.25f, true, 10.25f));
        }

        [Fact]
        public void NearThePointItWaitsRatherThanSetAgain()
        {
            var seek = Watching(4f, now: 10f);

            Assert.Equal(SeekStep.Waiting, seek.Step(3.9f, true, 10.5f));
        }

        [Fact]
        public void AfterTwoSecondsTheWatchEnds()
        {
            var seek = Watching(4f, now: 10f);

            Assert.Equal(SeekStep.Waiting, seek.Step(0f, false, 12f));
            Assert.Equal(SeekStep.Expired, seek.Step(0f, false, 12.01f));
            Assert.Null(seek.Watching);
        }

        [Fact]
        public void StoppingLetsGoOfBoth()
        {
            var seek = Watching(4f, now: 10f);
            seek.Sought(2f, paused: true);

            seek.Forget();

            Assert.Null(seek.WhilePaused);
            Assert.Null(seek.Watching);
        }

        [Fact]
        public void TheWatchCanEndWithoutTheRest()
        {
            var seek = Watching(4f, now: 10f);
            seek.Sought(2f, paused: true);

            seek.StopWatching();

            Assert.Null(seek.Watching);
            Assert.Equal(2f, seek.WhilePaused);
        }

        private static SeekWatch Watching(float point, float now)
        {
            var seek = new SeekWatch();
            seek.Sought(point, paused: true);
            seek.GoOn(now);
            return seek;
        }
    }
}
