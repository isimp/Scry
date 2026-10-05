using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FactLayoutTests
    {
        // In the game is laid out by topic, in an order fixed for each kind, each under a small
        // heading: a few headline numbers as tiles, facts that qualify a row as notes under it,
        // and every fact read shown once, whatever reader told it and in whatever order.

        private static readonly (string Label, string Part)[] TrollPairs =
        {
            ("Health", ""), ("Faction", ""), ("Health with stars", ""), ("Damage with stars", ""),
            ("Attack: Club", "attacks"), ("Hit on the head", "weak spots"), ("Moves", "behaviour"), ("Sees", "behaviour"),
            ("Gives up chasing", "behaviour"), ("Takes to tame", "behaviour"), ("Tameable", ""), ("Forsaken power", ""),
            ("Drops with stars", ""), ("In this world", "world settings"), ("Something new", "a mod's part"),
            ("Love", "breeding"), ("Ridden with", "riding"), ("Keeps its distance", "behaviour"),
        };

        private static readonly (string Title, string Part)[] TrollRows = { ("Damage it takes", "resistances"), ("Eats", ""), ("Drops", ""), ("Seen dropping in your play (3 kills)", "") };

        private static List<FactTopicPlan> Troll(params FactBlock[] blocks) =>
            FactLayout.Plan(Kind.Creature, TrollPairs, TrollRows, blocks.Length > 0 ? blocks : new[] { FactBlock.Where, FactBlock.Biomes, FactBlock.Hooks });

        private static string Pair(int i) => TrollPairs[i].Label;

        private static string Bit(FactBit bit) => bit.Pair >= 0 ? Pair(bit.Pair) : bit.Row >= 0 ? "row " + TrollRows[bit.Row].Title : "block " + bit.Block;

        [Fact]
        public void ACreatureTellsItsFightFirstThenItsSensesLootHomeAndTaming()
        {
            var plan = Troll();
            Assert.Equal(new[] { null, "Fight", "Senses and behaviour", "Loot", "Where it lives", "Taming and breeding", "Riding", "After it falls", "More", null }, plan.Select(t => t.Heading));

            // Its headline numbers as tiles, the boss and moves among them where told.
            Assert.Equal(new[] { "Health", "Faction", "Moves", "Tameable" }, plan[0].Tiles.Select(Pair));
            Assert.Equal(new[] { "Health with stars", "Damage with stars", "Attack: Club", "Hit on the head", "row Damage it takes" }, plan[1].Bits.Select(Bit));
            Assert.Equal(new[] { "Sees" }, plan[2].Tiles.Select(Pair));
            // A new fact of a reader's part goes where that part's facts go.
            Assert.Equal(new[] { "Gives up chasing", "Keeps its distance" }, plan[2].Bits.Select(Bit));
            Assert.Equal(new[] { "block Where", "block Biomes" }, plan[4].Bits.Select(Bit));
            Assert.Equal(new[] { "Takes to tame", "Love", "row Eats" }, plan[5].Bits.Select(Bit));
            Assert.Equal(new[] { "Ridden with" }, plan[6].Bits.Select(Bit));
            Assert.Equal(new[] { "Forsaken power" }, plan[7].Bits.Select(Bit));
            Assert.Equal(new[] { "Something new" }, plan[8].Bits.Select(Bit));
            Assert.Equal(new[] { "block Hooks" }, plan[9].Bits.Select(Bit));
        }

        [Fact]
        public void WhatQualifiesItsDropsIsToldUnderThem()
        {
            var loot = Troll().Single(t => t.Heading == "Loot");
            Assert.Equal(new[] { "row Drops", "row Seen dropping in your play (3 kills)" }, loot.Bits.Select(Bit));
            Assert.Equal(new[] { "Drops with stars", "In this world" }, loot.Bits[0].Notes.Select(Pair));

            // With nothing dropped, the world's note stands under the line saying so.
            var none = FactLayout.Plan(Kind.Creature, new[] { ("Drops", ""), ("In this world", "world settings") }, new (string, string)[0], new FactBlock[0]);
            Assert.Equal(new[] { 1 }, Assert.Single(none.Single(t => t.Heading == "Loot").Bits).Notes);
        }

        [Fact]
        public void EveryFactReadIsShownOnceAndATopicWithNothingIsLeftOut()
        {
            var plan = Troll(FactBlock.Hooks);
            var pairs = plan.SelectMany(t => t.Tiles.Concat(t.Bits.Where(b => b.Pair >= 0).Select(b => b.Pair)).Concat(t.Bits.SelectMany(b => b.Notes))).OrderBy(i => i);
            Assert.Equal(Enumerable.Range(0, TrollPairs.Length), pairs);
            var rows = plan.SelectMany(t => t.Bits.Where(b => b.Row >= 0).Select(b => b.Row)).OrderBy(i => i);
            Assert.Equal(Enumerable.Range(0, TrollRows.Length), rows);
            // No Where or Biomes this time, so no Where it lives.
            Assert.DoesNotContain(plan, t => t.Heading == "Where it lives");
            Assert.All(plan, t => Assert.True(t.Tiles.Count + t.Bits.Count > 0));
        }

        [Fact]
        public void ANoteWithNothingToStandUnderStaysAFactOfItsOwn()
        {
            var plan = FactLayout.Plan(Kind.Creature, new[] { ("Health", ""), ("In this world", "world settings") }, new (string, string)[0], new FactBlock[0]);
            var loot = plan.Single(t => t.Heading == "Loot");
            Assert.Equal(1, Assert.Single(loot.Bits).Pair);
        }

        private static List<(string Heading, string[] Shown)> Item((string, string)[] pairs, (string, string)[] rows, params FactBlock[] blocks)
        {
            var plan = FactLayout.Plan(Kind.Item, pairs, rows, blocks);
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            return plan.Select(t => (t.Heading, t.Tiles.Select(i => pairs[i].Item1).Concat(t.Bits.Select(Name)).ToArray())).ToList();
        }

        [Fact]
        public void AWeaponShowsItsDamageWeightQualityAndWearThenHowItFightsAndIsMade()
        {
            var weapon = Item(
                new[] { ("Type", ""), ("Weight", ""), ("Quality", ""), ("Portals", ""), ("Damage", ""), ("Per quality", ""), ("Skill", "item stats"), ("Block", "item stats"),
                    ("Durability", "item stats"), ("Repaired at", "item stats"), ("Fire damage causes", "") },
                new[] { ("Made at forge", "recipe") },
                FactBlock.Where, FactBlock.Uses, FactBlock.Hooks);
            Assert.Equal(new[] { null, "Fight", "Making", "Where it comes from", "What it is used for", null }, weapon.Select(t => t.Heading));
            Assert.Equal(new[] { "Damage", "Weight", "Quality", "Durability" }, weapon[0].Shown);
            Assert.Equal(new[] { "Per quality", "Block", "Fire damage causes" }, weapon[1].Shown);
            Assert.Equal(new[] { "Type", "Portals", "Skill", "Repaired at", "row Made at forge" }, weapon[2].Shown);
            Assert.Equal(new[] { "block Where" }, weapon[3].Shown);
            Assert.Equal(new[] { "block Uses" }, weapon[4].Shown);
        }

        [Fact]
        public void ArmourShowsItsArmourAndMovementAndWhatItDoesWorn()
        {
            var armour = Item(
                new[] { ("Type", ""), ("Weight", ""), ("Quality", ""), ("Armour", ""), ("Durability", "item stats"), ("Movement", ""), ("Set bonus", ""), ("When worn", ""), ("Stamina use", "gear") },
                new[] { (ItemWords.DamageTaken(true), "resistances") });
            Assert.Equal(new[] { null, "Wearing", "Making" }, armour.Select(t => t.Heading));
            Assert.Equal(new[] { "Armour", "Weight", "Quality", "Movement" }, armour[0].Shown);
            Assert.Equal(new[] { "Set bonus", "When worn", "Stamina use", "row " + ItemWords.DamageTaken(true) }, armour[1].Shown);
            Assert.Equal(new[] { "Type", "Durability" }, armour[2].Shown);
        }

        [Fact]
        public void FoodShowsWhatItGivesAndAMaterialItsWeightAndStack()
        {
            var food = Item(new[] { ("Type", ""), ("Weight", ""), ("Food", ""), ("Heals", ""), ("Lasts", ""), ("When used", "") }, new (string, string)[0]);
            Assert.Equal(new[] { "Food", "Heals", "Lasts", "Weight" }, food[0].Shown);
            Assert.Equal("Food", food[1].Heading);
            Assert.Equal(new[] { "When used" }, food[1].Shown);

            var wood = Item(new[] { ("Type", ""), ("Weight", ""), ("Worth", ""), ("Stacks to", ""), ("Portals", "") }, new (string, string)[0]);
            Assert.Equal(new[] { "Type", "Weight", "Stacks to", "Worth" }, wood[0].Shown);
            Assert.Equal("Making", wood[1].Heading);
            Assert.Equal(new[] { "Portals" }, wood[1].Shown);
        }

        [Fact]
        public void ANoteReadsItsLabelThenItsValue()
        {
            Assert.Equal("Drops with stars: more with each star", FactWords.Note("Drops with stars", "more with each star"));
        }

        [Fact]
        public void AKindNotYetLaidOutKeepsItsOldPage()
        {
            Assert.Null(FactLayout.Plan(Kind.Sound, new[] { ("Length", "") }, new (string, string)[0], new FactBlock[0]));
        }
    }
}
