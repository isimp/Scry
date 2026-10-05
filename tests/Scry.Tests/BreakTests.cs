using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class BreakTests
    {
        // A rock, ore vein or tree breaks under a hit whose tool tier is at least its own and some
        // of whose damage gets past its resistances (HitData.CheckToolTier, then the damage left).
        // Of all that break it, the weakest of each tier is told: a tree took 32 tools in a
        // modded game, many of one tier, where one of each says it all (Kevin's pick).

        private static readonly (string Name, int Tier, (string Type, float Amount)[] Deals)[] Tools =
        {
            ("PickaxeIron", 3, new[] { ("pickaxe", 33f), ("pierce", 33f) }),
            ("PickaxeAntler", 1, new[] { ("pickaxe", 22f), ("pierce", 22f) }),
            ("AxeBronze", 2, new[] { ("slash", 20f), ("chop", 40f) }),
            ("PickaxeBronze", 2, new[] { ("pickaxe", 27f), ("pierce", 27f) }),
            ("SwordIron", 3, new[] { ("slash", 55f) }),
            ("AxeStone", 0, new[] { ("slash", 10f), ("chop", 20f) }),
        };

        private static string[] Names(System.Collections.Generic.List<(string Name, int Tier)> tools) => tools.Select(t => t.Name).ToArray();

        [Fact]
        public void ARockIsBrokenWithPickaxesOfItsTierAndUpTheWeakestFirst()
        {
            Assert.Equal(new[] { "PickaxeBronze", "PickaxeIron" }, Names(GatherWords.BreaksIt(2, new[] { "pickaxe" }, Tools)));
            // Said so, and each tool with its tier, as one tool stands for its whole tier and up.
            Assert.Equal("Broken with, the weakest of each tier", GatherWords.BrokenWithTitle);
            Assert.Equal(new[] { ("PickaxeBronze", 2), ("PickaxeIron", 3) }, GatherWords.BreaksIt(2, new[] { "pickaxe" }, Tools));
            Assert.Equal("Tier 2", GatherWords.TierChip(2));
        }

        [Fact]
        public void ATreeTakesOnlyWhatChopsItAnyTierForOneThatNeedsNone()
        {
            Assert.Equal(new[] { "AxeStone", "AxeBronze" }, Names(GatherWords.BreaksIt(0, new[] { "chop" }, Tools)));
            // A sword's slash gets nowhere on a tree, whatever its tier.
            Assert.DoesNotContain("SwordIron", Names(GatherWords.BreaksIt(0, new[] { "chop" }, Tools)));
        }

        [Fact]
        public void OfAllThatBreakItTheWeakestOfEachTierIsTold()
        {
            // Three axes of tier 2 and two of tier 4: the one chopping least of each, the lowest tier first.
            var axes = new (string Name, int Tier, (string Type, float Amount)[] Deals)[]
            {
                ("BattleaxeBronze", 2, new[] { ("slash", 30f), ("chop", 60f) }),
                ("AxeBronze", 2, new[] { ("slash", 20f), ("chop", 40f) }),
                ("AxeBerzerkr", 4, new[] { ("slash", 50f), ("chop", 90f) }),
                ("AxeBlackMetal", 4, new[] { ("slash", 40f), ("chop", 80f) }),
                ("CrossbowArbalest", 2, new[] { ("pierce", 90f), ("chop", 40f) }),
                ("AxeStone", 0, new[] { ("slash", 10f), ("chop", 20f) }),
            };
            Assert.Equal(new[] { "AxeStone", "AxeBronze", "AxeBlackMetal" }, Names(GatherWords.BreaksIt(0, new[] { "chop" }, axes)));
            // Weakest by what the thing takes: the crossbow's pierce counts for nothing on a tree,
            // so it chops as little as the bronze axe, and the name settles it.
            Assert.Equal("AxeBronze", Names(GatherWords.BreaksIt(2, new[] { "chop" }, axes))[0]);
            Assert.Equal(new[] { "AxeBlackMetal" }, Names(GatherWords.BreaksIt(3, new[] { "chop" }, axes)));
        }

        [Fact]
        public void WhatTakesNoDamageIsBrokenWithNothing()
        {
            Assert.Empty(Names(GatherWords.BreaksIt(0, new string[0], Tools)));
            Assert.Empty(Names(GatherWords.BreaksIt(5, new[] { "pickaxe" }, Tools)));
        }
    }
}
