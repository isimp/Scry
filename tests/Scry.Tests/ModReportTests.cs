using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ModReportTests
    {
        // The mod report tells, for each mod, what it adds, what Scry links for its stations and
        // tools, which of the game's rules it hooks into, and what Scry could not place: a station
        // nothing is made or built at, an item with no source Scry sees, a creature that spawns
        // nowhere Scry sees, a piece in no build menu. What no mod added is not in it.

        private static ModEntry E(string name, Kind kind, string mod) => new ModEntry { Key = name, Name = name, Shown = name, Kind = kind, Mod = mod };

        private static List<ModEntry> Bamboo() => new List<ModEntry>
        {
            new ModEntry { Key = "OP_Bamboo_Build_Totem", Name = "OP_Bamboo_Build_Totem", Shown = "Bamboo Totem", Kind = Kind.Piece, Mod = "Bamboozled", Station = true, BuiltNear = 83, InBuildMenu = true },
            new ModEntry { Key = "OP_Bamboo_Hammer", Name = "OP_Bamboo_Hammer", Shown = "Bamboo Hammer", Kind = Kind.Item, Mod = "Bamboozled", Builds = 87, HasSource = true },
            new ModEntry { Key = "OP_Bamboo_Wood", Name = "OP_Bamboo_Wood", Shown = "Bamboo", Kind = Kind.Item, Mod = "Bamboozled", HasSource = true },
            new ModEntry { Key = "OP_Bamboo_Pole", Name = "OP_Bamboo_Pole", Shown = "Bamboo Pole", Kind = Kind.Piece, Mod = "Bamboozled", InBuildMenu = true },
            new ModEntry { Key = "OP_Bamboo_Tree_1", Name = "OP_Bamboo_Tree_1", Shown = "Bamboo", Kind = Kind.Resource, Mod = "Bamboozled" },
        };

        [Fact]
        public void EachModTellsHowManyOfEachKindItAdds()
        {
            var report = ModReport.Of(Bamboo(), NoHooks);

            var bamboo = Assert.Single(report);
            Assert.Equal("Bamboozled", bamboo.Mod);
            Assert.Equal("2 items, 2 pieces and 1 resource", ModReportWords.Counts(bamboo));
        }

        [Fact]
        public void TheKindAModAddsMostOfComesFirst()
        {
            var entries = new List<ModEntry> { E("Bow_TW", Kind.Item, "Warfare"), E("Rage", Kind.StatusEffect, "Warfare"), E("A", Kind.Piece, "Warfare"), E("B", Kind.Piece, "Warfare"), E("C", Kind.Piece, "Warfare") };

            Assert.Equal("3 pieces, 1 item and 1 status effect", ModReportWords.Counts(ModReport.Of(entries, NoHooks)[0]));
        }

        [Fact]
        public void WhatNoModAddedIsLeftOut()
        {
            var entries = Bamboo();
            entries.Add(E("Wood", Kind.Item, ""));
            entries.Add(E("Troll", Kind.Creature, null));

            Assert.Equal(new[] { "Bamboozled" }, ModReport.Of(entries, NoHooks).Select(m => m.Mod));
        }

        [Fact]
        public void ModsAreInOrderOfTheirNames()
        {
            var entries = Bamboo();
            entries.Add(E("KnifeViper_TW", Kind.Item, "Monstrum"));
            entries.Add(E("ArrowX", Kind.Item, "archery"));

            Assert.Equal(new[] { "archery", "Bamboozled", "Monstrum" }, ModReport.Of(entries, NoHooks).Select(m => m.Mod));
        }

        [Fact]
        public void AStationTellsWhatIsMadeBuiltAndUpgradedAtIt()
        {
            var bamboo = ModReport.Of(Bamboo(), NoHooks)[0];

            var totem = Assert.Single(bamboo.Stations);
            Assert.Equal("83 built near it", ModReportWords.Station(totem));
            Assert.Equal("12 made here, 3 built near it and 4 upgrading it", ModReportWords.Station(new ModEntry { MadeHere = 12, BuiltNear = 3, Upgrades = 4 }));
            Assert.Empty(bamboo.IdleStations);
        }

        [Fact]
        public void AStationNothingIsMadeOrBuiltAtIsAGap()
        {
            var entries = Bamboo();
            entries.Add(new ModEntry { Key = "FletcherTable_TW", Name = "FletcherTable_TW", Shown = "Fletcher Table", Kind = Kind.Piece, Mod = "Bamboozled", Station = true, InBuildMenu = true });

            var bamboo = ModReport.Of(entries, NoHooks)[0];

            Assert.Equal(new[] { "FletcherTable_TW" }, bamboo.IdleStations.Select(e => e.Key));
            Assert.Equal("nothing is made or built at it", ModReportWords.Station(bamboo.IdleStations[0]));
        }

        [Fact]
        public void AToolTellsHowManyPiecesItBuilds()
        {
            var bamboo = ModReport.Of(Bamboo(), NoHooks)[0];

            var hammer = Assert.Single(bamboo.Tools);
            Assert.Equal("builds 87 pieces", ModReportWords.Tool(hammer));
            Assert.Equal("builds 1 piece", ModReportWords.Tool(new ModEntry { Builds = 1 }));
        }

        [Fact]
        public void ItemsCreaturesAndPiecesScryCannotPlaceAreGaps()
        {
            var entries = Bamboo();
            entries.Add(E("GemstoneRed", Kind.Item, "Bamboozled"));
            entries.Add(E("Asmodeus_TW", Kind.Creature, "Bamboozled"));
            entries.Add(new ModEntry { Key = "Svalt_TW", Name = "Svalt_TW", Shown = "Svalt", Kind = Kind.Creature, Mod = "Bamboozled", Spawns = true });
            entries.Add(E("OP_Bamboo_Secret", Kind.Piece, "Bamboozled"));

            var bamboo = ModReport.Of(entries, NoHooks)[0];

            Assert.Equal(new[] { "GemstoneRed" }, bamboo.Sourceless.Select(e => e.Key));
            Assert.Equal(new[] { "Asmodeus_TW" }, bamboo.Unspawned.Select(e => e.Key));
            Assert.Equal(new[] { "OP_Bamboo_Secret" }, bamboo.Unbuilt.Select(e => e.Key));
        }

        [Fact]
        public void TheRulesAModHooksIntoAreTold()
        {
            var hooks = new Dictionary<string, IReadOnlyList<HookedRule>> { ["Bamboozled"] = new[] { HookedRule.Drops, HookedRule.Spawns } };

            var bamboo = ModReport.Of(Bamboo(), hooks)[0];

            Assert.Equal(new[] { HookedRule.Drops, HookedRule.Spawns }, bamboo.Hooks);
            Assert.Equal("what creatures drop and where creatures spawn", ModReportWords.Hooks(bamboo.Hooks));
            Assert.Equal("what drop tables, chests and plants give", ModReportWords.Hooks(new[] { HookedRule.Loot }));
        }

        [Fact]
        public void AModThatOnlyHooksInIsInTheReportToo()
        {
            var hooks = new Dictionary<string, IReadOnlyList<HookedRule>> { ["Drop That"] = new[] { HookedRule.Drops, HookedRule.Loot } };

            var report = ModReport.Of(Bamboo(), hooks);

            var dropThat = report.Single(m => m.Mod == "Drop That");
            Assert.DoesNotContain(ModReport.Of(Bamboo(), new Dictionary<string, IReadOnlyList<HookedRule>> { ["Quiet"] = new HookedRule[0], [""] = new[] { HookedRule.Drops } }), m => m.Mod != "Bamboozled");
            Assert.Equal("adds nothing of its own", ModReportWords.Counts(dropThat));
            Assert.Equal(new[] { "Bamboozled", "Drop That" }, report.Select(m => m.Mod));
        }

        [Fact]
        public void AModsOwnPageIsNothingItAdds()
        {
            // Every mod loaded has a page of its own in the catalog, under its own name. That page
            // is Scry's, so a mod adding nothing else is not in the report, and one that adds
            // things counts only those.
            var entries = Bamboo();
            entries.Add(E("Bamboozled", Kind.Mod, "Bamboozled"));
            entries.Add(E("AzuAutoStore", Kind.Mod, "AzuAutoStore"));
            entries.Add(E("Drop That", Kind.Mod, "Drop That"));
            var hooks = new Dictionary<string, IReadOnlyList<HookedRule>> { ["Drop That"] = new[] { HookedRule.Loot } };

            var report = ModReport.Of(entries, hooks);

            Assert.Equal(new[] { "Bamboozled", "Drop That" }, report.Select(m => m.Mod));
            Assert.Equal("2 items, 2 pieces and 1 resource", ModReportWords.Counts(report[0]));
            Assert.Equal("adds nothing of its own", ModReportWords.Counts(report[1]));
        }

        [Fact]
        public void AnItemACreatureCarriesHasASource()
        {
            // A creature's own attack (a horse's bite) is an item it carries, never one a player
            // finds: Scry places it with the creature, so it is no gap.
            var entries = Bamboo();
            entries.Add(new ModEntry { Key = "horse_bite_attack", Name = "horse_bite_attack", Shown = "horse_bite_attack", Kind = Kind.Item, Mod = "Bamboozled", Carried = true });
            entries.Add(E("GemstoneRed", Kind.Item, "Bamboozled"));

            var bamboo = ModReport.Of(entries, NoHooks)[0];

            Assert.Equal(new[] { "GemstoneRed" }, bamboo.Sourceless.Select(e => e.Key));
        }

        private static readonly Dictionary<string, IReadOnlyList<HookedRule>> NoHooks = new Dictionary<string, IReadOnlyList<HookedRule>>();
    }
}
