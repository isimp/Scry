using Xunit;

namespace Scry.Tests
{
    public class UnlockTests
    {
        // A creature whose defeat sets a world key (Character.m_defeatSetGlobalKey) opens what
        // waits for that key: raids that may then come, those that then stop, what then spawns
        // in the world or from spawners, what spawners then stop, and what traders then sell.

        [Fact]
        public void WhatAKeyOpensIsKeptKindByKind()
        {
            var book = new UnlockBook();
            book.Add("defeated_eikthyr", Unlock.RaidStarts, "raid:army_theelder");
            book.Add("defeated_eikthyr", Unlock.RaidEnds, "raid:army_eikthyr");
            book.Add("defeated_eikthyr", Unlock.Spawns, "Greydwarf_Elite");
            book.Add("defeated_eikthyr", Unlock.Sells, "BeltStrength");
            book.Add("defeated_gdking", Unlock.Spawns, "Troll");

            Assert.Equal(new[] { "raid:army_theelder" }, book.Of("defeated_eikthyr", Unlock.RaidStarts));
            Assert.Equal(new[] { "raid:army_eikthyr" }, book.Of("defeated_eikthyr", Unlock.RaidEnds));
            Assert.Equal(new[] { "Greydwarf_Elite" }, book.Of("defeated_eikthyr", Unlock.Spawns));
            Assert.Equal(new[] { "BeltStrength" }, book.Of("defeated_eikthyr", Unlock.Sells));
            Assert.Equal(new[] { "Troll" }, book.Of("defeated_gdking", Unlock.Spawns));
            Assert.Empty(book.Of("defeated_gdking", Unlock.Sells));
            Assert.True(book.Any("defeated_eikthyr"));
            Assert.False(book.Any("defeated_queen"));
        }

        [Fact]
        public void EachThingIsKeptOnceAndBlanksNotAtAll()
        {
            var book = new UnlockBook();
            book.Add("defeated_eikthyr", Unlock.Spawns, "Greydwarf");
            book.Add("defeated_eikthyr", Unlock.Spawns, "Greydwarf");
            book.Add("", Unlock.Spawns, "Neck");
            book.Add(null, Unlock.Spawns, "Neck");
            book.Add("defeated_eikthyr", Unlock.Spawns, "");

            Assert.Equal(new[] { "Greydwarf" }, book.Of("defeated_eikthyr", Unlock.Spawns));
            Assert.False(book.Any(""));
            Assert.Empty(book.Of(null, Unlock.Spawns));

            book.Clear();
            Assert.False(book.Any("defeated_eikthyr"));
        }

        [Fact]
        public void EachKindHasItsTitle()
        {
            Assert.Equal("After it falls, raids that may come (2)", UnlockWords.Title(Unlock.RaidStarts, 2));
            Assert.Equal("After it falls, raids that stop (1)", UnlockWords.Title(Unlock.RaidEnds, 1));
            Assert.Equal("After it falls, these spawn (3)", UnlockWords.Title(Unlock.Spawns, 3));
            Assert.Equal("After it falls, spawners stop placing these (1)", UnlockWords.Title(Unlock.StopsSpawning, 1));
            Assert.Equal("After it falls, traders sell (4)", UnlockWords.Title(Unlock.Sells, 4));
        }
    }
}
