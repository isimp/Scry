using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class BreakTests
    {
        // A rock, ore vein or tree breaks under a hit whose tool tier is at least its own and some
        // of whose damage gets past its resistances (HitData.CheckToolTier, then the damage left).
        // Of all that break it, the tools made for it are told, by the skill the game trains with
        // them: axes for what is chopped, pickaxes for what is mined, the weakest first. A weapon
        // that happens to deal a little chop, as a crossbow can, is no tool for a tree (Kevin).

        private static readonly (string Name, int Tier, string Skill, (string Type, float Amount)[] Deals)[] Tools =
        {
            ("PickaxeIron", 3, "Pickaxes", new[] { ("pickaxe", 33f), ("pierce", 33f) }),
            ("PickaxeAntler", 1, "Pickaxes", new[] { ("pickaxe", 22f), ("pierce", 22f) }),
            ("CrossbowArbalest", 5, "Crossbows", new[] { ("pierce", 90f), ("chop", 10f) }),
            ("BattleaxeCrystal", 3, "Axes", new[] { ("slash", 50f), ("chop", 40f) }),
            ("AxeBronze", 2, "Axes", new[] { ("slash", 20f), ("chop", 40f) }),
            ("PickaxeBronze", 2, "Pickaxes", new[] { ("pickaxe", 27f), ("pierce", 27f) }),
            ("SwordIron", 3, "Swords", new[] { ("slash", 55f) }),
            ("AxeStone", 0, "Axes", new[] { ("slash", 10f), ("chop", 20f) }),
            ("SledgeIron", 2, "Clubs", new[] { ("blunt", 70f), ("pickaxe", 10f) }),
        };

        [Fact]
        public void ARockIsBrokenWithPickaxesOfItsTierAndUpTheWeakestFirst()
        {
            Assert.Equal(new[] { "PickaxeBronze", "PickaxeIron" }, GatherWords.BreaksIt(2, new[] { "pickaxe" }, Tools));
            Assert.Equal("Broken with", GatherWords.BrokenWithTitle);
        }

        [Fact]
        public void ATreeIsBrokenWithEveryAxeTheWeakestFirstAndNoWeaponThatJustChopsALittle()
        {
            // Every axe, battleaxes too (the game trains Axes with them), the least chopping first,
            // ties by name; the crossbow's bit of chop and the sword's slash do not make them tools.
            Assert.Equal(new[] { "AxeStone", "AxeBronze", "BattleaxeCrystal" }, GatherWords.BreaksIt(0, new[] { "chop" }, Tools));
            Assert.DoesNotContain("CrossbowArbalest", GatherWords.BreaksIt(0, new[] { "chop" }, Tools));
            Assert.DoesNotContain("SwordIron", GatherWords.BreaksIt(0, new[] { "chop" }, Tools));
            // Nor does a sledge's bit of pickaxe damage make it a tool for a rock.
            Assert.DoesNotContain("SledgeIron", GatherWords.BreaksIt(0, new[] { "pickaxe" }, Tools));
        }

        [Fact]
        public void WhatNoToolIsMadeForIsBrokenWithAnythingThatBreaksIt()
        {
            // A sack that takes only slash and blunt: no axe or pickaxe is made for it, so all that break it.
            Assert.Equal(new[] { "AxeStone", "AxeBronze", "BattleaxeCrystal", "SwordIron", "SledgeIron" }, GatherWords.BreaksIt(0, new[] { "slash", "blunt" }, Tools));
        }

        [Fact]
        public void WhatTakesNoDamageIsBrokenWithNothing()
        {
            Assert.Empty(GatherWords.BreaksIt(0, new string[0], Tools));
            Assert.Empty(GatherWords.BreaksIt(5, new[] { "pickaxe" }, Tools));
        }
    }
}
