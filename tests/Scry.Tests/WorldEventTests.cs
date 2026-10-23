using Xunit;

namespace Scry.Tests
{
    public class WorldEventTests
    {
        // What happens to a thing in the world runs in one game only, the one that runs the thing:
        // a death and its loot, a break, a pick's yield, a birth, a growing up, a spawn, a
        // station's output, a creature eating. That game knows all of it, who did it too, and
        // tells it as one event. The character's book records an event where the character did
        // it, however far away, or saw it, within 30 m of it as it is told, as births were
        // counted before; once, however often it is told. The character stands at the origin.

        private const long Me = 101;
        private const long Friend = 202;
        private const long OtherGame = 303;

        private static WorldEvent Event(Happened what, string thing, float away, int number = 1, (string, int)[] came = null, int level = 1,
            long[] doers = null, string with = null, long game = Me) =>
            new WorldEvent(game, number, what, thing, away, 0f, 0f, came, level, doers, with);

        private static WorldEvent Death(float away, long[] doers = null, int number = 1, int level = 1, (string, int)[] came = null) =>
            Event(Happened.Died, "Troll", away, number, came ?? new[] { ("TrollHide", 2) }, level, doers);

        /// <summary>The character's game told each event, in turn, into a book of its own.</summary>
        private static CompendiumBook Told(params WorldEvent[] events) => Told(null, events);

        private static CompendiumBook Told(PlayCounts counts, params WorldEvent[] events)
        {
            var book = new CompendiumBook();
            var told = new EventLedger();
            foreach (var e in events) EventRule.Tell(e, Me, 0f, 0f, 0f, told, book, counts);
            return book;
        }

        [Fact]
        public void AKillTheCharacterTookPartInCountsItsLootWhereverItFell()
        {
            var book = Told(Death(80f, doers: new[] { Friend, Me }));
            Assert.Equal(1, book.Tallied(Tally.Fell).Kills("Troll"));
            Assert.True(book.Witnessed("Troll", "TrollHide"));
        }

        [Fact]
        public void ADeathTheCharacterSawWithinReachCountsItsLootThoughAFriendKilledIt()
        {
            var book = Told(Death(20f, doers: new[] { Friend }));
            Assert.Equal(1, book.Tallied(Tally.Fell).Kills("Troll"));
            Assert.True(book.Witnessed("Troll", "TrollHide"));
        }

        [Fact]
        public void ADeathFartherAwayThatTheCharacterHadNoHandInRecordsNothing()
        {
            var book = Told(Death(40f, doers: new[] { Friend }), Death(40f, number: 2));
            Assert.Equal(0, book.Tallied(Tally.Fell).Kills("Troll"));
            Assert.False(book.Witnessed("Troll", "TrollHide"));
            Assert.False(book.Changed);
        }

        [Fact]
        public void TheCharacterSeesAsFarAs30MetresUpDownOrAcross()
        {
            Assert.Equal(EventShare.Saw, EventRule.Tell(Death(30f), Me, 0f, 0f, 0f, new EventLedger(), new CompendiumBook(), null));
            Assert.Equal(EventShare.None, EventRule.Tell(Death(30.5f), Me, 0f, 0f, 0f, new EventLedger(), new CompendiumBook(), null));
            // 20 m across and 25 m up is 32 m away.
            var above = new WorldEvent(Me, 1, Happened.Died, "Troll", 20f, 25f, 0f);
            Assert.Equal(EventShare.None, EventRule.Tell(above, Me, 0f, 0f, 0f, new EventLedger(), new CompendiumBook(), null));
            // Standing where it happened, wherever that is.
            Assert.Equal(EventShare.Saw, EventRule.Tell(above, Me, 20f, 25f, 0f, new EventLedger(), new CompendiumBook(), null));
            // 31 m off the other way across the ground.
            var aside = new WorldEvent(Me, 1, Happened.Died, "Troll", 0f, 0f, 31f);
            Assert.Equal(EventShare.None, EventRule.Tell(aside, Me, 0f, 0f, 0f, new EventLedger(), new CompendiumBook(), null));
        }

        [Fact]
        public void WhatTheCharacterDidCountsAsDoneEvenWithinSight()
        {
            Assert.Equal(EventShare.Did, EventRule.Tell(Death(5f, doers: new[] { Me }), Me, 0f, 0f, 0f, new EventLedger(), new CompendiumBook(), null));
            Assert.Equal(EventShare.Saw, EventRule.Tell(Death(5f, doers: new[] { Friend }), Me, 0f, 0f, 0f, new EventLedger(), new CompendiumBook(), null));
        }

