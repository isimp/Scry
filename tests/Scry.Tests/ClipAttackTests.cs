using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class ClipAttackTests
    {
        private static readonly Dictionary<string, string> NoneSeen = new Dictionary<string, string>();

        [Fact]
        public void AnAttackPlaysTheClipTheCreaturesAnimatorWasSeenToPlayForIt()
        {
            // A troll's side log swing has the trigger swing_logh and plays a clip named otherwise.
            var seen = new Dictionary<string, string> { ["swing_logh"] = "Sword-Attack-R2", ["swing_logv"] = "Mace-Attack-R2" };

            var attacks = ClipAttacks.Match(new[] { ("swing_logv", (object)"log v"), ("swing_logh", "log h") }, seen,
                new[] { "Idle_Breathing", "Mace-Attack-R2", "Sword-Attack-R2", "Walk" });

            Assert.Equal("log h", attacks["Sword-Attack-R2"]);
            Assert.Equal("log v", attacks["Mace-Attack-R2"]);
            Assert.Equal(2, attacks.Count);
        }

        [Fact]
        public void AClipNamedAsTheTriggerIsItsAttackWhenTheAnimatorWasNotSeenToPlayOne()
        {
            var attacks = ClipAttacks.Match(new[] { ("attack_claws", (object)"claw") }, NoneSeen,
                new[] { "Attack Claws", "Attack Claws Loop", "Attack Jump", "Idle" });

            Assert.Equal("claw", attacks["Attack Claws"]);
            Assert.Single(attacks);
        }

        [Fact]
        public void AClipHoldingTheWholeTriggerIsItsAttackWhenItIsTheOnlyOne()
        {
            var attacks = ClipAttacks.Match(new[] { ("stomp_l", (object)"stomp"), ("throw", "throw") }, NoneSeen,
                new[] { "Attack Backhand L", "Attack Stomp L", "Attack Stomp R", "Attack TreeThrow", "Attack TreeThrow", "Idle" });

            Assert.Equal("stomp", attacks["Attack Stomp L"]);
            Assert.Equal("throw", attacks["Attack TreeThrow"]);
            Assert.Equal(2, attacks.Count);
        }

        [Fact]
        public void ATriggerSeveralClipsHoldIsTheAttackOfNone()
        {
            var attacks = ClipAttacks.Match(new[] { ("attack", (object)"bite") }, NoneSeen,
                new[] { "Attack Claws", "Attack Jump" });

            Assert.Empty(attacks);
        }

        [Fact]
        public void WhatTheAnimatorWasSeenToPlayOutranksTheNames()
        {
            var seen = new Dictionary<string, string> { ["attack_claws"] = "Attack Jump" };

            var attacks = ClipAttacks.Match(new[] { ("attack_claws", (object)"claw") }, seen,
                new[] { "Attack Claws", "Attack Jump" });

            Assert.Equal("claw", attacks["Attack Jump"]);
            Assert.Single(attacks);
        }

        [Fact]
        public void OfTwoAttacksPlayingOneClipTheFirstGivenIsIt()
        {
            // Given what the creature has now first: a troll's punch in the look shown.
            var seen = new Dictionary<string, string> { ["punch"] = "Zombie Attack" };

            var attacks = ClipAttacks.Match(new[] { ("punch", (object)"punch now"), ("punch", "punch of another set") }, seen,
                new[] { "Zombie Attack" });

            Assert.Equal("punch now", attacks["Zombie Attack"]);
        }

        [Fact]
        public void AnAttackWithNoTriggerOrNoClipPlaysNone()
        {
            var attacks = ClipAttacks.Match(new[] { ("", (object)"aoe"), (null, "other"), ("fireball", "fireball") }, NoneSeen,
                new[] { "Idle", "Walk" });
            var unnamed = ClipAttacks.Match(new[] { ("_", (object)"unnamed") }, NoneSeen, new[] { "Idle" });

            Assert.Empty(attacks);
            Assert.Empty(unnamed);
        }

        [Fact]
        public void ACaseSpaceUnderscoreAndHyphenMakeNoDifferenceToAName()
        {
            Assert.True(ClipAttacks.SameName("attack_slash1", "Attack Slash 1"));
            Assert.True(ClipAttacks.SameName("swing-log", "Swing Log"));
            Assert.False(ClipAttacks.SameName("attack_slash1", "Attack Slash 2"));
            Assert.False(ClipAttacks.SameName("", "Idle"));
            Assert.False(ClipAttacks.SameName("_", "-"));
        }
    }
}
