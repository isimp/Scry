using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    /// <summary>
    /// What a creature's stars change waits by the cell under the strict rules: each starred
    /// column once one with that many stars was seen to fall, its drops' chances told as the
    /// character's own counts at that many stars.
    /// </summary>
    public class CompendiumStarsTests
    {
        [Fact]
        public void AStarredFallIsProvedByItsStars()
        {
            Assert.Equal("fell:2", EntryKeys.StarsFell(2));
            Assert.True(EntryKeys.IsStarsFell("fell:2", out var stars) && stars == 2);
            Assert.False(EntryKeys.IsStarsFell("did:StarredFell", out _));
            Assert.False(EntryKeys.IsStarsFell("fell:many", out _));
            Assert.False(EntryKeys.IsStarsFell(null, out _));
            Assert.False(EntryKeys.IsDeed(EntryKeys.StarsFell(1), out _));
        }

        [Fact]
        public void KillsAreCountedByTheStarsTheFallenHad()
        {
            var book = new CompendiumBook();
            book.Dropped("Troll", new[] { ("TrollHide", 2), ("TrophyTroll", 1) }, 2);
            book.Dropped("Troll", new[] { ("TrollHide", 3) }, 2);
            book.Dropped("Troll", new[] { ("TrollHide", 1) });
            Assert.Equal(3, book.Tallied(Tally.Fell).Kills("Troll"));
            Assert.Equal(2, book.StarredKills("Troll", 1));
            Assert.Equal(1, book.StarredKills("Troll", 0));
            Assert.Equal(0, book.StarredKills("Troll", 2));
            Assert.Equal(0, book.StarredKills(null, 1));
            var trophy = book.StarredDrops("Troll", 1).First(d => d.Item == "TrophyTroll");
            Assert.Equal(1, trophy.Times);
            Assert.Empty(book.StarredDrops("Troll", 2));

            var read = CompendiumBook.Load(book.Save());
            Assert.Equal(2, read.StarredKills("Troll", 1));
            Assert.Equal(1, read.StarredDrops("Troll", 1).First(d => d.Item == "TrophyTroll").Times);
            Assert.Contains("starfell\t", book.Save());
        }

        [Fact]
        public void ADeathSeenProvesTheStarsTheFallenHadAndCountsItsLootAtThem()
        {
            var book = new CompendiumBook();
            book.Now = () => new Moment(3, "Meadows");
            book.Raise("Troll", KnownLevel.Known);
            var told = new WorldEvent(7, 1, Happened.Died, "Troll", 0f, 0f, 0f, new[] { ("TrollHide", 2) }, 2);
            EventRule.Tell(told, 7, 0f, 0f, 0f, new EventLedger(), book, null);
            Assert.True(book.Proved("Troll", EntryKeys.StarsFell(1)));
            Assert.True(book.Proved("Troll", EntryKeys.Did(Deed.StarredFell)));
            Assert.False(book.Proved("Troll", EntryKeys.StarsFell(2)));
            Assert.Equal(1, book.StarredKills("Troll", 1));
            Assert.Equal(EntryKeys.StarsFell(1), book.FoundOutOf("Troll"));
        }

        private sealed class World
        {
            public readonly Entry Troll = E("Troll", Kind.Creature);
            public readonly Entry Trophy = E("TrophyTroll", Kind.Item);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                var all = new[] { Troll, Trophy }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                Book.Raise(Troll.Key, KnownLevel.Known);
                Book.Raise(Trophy.Key, KnownLevel.Known);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        [Fact]
        public void UnderStrictATrophysChanceByStarsIsTheCharactersOwnCountAtThoseStars()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Troll);
            Assert.Null(world.Explorer.StrictStarShare("Troll", "TrophyTroll", 1));

            world.Rule(CompendiumRule.Strict);
            Assert.Equal(CompendiumWords.Hidden, world.Explorer.StrictStarShare("Troll", "TrophyTroll", 1));
            world.Book.Dropped("Troll", new[] { ("TrophyTroll", 1) }, 2);
            world.Rule(CompendiumRule.Strict);
            Assert.Equal("1 of 1 kill", world.Explorer.StrictStarShare("Troll", "TrophyTroll", 1));
            world.Book.Dropped("Troll", new[] { ("TrollHide", 4) }, 2);
            world.Rule(CompendiumRule.Strict);
            Assert.Equal("1 of 2 kills", world.Explorer.StrictStarShare("Troll", "TrophyTroll", 1));
            Assert.Equal("0 of 2 kills", world.Explorer.StrictStarShare("Troll", "Coins", 1));
            Assert.Equal(CompendiumWords.Hidden, world.Explorer.StrictStarShare("Troll", "TrophyTroll", 0));
        }

        [Fact]
        public void TheWordsOfAStarredFallNameItsStars()
        {
            Assert.Equal("shows once you have seen one with 1 star fall", CompendiumWords.ShowsOnce(EntryKeys.StarsFell(1)));
            Assert.Equal("shows once you have seen one with 2 stars fall", CompendiumWords.ShowsOnce(EntryKeys.StarsFell(2)));
            Assert.Equal("what 1 star changes found out", CompendiumWords.FoundOutNote(EntryKeys.StarsFell(1)));
            Assert.Equal("what 2 stars change found out", CompendiumWords.FoundOutNote(EntryKeys.StarsFell(2)));
            Assert.Equal("what 2 stars change on Troll", JournalWords.FoundOutOf(EntryKeys.StarsFell(2), "Troll"));
            Assert.Equal("what 1 star changes on Troll", JournalWords.FoundOutOf(EntryKeys.StarsFell(1), "Troll"));
            var rows = CompendiumWords.StillToFindOut(new[] { EntryKeys.StarsFell(2), EntryKeys.StarsFell(1), EntryKeys.StarsFell(1) }, new string[0], 0, 0, new Tally[0]);
            Assert.Equal(new[] { ("See one with 1 star fall", "shows what 1 star changes"), ("See one with 2 stars fall", "shows what 2 stars change") }, rows);
            Assert.Equal(new[] { "fight", "loot" }, NewsArrival.Of(new BookNews(NewsHow.FoundOut, EntryKeys.StarsFell(1), null, 12)).Topics);
            Assert.Equal("1 of 2 kills", TallyWords.Share(1, 2));
            Assert.Equal("0 of 1 kill", TallyWords.Share(0, 1));
        }
    }
}
