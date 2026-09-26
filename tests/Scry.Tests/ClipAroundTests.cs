using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class ClipAroundTests
    {
        private static Dictionary<string, IReadOnlyList<string>> Seen(params (string Action, string[] Clips)[] seen)
        {
            var map = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var (action, clips) in seen) map[action] = clips;
            return map;
        }

        [Fact]
        public void WakingAndASpawnRoarAreHeardWithWhatTheCreatureCallsOutWhenAlerted()
        {
            // A troll wakes silently and roars once it spots you; spawned, it roars as it rises.
            var seen = Seen(("wake", new[] { "Wakeup", "Idle_Breathing" }), ("spawn", new[] { "Spawn Roar" }));

            var around = ClipAround.Match(new[] { ("wake", (object)"alerted"), ("spawn", "alerted") }, seen,
                new[] { "Idle_Breathing" }, null, new string[0]);

            Assert.Equal("alerted", around["Wakeup"]);
            Assert.Equal("alerted", around["Spawn Roar"]);
            Assert.Equal(2, around.Count);
        }

        [Fact]
        public void TheClipsItIdlesInAreHeardWithItsIdleSound()
        {
            var around = ClipAround.Match(new (string, object)[0], Seen(),
                new[] { "Idle_Breathing", "Idle_Smelling_Something" }, "idle sound", new string[0]);

            Assert.Equal("idle sound", around["Idle_Breathing"]);
            Assert.Equal("idle sound", around["Idle_Smelling_Something"]);
            Assert.Equal(2, around.Count);
        }

        [Fact]
        public void WithNoIdleSoundItsIdleClipsHearNothingAround()
        {
            var around = ClipAround.Match(new (string, object)[0], Seen(), new[] { "Idle" }, null, new string[0]);

            Assert.Empty(around);
        }

        [Fact]
        public void AnAttackClipIsHeardWithNothingAround()
        {
            var seen = Seen(("wake", new[] { "Attack Jump" }));

            var around = ClipAround.Match(new[] { ("wake", (object)"alerted") }, seen,
                new[] { "Attack Claws" }, "idle sound", new[] { "Attack Jump", "Attack Claws" });

            Assert.Empty(around);
        }

        [Fact]
        public void WhatAnActionLeadsToOutranksIdling()
        {
            var seen = Seen(("wake", new[] { "Idle" }));

            var around = ClipAround.Match(new[] { ("wake", (object)"alerted") }, seen, new[] { "Idle" }, "idle sound", new string[0]);

            Assert.Equal("alerted", around["Idle"]);
        }

        [Fact]
        public void OfTwoActionsLeadingToOneClipTheFirstGivenIsIt()
        {
            var seen = Seen(("wake", new[] { "Arise" }), ("spawn", new[] { "Arise" }));

            var around = ClipAround.Match(new[] { ("wake", (object)"woken"), ("spawn", "spawned") }, seen, new string[0], null, new string[0]);

            Assert.Equal("woken", around["Arise"]);
        }

        [Fact]
        public void AnActionSeenToLeadNowhereIsHeardNowhere()
        {
            var seen = Seen(("spawn", new string[0]));

            var around = ClipAround.Match(new[] { ("spawn", (object)"alerted"), ("wake", "alerted") }, seen, new string[0], null, new string[0]);

            Assert.Empty(around);
        }
    }
}
