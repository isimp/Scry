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
            ("Health", ""), ("Faction", ""),
            ("Attack: Club", "attacks"), ("Hit on the head", "weak spots"), ("Moves", "behaviour"), ("Sees", "behaviour"),
            ("Gives up chasing", "behaviour"), ("Takes to tame", "behaviour"), ("Tameable", ""), ("Forsaken power", ""),
            ("Drops with stars", ""), ("In this world", "world settings"), ("Something new", "a mod's part"),
            ("Love", "breeding"), ("Ridden with", "riding"), ("Keeps its distance", "behaviour"),
        };

        private static readonly (string Title, string Part)[] TrollRows = { ("With stars", ""), ("Damage it takes", "resistances"), ("Eats", ""), ("Drops", ""), ("Seen dropping in your play (3 kills)", "") };

        private static List<FactTopicPlan> Troll(params FactBlock[] blocks) =>
            FactLayout.Plan(Kind.Creature, TrollPairs, TrollRows, blocks.Length > 0 ? blocks : new[] { FactBlock.Where, FactBlock.Biomes, FactBlock.Hooks });

        private static string Pair(int i) => TrollPairs[i].Label;

        private static string Bit(FactBit bit) => bit.Pair >= 0 ? Pair(bit.Pair) : bit.Row >= 0 ? "row " + TrollRows[bit.Row].Title : "block " + bit.Block;

        [Fact]
        public void ATameableCreatureTellsItsTamingFirstThenItsFightLootHomeAndSenses()
        {
            // Taming is why a tameable one's page is opened: right under its tiles.
            var plan = Troll();
            Assert.Equal(new[] { null, "Taming and breeding", "Riding", "Fight", "Loot", "Where it lives", "Senses and behaviour", "After it falls", "More", null }, plan.Select(t => t.Heading));

            // Its headline numbers as tiles, the boss and moves among them where told.
            Assert.Equal(new[] { "Health", "Faction", "Moves", "Tameable" }, plan[0].Tiles.Select(Pair));
            Assert.Equal(new[] { "Takes to tame", "Love", "row Eats" }, plan[1].Bits.Select(Bit));
            Assert.Equal(new[] { "Ridden with" }, plan[2].Bits.Select(Bit));
            Assert.Equal(new[] { "Attack: Club", "Hit on the head", "row With stars", "row Damage it takes" }, plan[3].Bits.Select(Bit));
            Assert.Equal(new[] { "block Where", "block Biomes" }, plan[5].Bits.Select(Bit));
            Assert.Equal(new[] { "Sees" }, plan[6].Tiles.Select(Pair));
            // A new fact of a reader's part goes where that part's facts go.
            Assert.Equal(new[] { "Gives up chasing", "Keeps its distance" }, plan[6].Bits.Select(Bit));
            Assert.Equal(new[] { "Forsaken power" }, plan[7].Bits.Select(Bit));
            Assert.Equal(new[] { "Something new" }, plan[8].Bits.Select(Bit));
            Assert.Equal(new[] { "block Hooks" }, plan[9].Bits.Select(Bit));
        }

        [Fact]
        public void AChainAThingIsAStepOfComesRightUnderItsTilesOnEveryKindLaidOut()
        {
            // Where it sits among what comes before and after it, before anything else it tells.
            foreach (var kind in new[] { Kind.Creature, Kind.Item, Kind.Piece, Kind.Resource, Kind.StatusEffect, Kind.Location })
            {
                var plan = FactLayout.Plan(kind, new[] { ("Health", ""), ("Lasts", ""), ("Biome", "") }, new[] { ("Drops", ""), (ChainWords.Breeding, "chain") }, new FactBlock[0]);
                var chain = plan.Single(t => t.Bits.Any(b => b.Row == 1));
                var at = plan.IndexOf(chain);
                Assert.Null(chain.Heading);
                Assert.Single(chain.Bits);
                // First, or right after the tiles.
                Assert.True(at == 0 || (at == 1 && plan[0].Heading == null && plan[0].Tiles.Count > 0), kind.ToString());
            }

            // The same in an order of its own: a boss's chain from offering to power.
            var boss = FactLayout.Plan(Kind.Creature, new[] { ("Health", ""), ("Boss", ""), ("Weak spots", "weak spots") }, new[] { (ChainWords.Summoning, "chain") }, new FactBlock[0]);
            Assert.Equal(new[] { null, null, "Fight" }, boss.Select(t => t.Heading));
            Assert.Equal(0, Assert.Single(boss[1].Bits).Row);
        }

        [Fact]
        public void WhatToBringIsPartOfACreaturesFight()
        {
            var plan = FactLayout.Plan(Kind.Creature, new[] { ("Health", "") }, new[] { ("Damage it takes", "resistances"), (SearchFight.BringTitle, "what to bring") }, new FactBlock[0]);
            Assert.Equal(new[] { 0, 1 }, plan.Single(t => t.Heading == "Fight").Bits.Select(b => b.Row));
        }

        [Fact]
        public void ACreatureTellsItsFightThenItsLootAndHomeTogetherThenItsSenses()
        {
            // What it drops and where it lives answer one question, where to farm it; how it
            // notices you comes after.
            var pairs = new[] { ("Health", ""), ("Weak spots", "weak spots"), ("Gives up chasing", "behaviour"), ("Drops", ""), ("Forsaken power", "") };
            var plan = FactLayout.Plan(Kind.Creature, pairs, new (string, string)[0], new[] { FactBlock.Where });
            Assert.Equal(new[] { null, "Fight", "Loot", "Where it lives", "Senses and behaviour", "After it falls" }, plan.Select(t => t.Heading));
        }

        [Fact]
        public void ABossTellsHowToSummonItAndWhatItsFallOpensBeforeItsLoot()
        {
            var pairs = new[] { ("Health", ""), ("Boss", ""), ("Weak spots", "weak spots"), ("Gives up chasing", "behaviour"), ("Drops", ""), ("Forsaken power", "") };
            var rows = new[] { ("Summoned at Eikthyr's altar", "summoning") };
            var plan = FactLayout.Plan(Kind.Creature, pairs, rows, new[] { FactBlock.Where });
            Assert.Equal(new[] { null, "Fight", "Where it lives", "After it falls", "Loot", "Senses and behaviour" }, plan.Select(t => t.Heading));
            Assert.Contains(plan[0].Tiles, i => pairs[i].Item1 == "Boss");

            // One that is both a boss and tameable takes the boss's order, the first of the two.
            var both = FactLayout.Plan(Kind.Creature, pairs.Append(("Takes to tame", "taming")).ToArray(), rows, new[] { FactBlock.Where });
            Assert.Equal(new[] { null, "Fight", "Where it lives", "After it falls", "Loot", "Senses and behaviour", "Taming and breeding" }, both.Select(t => t.Heading));
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
                new[] { ("Type", ""), ("Weight", ""), ("Quality", ""), ("Portals", ""), ("Damage", ""), ("Skill", "item stats"), ("Block", "item stats"),
                    ("Durability", "item stats"), ("Repaired at", "item stats"), ("Fire damage causes", "") },
                new[] { ("By quality", ""), ("Made at forge", "recipe") },
                FactBlock.Where, FactBlock.Uses, FactBlock.Hooks);
            Assert.Equal(new[] { null, "Fight", "Making", "Where it comes from", "What it is used for", null }, weapon.Select(t => t.Heading));
            Assert.Equal(new[] { "Damage", "Weight", "Quality", "Durability" }, weapon[0].Shown);
            Assert.Equal(new[] { "Block", "Fire damage causes", "row By quality" }, weapon[1].Shown);
            Assert.Equal(new[] { "Type", "Portals", "Skill", "Repaired at", "row Made at forge" }, weapon[2].Shown);
            Assert.Equal(new[] { "block Where" }, weapon[3].Shown);
            Assert.Equal(new[] { "block Uses" }, weapon[4].Shown);
        }

        [Fact]
        public void WhatAnItemIsForComesRightUnderItsTiles()
        {
            // What a trophy, a building tool, a fish, a bait or a key is for is the point of its
            // page; told first, not under More at the bottom.
            var trophy = Item(new[] { ("Type", ""), ("Weight", ""), ("On its boss stone", "") }, new (string, string)[0], FactBlock.Where);
            Assert.Equal(new[] { null, "What it does", "Where it comes from" }, trophy.Select(t => t.Heading));
            Assert.Equal(new[] { "On its boss stone" }, trophy[1].Shown);

            var hammer = Item(new[] { ("Type", ""), ("Weight", ""), ("Durability", "item stats") }, new[] { ("Builds on the Misc tab (12)", "builds"), ("Made at workbench", "recipe") });
            Assert.Equal(new[] { null, "What it does", "Making" }, hammer.Select(t => t.Heading));
            Assert.Equal(new[] { "row Builds on the Misc tab (12)" }, hammer[1].Shown);

            var fish = Item(new[] { ("Type", "") }, new[] { ("Bites on, when it reaches the hook", "fishing") });
            Assert.Equal(new[] { "row Bites on, when it reaches the hook" }, fish.Single(t => t.Heading == "What it does").Shown);
            var bait = Item(new[] { ("Type", "") }, new[] { ("Catches, when one reaches the hook", "bait") });
            Assert.Equal(new[] { "row Catches, when one reaches the hook" }, bait.Single(t => t.Heading == "What it does").Shown);
            var key = Item(new[] { ("Type", "") }, new[] { ("Opens", "") });
            Assert.Equal(new[] { "row Opens" }, key.Single(t => t.Heading == "What it does").Shown);

            // An egg's hatching comes next, before what a weapon or armour would show.
            var egg = Item(new[] { ("Type", ""), ("Hatches into", ""), ("Block", "item stats") }, new (string, string)[0]);
            Assert.Equal(new[] { null, "Hatching", "Fight" }, egg.Select(t => t.Heading));
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

        private static List<(string Heading, string[] Shown)> Piece((string, string)[] pairs, (string, string)[] rows, params FactBlock[] blocks)
        {
            var plan = FactLayout.Plan(Kind.Piece, pairs, rows, blocks);
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            return plan.Select(t => (t.Heading, t.Tiles.Select(i => pairs[i].Item1).Concat(t.Bits.Select(Name)).ToArray())).ToList();
        }

        [Fact]
        public void APieceShowsWhatItDoesThenHowItIsBuiltWhatItGivesForRestAndHowItStands()
        {
            var plan = Piece(
                new[]
                {
                    ("Comfort", "piece"), ("Comfort group", "piece"), ("Health", "piece"), ("Material", "piece"), ("Support", "support"), ("Support lost", "support"),
                    ("Rain", "weather"), ("Placed", "placement"), ("Stands near a fire", "placement"), ("Upgrades", "piece"), ("Sleeping in it", "piece"), ("Built with", "built with"), ("Makes", "station"),
                },
                new[] { ("Build cost", "piece"), ("Damage it takes", "resistances"), ("Made here", "station") },
                FactBlock.Where, FactBlock.Hooks);
            Assert.Equal(new[] { null, "What it does", "Building", "Resting", "Standing", "Where it comes from", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Health", "Comfort", "Material", "Support" }, plan[0].Shown);
            Assert.Equal(new[] { "Makes", "row Made here" }, plan[1].Shown);
            Assert.Equal(new[] { "Placed", "Stands near a fire", "Upgrades", "Built with", "row Build cost" }, plan[2].Shown);
            // Comfort, its group and sleeping all go to resting.
            Assert.Equal(new[] { "Comfort group", "Sleeping in it" }, plan[3].Shown);
            Assert.Equal(new[] { "Support lost", "Rain", "row Damage it takes" }, plan[4].Shown);
        }

        [Fact]
        public void APiecesTilesAreWhatMattersForWhatItIs()
        {
            // A chest, cart or ship by what it holds; a station by its reach; the rest by health, comfort, material and support.
            var chest = Piece(new[] { ("Health", "piece"), ("Material", "piece"), ("Support", "support"), ("Slots", "") }, new (string, string)[0]);
            Assert.Equal(new[] { "Slots", "Health", "Material" }, chest[0].Shown);
            var bench = Piece(new[] { ("Health", "piece"), ("Material", "piece"), ("Support", "support"), ("Building reach", "station") }, new (string, string)[0]);
            Assert.Equal(new[] { "Building reach", "Health", "Material" }, bench[0].Shown);
            var wall = Piece(new[] { ("Health", "piece"), ("Material", "piece"), ("Support", "support") }, new (string, string)[0]);
            Assert.Equal(new[] { "Health", "Material", "Support" }, wall[0].Shown);
        }

        [Fact]
        public void WhatAMachineAChestADoorOrAFireDoesIsToldFirstNotUnderMore()
        {
            // A ship: its slots and how it fares at sea.
            var ship = Piece(new[] { ("Health", "piece"), ("Slots", ""), ("Ashlands", "machines"), ("Capsized", "machines") }, new[] { ("Build cost", "piece") });
            Assert.Equal(new[] { null, "What it does", "Building" }, ship.Select(t => t.Heading));
            Assert.Equal(new[] { "Ashlands", "Capsized" }, ship[1].Shown.Where(s => s != "Slots").ToArray());
            Assert.Contains("Slots", ship.SelectMany(t => t.Shown));
            Assert.DoesNotContain("More", ship.Select(t => t.Heading));

            // A ballista's ammo, a fire's warmth, a door's key, a chest's contents.
            var ballista = Piece(new[] { ("Health", "piece"), ("Targets", "machines") }, new[] { ("Fires", "machines") });
            Assert.Equal(new[] { "Targets", "row Fires" }, ballista.Single(t => t.Heading == "What it does").Shown);
            var fire = Piece(new[] { ("Health", "piece"), ("Burns", "station"), ("Warmth", "areas"), ("Gives those in it", "areas") }, new (string, string)[0]);
            Assert.Equal(new[] { "Burns", "Warmth", "Gives those in it" }, fire.Single(t => t.Heading == "What it does").Shown);
            var door = Piece(new[] { ("Health", "piece"), ("Opened with", "") }, new (string, string)[0]);
            Assert.Equal(new[] { "Opened with" }, door.Single(t => t.Heading == "What it does").Shown);
            var chest = Piece(new[] { ("Health", "piece"), ("Slots", "") }, new[] { ("Holds", "chest") });
            Assert.Contains("row Holds", chest.Single(t => t.Heading == "What it does").Shown);
        }

        [Fact]
        public void AResourceShowsItsHealthAndToolThenWhatGatheringItGivesHowItGrowsAndWhereItLives()
        {
            var pairs = new[]
            {
                ("Health", "resource"), ("Needs tool tier", "resource"), ("Breaks into", "resource"), ("Grows back in", "resource"),
                ("Takes to grow", "resource"), ("Needs", "resource"), ("Tolerates", "resource"),
            };
            var rows = new[] { ("Gives", "resource"), ("Damage it takes", "resource"), (GatherWords.GrowsInto(false), "resource"), (GatherWords.GrowsInto(true), "resource") };
            var plan = FactLayout.Plan(Kind.Resource, pairs, rows, new[] { FactBlock.Where, FactBlock.Biomes, FactBlock.Hooks });
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            // A plant's growing apart from what gathering it gives: the farmer's facts, not the woodcutter's.
            Assert.Equal(new[] { null, "Gathering", "Growing", "Where it lives", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Health", "Needs tool tier" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Breaks into", "Grows back in", "row Gives", "row Damage it takes" }, plan[1].Bits.Select(Name));
            Assert.Equal(new[] { "Takes to grow", "Needs", "Tolerates", "row Grows into", "row Grows into one of" }, plan[2].Bits.Select(Name));
            Assert.Equal(new[] { "block Where", "block Biomes" }, plan[3].Bits.Select(Name));
        }

        [Fact]
        public void AProjectileShowsItsDamageThenHowItHitsAndFlies()
        {
            var pairs = new[]
            {
                ("Own damage", "projectile"), ("Hits", "projectile"), ("Knockback", "projectile"), ("Can be", "projectile"), ("On hit", "projectile"),
                ("Flies for", "projectile"), ("Falls", "projectile"), ("Bounces", "projectile"), ("After a hit", "projectile"), ("Leaves", "projectile"),
            };
            var plan = FactLayout.Plan(Kind.Projectile, pairs, new (string, string)[0], new[] { FactBlock.Where });
            // What fires it is where a visitor comes from, so it comes first.
            Assert.Equal(new[] { null, "Where it comes from", "Hit", "Flight" }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Own damage", "Flies for" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Hits", "Knockback", "Can be", "On hit", "After a hit" }, plan[2].Bits.Select(b => pairs[b.Pair].Item1));
            Assert.Equal(new[] { "Falls", "Bounces", "Leaves" }, plan[3].Bits.Select(b => pairs[b.Pair].Item1));
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
            // What it holds first, the reason to go; where it may be placed last, for the curious.
            Assert.Equal(new[] { null, "Contents", "Layout", "Music", "Placement", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Biome", "Per world" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Levels at its spawn points", "What its rooms hold", "row Its spawn points place", "row Chests and pickups", "row Built of",
                "row Its rooms hold, 3 of 12 kinds of room read", "row Loot in its rooms", "row Chests and pickups in its rooms" }, plan[1].Bits.Select(Name));
            Assert.Equal(new[] { "Building", "Laid out", "Picks rooms", "row Doors in half of doorways", "row Built of 12 kinds of room" }, plan[2].Bits.Select(Name));
            Assert.Equal(new[] { "Music" }, plan[3].Bits.Select(Name));
            Assert.Equal(new[] { "Above the sea", "Placed", "block Biomes" }, plan[4].Bits.Select(Name));
        }

        [Fact]
        public void ARoomShowsItsSizeThenWhatItIsAndHowItJoinsTheRest()
        {
            var pairs = new[] { ("Is", "room"), ("Size", "room"), ("Doorways", "room"), ("Not before", "room"), ("Built into", "room") };
            var plan = FactLayout.Plan(Kind.Location, pairs, new (string, string)[0], new FactBlock[0]);
            // The dungeon it is built into comes first.
            Assert.Equal(new[] { null, "Layout", "Placement" }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Size" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Doorways", "Built into" }, plan[1].Bits.Select(b => pairs[b.Pair].Item1));
            Assert.Equal(new[] { "Is", "Not before" }, plan[2].Bits.Select(b => pairs[b.Pair].Item1));
            // Told as a row of the dungeons it is built into, the same; before what the room holds, where a dungeon's own come first.
            var asRow = FactLayout.Plan(Kind.Location, new[] { ("Is", "room"), ("Size", "room"), ("What it holds", "location") }, new[] { ("Built into", "room") }, new FactBlock[0]);
            Assert.Equal(new[] { null, "Layout", "Contents", "Placement" }, asRow.Select(t => t.Heading));
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
            // A raid's page is opened while it comes: what comes first, then why now.
            Assert.Equal(new[] { null, "What comes", "When", "Music" }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Lasts", "Weather", "Comes for" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Keeps coming", "Troll" }, plan[1].Bits.Select(b => pairs[b.Pair].Item1));
            Assert.Equal(new[] { "On the table", "Rolled", "Also rolled", "Ends with" }, plan[2].Bits.Where(b => b.Pair >= 0).Select(b => pairs[b.Pair].Item1));
            Assert.Equal(FactBlock.Biomes, plan[2].Bits.Last().Block);
            Assert.Equal(new[] { "Music" }, plan[3].Bits.Select(b => pairs[b.Pair].Item1));
        }

        [Fact]
        public void ABiomeShowsWhatItPutsOnYouAndHowManyLiveThereThenWhatIsThereItsWeathersAndMusic()
        {
            var pairs = new[] { ("Creatures", "biome"), ("Puts on you", "biome"), ("Places", "biome") };
            var rows = new[] { ("Weathers", "biome"), ("Music", "biome"), ("Lives here (12)", "biome"), ("Grows here (8)", "biome") };
            var plan = FactLayout.Plan(Kind.Biome, pairs, rows, new[] { FactBlock.Hooks });
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            // How dangerous it is and what is there first; its weathers' shares and music are flavour.
            Assert.Equal(new[] { null, "What is there", "Weather", "Music", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Puts on you", "Creatures", "Places" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "row Lives here (12)", "row Grows here (8)" }, plan[1].Bits.Select(Name));
            Assert.Equal(new[] { "row Weathers" }, plan[2].Bits.Select(Name));
            Assert.Equal(new[] { "row Music" }, plan[3].Bits.Select(Name));
        }

        [Fact]
        public void AStatusEffectShowsHowLongItLastsThenWhatItChanges()
        {
            var pairs = new[] { ("Lasts", "status effect"), ("Health regen", "status effect"), ("Resists", "status effect") };
            var rows = new[] { ("While it lasts, cannot take", "status effect") };
            var plan = FactLayout.Plan(Kind.StatusEffect, pairs, rows, new[] { FactBlock.Users, FactBlock.Hooks });
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            Assert.Equal(new[] { null, "Changes", "With other effects", "More", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Lasts" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Health regen", "Resists" }, plan[1].Bits.Select(Name));
            Assert.Equal(new[] { "row While it lasts, cannot take" }, plan[2].Bits.Select(Name));
            Assert.Equal(new[] { "block Users" }, plan[3].Bits.Select(Name));
        }

        [Fact]
        public void AModShowsItsVersionAndMakerThenAboutItItsTiesAndWhatItAdds()
        {
            var pairs = new[]
            {
                ("By", "mod"), ("Website", "mod"), ("Version", "mod"), ("Id", "mod"), ("Folder", "mod"), ("Will not run with", "mod"),
                ("Adds", "mod"), ("Hooks into", "mod"), ("Station: Forge", "mod"),
            };
            var rows = new[] { ("Will not run with (1)", "mod"), ("Needs (2)", "mod"), ("Needed by (3)", "mod"), ("Works with, when there (1)", "mod"), ("Adds 12 items", "mod") };
            var plan = FactLayout.Plan(Kind.Mod, pairs, rows, new[] { FactBlock.Hooks });
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            // What it adds, and what it hooks into, which is the question when something breaks.
            Assert.Equal(new[] { null, "What it adds", "What it hooks into", "Ties to other mods", "About", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Version", "By" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Adds", "Station: Forge", "row Adds 12 items" }, plan[1].Bits.Select(Name));
            Assert.Equal(new[] { "Hooks into" }, plan[2].Bits.Select(Name));
            Assert.Equal(new[] { "Will not run with", "row Will not run with (1)", "row Needs (2)", "row Needed by (3)", "row Works with, when there (1)" }, plan[3].Bits.Select(Name));
            Assert.Equal(new[] { "Website", "Id", "Folder" }, plan[4].Bits.Select(Name));
        }

        [Fact]
        public void ASpawnerShowsItsPaceThenWhatItSpawnsHowItBreaksAndWhereItIs()
        {
            var pairs = new[]
            {
                ("Spawns Greydwarf", "spawner"), ("Spawns Greydwarf brute", "spawner"), ("Star chance", "spawner"), ("Works", "spawner"), ("Pace", "spawner"),
                ("Keeps alive", "spawner"), ("Puts them", "spawner"), ("Health", "resource"), ("Needs tool tier", "resource"),
            };
            var rows = new[] { ("Drops", "resource"), ("Damage it takes", "resource") };
            var plan = FactLayout.Plan(Kind.Spawner, pairs, rows, new[] { FactBlock.Where, FactBlock.Biomes, FactBlock.Hooks });
            string Name(FactBit b) => b.Pair >= 0 ? pairs[b.Pair].Item1 : b.Row >= 0 ? "row " + rows[b.Row].Item1 : "block " + b.Block;
            Assert.Equal(new[] { null, "Spawns", "Breaking it", "Where it is", null }, plan.Select(t => t.Heading));
            Assert.Equal(new[] { "Pace", "Keeps alive", "Health" }, plan[0].Tiles.Select(i => pairs[i].Item1));
            Assert.Equal(new[] { "Spawns Greydwarf", "Spawns Greydwarf brute", "Star chance", "Works", "Puts them" }, plan[1].Bits.Select(Name));
            Assert.Equal(new[] { "Needs tool tier", "row Drops", "row Damage it takes" }, plan[2].Bits.Select(Name));
            Assert.Equal(new[] { "block Where", "block Biomes" }, plan[3].Bits.Select(Name));
        }

        [Fact]
        public void AStatusEffectSaysHowYouGetItRightUnderHowLongItLasts()
        {
            // What gives it was only under Linked, sections below; it is the second thing anyone asks.
            var plan = FactLayout.Plan(Kind.StatusEffect, new[] { ("Lasts", "status effect"), ("Health regen", "status effect") }, new (string, string)[0], new FactBlock[0],
                new[] { LinkWords.GivenBy });
            Assert.Equal(new[] { null, "How you get it", "Changes" }, plan.Select(t => t.Heading));
            Assert.Equal(LinkWords.GivenBy, Assert.Single(plan[1].Bits).LinkGroup);
            Assert.True(FactLayout.Places(Kind.StatusEffect, LinkWords.GivenBy));
        }

        [Fact]
        public void LinkGroupsGoToTheTopicTheyBelongToAndTheRestStayInLinked()
        {
            // A creature's carried items and what its attacks put on you are part of its fight,
            // after the attacks table; its footsteps stay under Linked.
            var creature = FactLayout.Plan(Kind.Creature, new[] { ("Health", "") }, new[] { (CombatWords.AttacksTitle, "attacks") }, new FactBlock[0],
                new[] { LinkWords.Footsteps, LinkWords.Carries, LinkWords.StatusEffects });
            var fight = creature.Single(t => t.Heading == "Fight");
            Assert.Equal(new[] { null, LinkWords.Carries, LinkWords.StatusEffects }, fight.Bits.Select(b => b.LinkGroup));
            Assert.False(FactLayout.Places(Kind.Creature, LinkWords.Footsteps));
            Assert.DoesNotContain(creature.SelectMany(t => t.Bits), b => b.LinkGroup == LinkWords.Footsteps);

            // What a biome's weather puts on you goes with its weathers.
            var biome = FactLayout.Plan(Kind.Biome, new (string, string)[0], new[] { (BiomeWords.WeathersTitle, "biome") }, new FactBlock[0], new[] { LinkWords.StatusEffects });
            Assert.Equal(new[] { null, LinkWords.StatusEffects }, biome.Single(t => t.Heading == "Weather").Bits.Select(b => b.LinkGroup));

            // A kind with no plan places none.
            Assert.False(FactLayout.Places(Kind.Sound, LinkWords.Carries));
        }

        [Fact]
        public void LinkedShowsEachLinkOnceLeavingOutWhatTheTopicsShow()
        {
            var shown = new HashSet<string> { "Wood", "se:Rested" };
            // A group a topic places shows there, not under Linked too.
            Assert.Empty(FactLayout.LeftInLinked(Kind.StatusEffect, LinkWords.GivenBy, new[] { "Bed", "Fire" }, shown));
            // Of the rest, what In the game already links is left out; a row left with none goes.
            Assert.Equal(new[] { 0, 2 }, FactLayout.LeftInLinked(Kind.Piece, LinkWords.Upgrades, new[] { "Chopping block", "Wood", "Tanning rack" }, shown));
            Assert.Empty(FactLayout.LeftInLinked(Kind.Item, LinkWords.Items, new[] { "se:Rested", "Wood" }, shown));
            Assert.Equal(new[] { 0, 1 }, FactLayout.LeftInLinked(Kind.Item, LinkWords.Items, new[] { "Stone", "Flint" }, shown));
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
