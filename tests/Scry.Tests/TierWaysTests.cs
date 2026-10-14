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
    }
}
