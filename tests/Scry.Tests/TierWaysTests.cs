using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class TierWaysTests
    {
        // The ways things are made and raids come, as the milestone tiers count them: each recipe,
        // each piece's cost and each thing a station turns into another is a way of its own, and a
        // raid comes no sooner than what it waits for.

        private static readonly Dictionary<string, int> BossKeys = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["defeated_eikthyr"] = 1,
            ["defeated_gdking"] = 2,
            ["defeated_bonemass"] = 3,
        };

        /// <summary>Names that are no thing of their own: making by hand, a station no mod made a thing of, a mod's hidden ingredient.</summary>
        private static readonly HashSet<string> NoThing = new HashSet<string>(StringComparer.Ordinal) { "hand", "workbench", "ModsSecretString" };

        /// <summary>A pass knowing a few things by tier; every name is a thing of its own, its own key, but those of <see cref="NoThing"/>.</summary>
        private static (TierPass Pass, Func<string, string> KeyOf) Known(params (string Name, int Tier)[] things)
        {
            var pass = new TierPass();
            foreach (var (name, tier) in things) pass.Add(name, tier);
            return (pass, n => string.IsNullOrEmpty(n) || NoThing.Contains(n) ? null : n);
        }

        [Fact]
        public void EachRecipeForAThingIsAWayOfItsOwn()
        {
            var (pass, keyOf) = Known(("Copper", 2), ("Tin", 2), ("Eitr", 6), ("forge", 2));
            TierWays.Making(pass, new MakingWay("Bronze", "forge", ("Copper", 2), ("Tin", 1)), keyOf);
            TierWays.Making(pass, new MakingWay("Bronze", "forge", ("Eitr", 1)), keyOf);

            Assert.Equal(2, pass.Work()["Bronze"]);
        }

        [Fact]
        public void EachThingAStationTurnsIntoAnotherIsAWayOfItsOwn()
        {
            // A smelter turns ore or scrap into copper: either will do.
            var (pass, keyOf) = Known(("CopperOre", 2), ("CopperScrap", 6), ("smelter", 2));
            TierWays.Making(pass, new MakingWay("Copper", "smelter", ("CopperOre", 1)), keyOf);
            TierWays.Making(pass, new MakingWay("Copper", "smelter", ("CopperScrap", 1)), keyOf);

            Assert.Equal(2, pass.Work()["Copper"]);
        }

        [Fact]
        public void WhatARecipeAsksForOnlyWhenUpgradingDoesNotCount()
        {
            // Crafting takes each ingredient's first amount; one of none is asked for only by an upgrade.
            var (pass, keyOf) = Known(("Iron", 3), ("Wood", 1), ("Eitr", 6), ("forge", 2));
            TierWays.Making(pass, new MakingWay("SwordIron", "forge", ("Iron", 20), ("Wood", 2), ("Eitr", 0)), keyOf);

            Assert.Equal(3, pass.Work()["SwordIron"]);
        }

        [Fact]
        public void AStationTakingAnyOneOfItsItemsMakesItFromEachAlone()
        {
            var smelting = new Making { Station = "smelter", Output = "Copper", AnyOne = true };
            smelting.Inputs.Add(("CopperOre", 1));
            smelting.Inputs.Add(("CopperScrap", 1));
            var burning = new Making { Station = "incinerator", Output = "Coal" };
            burning.Inputs.Add(("Wood", 2));
            burning.Inputs.Add(("Resin", 1));

            var fromEither = TierWays.Of(smelting);
            Assert.Equal(new[] { "CopperOre", "CopperScrap" }, fromEither.Select(w => w.Needs.Single().Name));
            Assert.All(fromEither, w => Assert.Equal(("Copper", "smelter"), (w.Target, w.Place)));
            var fromBoth = Assert.Single(TierWays.Of(burning));
            Assert.Equal(new[] { ("Wood", 2), ("Resin", 1) }, fromBoth.Needs);
            Assert.Equal("incinerator", fromBoth.Place);
        }

        [Fact]
        public void AWayNeedsItsStationWhereThatIsAThingOfItsOwn()
        {
            var (pass, keyOf) = Known(("Wood", 1), ("Stone", 1), ("blackforge", 6));
            TierWays.Making(pass, new MakingWay("Club", "hand", ("Wood", 6)), keyOf);
            TierWays.Making(pass, new MakingWay("Mace", "blackforge", ("Wood", 6), ("Stone", 1)), keyOf);
            TierWays.Making(pass, new MakingWay("Torch", null, ("Wood", 1)), keyOf);

            var tiers = pass.Work();
            Assert.Equal(1, tiers["Club"]);
            Assert.Equal(6, tiers["Mace"]);
            Assert.Equal(1, tiers["Torch"]);
        }

        [Fact]
        public void WhatIsNoThingOfItsOwnIsLeftOutOfAWay()
        {
            var (pass, keyOf) = Known(("Wood", 1));
            TierWays.Making(pass, new MakingWay("Bow", "workbench", ("Wood", 10), ("ModsSecretString", 8)), keyOf);
            TierWays.Making(pass, new MakingWay("ModsThing", "workbench", ("Wood", 1)), n => n == "ModsThing" ? null : keyOf(n));

            var tiers = pass.Work();
            Assert.Equal(1, tiers["Bow"]);
            Assert.False(tiers.ContainsKey("ModsThing"));
        }

        [Fact]
        public void AWorldWideRaidComesOnceTheBossesItsKeysMarkHaveFallen()
        {
            var ready = TierWays.Ready(false, new[] { "defeated_gdking" }, new[] { "TrophyTroll" }, new[] { "defeated_eikthyr" }, Array.Empty<string>());
            var (pass, keyOf) = Known(("TrophyTroll", 2));
            TierWays.Raid(pass, "army_theelder", new[] { 1, 2 }, ready, BossKeys, keyOf);

            // The player's own conditions do not count for a raid the world's keys start.
            Assert.Single(ready);
            Assert.Equal(new[] { "defeated_gdking" }, ready[0].Keys);
            Assert.Empty(ready[0].Items);
            Assert.Equal(2, pass.Work()["army_theelder"]);
        }

        [Fact]
        public void APlayersRaidComesByAnyItemKnownOrAnyKeyHad()
        {
            var ready = TierWays.Ready(true, new[] { "defeated_bonemass" }, new[] { "TrophyTroll" }, new[] { "defeated_bonemass" }, new[] { "defeated_eikthyr" });
            var (pass, keyOf) = Known(("TrophyTroll", 2));
            TierWays.Raid(pass, "foresttrolls", new[] { 1 }, ready, BossKeys, keyOf);

            // Either does; the keys every player needs count only for a raid naming neither.
            Assert.Equal(2, ready.Count);
            Assert.Equal(2, pass.Work()["foresttrolls"]);
        }

        [Fact]
        public void APlayersRaidNamingNoItemNorAnyKeyNeedsAllItsKeys()
        {
            var ready = TierWays.Ready(true, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), new[] { "defeated_eikthyr", "defeated_gdking" });
            var (pass, keyOf) = Known();
            TierWays.Raid(pass, "army_gdking", new[] { 1 }, ready, BossKeys, keyOf);

            Assert.Single(ready);
            Assert.Equal(2, pass.Work()["army_gdking"]);
        }

        [Fact]
        public void ARaidComesInTheEarliestBiomeItCanAndAKeyMarkingNoBossAddsNothing()
        {
            var ready = TierWays.Ready(false, new[] { "KilledTroll" }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());
            var (pass, keyOf) = Known();
            TierWays.Raid(pass, "skeletons", new[] { 3, 1, Tiers.None }, ready, BossKeys, keyOf);
            TierWays.Raid(pass, "nowhere", Array.Empty<int>(), ready, BossKeys, keyOf);
            // A key marking an earlier boss leaves it where its biome puts it.
            var early = TierWays.Ready(false, new[] { "defeated_eikthyr" }, null, null, null);
            TierWays.Raid(pass, "army_bonemass", new[] { 3 }, early, BossKeys, keyOf);

            var tiers = pass.Work();
            Assert.Equal(1, tiers["skeletons"]);
            Assert.False(tiers.ContainsKey("nowhere"));
            Assert.Equal(3, tiers["army_bonemass"]);
        }

        [Fact]
        public void TheCheapestWayInTellsWhyAThingHasItsTier()
        {
            var (pass, keyOf) = Known(("Iron", 3), ("Wood", 1), ("forge", 2), ("Eitr", 6));
            TierWays.Making(pass, new MakingWay("SwordIron", "forge", ("Eitr", 1)), keyOf);
            TierWays.Making(pass, new MakingWay("SwordIron", "forge", ("Iron", 20), ("Wood", 2)), keyOf);
            var tiers = pass.Work();

            var way = pass.WayIn("SwordIron", tiers);
            Assert.NotNull(way);
            Assert.Equal(0, way.Value.Tier);
            Assert.Equal(new[] { "Iron", "Wood", "forge" }, way.Value.Needs);
            Assert.Equal(3, pass.WayIn("Iron", tiers).Value.Tier);
            Assert.Null(pass.WayIn("Nothing", tiers));
        }

        [Fact]
        public void AThingMadeFromItselfIsNoWayToItSoAModsRecipeCannotMakeItFree()
        {
            var (pass, keyOf) = Known(("MeadBaseEitrMinor", 6));
            TierWays.Making(pass, new MakingWay("MeadEitrMinor", null, ("MeadEitrMinor", 1)), keyOf);
            TierWays.Making(pass, new MakingWay("MeadEitrMinor", "workbench", ("MeadBaseEitrMinor", 1)), keyOf, WayFrom.Station);
            Assert.Equal(6, pass.Work()["MeadEitrMinor"]);

            // Made from itself alone, nothing leads to it.
            var (alone, aloneKeyOf) = Known();
            TierWays.Making(alone, new MakingWay("MeadTrollPheromones", null, ("MeadTrollPheromones", 1)), aloneKeyOf);
            Assert.False(alone.Work().ContainsKey("MeadTrollPheromones"));
        }

        [Fact]
        public void APieceIsMadeOnlyWithAToolThatBuildsItSoAWorldChestsCostGivesNothing()
        {
            var (pass, keyOf) = Known(("Wood", 1), ("Hammer", 1), ("piece_workbench", 1), ("Cultivator", 2), ("HammerOfMods", 4));
            // Built with the hammer at a workbench: its cost, its station and the hammer.
            TierWays.Building(pass, MakingWay.Piece("piece_chest", "piece_workbench", ("Wood", 10)), new[] { "Hammer" }, keyOf);
            // A world chest's cost only tells what breaking it gives back: no tool builds it.
            TierWays.Building(pass, MakingWay.Piece("TreasureChest_heath_hildir", "piece_workbench", ("Wood", 10)), Array.Empty<string>(), keyOf);
            // Planted with the cultivator; built with either of two tools, the cheaper one.
            TierWays.Building(pass, MakingWay.Piece("sapling", null, ("Wood", 1)), new[] { "Cultivator" }, keyOf);
            TierWays.Building(pass, MakingWay.Piece("bench", null, ("Wood", 4)), new[] { "HammerOfMods", "Hammer" }, keyOf);
            var tiers = pass.Work();

            Assert.Equal(1, tiers["piece_chest"]);
            Assert.False(tiers.ContainsKey("TreasureChest_heath_hildir"));
            Assert.Equal(2, tiers["sapling"]);
            Assert.Equal(1, tiers["bench"]);
            Assert.Contains("Hammer", pass.WayIn("piece_chest", tiers).Value.Needs);
            Assert.True(MakingWay.Piece("bench", null).Built);
            Assert.False(new MakingWay("SwordIron", "forge").Built);
        }

        [Fact]
        public void ARaidComesNoSoonerThanTheWeakestCreatureItBrings()
        {
            var (pass, keyOf) = Known(("Boar", 1), ("Goblin", 5), ("Seeker", 6));
            // Started by a place, not by conditions of its own: it seems open in every biome, its creatures tell when it is met.
            var open = TierWays.Ready(false, Array.Empty<string>(), null, null, null);
            TierWays.Raid(pass, "hildirboss3", new[] { 1 }, open, BossKeys, keyOf, new[] { "Goblin", "Seeker" });
            TierWays.Raid(pass, "mixed", new[] { 1 }, open, BossKeys, keyOf, new[] { "Seeker", "Boar" });
            // Its conditions still hold it back where they wait longer than its creatures.
            var late = TierWays.Ready(false, new[] { "defeated_bonemass" }, null, null, null);
            TierWays.Raid(pass, "boars_late", new[] { 1 }, late, BossKeys, keyOf, new[] { "Boar" });
            // One whose creatures were not read keeps the tier its conditions give.
            TierWays.Raid(pass, "nobody", new[] { 2 }, open, BossKeys, keyOf, Array.Empty<string>());
            // A creature met only in that raid leaves it no way in.
            pass.Add(WayFrom.Spawned, "Brute", 0, "onlyhere");
            TierWays.Raid(pass, "onlyhere", new[] { 1 }, open, BossKeys, keyOf, new[] { "Brute" });
            var tiers = pass.Work();

            Assert.Equal(5, tiers["hildirboss3"]);
            Assert.Equal(1, tiers["mixed"]);
            Assert.Equal(3, tiers["boars_late"]);
            Assert.Equal(2, tiers["nobody"]);
            Assert.False(tiers.ContainsKey("onlyhere"));
            Assert.False(tiers.ContainsKey("Brute"));
        }

        [Fact]
        public void ATierSetThroughALoopTracesToTheWayThatSetItNotTheLoop()
        {
            var pass = new TierPass();
            // Berries come from the bush and the bush is planted with berries; the forest is the way in.
            pass.Add(WayFrom.Source, "Berries", 0, "Bush");
            pass.Add(WayFrom.Biome, "Berries", 2);
            pass.Add(WayFrom.Crafted, "Bush", 0, "Berries");
            pass.Add(WayFrom.Biome, "Bush", 2);
            var tiers = pass.Work();

            Assert.Equal((2, 2), (tiers["Berries"], tiers["Bush"]));
            Assert.Equal(WayFrom.Biome, pass.WayIn("Berries", tiers).Value.From);
            // From the bush, the ways that set the tiers end at the forest within the two of them.
            var key = "Bush";
            for (var step = 0; step < 2 && pass.WayIn(key, tiers).Value.Needs.Length > 0; step++) key = pass.WayIn(key, tiers).Value.Needs[0];
            Assert.Equal(WayFrom.Biome, pass.WayIn(key, tiers).Value.From);
        }

        [Fact]
        public void TheWayInTellsWhereItComesFromSoAThingTooEarlyCanBeTracedToIt()
        {
            var (pass, keyOf) = Known(("Wood", 1), ("forge", 2), ("Iron", 3));
            pass.Add(WayFrom.Biome, "Boar", 1);
            TierWays.Making(pass, new MakingWay("Club", null, ("Wood", 6)), keyOf, WayFrom.Crafted);
            TierWays.Making(pass, new MakingWay("Coal", "forge", ("Wood", 1)), keyOf, WayFrom.Station);
            // Every ingredient asked for in no amount: nothing is needed, so the way is free.
            TierWays.Making(pass, new MakingWay("Freebie", null, ("Iron", 0)), keyOf, WayFrom.Crafted);
            var ready = TierWays.Ready(false, new[] { "defeated_gdking" }, null, null, null);
            TierWays.Raid(pass, "army_theelder", new[] { 1 }, ready, BossKeys, keyOf);
            var tiers = pass.Work();

            Assert.Equal(WayFrom.Biome, pass.WayIn("Boar", tiers).Value.From);
            Assert.Equal(WayFrom.Crafted, pass.WayIn("Club", tiers).Value.From);
            Assert.Equal(WayFrom.Station, pass.WayIn("Coal", tiers).Value.From);
            Assert.Equal(WayFrom.Raid, pass.WayIn("army_theelder", tiers).Value.From);
            var free = pass.WayIn("Freebie", tiers).Value;
            Assert.Equal((0, WayFrom.Crafted), (free.Tier, free.From));
            Assert.Empty(free.Needs);
            // A way added without saying where it comes from says so.
            Assert.Equal(WayFrom.Unnamed, pass.WayIn("Wood", tiers).Value.From);
        }

        [Fact]
        public void AGuardianPowerComesNoSoonerThanTheTrophyItsStoneTakes()
        {
            // Every boss stone stands in the Meadows' start temple; the power waits for the boss's trophy.
            var (pass, keyOf) = Known(("BossStone_DragonQueen", 1), ("TrophyDragonQueen", 4));
            TierWays.Given(pass, "se:GP_Moder", "BossStone_DragonQueen", new[] { "TrophyDragonQueen" }, keyOf);

            var tiers = pass.Work();
            Assert.Equal(4, tiers["se:GP_Moder"]);
            var way = pass.WayIn("se:GP_Moder", tiers).Value;
            Assert.Equal(WayFrom.Given, way.From);
            Assert.Contains("TrophyDragonQueen", way.Needs);
        }

        [Fact]
        public void AStoneTakingAnyOfSeveralThingsGivesByTheCheapest()
        {
            var (pass, keyOf) = Known(("ModStone", 1), ("TrophyA", 5), ("TrophyB", 3));
            TierWays.Given(pass, "se:ModPower", "ModStone", new[] { "TrophyA", "TrophyB" }, keyOf);

            Assert.Equal(3, pass.Work()["se:ModPower"]);
        }

        [Fact]
        public void AGiverTakingNothingGivesByItself()
        {
            var (pass, keyOf) = Known(("piece_bed", 2));
            TierWays.Given(pass, "se:Resting", "piece_bed", Array.Empty<string>(), keyOf);
            TierWays.Given(pass, "se:Rested", "piece_bed", null, keyOf);

            var tiers = pass.Work();
            Assert.Equal(2, tiers["se:Resting"]);
            Assert.Equal(2, tiers["se:Rested"]);
        }

        [Fact]
        public void WhatAGiverTakesThatIsNoThingOfItsOwnIsLeftOut()
        {
            // A stone whose only trophy was not read gives as a giver taking nothing would.
            var (pass, keyOf) = Known(("BossStone_Mod", 1));
            TierWays.Given(pass, "se:GP_Mod", "BossStone_Mod", new[] { "ModsSecretString" }, keyOf);

            Assert.Equal(1, pass.Work()["se:GP_Mod"]);
        }

        [Fact]
        public void AGiverNoThingOfItsOwnGivesNothing()
        {
            var (pass, keyOf) = Known(("TrophyEikthyr", 1));
            TierWays.Given(pass, "se:GP_Eikthyr", "workbench", new[] { "TrophyEikthyr" }, keyOf);

            Assert.False(pass.Work().ContainsKey("se:GP_Eikthyr"));
        }
    }
}
