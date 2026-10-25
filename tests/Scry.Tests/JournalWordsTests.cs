using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class JournalWordsTests
    {
        // What the character did with a thing reads as a moment of its own: the day, the time of
        // day and the weather, the biome, what was in hand; kills by how they were dealt, deaths
        // by what dealt them; the Learned list by day, each row saying how it came; the news in
        // words of what happened.

        [Theory]
        [InlineData("Rain", "in the rain")]
        [InlineData("LightRain", "in light rain")]
        [InlineData("ThunderStorm", "in a thunderstorm")]
        [InlineData("Mistlands_thunder", "in a thunderstorm")]
        [InlineData("Misty", "in the mist")]
        [InlineData("DeepForest Mist", "in the mist")]
        [InlineData("SnowStorm", "in a snowstorm")]
        [InlineData("Twilight_SnowStorm", "in a snowstorm")]
        [InlineData("Snow", "in the snow")]
        [InlineData("Ashrain", "in the ash rain")]
        [InlineData("Ashlands_CinderRain", "in the cinder rain")]
        [InlineData("Darklands_dark", "in the dark")]
        [InlineData("Clear", null)]
        [InlineData("Heath clear", null)]
        [InlineData("Crypt", null)]
        [InlineData(null, null)]
        public void AWeatherReadsAsWhatYouWereOutIn(string weather, string during)
        {
            Assert.Equal(during, WeatherWords.During(weather));
        }

        [Fact]
        public void AFirstSightingTellsItsMoment()
        {
            Assert.Equal("day 9, at dusk in the rain, in the Black Forest; it had 1 star",
                JournalWords.FirstSeen(9, DayPart.Dusk, "Rain", "Black Forest", 1));
            Assert.Equal("day 9, in the rain, in the Black Forest", JournalWords.FirstSeen(9, DayPart.Day, "Rain", "Black Forest", 0));
            Assert.Equal("day 9, at night, in the Black Forest; it had 2 stars", JournalWords.FirstSeen(9, DayPart.Night, "Clear", "Black Forest", 2));
            Assert.Equal("day 9, in the Meadows", JournalWords.FirstSeen(9, DayPart.None, null, "Meadows", -1));
            Assert.Equal("at dawn", JournalWords.FirstSeen(-1, DayPart.Dawn, null, null, -1));
            Assert.Equal("day 9 in Elderwood, in the Meadows", JournalWords.FirstSeen(9, DayPart.None, null, "Meadows", -1, "Elderwood"));
            Assert.Equal("in Elderwood, at dawn", JournalWords.FirstSeen(-1, DayPart.Dawn, null, null, -1, "Elderwood"));
        }

        [Fact]
        public void ComingToKnowTellsItsMomentAndWhatWasInHandForAFight()
        {
            Assert.Equal("day 12, at night, fighting it with your Flint spear in the Black Forest",
                CompendiumWords.CameToKnow(CameBy.Fought, 12, "Black Forest", DayPart.Night, null, "Flint spear"));
            Assert.Equal("day 12, at dusk in the rain, killing it with your Bow in the Black Forest",
                CompendiumWords.CameToKnow(CameBy.Killed, 12, "Black Forest", DayPart.Dusk, "Rain", "Bow"));
            // What is in hand only where a hand did it.
            Assert.Equal("day 3, picking it in the Meadows", CompendiumWords.CameToKnow(CameBy.Picked, 3, "Meadows", DayPart.Day, "Clear", "Club"));
            // Kept before moments were, it reads as it did.
            Assert.Equal("day 12, fighting it in the Black Forest", CompendiumWords.CameToKnow(CameBy.Fought, 12, "Black Forest"));
        }

        [Fact]
        public void KillsAreToldByHowTheyWereDealt()
        {
            // The game counts a kill under melee, ranged, magic or bare hands where every hit was
            // of that kind (Character.ApplyDamage, KillModifiers), else only in the total.
            Assert.Equal("14 times: 9 in melee, 3 with arrows or bolts, 2 with mixed attacks", JournalWords.Kills(14, melee: 9, ranged: 3, magic: 0, unarmed: 0));
            Assert.Equal("5 times, all in melee", JournalWords.Kills(5, melee: 5, ranged: 0, magic: 0, unarmed: 0));
            Assert.Equal("once, by magic", JournalWords.Kills(1, melee: 0, ranged: 0, magic: 1, unarmed: 0));
            Assert.Equal("4 times: 3 bare-handed, 1 by magic", JournalWords.Kills(4, melee: 0, ranged: 0, magic: 1, unarmed: 3));
            Assert.Equal("twice", JournalWords.Kills(2, 0, 0, 0, 0));
        }

        [Fact]
        public void TheNoticeCountsItsDaysInTheWorldPlayedAlone()
        {
            // A thing learned on day 300 of another world is told, but the notice's days are this world's.
            var lines = new[] { new NewsLine("Troll", NewsHow.Known, null, 300, "Elderwood"), new NewsLine("Boar", NewsHow.Seen, null, 14) };
            // What came in another world is said as that world's, after this one's.
            Assert.Equal("Day 14: saw Boar, and in Elderwood came to know Troll", JournalWords.News(lines, 0, 0));
            Assert.Equal("In Elderwood came to know Troll", JournalWords.News(new[] { lines[0] }, 0, 0));
            // A moment kept before worlds were, grouped under the heading of a world not kept, is said so in words.
            var unkept = new NewsLine("Troll", NewsHow.Known, null, 12, JournalWords.WorldUnkept);
            Assert.Equal("In a world not kept came to know Troll", JournalWords.News(new[] { unkept }, 0, 0));
            Assert.Equal("Day 14: saw Boar, and in a world not kept came to know Troll", JournalWords.News(new[] { unkept, lines[1] }, 0, 0));
        }

        [Fact]
        public void ASectionsLeftOutLineSaysOnHoverWhatNamesThem()
        {
            Assert.Equal("Named here once you meet each", CompendiumWords.LeftOutTip(strict: false));
            Assert.Equal("Named here once you see each together with this page; met elsewhere, it stays left out here", CompendiumWords.LeftOutTip(strict: true));
            Assert.Equal("1 recipe row waits for the game to list it", CompendiumWords.RecipeRowsWait(1));
            Assert.Equal("2 recipe rows wait for the game to list them", CompendiumWords.RecipeRowsWait(2));
        }

        [Fact]
        public void AMomentKeptBeforeWorldsWereHasAHeadingOfItsOwn()
        {
            Assert.Equal("Elderwood  \u00B7  Day 12", JournalWords.DayHeading(12, JournalWords.WorldGroup("Elderwood", "Midgard")));
            Assert.Equal("Day 12", JournalWords.DayHeading(12, JournalWords.WorldGroup("Midgard", "Midgard")));
            Assert.Equal("World not kept  \u00B7  Day 12", JournalWords.DayHeading(12, JournalWords.WorldGroup(null, "Midgard")));
            // After a day, a world reads as a phrase; a moment of no world adds none.
            Assert.Equal(" in Elderwood", JournalWords.InWorld("Elderwood"));
            Assert.Equal("", JournalWords.InWorld(null));
            // With no world played, as off the world, nothing is told apart.
            Assert.Equal("Day 12", JournalWords.DayHeading(12, JournalWords.WorldGroup(null, null)));
        }

        [Fact]
        public void DeathsACreatureDealtAreCountedWithTheLast()
        {
            Assert.Equal("once, on day 21", JournalWords.KilledYou(1, 21, null));
            Assert.Equal("twice, last on day 21", JournalWords.KilledYou(2, 21, null));
            Assert.Equal("3 times", JournalWords.KilledYou(3, -1, null));
        }

        [Fact]
        public void TheYouBoxTellsKillsDeathsTheFirstSightingAndComingToKnow()
        {
            var counts = new CharacterCounts { Killed = 14, Melee = 9, Ranged = 3, Built = 2 };

            var lines = CompendiumWords.You(counts, "day 12, fighting it in the Black Forest", firstSeen: "day 9, in the Black Forest", killedYou: "twice, last on day 21");

            Assert.Equal(new[]
            {
                ("Killed", "14 times: 9 in melee, 3 with arrows or bolts, 2 with mixed attacks"),
                ("It killed you", "twice, last on day 21"),
                ("Built", "twice"),
                ("First seen", "day 9, in the Black Forest"),
                ("Came to know it", "day 12, fighting it in the Black Forest"),
            }, lines);
        }

        [Fact]
        public void ARowOfLearnedSaysHowItCame()
        {
            Assert.Equal("seen", JournalWords.LearnedNote(new BookNews(NewsHow.Seen, null, null, 14)));
            Assert.Equal("killed", JournalWords.LearnedNote(new BookNews(NewsHow.Known, null, CameBy.Killed, 14)));
            Assert.Equal("stepped into", JournalWords.LearnedNote(new BookNews(NewsHow.Known, null, CameBy.StoodIn, 14)));
            Assert.Equal("known", JournalWords.LearnedNote(new BookNews(NewsHow.Known, null, null, -1)));
            Assert.Equal("how fire lands found out", JournalWords.LearnedNote(new BookNews(NewsHow.FoundOut, EntryKeys.Damage("fire"), CameBy.Fought, 14)));
            Assert.Equal("seen on your explored map", JournalWords.LearnedNote(new BookNews(NewsHow.Map, EntryKeys.OnMap, null, 14)));
        }

        [Fact]
        public void EveryWayOfComingToKnowHasAWordForLearned()
        {
            foreach (CameBy way in System.Enum.GetValues(typeof(CameBy)))
            {
                var said = JournalWords.LearnedNote(new BookNews(NewsHow.Known, null, way, 1));
                Assert.False(string.IsNullOrEmpty(said), way.ToString());
                Assert.DoesNotContain(way.ToString(), said);
            }
        }

        [Fact]
        public void LearnedIsHeadedByDay()
        {
            Assert.Equal("Day 14", JournalWords.DayHeading(14));
            Assert.Equal("Earlier", JournalWords.DayHeading(-1));
            // The newest two groups, in the order things were learned, stay open; older ones and what has no day fold away.
            Assert.False(JournalWords.FoldedAtFirst(groupRank: 0, day: 14));
            Assert.False(JournalWords.FoldedAtFirst(groupRank: 1, day: 13));
            Assert.True(JournalWords.FoldedAtFirst(groupRank: 2, day: 12));
            Assert.True(JournalWords.FoldedAtFirst(groupRank: 0, day: -1));
        }

        private static NewsLine N(string name, NewsHow how, int day, string proof = null) => new NewsLine(name, how, proof, day);

        [Fact]
        public void TheNewsTellsWhatHappenedInWords()
        {
            var day14 = new List<NewsLine>
            {
                N("Troll", NewsHow.Known, 14), N("Greydwarf Brute", NewsHow.Known, 14), N("Troll Hide", NewsHow.Seen, 14), N("Deer", NewsHow.Seen, 14),
                N("Neck", NewsHow.FoundOut, 14, EntryKeys.Damage("fire")),
            };
            Assert.Equal("Day 14: came to know Troll and Greydwarf Brute, saw 2 more, and found out how fire lands on Neck", JournalWords.News(day14, 0, 0));

            var days = Enumerable.Range(0, 9).Select(i => N(i == 0 ? "Troll" : i == 1 ? "Forge" : "Known" + i, NewsHow.Known, 12 + i % 3))
                .Concat(Enumerable.Range(0, 14).Select(i => N("Seen" + i, NewsHow.Seen, 13))).ToList();
            Assert.Equal("Since day 12: came to know 9 things, among them Troll and Forge, and saw 14 more", JournalWords.News(days, 0, 0));

            Assert.Equal("Day 3: saw Boar and Deer", JournalWords.News(new[] { N("Boar", NewsHow.Seen, 3), N("Deer", NewsHow.Seen, 3) }, 0, 0));
            Assert.Equal("Day 14: came to know Troll, and your explored map showed 12 places and 1 biome", JournalWords.News(new[] { N("Troll", NewsHow.Known, 14) }, 12, 1));
            Assert.Equal("Your explored map showed 12 places", JournalWords.News(new NewsLine[0], 12, 0));
        }

        [Fact]
        public void WhatWasFoundOutIsNamedByWhatItTells()
        {
            Assert.Equal("Day 2: found out the health of Troll", JournalWords.News(new[] { N("Troll", NewsHow.FoundOut, 2, EntryKeys.Did(Deed.Killed)) }, 0, 0));
            Assert.Equal("Day 2: found out how Boar tames and 2 more", JournalWords.News(new[]
            {
                N("Boar", NewsHow.FoundOut, 2, EntryKeys.Did(Deed.Tamed)), N("Troll", NewsHow.FoundOut, 2, EntryKeys.Did(Deed.Killed)), N("Neck", NewsHow.FoundOut, 2, EntryKeys.Damage("fire")),
            }, 0, 0));
            foreach (Deed deed in System.Enum.GetValues(typeof(Deed)))
            {
                var said = JournalWords.FoundOutOf(EntryKeys.Did(deed), "Troll");
                Assert.Contains("Troll", said);
                Assert.DoesNotContain(deed.ToString(), said);
            }
        }

        [Fact]
        public void WhatTheGamesRecordsToldIsCountedNotNamed()
        {
            var before = Enumerable.Range(0, 900).Select(i => N("Thing" + i, NewsHow.Known, -1)).ToList();
            Assert.Equal("Read 900 things from the game's records of your character", JournalWords.News(before, 0, 0));
            before.Add(N("Troll", NewsHow.Known, 14));
            Assert.Equal("Day 14: came to know Troll, and read 900 things from the game's records of your character", JournalWords.News(before, 0, 0));
        }
    }
}
