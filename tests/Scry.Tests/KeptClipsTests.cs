using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class KeptClipsTests
    {
        // What a hidden copy of an animator was seen to play is kept from one session to the next,
        // each probe by its key with the stamp of what it was seen with (the probe's way of
        // working, the game's version, the mods loaded, the animator), so a page opens at once the
        // next time; a stamp that differs is seen again, and a probe cut short is left out.

        private static Probed Person()
        {
            var seen = new Probed();
            seen.Idle.Add("IdleTweaked");
            seen.Triggers["swing_longsword0"] = new List<string> { "Attack1" };
            seen.Triggers["swing_axe0"] = new List<string>();
            seen.Actions["jump"] = new List<string> { "JumpTweaked" };
            seen.Stances[1] = new HashSet<string> { "Run New Sword Right", "Block idle" };
            seen.Stances[0] = new HashSet<string> { "Run New" };
            seen.Holds["bow_aim"] = new HashSet<string> { "Bow Aim Idle 01" };
            seen.Visits["move"] = new HashSet<string> { "Run New", "Run New Sword Right" };
            seen.Switched["emote_wave"] = new HashSet<string> { "Emote Wave" };
            // Names with tabs, line breaks or backslashes come back as they were.
            seen.Switched["odd\tname\\"] = new HashSet<string> { "a\nclip" };
            return seen;
        }

        private static void Same(Probed a, Probed b)
        {
            Assert.Equal(a.Idle.OrderBy(x => x), b.Idle.OrderBy(x => x));
            Assert.Equal(a.Triggers.Keys.OrderBy(x => x), b.Triggers.Keys.OrderBy(x => x));
            foreach (var pair in a.Triggers) Assert.Equal(pair.Value, b.Triggers[pair.Key]);
            foreach (var pair in a.Actions) Assert.Equal(pair.Value, b.Actions[pair.Key]);
            foreach (var pair in a.Stances) Assert.Equal(pair.Value.OrderBy(x => x), b.Stances[pair.Key].OrderBy(x => x));
            foreach (var pair in a.Holds) Assert.Equal(pair.Value.OrderBy(x => x), b.Holds[pair.Key].OrderBy(x => x));
            foreach (var pair in a.Visits) Assert.Equal(pair.Value.OrderBy(x => x), b.Visits[pair.Key].OrderBy(x => x));
            foreach (var pair in a.Switched) Assert.Equal(pair.Value.OrderBy(x => x), b.Switched[pair.Key].OrderBy(x => x));
            Assert.Equal(a.Actions.Count, b.Actions.Count);
            Assert.Equal(a.Stances.Count, b.Stances.Count);
            Assert.Equal(a.Holds.Count, b.Holds.Count);
            Assert.Equal(a.Visits.Count, b.Visits.Count);
            Assert.Equal(a.Switched.Count, b.Switched.Count);
        }

        [Fact]
        public void WhatWasSeenComesBackTheNextSession()
        {
            var kept = new KeptClips();
            kept.Put("Player|statei=1", "stamp A", Person());
            var again = KeptClips.Load(kept.Save());
            Assert.True(again.TryGet("Player|statei=1", "stamp A", out var seen));
            Same(Person(), seen);
            Assert.False(seen.Busy);
        }

        [Fact]
        public void WhatWasSeenWithSomethingElseIsSeenAgain()
        {
            var kept = new KeptClips();
            kept.Put("Player|statei=1", "stamp A", Person());
            var again = KeptClips.Load(kept.Save());
            // Another version of the probe, the game or the mods: not taken.
            Assert.False(again.TryGet("Player|statei=1", "stamp B", out _));
            Assert.False(again.TryGet("Player|statei=2", "stamp A", out _));
            // Kept again with the new stamp, it replaces the old.
            again.Put("Player|statei=1", "stamp B", new Probed());
            Assert.Equal(1, again.Count);
        }

        [Fact]
        public void AProbeCutShortOrAFileOfAnotherKindGivesNothing()
        {
            var kept = new KeptClips();
            kept.Put("Troll", "s", Person());
            kept.Put("Boar", "s", Person());
            var text = kept.Save();
            // Cut before the second probe's end: the first stays, the second is left out.
            var cut = text.Substring(0, text.LastIndexOf("end", System.StringComparison.Ordinal));
            var again = KeptClips.Load(cut);
            Assert.Equal(1, again.Count);
            Assert.True(again.TryGet(again.Keys.Single(), "s", out _));
            Assert.Equal(0, KeptClips.Load("Scry places 1\n").Count);
            // A probe with a line that cannot be read is left out, the rest kept.
            var bad = text.Replace("stance\t1\t", "stance\tone\t");
            Assert.Equal(0, KeptClips.Load(bad).Count);
            var one = new KeptClips();
            one.Put("Troll", "s", new Probed());
            one.Put("Boar", "s", Person());
            Assert.Equal(new[] { "Troll" }, KeptClips.Load(one.Save().Replace("stance\t1\t", "stance\tone\t")).Keys.ToArray());
            // Another version of the file, its probes as they were: nothing.
            Assert.Equal(0, KeptClips.Load(one.Save().Replace("Scry clips 1", "Scry clips 0")).Count);
            Assert.Equal(0, KeptClips.Load(null).Count);
        }

        [Fact]
        public void WhatIsTakenIsACopy()
        {
            // A probe goes on adding to what it took (a trigger asked later): what is kept stays as put until put again.
            var kept = new KeptClips();
            kept.Put("Troll", "s", Person());
            kept.TryGet("Troll", "s", out var seen);
            seen.Triggers["new_trigger"] = new List<string> { "X" };
            kept.TryGet("Troll", "s", out var again);
            Assert.False(again.Triggers.ContainsKey("new_trigger"));
        }

        [Fact]
        public void ForgettingLeavesNothing()
        {
            var kept = new KeptClips();
            kept.Put("Troll", "s", Person());
            kept.Clear();
            Assert.Equal(0, kept.Count);
            Assert.False(kept.TryGet("Troll", "s", out _));
        }
    }
}
