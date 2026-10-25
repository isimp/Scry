using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CompendiumMomentTests
    {
        // The book keeps the moment of a first sighting as it keeps that of coming to know a
        // thing: the game's day, the biome, the time of day, the weather and what was in hand,
        // and how many stars a creature had. It counts the deaths each creature dealt the
        // character, and keeps everything learned with the day it was learned, not only the
        // latest few, for the Learned list to group by day.

        private static readonly Moment Dusk = new Moment(9, "BlackForest", DayPart.Dusk, "Rain", "SpearFlint");

        private static CompendiumBook Book(Moment now) => new CompendiumBook { Now = () => now };

        [Fact]
        public void AFirstSightingIsKeptWithItsMomentAndStars()
        {
            var book = Book(Dusk);

            book.Raise("Troll", KnownLevel.Seen, stars: 1);

            Assert.Equal((Dusk, 1), book.FirstSeen("Troll"));
            // Only the first: seen again or come to know later, it stays as it was.
            book.Now = () => new Moment(12, "Meadows", DayPart.Night);
            book.Raise("Troll", KnownLevel.Seen, stars: 2);
            book.Raise("Troll", KnownLevel.Known, way: CameBy.Fought);
            Assert.Equal((Dusk, 1), book.FirstSeen("Troll"));
        }

        [Fact]
        public void ComingToKnowKeepsTheWholeMoment()
        {
            var book = Book(new Moment(12, "BlackForest", DayPart.Night, "ThunderStorm", "SpearFlint"));

            book.Raise("Troll", KnownLevel.Known, way: CameBy.Fought);

            Assert.Equal((CameBy.Fought, new Moment(12, "BlackForest", DayPart.Night, "ThunderStorm", "SpearFlint")), book.Came("Troll"));
        }

        [Fact]
        public void AMomentKeepsTheWorldItFellInThroughSaveAndLoad()
        {
            // A character's book spans worlds, whose days run apart: each moment keeps its world's
            // name, as does the news of each thing learned, for the page and the Learned list to
            // name it where it is not the world played now. A book written before keeps none.
            var book = Book(new Moment(300, "Meadows", DayPart.Day, "Clear", null, "PipiKakaLand"));
            book.Raise("Troll", KnownLevel.Seen, stars: 1);
            book.Raise("Troll", KnownLevel.Known, way: CameBy.Fought);
            Assert.Equal("PipiKakaLand", book.FirstSeen("Troll")?.At.World);
            Assert.Equal("PipiKakaLand", book.Came("Troll")?.At.World);
            Assert.Equal("PipiKakaLand", book.NewsOf("Troll")?.World);

            var read = CompendiumBook.Load(book.Save());
            Assert.Equal("PipiKakaLand", read.FirstSeen("Troll")?.At.World);
            Assert.Equal("PipiKakaLand", read.Came("Troll")?.At.World);
            Assert.Equal("PipiKakaLand", read.NewsOf("Troll")?.World);
            Assert.Equal(300, read.NewsOf("Troll")?.Day);

            var old = CompendiumBook.Load("know\tTroll\t3\ncame\tTroll\tFought\t12\tBlackForest\nlately\tTroll\t12\n");
            Assert.Null(old.Came("Troll")?.At.World);
            Assert.Null(old.NewsOf("Troll")?.World);
            Assert.Equal(12, old.NewsOf("Troll")?.Day);
        }

        [Fact]
        public void AThingKnownAtOnceHasNoFirstSighting()
        {
            var book = Book(Dusk);
            book.Raise("Club", KnownLevel.Known, way: CameBy.Held);
            Assert.Null(book.FirstSeen("Club"));
        }

        [Fact]
        public void WhatTheMapOrTheGamesRecordsTellIsNoSighting()
        {
            var book = Book(Dusk);
            book.Raise("loc:Crypt2", KnownLevel.Seen, sighted: false);
            Assert.Null(book.FirstSeen("loc:Crypt2"));

            // Caught up from what the game kept, nothing says when, so nothing is kept of the moment,
            // and the news has no day.
            book.CatchingUp = true;
            book.Raise("Wood", KnownLevel.Seen);
            book.Raise("Boar", KnownLevel.Known, way: CameBy.Killed);
            book.CatchingUp = false;
            Assert.Null(book.FirstSeen("Wood"));
            Assert.Null(book.Came("Boar"));
            Assert.Equal(-1, book.NewsOf("Wood")?.Day);
            Assert.Equal(-1, book.NewsOf("Boar")?.Day);
        }

        [Fact]
        public void TheDeathsACreatureDealtAreCountedWithTheDayOfTheLast()
        {
            var book = Book(new Moment(18, "BlackForest"));
            book.Died("Troll");
            book.Now = () => new Moment(21, "BlackForest", world: "Elderwood");
            book.Died("Troll");

            Assert.Equal((2, 21), book.DeathsBy("Troll"));
            Assert.Equal("Elderwood", book.DeathsWorld("Troll"));
            Assert.Null(book.DeathsBy("Boar"));
            Assert.True(book.Changed);
            // Told with the world where it differs from the one played, as every other moment is.
            Assert.Equal("twice, last on day 21", JournalWords.KilledYou(2, 21, null));
            Assert.Equal("once, on day 21 in Elderwood", JournalWords.KilledYou(1, 21, "Elderwood"));
        }

        [Fact]
        public void ABookWhoseChestsWereCountedByTheGamesNumbersLetsTheirCountsGoOnce()
        {
            // The game numbered chests anew each load, so such a book counted a chest again each time: its ids are dropped on reading, and the counts with them.
            var book = new CompendiumBook();
            Assert.True(book.FirstOpen("world/2318823923:41"));
            book.Held("TreasureChest_meadows", new[] { ("Coins", 5) });
            var read = CompendiumBook.Load(book.Save());
            Assert.Equal(0, read.Tallied(Tally.Held).Kills("TreasureChest_meadows"));
            Assert.True(read.FirstOpen("world/2318823923:41"));
            // A book with ids of the lasting kind keeps its counts.
            var lasting = new CompendiumBook();
            Assert.True(lasting.FirstOpen(CompendiumBook.ChestId("world", "TreasureChest_meadows", 10f, 20f, 30f)));
            lasting.Held("TreasureChest_meadows", new[] { ("Coins", 5) });
            Assert.Equal(1, CompendiumBook.Load(lasting.Save()).Tallied(Tally.Held).Kills("TreasureChest_meadows"));
        }

        [Fact]
        public void AMomentOfNoWorldIsNoTodayWhileAWorldIsPlayed()
        {
            Assert.True(CompendiumView.OnToday(new Moment(5, "Meadows", world: "Midgard"), 5, "Midgard"));
            Assert.False(CompendiumView.OnToday(new Moment(5, "Meadows", world: "Elderwood"), 5, "Midgard"));
            Assert.False(CompendiumView.OnToday(new Moment(5, "Meadows"), 5, "Midgard"));
            Assert.True(CompendiumView.OnToday(new Moment(5, "Meadows"), 5, null));
            Assert.False(CompendiumView.OnToday(null, 5, "Midgard"));
        }

        [Fact]
        public void AThingSeenOffTheMapOrTheRecordsIsFirstSightedWhenReallySeen()
        {
            // Seen before with no sighting kept, the first real sighting is the first.
            var book = Book(Dusk);
            book.Raise("loc:Crypt2", KnownLevel.Seen, sighted: false);
            Assert.Null(book.FirstSeen("loc:Crypt2"));
            Assert.False(book.Raise("loc:Crypt2", KnownLevel.Seen));
            Assert.Equal((Dusk, -1), book.FirstSeen("loc:Crypt2"));
            // Not while catching up, which keeps no moment.
            book.Raise("Wood", KnownLevel.Seen, sighted: false);
            book.CatchingUp = true;
            book.Raise("Wood", KnownLevel.Seen);
            book.CatchingUp = false;
            Assert.Null(book.FirstSeen("Wood"));
        }

        [Fact]
        public void EverythingLearnedIsKeptWithTheDayAndHowItCame()
        {
            var book = Book(new Moment(14, "Meadows"));
            for (var i = 0; i < 80; i++) book.Raise("Item" + i, KnownLevel.Seen);
            book.Raise("Troll", KnownLevel.Known, way: CameBy.Killed);
            book.Raise("Neck", KnownLevel.Known, way: CameBy.Fought);
            book.FoundOut("Neck", EntryKeys.Damage("fire"));

            Assert.Equal(82, book.Lately.Count());
            Assert.Equal("Neck", book.Lately.First());
            Assert.Equal(new BookNews(NewsHow.Seen, null, null, 14), book.NewsOf("Item0"));
            Assert.Equal(new BookNews(NewsHow.Known, null, CameBy.Killed, 14), book.NewsOf("Troll"));
            Assert.Equal(new BookNews(NewsHow.FoundOut, EntryKeys.Damage("fire"), CameBy.Fought, 14), book.NewsOf("Neck"));
            Assert.Null(book.NewsOf("Boar"));
        }

        [Fact]
        public void NewsOffTheMapSaysSo()
        {
            var book = Book(new Moment(14, "Meadows"));
            book.Raise("loc:Crypt2", KnownLevel.Seen, sighted: false);
            book.NoteNews("loc:Crypt2", EntryKeys.OnMap);
            Assert.Equal(NewsHow.Map, book.NewsOf("loc:Crypt2")?.How);
        }

        [Fact]
        public void MomentsDeathsAndDaysAreKeptWithTheBook()
        {
            var book = Book(Dusk);
            book.Raise("Troll", KnownLevel.Seen, stars: 2);
            book.Raise("Troll", KnownLevel.Known, way: CameBy.Fought);
            book.Now = () => new Moment(9, "BlackForest", DayPart.Dusk, "Rain", "SpearFlint", "Elderwood");
            book.Died("Troll");
            book.Now = null;
            book.Raise("Boar", KnownLevel.Seen);

            var read = CompendiumBook.Load(book.Save());

            Assert.Equal((Dusk, 2), read.FirstSeen("Troll"));
            Assert.Equal((CameBy.Fought, Dusk), read.Came("Troll"));
            Assert.Equal((1, 9), read.DeathsBy("Troll"));
            Assert.Equal("Elderwood", read.DeathsWorld("Troll"));
            // A line kept before worlds were: the deaths and the day, no world.
            Assert.Null(CompendiumBook.Load("format\t2\ndied\tTroll\t3\t5\n").DeathsWorld("Troll"));
            Assert.Equal((3, 5), CompendiumBook.Load("format\t2\ndied\tTroll\t3\t5\n").DeathsBy("Troll"));
            Assert.Equal(9, read.NewsOf("Troll")?.Day);
            Assert.Equal(-1, read.NewsOf("Boar")?.Day);
            Assert.Equal(book.Lately, read.Lately);
        }

        [Fact]
        public void ABookKeptBeforeMomentsReadsAsItWas()
        {
            var read = CompendiumBook.Load("know\tTroll\t3\ncame\tTroll\tFought\t12\tBlackForest\nlately\tTroll\n");

            Assert.Equal((CameBy.Fought, new Moment(12, "BlackForest")), read.Came("Troll"));
            Assert.Null(read.FirstSeen("Troll"));
            Assert.Equal(-1, read.NewsOf("Troll")?.Day);
        }

        [Fact]
        public void TakenBackForgetsTheSightingAndTheNews()
        {
            var book = Book(Dusk);
            book.Raise("Troll", KnownLevel.Seen);
            book.TakeBack("Troll", null);
            Assert.Null(book.FirstSeen("Troll"));
            Assert.Null(book.NewsOf("Troll"));
        }

        [Theory]
        [InlineData(0.10f, DayPart.Night)]
        [InlineData(0.19f, DayPart.Night)]
        [InlineData(0.20f, DayPart.Dawn)]
        [InlineData(0.29f, DayPart.Dawn)]
        [InlineData(0.30f, DayPart.Day)]
        [InlineData(0.50f, DayPart.Day)]
        [InlineData(0.70f, DayPart.Dusk)]
        [InlineData(0.79f, DayPart.Dusk)]
        [InlineData(0.80f, DayPart.Night)]
        [InlineData(0.95f, DayPart.Night)]
        public void TheTimeOfDayFollowsTheGamesMorningAndEvening(float fraction, object part)
        {
            // The game's day runs from 0.25 to 0.75 of its turn, its morning and evening
            // told within 0.05 of either end (EnvMan.OnMorning, OnEvening).
            Assert.Equal((DayPart)part, Moment.PartOf(fraction));
        }
    }
}
