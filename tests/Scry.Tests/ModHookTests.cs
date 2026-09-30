using Xunit;

namespace Scry.Tests
{
    public class ModHookTests
    {
        // A mod that hooks into the game's own code for what a creature drops, what a chest, rock
        // or plant gives, or where creatures spawn, can change it beyond anything Scry reads from
        // the prefabs; the details say which mods do, so nothing looks more complete than it is.

        [Fact]
        public void NoModHookingInTellsNothing()
        {
            Assert.Null(ModHookWords.Note(HookedRule.Drops, new string[0]));
            Assert.Null(ModHookWords.Note(HookedRule.Drops, null));
        }

        [Fact]
        public void ACreaturesDropsNameTheModsThatHookIntoThem()
        {
            Assert.Equal("Jewelcrafting hooks into them: it may drop more or other than listed",
                ModHookWords.Note(HookedRule.Drops, new[] { "Jewelcrafting" }));
            Assert.Equal("Jewelcrafting and Epic Loot hook into them: it may drop more or other than listed",
                ModHookWords.Note(HookedRule.Drops, new[] { "Jewelcrafting", "Epic Loot" }));
        }

        [Fact]
        public void ThreeOrMoreModsAreListedWithAnAndBeforeTheLast()
        {
            Assert.Equal("Drop That, Jewelcrafting and Epic Loot hook into them: it may drop more or other than listed",
                ModHookWords.Note(HookedRule.Drops, new[] { "Drop That", "Jewelcrafting", "Epic Loot" }));
        }

        [Fact]
        public void LootAndSpawningSayWhatTheyMayChange()
        {
            Assert.Equal("Drop That hooks into it: it may give more or other than listed", ModHookWords.Note(HookedRule.Loot, new[] { "Drop That" }));
            Assert.Equal("Monstrum hooks into it: it may spawn elsewhere or otherwise than told", ModHookWords.Note(HookedRule.Spawns, new[] { "Monstrum" }));
        }

        [Fact]
        public void EachRuleHasItsOwnLabel()
        {
            Assert.Equal("Mods and its drops", ModHookWords.Label(HookedRule.Drops));
            Assert.Equal("Mods and what it gives", ModHookWords.Label(HookedRule.Loot));
            Assert.Equal("Mods and its spawning", ModHookWords.Label(HookedRule.Spawns));
        }

        [Fact]
        public void AModNamedTwiceIsNamedOnce()
        {
            Assert.Equal("Epic Loot hooks into them: it may drop more or other than listed",
                ModHookWords.Note(HookedRule.Drops, new[] { "Epic Loot", "Epic Loot" }));
        }
    }
}
