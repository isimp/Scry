using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class HeldClipTests
    {
        // A person moves by what it holds: the game sets statef and statei from the held item's
        // stance (Humanoid.SetAnimationState), and the animator's movement, crouching, blocking and
        // jumping blend by them, so each stance runs, walks and blocks in clips of its own. Drawing a
        // bow, reeling in a line or reloading a crossbow is a switch the item names
        // (Attack.m_drawAnimationState, m_reloadAnimation, m_chargeAnimationBool).

        private const int Unarmed = 0, OneHanded = 1, TwoHandedClub = 2, Bow = 3;

        private static Dictionary<int, HashSet<string>> Stances() => new Dictionary<int, HashSet<string>>
        {
            [Unarmed] = new HashSet<string> { "Run New", "IdleTweaked", "Crouch Idle", "Jump Loop" },
            [OneHanded] = new HashSet<string> { "Run New Sword Right", "IdleTweaked", "Crouch Idle", "Block idle", "Jump Loop" },
            [TwoHandedClub] = new HashSet<string> { "Run New Pickaxe", "2-hand-pickaxe-Idle", "Crouch Idle", "Jump Loop" },
            [Bow] = new HashSet<string> { "Run New Sword Right", "IdleTweaked", "Crouch Idle", "Jump Loop" },
        };

        private static Dictionary<string, HashSet<string>> Holds() => new Dictionary<string, HashSet<string>>
        {
            ["bow_aim"] = new HashSet<string> { "Bow Aim Walk Forward", "Bow Aim Idle 01" },
            ["reload_crossbow"] = new HashSet<string> { "Reload Crossbow" },
        };

        private static Dictionary<string, int> Hidden(int stance, params string[] held) =>
            HeldClips.OtherGear(Stances(), Holds(), stance, held);

        [Fact]
        public void HoldingASwordShowsOnlyHowASwordIsHeld()
        {
            var hidden = Hidden(OneHanded);
            Assert.Contains("Run New", hidden.Keys);
            Assert.Contains("Run New Pickaxe", hidden.Keys);
            Assert.Contains("2-hand-pickaxe-Idle", hidden.Keys);
            Assert.DoesNotContain("Run New Sword Right", hidden.Keys);
            Assert.DoesNotContain("Block idle", hidden.Keys);
            // Shared by every stance: shown whatever is held.
            Assert.DoesNotContain("Crouch Idle", hidden.Keys);
            Assert.DoesNotContain("Jump Loop", hidden.Keys);
        }

        [Fact]
        public void HoldingNothingShowsTheBareHandedOnes()
        {
            var hidden = Hidden(Unarmed);
            Assert.DoesNotContain("Run New", hidden.Keys);
            Assert.Contains("Run New Sword Right", hidden.Keys);
            Assert.Contains("Block idle", hidden.Keys);
        }

        [Fact]
        public void AClipSeveralStancesShareShowsForEachOfThem()
        {
            // A bow is run with as a sword is.
            Assert.DoesNotContain("Run New Sword Right", Hidden(Bow, "bow_aim").Keys);
        }

        [Fact]
        public void WhatOnlyAnotherItemsSwitchPlaysIsHidden()
        {
            var hidden = Hidden(OneHanded);
            Assert.Contains("Bow Aim Walk Forward", hidden.Keys);
            Assert.Contains("Reload Crossbow", hidden.Keys);
            var bow = Hidden(Bow, "bow_aim");
            Assert.DoesNotContain("Bow Aim Walk Forward", bow.Keys);
            Assert.Contains("Reload Crossbow", bow.Keys);
        }

        [Fact]
        public void AClipTheHeldItemsSwitchPlaysShowsThoughAnotherStanceHasItToo()
        {
            var stances = Stances();
            stances[OneHanded].Add("Bow Aim Idle 01");
            Assert.DoesNotContain("Bow Aim Idle 01", HeldClips.OtherGear(stances, Holds(), Bow, new[] { "bow_aim" }).Keys);
        }

        [Fact]
        public void AHiddenClipSaysWhatItIsHeldWith()
        {
            var hidden = Hidden(OneHanded);
            Assert.Equal(TwoHandedClub, hidden["Run New Pickaxe"]);
            Assert.Equal(Unarmed, hidden["Run New"]);
            // Held by a switch, by the stance of the item that names it.
            Assert.Equal(Bow, HeldClips.OtherGear(Stances(), Holds(), OneHanded, new string[0], new Dictionary<string, int> { ["bow_aim"] = Bow })["Bow Aim Idle 01"]);
        }

        [Fact]
        public void NothingIsHiddenWhileTheStancesAreNotSeenYet()
        {
            Assert.Empty(HeldClips.OtherGear(null, null, OneHanded, new string[0]));
        }

        [Fact]
        public void AClipNeverSeenUnderAnyStanceIsShown()
        {
            Assert.DoesNotContain("Emote Wave", Hidden(OneHanded).Keys);
        }

        [Fact]
        public void TheFilterFindsAHiddenClipSayingWhatItIsHeldWith()
        {
            Assert.Equal("With other gear", ClipWords.OtherGearHeading);
            Assert.Equal("Run New Pickaxe  ·  with two-handed clubs", ClipWords.OtherGear("Run New Pickaxe", TwoHandedClub));
            Assert.Equal("Run New  ·  with bare hands", ClipWords.OtherGear("Run New", Unarmed));
            Assert.Equal("Reload Crossbow  ·  with other gear", ClipWords.OtherGear("Reload Crossbow", -1));
            Assert.Equal("Idle  ·  with other gear", ClipWords.OtherGear("Idle", 99));
        }
    }
}
