using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class LootMarkTests
    {
        // Rarest is not most worth having: what nothing else in the world gives, and what a trader
        // pays for, are marked and told first; the rest rarest first, by a real chance.

        [Fact]
        public void WhatOnlyThisGivesIsOnlyHere()
        {
            // A troll's trophy: only trolls drop it, nothing makes it.
            Assert.True(LootMarks.OnlyHere(new[] { "Troll" }, madeOrPlaced: false, new[] { "Troll" }));
            // A dungeon's rooms' chests are all here.
            Assert.True(LootMarks.OnlyHere(new[] { "TreasureChest_forestcrypt", "TreasureChest_forestcrypt_hard" }, false, new[] { "Crypt2", "TreasureChest_forestcrypt", "TreasureChest_forestcrypt_hard" }));
        }

        [Fact]
        public void WhatOthersGiveMakeOrPlaceIsNotOnlyHere()
        {
            Assert.False(LootMarks.OnlyHere(new[] { "Greydwarf", "Greydwarf_Elite" }, false, new[] { "Greydwarf" }));
            // Made at a station or placed by the world, it is had elsewhere too.
            Assert.False(LootMarks.OnlyHere(new[] { "Troll" }, madeOrPlaced: true, new[] { "Troll" }));
            // Nothing known to give it says nothing.
            Assert.False(LootMarks.OnlyHere(new string[0], false, new[] { "Troll" }));
        }

        [Fact]
        public void LootIsToldWhatOnlyThisGivesFirstThenWhatIsWorthCoinsThenRarestFirst()
        {
            var loot = new[]
            {
                (Name: "Wood", Chance: 0.9, Only: false, Worth: 0),
                (Name: "Ruby", Chance: 0.3, Only: false, Worth: 20),
                (Name: "Resin", Chance: 0.2, Only: false, Worth: 0),
                (Name: "Trophy", Chance: 0.5, Only: true, Worth: 0),
                // Worth before rarity: amber is rarer than the ruby and the trophy than the pearl, yet the worthier comes first.
                (Name: "Amber", Chance: 0.25, Only: false, Worth: 5),
                (Name: "Pearl", Chance: 0.6, Only: true, Worth: 10),
            };
            var told = ContentOrder.LootFirst(loot, l => l.Chance, l => l.Only, l => l.Worth).Select(l => l.Name);
            Assert.Equal(new[] { "Pearl", "Trophy", "Ruby", "Amber", "Resin", "Wood" }, told);
        }

        [Fact]
        public void AMarkSaysWhyAndItsTipSaysMore()
        {
            Assert.Equal("only here", DropWords.OnlyHereMark);
            Assert.Equal("Nothing else in the world gives it, and nothing makes it", DropWords.OnlyHereTip);
            Assert.Equal("20 coins", DropWords.WorthMark(20));
            Assert.Equal("A trader pays 20 coins for one", DropWords.WorthTip(20));
            Assert.Equal("A trader pays 1 coin for one", DropWords.WorthTip(1));
        }
    }
}
