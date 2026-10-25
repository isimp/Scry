using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class TraderTests
    {
        // A trader (Haldor, Hildir, the Bog Witch, a mod's) is a kind of its own with a tab, and a
        // player's kind, so the compendium lists it. Its page sells what its own list holds, what
        // mods add to its store as it opens, and the upgrades the game sells as keys of the
        // character (Wider and Deeper Pockets), each told bought once the character has it. With
        // the compendium on, wares waiting for a boss not yet fallen are not named: under the
        // loose rules each such row keeps its title with how many, under the strict rules they
        // are only counted at the section's end.

        private static Ware Item(string prefab, int price, string key = "") => new Ware { Prefab = prefab, Name = prefab, Price = price, Stack = 1, Key = key };

        private static Ware Upgrade(string name, string buyKey, int price, string key = "", bool bought = false) =>
            new Ware { Name = name, BuyKey = buyKey, Price = price, Stack = 1, Key = key, Bought = bought };

        [Fact]
        public void ATraderIsAKindOfItsOwnWithATabAndAPlayersKind()
        {
            Assert.Equal(Kind.Trader, Kinds.Of(new PrefabTraits { HasTrader = true, HasRenderer = true, HasSolidCollider = true }));
            // One a mod makes a creature stays a trader, its store being what it is for.
            Assert.Equal(Kind.Trader, Kinds.Of(new PrefabTraits { HasTrader = true, HasCharacter = true }));
            Assert.Equal("Traders", Kinds.Label(Kind.Trader));
            Assert.True(Search.KindMatches(Kind.Trader, "trader"));
            Assert.True(Kinds.ForPlayers(Kind.Trader));
            Assert.True(CompendiumView.Counted(Kind.Trader));
        }

        [Fact]
        public void WhatModsAddToTheStoreJoinsTheTradersOwnListOnceEach()
        {
            var own = new[] { Item("HelmetYule", 100), Item("YmirRemains", 120, "defeated_gdking"), Upgrade("Deeper Pockets", "invrows2", 1000, "defeated_bonemass") };
            // As the store opens: the own wares its key lets through, and what mods add on the fly.
            var live = new[] { Item("HelmetYule", 100), Item("skaldsgirdle", 1000), Upgrade("Deeper Pockets", "invrows2", 1000, "defeated_bonemass") };

            var rows = TraderWares.ByKey(own, live);

            Assert.Equal(new[] { "", "defeated_gdking", "defeated_bonemass" }, rows.Select(r => r.Key).ToArray());
            Assert.Equal(new[] { "HelmetYule", "skaldsgirdle" }, rows[0].Wares.Select(w => w.Prefab ?? w.Name).ToArray());
            Assert.True(rows[0].Wares[1].FromMods);
            Assert.False(rows[0].Wares[0].FromMods);
            Assert.Equal(new[] { "Deeper Pockets" }, rows[2].Wares.Select(w => w.Name).ToArray());
            Assert.Null(rows[2].Wares[0].Prefab);
        }

        [Fact]
        public void AnUpgradeTellsItsPriceAndOnceBoughtSaysSo()
        {
            Assert.Equal(SourceWords.Price(1, 1000), TraderWares.Amount(Upgrade("Deeper Pockets", "invrows2", 1000)));
            Assert.Equal(SourceWords.Price(1, 1000) + " · bought", TraderWares.Amount(Upgrade("Wider Pockets", "invrows1", 1000, bought: true)));
            Assert.Equal("5 for 50 coins", TraderWares.Amount(new Ware { Prefab = "FishingBait", Stack = 5, Price = 50 }));
        }

        [Theory]
        [InlineData("", true, true, "Whole")]
        [InlineData("defeated_bonemass", false, false, "Whole")]
        [InlineData("defeated_gdking", true, false, "Whole")]
        [InlineData("defeated_gdking", true, true, "Whole")]
        [InlineData("defeated_bonemass", true, false, "Counted")]
        [InlineData("defeated_bonemass", true, true, "Counted")]
        public void WaresWaitingForABossNotYetFallenAreCountedNotNamed(string key, bool namesOnlyMet, bool strict, string shown)
        {
            // The Elder has fallen in this world, Bonemass not.
            bool Fallen(string k) => k == "defeated_gdking";
            // Under either rules the same: a store's wares are what the game shows.
            Assert.Equal(shown, UnlockRows.Of(key, namesOnlyMet, Fallen).ToString());
        }

        [Fact]
        public void RowsWaitingForBossesNotMetAreToldAsOneWhereverTheyStand()
        {
            // Haldor in a world where The Elder and Moder fell: the rows for Yagluth and the Queen
            // both read "Sells once –– is defeated", with Moder's row, now whole, between them.
            const string Alike = "Sells once –– is defeated";
            var rows = new (string CountedAs, int Wares)[] { (null, 7), (null, 2), (Alike, 1), (null, 1), (Alike, 1) };
            Assert.Equal(2, UnlockRows.Told(rows, 2, r => r.CountedAs, r => r.Wares));
            Assert.Null(UnlockRows.Told(rows, 4, r => r.CountedAs, r => r.Wares));
            Assert.Null(UnlockRows.Told(rows, 1, r => r.CountedAs, r => r.Wares));
            Assert.Null(UnlockRows.Told(rows, 0, r => r.CountedAs, r => r.Wares));
        }

        [Fact]
        public void RowsWaitingForDifferentBossesStayApart()
        {
            var rows = new (string CountedAs, int Wares)[] { ("Sells once Moder is defeated", 1), ("Sells once –– is defeated", 1) };
            Assert.Equal(1, UnlockRows.Told(rows, 0, r => r.CountedAs, r => r.Wares));
            Assert.Equal(1, UnlockRows.Told(rows, 1, r => r.CountedAs, r => r.Wares));
        }

        [Fact]
        public void HowManyAreStillToUnlockIsSaidInWords()
        {
            Assert.Equal("2 more to unlock", CompendiumWords.ToUnlock(2));
            Assert.Equal("1 more to unlock", CompendiumWords.ToUnlock(1));
            Assert.Null(CompendiumWords.ToUnlock(0));
        }
    }
}
