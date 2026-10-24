using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class LearnedByDayTests
    {
        // The Learned list holds everything the character learned, newest first, grouped by the
        // game's day, each row saying how it came; what was learned with no day (caught up from
        // the game's records, or kept before days were) comes last. The news names it the same way.
        // What was found out is news of the strict rules alone: the loose rules show it whole.

        private sealed class World
        {
            public readonly Entry Troll = E("Troll", Kind.Creature, "Troll");
            public readonly Entry Boar = E("Boar", Kind.Creature, "Boar");
            public readonly Entry Neck = E("Neck", Kind.Creature, "Neck");
            public readonly Entry Wood = E("Wood", Kind.Item, "Wood");
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                Explorer = new Explorer(new[] { Troll, Boar, Neck, Wood }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
            }

            public World Told(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        private static World Learned(CompendiumRule rule = CompendiumRule.Strict)
        {
            var world = new World();
            var book = world.Book;
            book.Now = () => new Moment(13, "Meadows");
            book.Raise(world.Boar.Key, KnownLevel.Seen);
            book.Now = () => new Moment(14, "BlackForest");
            book.Raise(world.Troll.Key, KnownLevel.Known, way: CameBy.Killed);
            book.Raise(world.Neck.Key, KnownLevel.Known, way: CameBy.Fought);
            book.FoundOut(world.Neck.Key, EntryKeys.Damage("fire"));
            // Caught up last, as a session's first reading of the game's records is.
            book.CatchingUp = true;
            book.Raise(world.Wood.Key, KnownLevel.Known);
            book.CatchingUp = false;
            return world.Told(rule);
        }

        [Fact]
        public void EachRowSaysTheDayAndHowItCame()
        {
            var world = Learned();
            var explorer = world.Explorer;

            Assert.Equal(14, explorer.LearnedDay(world.Troll));
            Assert.Equal(13, explorer.LearnedDay(world.Boar));
            Assert.Equal(-1, explorer.LearnedDay(world.Wood));
            Assert.Equal("killed", explorer.LearnedNote(world.Troll));
            Assert.Equal("seen", explorer.LearnedNote(world.Boar));
            Assert.Equal("how fire lands found out", explorer.LearnedNote(world.Neck));
            Assert.Equal("known", explorer.LearnedNote(world.Wood));
            // Under the loose rules the Neck is told as met: fought.
            Assert.Equal("fought", Learned(CompendiumRule.Loose).Explorer.LearnedNote(world.Neck));
        }

        [Fact]
        public void TheListComesNewestFirstWhatHasNoDayLast()
        {
            var world = Learned();
            world.Explorer.LearnedOnly = true;

            Assert.Equal(new[] { world.Neck, world.Troll, world.Boar, world.Wood }, world.Explorer.Results.ToArray());
            Assert.Equal(4, world.Explorer.LearnedCount);
        }

        [Fact]
        public void DaysOfAnotherWorldAreNamedAndComeByWhenTheyWereLearnedNotByTheirNumber()
        {
            // A character's book spans worlds, whose days run apart: day 50 of the world played now
            // may be newer than day 300 of another. The list keeps the order things were learned
            // in, names the world on a day of another world, and folds by that order.
            var world = new World();
            var book = world.Book;
            book.Now = () => new Moment(300, "Meadows", DayPart.Day, null, null, "PipiKakaLand");
            book.Raise(world.Troll.Key, KnownLevel.Known, way: CameBy.Killed);
            book.Now = () => new Moment(50, "Meadows", DayPart.Day, null, null, "Elderwood");
            book.Raise(world.Boar.Key, KnownLevel.Seen);
            var explorer = world.Told(CompendiumRule.Strict).Explorer;
            explorer.WorldNow = "Elderwood";
            explorer.LearnedOnly = true;

            Assert.Equal(new[] { world.Boar, world.Troll }, explorer.Results.ToArray());
            Assert.Equal("PipiKakaLand", explorer.LearnedWorld(world.Troll));
            Assert.Equal("Elderwood", explorer.LearnedWorld(world.Boar));
            Assert.Equal("PipiKakaLand", explorer.WorldShown(explorer.LearnedWorld(world.Troll)));
            Assert.Null(explorer.WorldShown(explorer.LearnedWorld(world.Boar)));
            Assert.Null(explorer.WorldShown(null));

            // Under the loose rules, which list what was met, the world comes with the news the same.
            var loose = world.Told(CompendiumRule.Loose).Explorer;
            Assert.Equal("PipiKakaLand", loose.LearnedWorld(world.Troll));
            Assert.Equal("Elderwood", loose.LearnedWorld(world.Boar));
            world.Told(CompendiumRule.Strict);

            Assert.Equal("Day 50", JournalWords.DayHeading(50));
            Assert.Equal("PipiKakaLand  \u00B7  Day 300", JournalWords.DayHeading(300, "PipiKakaLand"));
            Assert.Equal("PipiKakaLand  \u00B7  Earlier", JournalWords.DayHeading(-1, "PipiKakaLand"));
            Assert.Equal("day:50", ListWords.DayGroup(50));
            Assert.Equal("day:300:PipiKakaLand", ListWords.DayGroup(300, "PipiKakaLand"));
            // The newest two groups of the list start open, whatever their day numbers; what has no day starts folded.
            Assert.False(JournalWords.FoldedAtFirst(0, 50));
            Assert.False(JournalWords.FoldedAtFirst(1, 300));
            Assert.True(JournalWords.FoldedAtFirst(2, 299));
            Assert.True(JournalWords.FoldedAtFirst(0, -1));
        }

        [Fact]
        public void TheNewsNamesWhatWasLearnedAsTheListDoes()
        {
            var world = Learned();
            var news = new LearnedNotice().New(world.Explorer);

            Assert.Equal(new NewsLine("Troll", NewsHow.Known, null, 14), world.Explorer.NewsLineOf(world.Troll));
            Assert.Equal("Since day 13: came to know Troll, saw Boar, found out how fire lands on Neck, and read 1 thing from the game's records of this character",
                JournalWords.News(news.Select(world.Explorer.NewsLineOf).ToList(), 0, 0));

            var loose = Learned(CompendiumRule.Loose);
            var looseNews = new LearnedNotice().New(loose.Explorer);
            Assert.Equal("Since day 13: came to know Neck and Troll, saw Boar, and read 1 thing from the game's records of this character",
                JournalWords.News(looseNews.Select(loose.Explorer.NewsLineOf).ToList(), 0, 0));
        }

        [Fact]
        public void WithTheCompendiumOffNothingIsLearned()
        {
            var world = new World();
            Assert.Equal(-1, world.Explorer.LearnedDay(world.Troll));
            Assert.Null(world.Explorer.LearnedNote(world.Troll));
        }
    }
}
