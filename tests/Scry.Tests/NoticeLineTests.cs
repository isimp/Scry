using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class NoticeLineTests
    {
        // In the compact view the notices under the header share one strip: of those up, the most
        // pressing shows first (what of Scry is off, then peeking, then players whose games lack
        // Scry, then what was learned); the strip's step goes to the next, round to the first. The
        // one shown stays while it is up; once it goes, the most pressing takes the strip, and one
        // newly up that is more pressing than the one shown takes it at once, so a warning is never
        // left behind a step.

        private static List<Notice> Up(params Notice[] up) => new List<Notice>(up);

        [Fact]
        public void TheMostPressingNoticeUpShowsFirstWithItsPlaceAmongThem()
        {
            var line = new NoticeLine();
            var up = Up(Notice.PlayingWith, Notice.Learned, Notice.Off);
            Assert.Equal(Notice.Off, line.Shown(up));
            Assert.Equal((1, 3), line.Place(up));
        }

        [Fact]
        public void TheStepGoesToTheNextNoticeUpAndRoundToTheFirst()
        {
            var line = new NoticeLine();
            var up = Up(Notice.Off, Notice.PlayingWith, Notice.Learned);
            line.Shown(up);
            line.Step(up);
            Assert.Equal(Notice.PlayingWith, line.Shown(up));
            Assert.Equal((2, 3), line.Place(up));
            line.Step(up);
            Assert.Equal(Notice.Learned, line.Shown(up));
            Assert.Equal((3, 3), line.Place(up));
            line.Step(up);
            Assert.Equal(Notice.Off, line.Shown(up));
            Assert.Equal((1, 3), line.Place(up));
        }

        [Fact]
        public void TheNoticeShownStaysWhileItIsUpAndTheMostPressingTakesItsPlaceOnceItGoes()
        {
            var line = new NoticeLine();
            var up = Up(Notice.Off, Notice.PlayingWith, Notice.Learned);
            line.Shown(up);
            line.Step(up);
            line.Step(up);
            // What was off put away: the news shown stays, second of two now.
            up = Up(Notice.PlayingWith, Notice.Learned);
            Assert.Equal(Notice.Learned, line.Shown(up));
            Assert.Equal((2, 2), line.Place(up));
            // The news put away: the players' notice takes the strip.
            up = Up(Notice.PlayingWith);
            Assert.Equal(Notice.PlayingWith, line.Shown(up));
            Assert.Equal((1, 1), line.Place(up));
        }

        [Fact]
        public void ANoticeNewlyUpThatIsMorePressingThanTheOneShownTakesTheStrip()
        {
            var line = new NoticeLine();
            var up = Up(Notice.PlayingWith, Notice.Learned);
            line.Shown(up);
            line.Step(up);
            Assert.Equal(Notice.Learned, line.Shown(up));
            // Peeking begins: a warning comes before the news shown.
            up = Up(Notice.Peeking, Notice.PlayingWith, Notice.Learned);
            Assert.Equal(Notice.Peeking, line.Shown(up));
            // Up a while, a step past it goes on as for any other.
            line.Step(up);
            Assert.Equal(Notice.PlayingWith, line.Shown(up));
            // One newly up that is less pressing leaves the one shown.
            up = Up(Notice.Off);
            Assert.Equal(Notice.Off, line.Shown(up));
            up = Up(Notice.Off, Notice.Learned);
            Assert.Equal(Notice.Off, line.Shown(up));
        }

        [Fact]
        public void OfSeveralNewlyUpAtOnceTheMostPressingTakesTheStripAndOneBackAgainIsNewlyUp()
        {
            var line = new NoticeLine();
            var up = Up(Notice.Learned);
            Assert.Equal(Notice.Learned, line.Shown(up));
            up = Up(Notice.Off, Notice.PlayingWith, Notice.Learned);
            Assert.Equal(Notice.Off, line.Shown(up));
            line.Step(up);
            Assert.Equal(Notice.PlayingWith, line.Shown(up));
            // Gone and back, what is off is newly up again and comes before the players' notice.
            up = Up(Notice.PlayingWith, Notice.Learned);
            Assert.Equal(Notice.PlayingWith, line.Shown(up));
            up = Up(Notice.Off, Notice.PlayingWith, Notice.Learned);
            Assert.Equal(Notice.Off, line.Shown(up));
        }

        [Fact]
        public void WithNoNoticeUpTheStripShowsNoneAndAStepDoesNothing()
        {
            var line = new NoticeLine();
            var none = Up();
            Assert.Null(line.Shown(none));
            Assert.Equal((0, 0), line.Place(none));
            line.Step(none);
            Assert.Null(line.Shown(none));
            // Up again after none, the most pressing shows; all put away, none is shown, nor a place.
            var up = Up(Notice.Learned, Notice.Peeking);
            Assert.Equal(Notice.Peeking, line.Shown(up));
            Assert.Null(line.Shown(none));
            Assert.Equal((0, 0), line.Place(none));
        }
    }
}
