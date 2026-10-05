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
        public void APieceShowsHowItIsBuiltThenHowItStandsItsComfortAndWhatItDoesAsAStation()
        {
            var pairs = new[]
            {
                ("Comfort", "piece"), ("Comfort group", "piece"), ("Health", "piece"), ("Material", "piece"), ("Support", "support"), ("Support lost", "support"),
                ("Rain", "weather"), ("Placed", "placement"), ("Stands near a fire", "placement"), ("Upgrades", "piece"), ("Sleeping in it", "piece"), ("Built with", "built with"), ("Makes", "station"),
            };
            var rows = new[] { ("Build cost", "piece"), ("Damage it takes", "resistances"), ("Made here", "station") };
            var plan = FactLayout.Plan(Kind.Piece, pairs, rows, new[] { FactBlock.Where, FactBlock.Hooks });
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            Assert.Equal(new[] { null, "Building", "Standing", "Comfort", "Sleeping", "As a station", "Where it comes from", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Health", "Comfort", "Material", "Support" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Placed", "Stands near a fire", "Upgrades", "Built with", "row Build cost" }, plan[1].Bits.Select(Name));
            Assert.Equal(new[] { "Support lost", "Rain", "row Damage it takes" }, plan[2].Bits.Select(Name));
            Assert.Equal(new[] { "Comfort group" }, plan[3].Bits.Select(Name));
            Assert.Equal(new[] { "Sleeping in it" }, plan[4].Bits.Select(Name));
            Assert.Equal(new[] { "Makes", "row Made here" }, plan[5].Bits.Select(Name));
        }

        [Fact]
        public void AResourceShowsItsHealthAndToolThenWhatGatheringItGivesAndWhereItGrows()
        {
            var pairs = new[] { ("Health", "resource"), ("Needs tool tier", "resource"), ("Breaks into", "resource"), ("Grows back in", "resource") };
            var rows = new[] { ("Gives", "resource"), ("Damage it takes", "resource") };
            var plan = FactLayout.Plan(Kind.Resource, pairs, rows, new[] { FactBlock.Where, FactBlock.Biomes, FactBlock.Hooks });
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            Assert.Equal(new[] { null, "Gathering", "Where it lives", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Health", "Needs tool tier" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Breaks into", "Grows back in", "row Gives", "row Damage it takes" }, plan[1].Bits.Select(Name));
            Assert.Equal(new[] { "block Where", "block Biomes" }, plan[2].Bits.Select(Name));
        }

        [Fact]
        public void AProjectileShowsItsDamageThenHowItHitsAndFlies()
        {
            var pairs = new[]
            {
                ("Own damage", "projectile"), ("Hits", "projectile"), ("Knockback", "projectile"), ("Can be", "projectile"), ("On hit", "projectile"),
                ("Flies for", "projectile"), ("Falls", "projectile"), ("Bounces", "projectile"), ("After a hit", "projectile"), ("Leaves", "projectile"),
            };
            var plan = FactLayout.Plan(Kind.Projectile, pairs, new (string, string)[0], new FactBlock[0]);
            Assert.Equal(new[] { null, "Hit", "Flight" }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Own damage", "Flies for" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Hits", "Knockback", "Can be", "On hit", "After a hit" }, plan[1].Bits.Select(b => pairs[b.Pair].Item1));
            Assert.Equal(new[] { "Falls", "Bounces", "Leaves" }, plan[2].Bits.Select(b => pairs[b.Pair].Item1));
        }

        [Fact]
        public void ADungeonShowsWhereItIsPlacedHowItIsLaidOutWhatItHoldsAndItsMusic()
        {
            var pairs = new[]
            {
                ("Per world", "placement"), ("Biome", "placement"), ("Above the sea", "placement"), ("Placed", "placement"),
                ("Levels at its spawn points", "location"), ("Building", "location"), ("Music", "location"),
                ("Laid out", "dungeon"), ("Picks rooms", "dungeon"), ("What its rooms hold", "dungeon"),
            };
            var rows = new[]
            {
                ("Its spawn points place", "location"), ("Chests and pickups", "location"), ("Built of", "location"), ("Doors in half of doorways", "dungeon"),
                ("Built of 12 kinds of room", "dungeon"), ("Its rooms hold, 3 of 12 kinds of room read", "dungeon"), ("Loot in its rooms", "dungeon"), ("Chests and pickups in its rooms", "dungeon"),
            };
            var plan = FactLayout.Plan(Kind.Location, pairs, rows, new[] { FactBlock.Biomes, FactBlock.Hooks });
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            Assert.Equal(new[] { null, "Placement", "Layout", "Contents", "Music", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Biome", "Per world" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Above the sea", "Placed", "block Biomes" }, plan[1].Bits.Select(Name));
            Assert.Equal(new[] { "Building", "Laid out", "Picks rooms", "row Doors in half of doorways", "row Built of 12 kinds of room" }, plan[2].Bits.Select(Name));
            Assert.Equal(new[] { "Levels at its spawn points", "What its rooms hold", "row Its spawn points place", "row Chests and pickups", "row Built of",
                "row Its rooms hold, 3 of 12 kinds of room read", "row Loot in its rooms", "row Chests and pickups in its rooms" }, plan[3].Bits.Select(Name));
            Assert.Equal(new[] { "Music" }, plan[4].Bits.Select(Name));
        }

        [Fact]
        public void ARoomShowsItsSizeThenWhatItIsAndHowItJoinsTheRest()
        {
            var pairs = new[] { ("Is", "room"), ("Size", "room"), ("Doorways", "room"), ("Not before", "room"), ("Built into", "room") };
            var plan = FactLayout.Plan(Kind.Location, pairs, new (string, string)[0], new FactBlock[0]);
            Assert.Equal(new[] { null, "Placement", "Layout" }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Size" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Is", "Not before" }, plan[1].Bits.Select(b => pairs[b.Pair].Item1));
            Assert.Equal(new[] { "Doorways", "Built into" }, plan[2].Bits.Select(b => pairs[b.Pair].Item1));
        }

        [Fact]
        public void ARaidShowsHowLongItLastsThenWhenItComesWhatItBringsAndItsMusic()
        {
            var pairs = new[]
            {
                ("Comes for", "raid"), ("On the table", "raid"), ("Rolled", "raid"), ("Also rolled", "raid"), ("Lasts", "raid"), ("Keeps coming", "raid"),
                ("Ends with", "raid"), ("Music", "raid"), ("Weather", "raid"), ("Troll", "raid"),
            };
            var plan = FactLayout.Plan(Kind.Raid, pairs, new (string, string)[0], new[] { FactBlock.Biomes });
            Assert.Equal(new[] { null, "When", "What comes", "Music" }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Lasts", "Weather", "Comes for" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "On the table", "Rolled", "Also rolled", "Ends with" }, plan[1].Bits.Where(b => b.Pair >= 0).Select(b => pairs[b.Pair].Item1));
            Assert.Equal(FactBlock.Biomes, plan[1].Bits.Last().Block);
            Assert.Equal(new[] { "Keeps coming", "Troll" }, plan[2].Bits.Select(b => pairs[b.Pair].Item1));
            Assert.Equal(new[] { "Music" }, plan[3].Bits.Select(b => pairs[b.Pair].Item1));
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
