using Xunit;

namespace Scry.Tests
{
    public class ViewStepsTests
    {
        // Going into a room of a dungeon's example, from room to room and back out are steps of a
        // history, as in a browser: Back returns to the view a step was taken from, as it was left,
        // and Forward takes the step again.

        [Fact]
        public void BackReturnsToTheViewAStepWasTakenFromAndForwardTakesItAgain()
        {
            var steps = new ViewSteps<string>();
            steps.Took("level -16 m");
            Assert.True(steps.Back("in the shrine", out var back));
            Assert.Equal("level -16 m", back);
            Assert.True(steps.Forward("level -16 m", out var forward));
            Assert.Equal("in the shrine", forward);
        }

        [Fact]
        public void StepsGoBackOneAtATimeTheLastFirst()
        {
            var steps = new ViewSteps<string>();
            steps.Took("level 0 m");
            steps.Took("in the entrance");
            Assert.True(steps.Back("in the corridor", out var first));
            Assert.Equal("in the entrance", first);
            Assert.True(steps.Back("in the entrance", out var second));
            Assert.Equal("level 0 m", second);
            Assert.False(steps.CanGoBack);
        }

        [Fact]
        public void WithNothingToGoBackOrForwardToNothingChanges()
        {
            var steps = new ViewSteps<string>();
            Assert.False(steps.CanGoBack);
            Assert.False(steps.CanGoForward);
            Assert.False(steps.Back("level 0 m", out _));
            Assert.False(steps.Forward("level 0 m", out _));
        }

        [Fact]
        public void ANewStepLetsGoOfTheStepsForward()
        {
            var steps = new ViewSteps<string>();
            steps.Took("level 0 m");
            steps.Back("in the entrance", out _);
            Assert.True(steps.CanGoForward);
            steps.Took("level 0 m");
            Assert.False(steps.CanGoForward);
            Assert.True(steps.CanGoBack);
        }

        [Fact]
        public void LettingGoOfTheStepsLeavesNoneEitherWay()
        {
            // Another entry or another example shown: its rooms are gone.
            var steps = new ViewSteps<string>();
            steps.Took("level 0 m");
            steps.Took("in the entrance");
            steps.Back("in the corridor", out _);
            steps.Clear();
            Assert.False(steps.CanGoBack);
            Assert.False(steps.CanGoForward);
        }

        [Fact]
        public void OnlyTheLastStepsAreKept()
        {
            var steps = new ViewSteps<int>();
            for (var i = 0; i < ViewSteps<int>.Limit + 10; i++) steps.Took(i);
            var back = 0;
            var last = -1;
            while (steps.Back(-1, out var to))
            {
                back++;
                last = to;
            }
            Assert.Equal(ViewSteps<int>.Limit, back);
            Assert.Equal(10, last);
        }
    }
}
