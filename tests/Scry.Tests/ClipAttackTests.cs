using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class ClipAttackTests
    {
        private static readonly Dictionary<string, IReadOnlyList<string>> NoneSeen = new Dictionary<string, IReadOnlyList<string>>();
        private static readonly string[] NoneStrike = new string[0];

        private static Dictionary<string, IReadOnlyList<string>> Seen(params (string Trigger, string[] Clips)[] seen)
        {
            var map = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var (trigger, clips) in seen) map[trigger] = clips;
            return map;
        }

        [Fact]
        public void AnAttackPlaysTheClipTheCreaturesAnimatorWasSeenToPlayForIt()
        {
            // A troll's side log swing has the trigger swing_logh and plays a clip named otherwise.
            var seen = Seen(("swing_logh", new[] { "Sword-Attack-R2" }), ("swing_logv", new[] { "Mace-Attack-R2" }));

            var attacks = ClipAttacks.Match(new[] { ("swing_logv", (object)"log v"), ("swing_logh", "log h") }, seen,
                new[] { "Idle_Breathing", "Mace-Attack-R2", "Sword-Attack-R2", "Walk" }, new[] { "Mace-Attack-R2", "Sword-Attack-R2" });

            Assert.Equal("log h", attacks["Sword-Attack-R2"].Key);
            Assert.Equal("log v", attacks["Mace-Attack-R2"].Key);
            Assert.Equal(2, attacks.Count);
        }

        [Fact]
        public void AnAttackPlayingSeveralClipsBeginsInTheFirstAndStrikesWhereAClipSaysSo()
        {
            // A draugr's bow is drawn in one clip and let go in the next, which says when it shoots.
            var seen = Seen(("attack_bow", new[] { "Bow Aim Idle 01", "Bow Aim Recoil" }));

            var attacks = ClipAttacks.Match(new[] { ("attack_bow", (object)"bow") }, seen,
                new[] { "Bow Aim Idle 01", "Bow Aim Recoil", "Idle" }, new[] { "Bow Aim Recoil" });

            var draw = attacks["Bow Aim Idle 01"];
            var shot = attacks["Bow Aim Recoil"];
            Assert.Equal("bow", draw.Key);
            Assert.Equal("bow", shot.Key);
            Assert.True(draw.Begins);
            Assert.False(draw.Strikes);
            Assert.False(shot.Begins);
            Assert.True(shot.Strikes);
            Assert.False(shot.Halfway);
        }

        [Fact]
        public void AnAttackWhoseClipsNeverSayWhenItStrikesStrikesHalfwayThroughItsFirst()
        {
            var seen = Seen(("slam", new[] { "Slam Start", "Slam End" }));

            var attacks = ClipAttacks.Match(new[] { ("slam", (object)"slam") }, seen, new[] { "Slam Start", "Slam End" }, NoneStrike);

            Assert.True(attacks["Slam Start"].Strikes);
            Assert.True(attacks["Slam Start"].Halfway);
            Assert.False(attacks["Slam End"].Strikes);
            Assert.False(attacks["Slam End"].Halfway);
        }

        [Fact]
        public void AClipNamedAsTheTriggerIsItsAttackWhenTheAnimatorWasNotSeenToPlayOne()
        {
            var attacks = ClipAttacks.Match(new[] { ("attack_claws", (object)"claw") }, NoneSeen,
                new[] { "Attack Claws", "Attack Claws Loop", "Attack Jump", "Idle" }, new[] { "Attack Claws" });

            var claws = attacks["Attack Claws"];
            Assert.Equal("claw", claws.Key);
            Assert.True(claws.Begins);
            Assert.True(claws.Strikes);
            Assert.False(claws.Halfway);
            Assert.Single(attacks);
        }

        [Fact]
        public void AClipHoldingTheWholeTriggerIsItsAttackWhenItIsTheOnlyOne()
        {
            var attacks = ClipAttacks.Match(new[] { ("stomp_l", (object)"stomp"), ("throw", "throw") }, NoneSeen,
                new[] { "Attack Backhand L", "Attack Stomp L", "Attack Stomp R", "Attack TreeThrow", "Attack TreeThrow", "Idle" }, NoneStrike);

            Assert.Equal("stomp", attacks["Attack Stomp L"].Key);
            Assert.Equal("throw", attacks["Attack TreeThrow"].Key);
            Assert.Equal(2, attacks.Count);
        }

        [Fact]
        public void ATriggerSeveralClipsHoldIsTheAttackOfNone()
        {
            var attacks = ClipAttacks.Match(new[] { ("attack", (object)"bite") }, NoneSeen,
                new[] { "Attack Claws", "Attack Jump" }, NoneStrike);

            Assert.Empty(attacks);
        }

        [Fact]
        public void WhatTheAnimatorWasSeenToPlayOutranksTheNames()
        {
            var seen = Seen(("attack_claws", new[] { "Attack Jump" }));

            var attacks = ClipAttacks.Match(new[] { ("attack_claws", (object)"claw") }, seen,
                new[] { "Attack Claws", "Attack Jump" }, NoneStrike);

            Assert.Equal("claw", attacks["Attack Jump"].Key);
            Assert.Single(attacks);
        }

        [Fact]
        public void ATriggerSeenToPlayNothingFallsBackToTheNames()
        {
            var seen = Seen(("attack_claws", new string[0]));

            var attacks = ClipAttacks.Match(new[] { ("attack_claws", (object)"claw") }, seen,
                new[] { "Attack Claws", "Attack Jump" }, NoneStrike);

            Assert.Equal("claw", attacks["Attack Claws"].Key);
        }

        [Fact]
        public void OfTwoAttacksPlayingOneClipTheFirstGivenIsIt()
        {
            // Given what the creature has now first: a troll's punch in the look shown.
            var seen = Seen(("punch", new[] { "Zombie Attack" }));

            var attacks = ClipAttacks.Match(new[] { ("punch", (object)"punch now"), ("punch", "punch of another set") }, seen,
                new[] { "Zombie Attack" }, NoneStrike);

            Assert.Equal("punch now", attacks["Zombie Attack"].Key);
        }

        [Fact]
        public void AnAttackWithNoTriggerOrNoClipPlaysNone()
        {
            var attacks = ClipAttacks.Match(new[] { ("", (object)"aoe"), (null, "other"), ("fireball", "fireball") }, NoneSeen,
                new[] { "Idle", "Walk" }, NoneStrike);
            var unnamed = ClipAttacks.Match(new[] { ("_", (object)"unnamed") }, NoneSeen, new[] { "Idle" }, NoneStrike);

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
