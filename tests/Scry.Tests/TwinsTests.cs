using Xunit;

namespace Scry.Tests
{
    public class TwinsTests
    {
        // Variants of a tree or rock going by one name, with the same health, tier, modifiers and
        // drops, are one thing to find out: what was found out of one holds for each.

        [Fact]
        public void VariantsOfOneNameAndTheSameValuesAreTwins()
        {
            var twins = Twins.Of(new[] { ("Beech1", "Beech", 200L), ("Beech_small1", "Beech", 50L), ("Beech2", "Beech 2", 200L), ("Birch1", "Birch", 200L) });
            Assert.Equal(new[] { "Beech2" }, twins("Beech1"));
            Assert.Equal(new[] { "Beech1" }, twins("Beech2"));
            // Another health is no twin; another name is none either.
            Assert.Empty(twins("Beech_small1"));
            Assert.Empty(twins("Birch1"));
            Assert.Empty(twins("NotListed"));
        }

        [Fact]
        public void WhatWasFoundOutOfOneTwinHoldsForEach()
        {
            var book = new CompendiumBook { TwinsOf = Twins.Of(new[] { ("Beech1", "Beech", 1L), ("Beech2", "Beech", 1L) }) };
            book.FoundOut("Beech1", EntryKeys.Did(Deed.Broke));
            book.SawWith("Beech1", "AxeBronze", Tie.BrokenWith);
            Assert.True(book.Proved("Beech2", EntryKeys.Did(Deed.Broke)));
            Assert.True(book.Witnessed("Beech2", "AxeBronze"));
            Assert.True(book.Witnessed("AxeBronze", "Beech2", Tie.BrokenWith));
            // Nothing else is moved by it.
            Assert.False(book.Proved("Beech2", EntryKeys.Did(Deed.Killed)));
        }
    }
}
