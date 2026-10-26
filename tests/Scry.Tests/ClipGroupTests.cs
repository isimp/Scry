using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class ClipGroupTests
    {
        // The clip list groups each clip by what the game sends the animator there for: an attack;
        // moving, crouching or laden in the stance held; blocking; jumping, falling or swimming;
        // getting hurt or dying; an emote; riding, sitting or lying down; working at a station,
        // eating or equipping. Each is seen on the hidden copy: the stance sweep's runs, and every
        // switch or trigger of the animator pulled once.

        private static Dictionary<string, HashSet<string>> Visits() => new Dictionary<string, HashSet<string>>
        {
            [ClipGroups.MoveVisit] = new HashSet<string> { "IdleTweaked", "Run New Sword Right", "Sneak walk" },
            [ClipGroups.BlockVisit] = new HashSet<string> { "Block idle", "Block TwoHand Walk FW", "IdleTweaked" },
            [ClipGroups.JumpVisit] = new HashSet<string> { "JumpTweaked", "Jump Loop", "IdleTweaked" },
        };

        private static Dictionary<string, HashSet<string>> Switched() => new Dictionary<string, HashSet<string>>
        {
            ["emote_wave"] = new HashSet<string> { "Emote Wave", "IdleTweaked" },
            ["attach_chair"] = new HashSet<string> { "Player_Sit_Chair" },
            ["crafting"] = new HashSet<string> { "Workbench", "Forge" },
            ["stagger"] = new HashSet<string> { "player_stagger" },
            ["dodge"] = new HashSet<string> { "Dive" },
            ["inWater"] = new HashSet<string> { "Swimming", "Treading Water" },
            ["intro"] = new HashSet<string> { "flying" },
        };

        private static Dictionary<string, ClipGroup> Sorted(params string[] clips) =>
            ClipGroups.Sort(clips, new HashSet<string> { "Attack1" }, Visits(), Switched(), new HashSet<string>());

        [Fact]
        public void AnAttackIsAnAttackWhateverElseLeadsThere()
        {
            Assert.Equal(ClipGroup.Attacks, Sorted("Attack1")["Attack1"]);
        }

        [Fact]
        public void WhatItMovesInIsMovingThoughAnEmoteEndsThere()
        {
            var groups = Sorted("IdleTweaked", "Run New Sword Right", "Sneak walk");
            Assert.All(groups.Values, g => Assert.Equal(ClipGroup.Moving, g));
        }

        [Fact]
        public void BlockingAndJumpingAreTheirOwn()
        {
            var groups = Sorted("Block idle", "Block TwoHand Walk FW", "JumpTweaked", "Jump Loop", "Swimming");
            Assert.Equal(ClipGroup.Blocking, groups["Block idle"]);
            Assert.Equal(ClipGroup.Blocking, groups["Block TwoHand Walk FW"]);
            Assert.Equal(ClipGroup.Airborne, groups["JumpTweaked"]);
            Assert.Equal(ClipGroup.Airborne, groups["Jump Loop"]);
            Assert.Equal(ClipGroup.Airborne, groups["Swimming"]);
        }

        [Fact]
        public void EachSwitchSendsItsClipsToItsGroup()
        {
            var groups = Sorted("Emote Wave", "Player_Sit_Chair", "Workbench", "Forge", "player_stagger", "Dive");
            Assert.Equal(ClipGroup.Emotes, groups["Emote Wave"]);
            Assert.Equal(ClipGroup.Resting, groups["Player_Sit_Chair"]);
            Assert.Equal(ClipGroup.Working, groups["Workbench"]);
            Assert.Equal(ClipGroup.Working, groups["Forge"]);
            Assert.Equal(ClipGroup.Hurt, groups["player_stagger"]);
            Assert.Equal(ClipGroup.Hurt, groups["Dive"]);
        }

        [Fact]
        public void AClipNothingSortsIsOther()
        {
            var groups = Sorted("flying", "Mystery");
            Assert.Equal(ClipGroup.Other, groups["flying"]);
            Assert.Equal(ClipGroup.Other, groups["Mystery"]);
        }

        [Fact]
        public void AClipItIdlesInIsMoving()
        {
            // A creature without a stance sweep still idles where the probe saw it left alone.
            var groups = ClipGroups.Sort(new[] { "Idle", "Walk" }, new HashSet<string>(), null, null, new HashSet<string> { "Idle" });
            Assert.Equal(ClipGroup.Moving, groups["Idle"]);
            Assert.Equal(ClipGroup.Other, groups["Walk"]);
        }

        [Theory]
        [InlineData("emote_cheer", "Emotes")]
        [InlineData("emote_sit", "Emotes")]
        [InlineData("attach_lox", "Resting")]
        [InlineData("lying_down", "Resting")]
        [InlineData("sleep", "Resting")]
        [InlineData("wake", "Resting")]
        [InlineData("crafting", "Working")]
        [InlineData("interact", "Working")]
        [InlineData("eat", "Working")]
        [InlineData("consume", "Working")]
        [InlineData("equip_hip", "Working")]
        [InlineData("gpower", "Working")]
        [InlineData("stagger", "Hurt")]
        [InlineData("knockdown", "Hurt")]
        [InlineData("dead", "Hurt")]
        [InlineData("death1", "Hurt")]
        [InlineData("jump", "Airborne")]
        [InlineData("falling", "Airborne")]
        [InlineData("swim", "Airborne")]
        [InlineData("water", "Airborne")]
        [InlineData("fly", "Moving")]
        public void ASwitchIsSortedByWhatItIsFor(string name, string group)
        {
            Assert.Equal((ClipGroup)System.Enum.Parse(typeof(ClipGroup), group), ClipGroups.OfSwitch(name));
        }

        [Fact]
        public void AClipFoundByItsNameJumpsOrSwims()
        {
            Assert.Equal(ClipGroup.Airborne, ClipGroups.OfSwitch(ClipGroups.ByName("Jump Start")));
            Assert.Equal("jump", ClipGroups.ByName("Jump Start"));
            Assert.Equal("swim", ClipGroups.ByName("Swim Forward"));
        }

        [Fact]
        public void ASwitchOfNoKnownUseSortsNothing()
        {
            Assert.Null(ClipGroups.OfSwitch("intro"));
            Assert.Null(ClipGroups.OfSwitch("alert"));
        }

        [Fact]
        public void TheGroupsAreHeadedByWhatTheyAreFor()
        {
            Assert.Equal("Attacks  4", ClipWords.GroupHeading(ClipGroup.Attacks, 4, -1));
            Assert.Equal("Moving with one-handed weapons  14", ClipWords.GroupHeading(ClipGroup.Moving, 14, 1));
            Assert.Equal("Moving  6", ClipWords.GroupHeading(ClipGroup.Moving, 6, -1));
            Assert.Equal("Blocking  17", ClipWords.GroupHeading(ClipGroup.Blocking, 17, 1));
            Assert.Equal("Jumping, falling, swimming  9", ClipWords.GroupHeading(ClipGroup.Airborne, 9, 1));
            Assert.Equal("Hurt and dying  5", ClipWords.GroupHeading(ClipGroup.Hurt, 5, 1));
            Assert.Equal("Emotes  29", ClipWords.GroupHeading(ClipGroup.Emotes, 29, 1));
            Assert.Equal("Riding and resting  21", ClipWords.GroupHeading(ClipGroup.Resting, 21, 1));
            Assert.Equal("Working  15", ClipWords.GroupHeading(ClipGroup.Working, 15, 1));
            Assert.Equal("Other  3", ClipWords.GroupHeading(ClipGroup.Other, 3, 1));
        }
    }
}
