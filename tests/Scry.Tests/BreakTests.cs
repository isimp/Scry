using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class BreakTests
    {
        // A rock, ore vein or tree breaks under a hit whose tool tier is at least its own and some
        // of whose damage gets past its resistances (HitData.CheckToolTier, then the damage left).

        private static readonly (string Name, int Tier, string[] Deals)[] Tools =
        {
            ("PickaxeIron", 3, new[] { "pickaxe", "pierce" }),
            ("PickaxeAntler", 1, new[] { "pickaxe", "pierce" }),
            ("AxeBronze", 2, new[] { "slash", "chop" }),
            ("PickaxeBronze", 2, new[] { "pickaxe", "pierce" }),
            ("SwordIron", 3, new[] { "slash" }),
            ("AxeStone", 0, new[] { "slash", "chop" }),
        };

        [Fact]
        public void ARockIsBrokenWithPickaxesOfItsTierAndUpTheWeakestFirst()
        {
            Assert.Equal(new[] { "PickaxeBronze", "PickaxeIron" }, GatherWords.BreaksIt(2, new[] { "pickaxe" }, Tools));
            Assert.Equal("Broken with", GatherWords.BrokenWithTitle);
        }

        [Fact]
        public void ATreeTakesOnlyWhatChopsItAnyTierForOneThatNeedsNone()
        {
            Assert.Equal(new[] { "AxeStone", "AxeBronze" }, GatherWords.BreaksIt(0, new[] { "chop" }, Tools));
            // A sword's slash gets nowhere on a tree, whatever its tier.
            Assert.DoesNotContain("SwordIron", GatherWords.BreaksIt(0, new[] { "chop" }, Tools));
        }

        [Fact]
        public void WhatTakesNoDamageIsBrokenWithNothing()
        {
            Assert.Empty(GatherWords.BreaksIt(0, new string[0], Tools));
            Assert.Empty(GatherWords.BreaksIt(5, new[] { "pickaxe" }, Tools));
        }
    }
}
