using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ContentOrderTests
    {
        // Loot is told rarest first: the special find leads, what nearly always comes last. Among
        // equals the order given stays, so a drop table's own order breaks ties.

        [Fact]
        public void LootIsToldRarestFirst()
        {
            var drops = new[] { ("Wood", 1.0), ("TrophyGreydwarf", 0.05), ("GreydwarfEye", 0.5) };

            var ordered = ContentOrder.RarestFirst(drops, d => d.Item2).Select(d => d.Item1);

            Assert.Equal(new[] { "TrophyGreydwarf", "GreydwarfEye", "Wood" }, ordered);
        }

        [Fact]
        public void EquallyRareLootKeepsTheOrderGiven()
        {
            var drops = new[] { ("Stone", 1.0), ("Wood", 1.0), ("Resin", 1.0) };

            Assert.Equal(new[] { "Stone", "Wood", "Resin" }, ContentOrder.RarestFirst(drops, d => d.Item2).Select(d => d.Item1));
        }

        // Creatures are told toughest first: bosses, then the most health; what is no creature
        // comes after them all, in the order given.

        [Fact]
        public void CreaturesAreToldToughestFirst()
        {
            var foes = new Dictionary<string, Foe?>
            {
                ["Greydwarf"] = new Foe(false, 40f),
                ["Eikthyr"] = new Foe(true, 500f),
                ["Troll"] = new Foe(false, 600f),
                ["Greydwarf_Elite"] = new Foe(false, 150f),
            };

            var ordered = ContentOrder.ToughestFirst(foes.Keys, n => foes[n]);

            Assert.Equal(new[] { "Eikthyr", "Troll", "Greydwarf_Elite", "Greydwarf" }, ordered);
        }

        [Fact]
        public void WhatIsNoCreatureComesAfterTheCreaturesInTheOrderGiven()
        {
            var foes = new Dictionary<string, Foe?> { ["Cinder rain"] = null, ["Wolf"] = new Foe(false, 80f), ["Ash"] = null, ["Bat"] = new Foe(false, 1f) };

            Assert.Equal(new[] { "Wolf", "Bat", "Cinder rain", "Ash" }, ContentOrder.ToughestFirst(foes.Keys, n => foes[n]));
        }

        [Fact]
        public void EquallyToughCreaturesKeepTheOrderGiven()
        {
            var foes = new Dictionary<string, Foe?> { ["Draugr"] = new Foe(false, 100f), ["Blob"] = new Foe(false, 100f) };

            Assert.Equal(new[] { "Draugr", "Blob" }, ContentOrder.ToughestFirst(foes.Keys, n => foes[n]));
        }

        // Where a thing comes from is told surest first: what always gives it, then by chance;
        // among equally sure ones, what stands in the biome players reach first.

        [Fact]
        public void SourcesAreToldSurestFirst()
        {
            var lines = new[] { ("Dropped by a surtling", 0.5, 2), ("Mined from a vein", 1.0, 3), ("Found in a chest", 0.1, 0) };

            var ordered = ContentOrder.SurestFirst(lines, l => l.Item2, l => l.Item3).Select(l => l.Item1);

            Assert.Equal(new[] { "Mined from a vein", "Dropped by a surtling", "Found in a chest" }, ordered);
        }

        [Fact]
        public void EquallySureSourcesGoByTheBiomeReachedFirstThenTheOrderGiven()
        {
            var lines = new[] { ("Sold by Haldor", 1.0, int.MaxValue), ("Picked in the mountains", 1.0, 3), ("Picked in the meadows", 1.0, 0), ("Picked in the swamp", 1.0, 3) };

            var ordered = ContentOrder.SurestFirst(lines, l => l.Item2, l => l.Item3).Select(l => l.Item1);

            Assert.Equal(new[] { "Picked in the meadows", "Picked in the mountains", "Picked in the swamp", "Sold by Haldor" }, ordered);
        }

        // A biome's rank is the earliest of its biomes in the order players meet them; a biome a
        // mod adds comes after the game's, and none at all after every one.

        [Fact]
        public void TheEarliestBiomeRanksAPlace()
        {
            Assert.Equal(0, ContentOrder.Earliest(new[] { "Swamp", "Meadows" }));
            Assert.Equal(0, ContentOrder.Earliest(new[] { "Meadows", "Swamp" }));
            Assert.Equal(3, ContentOrder.Earliest(new[] { "Mountain" }));
            Assert.Equal(2, ContentOrder.Earliest(new[] { "ModdedBiome", "Swamp" }));
        }

        [Fact]
        public void AModsBiomeComesAfterTheGamesAndNoneAfterEveryOne()
        {
            var modded = ContentOrder.Earliest(new[] { "ModdedBiome" });
            var none = ContentOrder.Earliest(new string[0]);

            Assert.True(modded > ContentOrder.Earliest(new[] { "Ocean" }));
            Assert.True(none > modded);
            Assert.Equal(none, ContentOrder.Earliest(null));
        }

        // What grows or stands to be gathered is told hardest first: the tool tier it needs, then
        // its health; what is only picked comes after, in the order given.

        [Fact]
        public void WhatIsGatheredIsToldHardestFirst()
        {
            var things = new Dictionary<string, (int, float)?>
            {
                ["Beech"] = (0, 200f),
                ["Raspberry bush"] = null,
                ["Silver vein"] = (2, 50f),
                ["Copper deposit"] = (1, 50f),
                ["Old oak"] = (1, 400f),
            };

            var ordered = ContentOrder.HardestFirst(things.Keys, n => things[n]);

            Assert.Equal(new[] { "Silver vein", "Old oak", "Copper deposit", "Beech", "Raspberry bush" }, ordered);
        }

        [Fact]
        public void EquallyHardThingsKeepTheOrderGiven()
        {
            var things = new Dictionary<string, (int, float)?> { ["Rock"] = (0, 50f), ["Bush"] = (0, 50f), ["Berry"] = null, ["Mushroom"] = null };

            Assert.Equal(new[] { "Rock", "Bush", "Berry", "Mushroom" }, ContentOrder.HardestFirst(things.Keys, n => things[n]));
        }

        [Fact]
        public void WhatIsOnlyPickedComesAfterEvenWhatBreaksAtATouch()
        {
            var things = new Dictionary<string, (int, float)?> { ["Berry"] = null, ["Pebble"] = (0, 0f) };

            Assert.Equal(new[] { "Pebble", "Berry" }, ContentOrder.HardestFirst(things.Keys, n => things[n]));
        }

        // Places are told rarest first: the fewest the world places, one of a kind leading; a
        // place the world never places by itself comes after, in the order given.

        [Fact]
        public void PlacesAreToldRarestFirst()
        {
            var places = new[] { ("Runestone", 50), ("Eikthyr's altar", 3), ("Sacrificial stones", 1), ("Burial chambers", 100) };

            var ordered = ContentOrder.FewestFirst(places, p => p.Item2).Select(p => p.Item1);

            Assert.Equal(new[] { "Sacrificial stones", "Eikthyr's altar", "Runestone", "Burial chambers" }, ordered);
        }

        [Fact]
        public void APlaceTheWorldNeverPlacesComesAfterTheRest()
        {
            var places = new[] { ("Mod place", 0), ("Ruin", 20), ("Odd place", -1), ("Tower", 20) };

            Assert.Equal(new[] { "Ruin", "Tower", "Mod place", "Odd place" }, ContentOrder.FewestFirst(places, p => p.Item2).Select(p => p.Item1));
        }

        // How often one opening of a drop table gives an item, as DropTable.GetDropList rolls it:
        // the table drops anything at its chance, then rolls from its least to its most times,
        // each roll picking by weight; one that gives each item at most once leaves out what it
        // gave, so as many rolls as it has items give them all.

        [Fact]
        public void ASureTableWithOneRollGivesAnItemAtItsShare()
        {
            Assert.Equal(0.25, ContentOrder.AtLeastOnce(0.25, 1, 1, 1.0, false, 4), 6);
        }

        [Fact]
        public void MoreRollsGiveAnItemMoreOften()
        {
            // Two rolls at a half: 1 - 0.5^2.
            Assert.Equal(0.75, ContentOrder.AtLeastOnce(0.5, 2, 2, 1.0, false, 2), 6);
        }

        [Fact]
        public void RollsFromLeastToMostAreAveraged()
        {
            // One or two rolls at a half, each as likely: (0.5 + 0.75) / 2.
            Assert.Equal(0.625, ContentOrder.AtLeastOnce(0.5, 1, 2, 1.0, false, 2), 6);
        }

        [Fact]
        public void ATablesOwnChanceComesFirst()
        {
            Assert.Equal(0.1, ContentOrder.AtLeastOnce(1.0, 1, 1, 0.1, false, 1), 6);
        }

        [Fact]
        public void EachAtMostOnceWithAsManyRollsAsItemsGivesThemAll()
        {
            Assert.Equal(1.0, ContentOrder.AtLeastOnce(0.1, 3, 3, 1.0, true, 3), 6);
            Assert.Equal(1.0 - System.Math.Pow(0.9, 2), ContentOrder.AtLeastOnce(0.1, 2, 2, 1.0, true, 3), 6);
        }

        [Fact]
        public void NoRollsOrNoWeightGiveNothing()
        {
            Assert.Equal(0.0, ContentOrder.AtLeastOnce(0.5, 0, 0, 1.0, false, 2), 6);
            Assert.Equal(0.0, ContentOrder.AtLeastOnce(0.0, 3, 3, 1.0, false, 2), 6);
            Assert.Equal(0.0, ContentOrder.AtLeastOnce(0.0, 3, 3, 1.0, true, 2), 6);
        }

        [Fact]
        public void FewerThanNoRollsRollNone()
        {
            // -1, 0 or 1 rolls at a half, each as likely: (0 + 0 + 0.5) / 3.
            Assert.Equal(0.5 / 3, ContentOrder.AtLeastOnce(0.5, -1, 1, 1.0, false, 2), 6);
        }

        [Fact]
        public void ATableWhoseMostIsBelowItsLeastRollsItsLeast()
        {
            Assert.Equal(0.75, ContentOrder.AtLeastOnce(0.5, 2, 1, 1.0, false, 2), 6);
        }
    }
}
