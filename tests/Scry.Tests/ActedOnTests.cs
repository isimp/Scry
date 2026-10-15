using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class ActedOnTests
    {
        // Under the strict rules what the character saw go together shows: an item that came from
        // something it used or hit (a smelter's bar, a beehive's honey, a rock's ore) and what it
        // put into a station (ore, fuel, an offering). An item coming to be or picked up near a
        // thing acted on lately came from it, where the catalog says that thing gives it.

        private static bool Gives(string thing, string item) =>
            (thing == "smelter" && item == "Copper") || (thing == "Beehive" && item == "Honey") || (thing == "rock4_copper" && item == "CopperOre");

        [Fact]
        public void AnItemNearAThingActedOnLatelyThatGivesItCameFromIt()
        {
            var acted = new ActedOn();
            acted.Add("smelter", 10f, 10f, at: 100f);
            acted.Add("Beehive", 40f, 10f, at: 100f);

            Assert.Equal(new[] { "smelter" }, acted.Givers("Copper", 12f, 13f, at: 400f, Gives));
            Assert.Equal(new[] { "Beehive" }, acted.Givers("Honey", 41f, 9f, at: 105f, Gives));
        }

        [Fact]
        public void AnItemFarFromItOrLongAfterOrThatItDoesNotGiveDidNotComeFromIt()
        {
            var acted = new ActedOn();
            acted.Add("smelter", 10f, 10f, at: 100f);

            Assert.Empty(acted.Givers("Copper", 10f, 10f + ActedOn.Near + 1f, at: 101f, Gives));
            Assert.Empty(acted.Givers("Copper", 10f, 10f, at: 100f + ActedOn.Lately + 1f, Gives));
            Assert.Empty(acted.Givers("Copper", 10f, 10f, at: 99f, Gives));
            Assert.Empty(acted.Givers("Honey", 10f, 10f, at: 101f, Gives));
            Assert.Empty(acted.Givers("Copper", 10f, 10f, at: 101f, (a, b) => false));
        }

        [Fact]
        public void ActingOnTheSameThingAgainKeepsItLonger()
        {
            var acted = new ActedOn();
            acted.Add("smelter", 10f, 10f, at: 100f);
            acted.Add("smelter", 10f, 10f, at: 500f);

            Assert.Equal(new[] { "smelter" }, acted.Givers("Copper", 10f, 10f, at: 1000f, Gives));
            Assert.Equal(1, acted.Count);
        }

        [Fact]
        public void OnlyTheLatestThingsActedOnAreKept()
        {
            var acted = new ActedOn();
            acted.Add("smelter", 10f, 10f, at: 1f);
            for (var i = 0; i < ActedOn.Kept; i++) acted.Add("rock" + i, 100f + i * 20f, 0f, at: 2f + i);

            Assert.Equal(ActedOn.Kept, acted.Count);
            Assert.Empty(acted.Givers("Copper", 10f, 10f, at: 50f, Gives));
            acted.Clear();
            Assert.Equal(0, acted.Count);
        }

        [Fact]
        public void WhatLeftTheInventoryOnUsingSomethingWasPutIntoIt()
        {
            var before = new Dictionary<string, int> { ["CopperOre"] = 10, ["Coal"] = 20, ["Wood"] = 5 };
            var after = new Dictionary<string, int> { ["CopperOre"] = 9, ["Coal"] = 20, ["Wood"] = 7 };

            Assert.Equal(new[] { "CopperOre" }, ActedOn.Taken(before, after));
            Assert.Equal(new[] { "Wood" }, ActedOn.Taken(new Dictionary<string, int> { ["Wood"] = 1 }, new Dictionary<string, int>()));
            Assert.Empty(ActedOn.Taken(after, after));
        }

        [Fact]
        public void WhatAStationTurnedIntoAnItemIsWhatTheCharacterPutIntoIt()
        {
            // Copper ore and tin ore both go into a smelter; only the ore seen going in made the bar.
            var put = new HashSet<(string, string)> { ("smelter", "CopperOre") };

            var from = ActedOn.TurnedFrom("smelter", new[] { "CopperOre", "TinOre" }, (a, b) => put.Contains((a, b)));

            Assert.Equal(new[] { "CopperOre" }, from);
        }

        [Fact]
        public void AThingGivesWhatWhatItLeavesBehindGives()
        {
            // A copper deposit breaks into its fractured copy, which drops the ore; a tree falls
            // into a log, which gives the wood.
            var behind = new System.Collections.Generic.Dictionary<string, string[]>
            {
                ["rock4_copper"] = new[] { "rock4_copper_frac" },
                ["Beech1"] = new[] { "Beech_log" },
                ["Beech_log"] = new[] { "beech_log_half" },
                ["loop_a"] = new[] { "loop_b" },
                ["loop_b"] = new[] { "loop_a" },
            };
            bool Direct(string thing, string item) => (thing == "rock4_copper_frac" && item == "CopperOre") || (thing == "beech_log_half" && item == "Wood");
            System.Collections.Generic.IEnumerable<string> Behind(string thing) => behind.TryGetValue(thing, out var left) ? left : new string[0];

            Assert.True(ActedOn.GivesThrough("rock4_copper", "CopperOre", Behind, Direct));
            Assert.True(ActedOn.GivesThrough("rock4_copper_frac", "CopperOre", Behind, Direct));
            Assert.True(ActedOn.GivesThrough("Beech1", "Wood", Behind, Direct));
            Assert.False(ActedOn.GivesThrough("rock4_copper", "Wood", Behind, Direct));
            Assert.False(ActedOn.GivesThrough("loop_a", "Wood", Behind, Direct));
        }
    }
}