        [Fact]
        public void AnEventToldTwiceCountsOnce()
        {
            var book = Told(Death(10f, doers: new[] { Me }), Death(10f, doers: new[] { Me }));
            Assert.Equal(1, book.Tallied(Tally.Fell).Kills("Troll"));
            var told = new EventLedger();
            Assert.Equal(EventShare.Did, EventRule.Tell(Death(10f, doers: new[] { Me }), Me, 0f, 0f, 0f, told, new CompendiumBook(), null));
            Assert.Equal(EventShare.None, EventRule.Tell(Death(10f, doers: new[] { Me }), Me, 0f, 0f, 0f, told, new CompendiumBook(), null));
        }

        [Fact]
        public void EachTimeAThingIsPickedCounts()
        {
            var book = Told(Event(Happened.Picked, "RaspberryBush", 2f, 1, new[] { ("Raspberry", 1) }, doers: new[] { Me }),
                Event(Happened.Picked, "RaspberryBush", 2f, 2, new[] { ("Raspberry", 1) }, doers: new[] { Me }));
            Assert.Equal(2, book.Tallied(Tally.Gave).Kills("RaspberryBush"));
        }

        [Fact]
        public void AnotherGamesEventOfTheSameNumberIsAnotherEvent()
        {
            var book = Told(Death(10f, number: 5), Event(Happened.Died, "Troll", 10f, 5, new[] { ("TrollHide", 2) }, game: OtherGame));
            Assert.Equal(2, book.Tallied(Tally.Fell).Kills("Troll"));
        }

        [Fact]
        public void LeavingTheWorldForgetsWhatWasTold()
        {
            var told = new EventLedger();
            EventRule.Tell(Death(10f), Me, 0f, 0f, 0f, told, new CompendiumBook(), null);
            told.Forget();
            Assert.Equal(EventShare.Saw, EventRule.Tell(Death(10f), Me, 0f, 0f, 0f, told, new CompendiumBook(), null));
        }

        [Fact]
        public void ADeathWithNothingFallingStillCountsAKill()
        {
            var book = Told(Death(10f, came: new (string, int)[0]));
            Assert.Equal(1, book.Tallied(Tally.Fell).Kills("Troll"));
        }

        [Fact]
        public void AStarredCreatureFallingShowsWhatItsStarsChange()
        {
            Assert.Equal(EntryKeys.Did(Deed.StarredFell), Told(Death(10f, level: 2)).FoundOutOf("Troll"));
            Assert.False(Told(Death(10f)).Witnessed("Troll", EntryKeys.Did(Deed.StarredFell)));
        }

        [Fact]
        public void ABreakCountsWhatItGaveAndShowsItsHealth()
        {
            var book = Told(Event(Happened.Broke, "Beech1", 50f, came: new[] { ("Wood", 3), ("BeechSeeds", 1) }, doers: new[] { Me }));
            Assert.Equal(1, book.Tallied(Tally.Gave).Kills("Beech1"));
            Assert.True(book.Witnessed("Beech1", "Wood"));
            Assert.True(book.Witnessed("Beech1", "BeechSeeds"));
            Assert.Equal(EntryKeys.Did(Deed.Broke), book.FoundOutOf("Beech1"));
        }

        [Fact]
        public void APickCountsWhatItGaveAndIsNoBreak()
        {
            var book = Told(Event(Happened.Picked, "RaspberryBush", 2f, came: new[] { ("Raspberry", 1) }, doers: new[] { Me }));
            Assert.Equal(1, book.Tallied(Tally.Gave).Kills("RaspberryBush"));
            Assert.True(book.Witnessed("RaspberryBush", "Raspberry"));
            Assert.False(book.Witnessed("RaspberryBush", EntryKeys.Did(Deed.Broke)));
        }

        [Fact]
        public void ABreakOrAPickThatGaveNothingRecordsNothing()
        {
            var book = Told(Event(Happened.Broke, "Beech1", 2f, 1, doers: new[] { Me }), Event(Happened.Picked, "RaspberryBush", 2f, 2, doers: new[] { Me }));
            Assert.False(book.Changed);
        }

