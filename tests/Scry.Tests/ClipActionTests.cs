using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class ClipActionTests
    {
        private static Dictionary<string, IReadOnlyList<string>> Seen(params (string Action, string[] Clips)[] seen)
        {
            var map = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var (action, clips) in seen) map[action] = clips;
            return map;
        }

        [Fact]
        public void TheClipAnActionLeadsToPlaysWhatTheGamePlaysWithIt()
        {
            // A troll's jump trigger leads to its jump clip; waking a draugr to its wake-up clip.
            var seen = Seen(("jump", new[] { "Mutant Jumping" }), ("wake", new[] { "Wakeup", "Idle" }));

            var played = ClipActions.Match(new[] { ("jump", (object)"jump effects"), ("wake", "wakeup effects") }, seen, new string[0]);

            Assert.Equal("jump effects", played["Mutant Jumping"]);
            Assert.Equal("wakeup effects", played["Wakeup"]);
            Assert.Equal(2, played.Count);
        }

        [Fact]
        public void AnActionSeenToLeadNowhereIsNotGuessedByName()
        {
            var seen = Seen(("alert", new string[0]));

            var played = ClipActions.Match(new[] { ("alert", (object)"alerted effects"), ("sleep", "sleep effects") }, seen, new string[0]);

            Assert.Empty(played);
        }

        [Fact]
        public void AClipAnAttackPlaysKeepsToTheAttack()
        {
            var seen = Seen(("jump", new[] { "Attack Jump" }));

            var played = ClipActions.Match(new[] { ("jump", (object)"jump effects") }, seen, new[] { "Attack Jump" });

            Assert.Empty(played);
        }

        [Fact]
        public void OfTwoActionsLeadingToOneClipTheFirstGivenIsIt()
        {
            var seen = Seen(("sleep", new[] { "Sleeping" }), ("alert", new[] { "Sleeping" }));

            var played = ClipActions.Match(new[] { ("sleep", (object)"sleep effects"), ("alert", "alerted effects") }, seen, new string[0]);

            Assert.Equal("sleep effects", played["Sleeping"]);
        }
    }
}
