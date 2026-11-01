using System.Collections.Generic;
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
            Assert.Equal("Jewelcrafting may change its drops: it may drop more or other things than listed",
                ModHookWords.Note(HookedRule.Drops, new[] { "Jewelcrafting" }));
            Assert.Equal("Jewelcrafting and Epic Loot may change its drops: it may drop more or other things than listed",
                ModHookWords.Note(HookedRule.Drops, new[] { "Jewelcrafting", "Epic Loot" }));
        }

        [Fact]
        public void ThreeOrMoreModsAreListedWithAnAndBeforeTheLast()
        {
            Assert.Equal("Drop That, Jewelcrafting and Epic Loot may change its drops: it may drop more or other things than listed",
                ModHookWords.Note(HookedRule.Drops, new[] { "Drop That", "Jewelcrafting", "Epic Loot" }));
        }

        [Fact]
        public void LootAndSpawningSayWhatTheyMayChange()
        {
            Assert.Equal("Drop That may change what it gives: it may give more or other things than listed", ModHookWords.Note(HookedRule.Loot, new[] { "Drop That" }));
            Assert.Equal("Monstrum may change its spawning: it may spawn elsewhere or otherwise than told", ModHookWords.Note(HookedRule.Spawns, new[] { "Monstrum" }));
        }

        [Fact]
        public void APagesHooksAreToldInOneShortLine()
        {
            // Every rule mods hook into on a page in one line, with how many mods; who they are
            // and what each may change is told on hover.
            var hooks = new List<(HookedRule, IReadOnlyList<string>)>
            {
                (HookedRule.Drops, new[] { "Drop That", "Epic Loot" }),
                (HookedRule.Spawns, new[] { "Monstrum", "Drop That" }),
                (HookedRule.Taming, new[] { "AllTameable" }),
            };
            Assert.Equal("4 mods may change its drops, its spawning and taming", ModHookWords.Line(hooks));
            Assert.Equal("A mod may change its drops", ModHookWords.Line(new List<(HookedRule, IReadOnlyList<string>)> { (HookedRule.Drops, new[] { "Drop That" }) }));
            Assert.Equal("2 mods may change its drops", ModHookWords.Line(new List<(HookedRule, IReadOnlyList<string>)> { (HookedRule.Drops, new[] { "A", "B" }) }));
        }

        [Fact]
        public void TheLinesHoverNamesEachModAndWhatItMayChange()
        {
            var hooks = new List<(HookedRule, IReadOnlyList<string>)>
            {
                (HookedRule.Drops, new[] { "Drop That" }),
                (HookedRule.Spawns, new[] { "Monstrum" }),
            };
            Assert.Equal("Drop That may change its drops: it may drop more or other things than listed\nMonstrum may change its spawning: it may spawn elsewhere or otherwise than told", ModHookWords.Tip(hooks));
        }

        [Fact]
        public void APageNoModHooksIntoHasNoLine()
        {
            Assert.Null(ModHookWords.Line(new List<(HookedRule, IReadOnlyList<string>)>()));
            Assert.Null(ModHookWords.Line(new List<(HookedRule, IReadOnlyList<string>)> { (HookedRule.Drops, new string[0]) }));
            Assert.Null(ModHookWords.Tip(new List<(HookedRule, IReadOnlyList<string>)>()));

            // A rule no mod hooks into is left out of the line and its hover.
            var some = new List<(HookedRule, IReadOnlyList<string>)> { (HookedRule.Drops, new[] { "Drop That" }), (HookedRule.Spawns, new string[0]) };
            Assert.Equal("A mod may change its drops", ModHookWords.Line(some));
            Assert.Equal(ModHookWords.Note(HookedRule.Drops, new[] { "Drop That" }), ModHookWords.Tip(some));
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
                (HookedRule.Comfort, "Mods and its comfort", "ComfortMe may change comfort: it may count otherwise than told"),
                (HookedRule.Smelting, "Mods and how it works", "ComfortMe may change smelting: its pace, what it takes and what it holds may differ from what is told"),
                (HookedRule.Cooking, "Mods and how it works", "ComfortMe may change cooking: its pace, what it takes and what it holds may differ from what is told"),
                (HookedRule.Fermenting, "Mods and how it works", "ComfortMe may change fermenting: its pace, what it takes and what it holds may differ from what is told"),
                (HookedRule.Producing, "Mods and how it works", "ComfortMe may change how it produces: how fast it makes and how much it holds may differ from what is told"),
                (HookedRule.Burning, "Mods and its fuel", "ComfortMe may change fires: how long its fuel lasts may differ from what is told"),
                (HookedRule.Wear, "Mods and its wear", "ComfortMe may change wear and support: it may stand or wear otherwise than told"),
                (HookedRule.Crafting, "Mods and its cost", "ComfortMe may change crafting and building costs: it may cost otherwise than told"),
                (HookedRule.ItemStats, "Mods and its stats", "ComfortMe may change item stats: its figures may differ from those told"),
                (HookedRule.Food, "Mods and what eating it gives", "ComfortMe may change eating: what it gives may differ from what is told"),
                (HookedRule.Growth, "Mods and its growing", "ComfortMe may change growing: it may grow or grow back otherwise than told"),
                (HookedRule.Weather, "Mods and the weather", "ComfortMe may change the weather: it may come or act otherwise than told"),
                (HookedRule.Raids, "Mods and raids", "ComfortMe may change raids: they may come otherwise than told"),
                (HookedRule.Taming, "Mods and taming", "ComfortMe may change taming and breeding: it may tame or breed otherwise than told"),
                (HookedRule.Trading, "Mods and its wares", "ComfortMe may change trading: what is sold may differ from what is told"),
                (HookedRule.Storage, "Mods and its size", "ComfortMe may change containers: it may hold more or less than told"),
            };
            foreach (var (rule, label, note) in expected)
            {
                Assert.Equal(label, ModHookWords.Label(rule));
                Assert.Equal(note, ModHookWords.Note(rule, new[] { "ComfortMe" }));
            }
            Assert.Equal("ComfortMe and InfiniteFire may change fires: how long its fuel lasts may differ from what is told", ModHookWords.Note(HookedRule.Burning, new[] { "ComfortMe", "InfiniteFire" }));
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
            Assert.Equal("Epic Loot may change its drops: it may drop more or other things than listed",
                ModHookWords.Note(HookedRule.Drops, new[] { "Epic Loot", "Epic Loot" }));
        }
    }
}