        [Theory]
        [InlineData(Happened.Born, Deed.Born)]
        [InlineData(Happened.GrewUp, Deed.GrewUp)]
        [InlineData(Happened.GrewBack, Deed.GrewBack)]
        [InlineData(Happened.Grew, Deed.Grew)]
        [InlineData(Happened.Spawned, Deed.Spawned)]
        [InlineData(Happened.Finished, Deed.Finished)]
        [InlineData(Happened.Hatched, Deed.Hatched)]
        public void SomethingSeenDoneShowsTheNumberItTells(object what, object deed)
        {
            Assert.Equal(EntryKeys.Did((Deed)deed), Told(Event((Happened)what, "Thing", 10f)).FoundOutOf("Thing"));
            Assert.False(Told(Event((Happened)what, "Thing", 40f)).Changed);
        }

        [Fact]
        public void ACreatureSeenEatingIsPairedWithWhatItAte()
        {
            Assert.True(Told(Event(Happened.Ate, "Boar", 10f, with: "Raspberry")).Witnessed("Boar", "Raspberry"));
            Assert.False(Told(Event(Happened.Ate, "Boar", 40f, with: "Raspberry")).Witnessed("Boar", "Raspberry"));
        }

        [Theory]
        [InlineData(Happened.Born, "Lox", "Lox_Calf", CameBy.Born)]
        [InlineData(Happened.Hatched, "ChickenEgg", "Chicken", CameBy.Born)]
        [InlineData(Happened.GrewUp, "Chicken", "Hen", CameBy.GrewUp)]
        public void TheYoungSeenComeAboutComesToBeKnown(object what, string thing, string young, object way)
        {
            // A calf born, a chick hatched, a hen grown from it: seen within reach, the one that came
            // is known as a creature tamed near the character is, tame from the start, seen
            // together with what it came of, so a strict page names each on the other's.
            var book = Told(Event((Happened)what, thing, 10f, with: young));
            Assert.Equal(KnownLevel.Known, book.Level(young));
            Assert.Equal((CameBy)way, book.Came(young)?.Way);
            Assert.True(book.Witnessed(thing, young));
            // Too far away to see, nothing.
            Assert.Equal(KnownLevel.Unknown, Told(Event((Happened)what, thing, 40f, with: young)).Level(young));
            // Told with no young (a hen's egg laid alone is an item, no creature), only the deed shows.
            Assert.Equal(EntryKeys.Did(EventRule.DeedOf((Happened)what)), Told(Event((Happened)what, thing, 10f)).FoundOutOf(thing));
        }

        [Fact]
        public void TheWaysTheYoungComeToBeKnownAreToldInAFewWords()
        {
            Assert.Equal("seen born", JournalWords.LearnedNote(new BookNews(NewsHow.Known, null, CameBy.Born, 3)));
            Assert.Equal("seen grow up", JournalWords.LearnedNote(new BookNews(NewsHow.Known, null, CameBy.GrewUp, 3)));
            Assert.Equal("its hatching found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.Hatched)));
            Assert.Equal("how Chicken egg hatches", JournalWords.FoundOutOf(EntryKeys.Did(Deed.Hatched), "Chicken egg"));
            Assert.Equal("shows once you have seen one hatch", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.Hatched)));
        }

        [Fact]
        public void PlayCountsTellEachEventCountedOnce()
        {
            var counts = new PlayCounts();
            Told(counts, Death(10f, level: 2), Death(10f, level: 2), Death(40f, number: 2),
                Event(Happened.Broke, "Beech1", 2f, 3, new[] { ("Wood", 3) }), Event(Happened.Picked, "RaspberryBush", 2f, 4, new[] { ("Raspberry", 1) }),
                Event(Happened.Born, "Boar", 2f, 5), Event(Happened.GrewUp, "Boar", 2f, 6), Event(Happened.GrewBack, "RaspberryBush", 2f, 7), Event(Happened.Spawned, "Spawner_Skeleton", 2f, 8));
            Assert.Equal(1, counts.Of(PlayEvent.LootCounted));
            Assert.Equal(1, counts.Of(PlayEvent.StarredFell));
            Assert.Equal(1, counts.Of(PlayEvent.BreakCounted));
            Assert.Equal(1, counts.Of(PlayEvent.PickCounted));
            Assert.Equal(1, counts.Of(PlayEvent.Born));
            Assert.Equal(1, counts.Of(PlayEvent.GrewUp));
            Assert.Equal(1, counts.Of(PlayEvent.GrewBack));
            Assert.Equal(1, counts.Of(PlayEvent.Spawned));
        }
    }
}
