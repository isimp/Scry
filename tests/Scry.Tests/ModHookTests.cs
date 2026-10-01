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
        public void EveryRuleScryTellsHasALabelAndANote()
        {
            // Beyond drops and spawning: what a mod hooking into the game's code for comfort, a
            // station's working, a fire's fuel, wear and support, costs, item stats, eating,
            // growing, the weather, raids, taming and trading may change on a page.
            var expected = new (HookedRule Rule, string Label, string Note)[]
            {
                (HookedRule.Comfort, "Mods and its comfort", "ComfortMe hooks into comfort: it may count otherwise than told"),
                (HookedRule.Smelting, "Mods and how it works", "ComfortMe hooks into smelting: its pace, what it takes and what it holds may differ from what is told"),
                (HookedRule.Cooking, "Mods and how it works", "ComfortMe hooks into cooking: its pace, what it takes and what it holds may differ from what is told"),
                (HookedRule.Fermenting, "Mods and how it works", "ComfortMe hooks into fermenting: its pace, what it takes and what it holds may differ from what is told"),
                (HookedRule.Producing, "Mods and how it works", "ComfortMe hooks into it: how fast it makes and how much it holds may differ from what is told"),
                (HookedRule.Burning, "Mods and its fuel", "ComfortMe hooks into fires: how long its fuel lasts may differ from what is told"),
                (HookedRule.Wear, "Mods and its wear", "ComfortMe hooks into wear and support: it may stand or wear otherwise than told"),
                (HookedRule.Crafting, "Mods and its cost", "ComfortMe hooks into crafting and building costs: it may cost otherwise than told"),
                (HookedRule.ItemStats, "Mods and its stats", "ComfortMe hooks into item stats: its figures may differ from those told"),
                (HookedRule.Food, "Mods and eating it", "ComfortMe hooks into eating: what it gives may differ from what is told"),
                (HookedRule.Growth, "Mods and its growing", "ComfortMe hooks into growing: it may grow or grow back otherwise than told"),
                (HookedRule.Weather, "Mods and the weather", "ComfortMe hooks into the weather: it may come or act otherwise than told"),
                (HookedRule.Raids, "Mods and raids", "ComfortMe hooks into raids: they may come otherwise than told"),
                (HookedRule.Taming, "Mods and taming", "ComfortMe hooks into taming and breeding: it may tame or breed otherwise than told"),
                (HookedRule.Trading, "Mods and its wares", "ComfortMe hooks into trading: what is sold may differ from what is told"),
                (HookedRule.Storage, "Mods and its size", "ComfortMe hooks into containers: it may hold more or less than told"),
            };
            foreach (var (rule, label, note) in expected)
            {
                Assert.Equal(label, ModHookWords.Label(rule));
                Assert.Equal(note, ModHookWords.Note(rule, new[] { "ComfortMe" }));
            }
            Assert.Equal("ComfortMe and InfiniteFire hook into fires: how long its fuel lasts may differ from what is told", ModHookWords.Note(HookedRule.Burning, new[] { "ComfortMe", "InfiniteFire" }));
        }

        [Fact]
        public void TheReportNamesEveryRuleAModHooksInto()
        {
            Assert.Equal("comfort, smelting, cooking, fermenting, beehives and sap, fires' fuel, wear and support, crafting and building costs, item stats, eating, growing, the weather, raids, taming and breeding, trading and container sizes",
                ModReportWords.Hooks(new[]
                {
                    HookedRule.Comfort, HookedRule.Smelting, HookedRule.Cooking, HookedRule.Fermenting, HookedRule.Producing, HookedRule.Burning, HookedRule.Wear,
                    HookedRule.Crafting, HookedRule.ItemStats, HookedRule.Food, HookedRule.Growth, HookedRule.Weather, HookedRule.Raids, HookedRule.Taming, HookedRule.Trading,
                    HookedRule.Storage,
                }));
            Assert.Equal("what creatures drop and where creatures spawn", ModReportWords.Hooks(new[] { HookedRule.Drops, HookedRule.Spawns }));
        }

        [Fact]
        public void AModNamedTwiceIsNamedOnce()
        {
            Assert.Equal("Epic Loot hooks into them: it may drop more or other than listed",
                ModHookWords.Note(HookedRule.Drops, new[] { "Epic Loot", "Epic Loot" }));
        }
    }
}
