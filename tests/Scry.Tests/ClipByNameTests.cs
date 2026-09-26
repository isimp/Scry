using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class ClipByNameTests
    {
        private static readonly (string Action, string[] Words, object Key)[] Water =
        {
            ("water", new[] { "swim", "tread" }, "water effect"),
        };

        private static Dictionary<string, IReadOnlyList<string>> Seen(params (string Action, string[] Clips)[] seen)
        {
            var map = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var (action, clips) in seen) map[action] = clips;
            return map;
        }

        [Fact]
        public void ClipsNamedForSwimmingAreFoundByNameWhereTheAnimatorShowedNone()
        {
            var found = ClipByName.Match(Water, Seen(("water", new string[0])),
                new[] { "Idle", "Swimming", "Treading Water", "Walk" }, new string[0]);

            Assert.Equal("water effect", found["Swimming"]);
            Assert.Equal("water effect", found["Treading Water"]);
            Assert.Equal(2, found.Count);
        }

        [Fact]
        public void NothingIsFoundByNameWhereTheAnimatorShowedAClip()
        {
            var found = ClipByName.Match(Water, Seen(("water", new[] { "Swim Idle" })),
                new[] { "Swim Idle", "Swimming" }, new string[0]);

            Assert.Empty(found);
        }

        [Fact]
        public void NamesAreMatchedWhateverTheirCase()
        {
            var found = ClipByName.Match(new[] { ("jump", new[] { "jump" }, (object)"jump effects") }, Seen(),
                new[] { "Mutant Jumping", "Idle" }, new string[0]);

            Assert.Equal("jump effects", Assert.Single(found).Value);
            Assert.True(found.ContainsKey("Mutant Jumping"));
        }

        [Fact]
        public void AnAttackOrAClipPairedAlreadyIsNotFoundByName()
        {
            var found = ClipByName.Match(new[] { ("jump", new[] { "jump" }, (object)"jump effects") }, Seen(),
                new[] { "Attack Jump", "Jump" }, new[] { "Attack Jump", "Jump" });

            Assert.Empty(found);
        }

        [Fact]
        public void AClipNamedForTwoIsTheFirstGivens()
        {
            var found = ClipByName.Match(new[]
            {
                ("water", new[] { "swim" }, (object)"water effect"),
                ("jump", new[] { "jump" }, "jump effects"),
            }, Seen(), new[] { "Swim Jump" }, new string[0]);

            Assert.Equal("water effect", found["Swim Jump"]);
        }
    }
}
