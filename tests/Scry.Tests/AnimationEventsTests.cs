using Xunit;

namespace Scry.Tests
{
    public class AnimationEventsTests
    {
        // A preview copy's animations sound only when every event its clips send is one Scry
        // answers, as the game's own scripts answer them; an event nothing hears would make Unity
        // log an error each time it fired.

        [Theory]
        [InlineData("FootStep")]
        [InlineData("OnAttackTrigger")]
        [InlineData("Effect")]
        [InlineData("Attach")]
        [InlineData("Die")]
        public void TheGamesOwnEventsAreAnswered(string name) => Assert.True(AnimationEvents.Answers(name));

        [Theory]
        [InlineData("SomeModsEvent")]
        [InlineData("footstep")]
        [InlineData("")]
        [InlineData(null)]
        public void AnyOtherEventIsNot(string name) => Assert.False(AnimationEvents.Answers(name));
    }
}
