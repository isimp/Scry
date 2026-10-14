using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class MilestoneTests
    {
        // A tier counts as beaten when the character's records show its boss fell: its kill in
        // the statistics, its defeat key among the character's uniques, or its trophy held. The
        // player raises the floor tier by tier, as far as the bosses beaten in a row reach, and
        // everything of a tier at or below the floor shows as known.

        private static readonly BossMarks[] Bosses =
        {
            new BossMarks("$enemy_eikthyr", "defeated_eikthyr", "TrophyEikthyr"),
            new BossMarks("$enemy_gdking", "defeated_gdking", "TrophyTheElder"),
            new BossMarks("$enemy_bonemass", "defeated_bonemass", "TrophyBonemass"),
            new BossMarks("$enemy_dragon", "defeated_dragon", "TrophyDragonQueen"),
        };

        [Fact]
        public void ABossFallsByItsKillItsKeyOrItsTrophy()
        {
            var records = new CompendiumRecords();
            Assert.False(Milestones.Beaten(records, Bosses[0]));

            records.Killed.Add("$enemy_eikthyr");
            records.Uniques.Add("defeated_gdking");
            records.Trophies.Add("TrophyBonemass");

            Assert.True(Milestones.Beaten(records, Bosses[0]));
            Assert.True(Milestones.Beaten(records, Bosses[1]));
            Assert.True(Milestones.Beaten(records, Bosses[2]));
            Assert.False(Milestones.Beaten(records, Bosses[3]));
            Assert.False(Milestones.Beaten(records, new BossMarks(null, null, null)));
            records.Killed.Add("");
            Assert.False(Milestones.Beaten(records, new BossMarks("", "", "")));
        }

        [Fact]
        public void TheFloorReachesAsFarAsTheBossesBeatenInARow()
        {
            var records = new CompendiumRecords();
            Assert.Equal(0, Milestones.Ceiling(records, Bosses));

            records.Killed.Add("$enemy_eikthyr");
            records.Killed.Add("$enemy_bonemass");
            Assert.Equal(1, Milestones.Ceiling(records, Bosses));

            records.Uniques.Add("defeated_gdking");
            Assert.Equal(3, Milestones.Ceiling(records, Bosses));
        }

        [Fact]
        public void TheFloorIsKeptWithTheCharactersBook()
        {
            var book = new CompendiumBook();
            Assert.Equal(0, book.Floor);

            book.Floor = 3;
            Assert.True(book.Changed);
            Assert.Equal(3, CompendiumBook.Load(book.Save()).Floor);
            Assert.False(book.Changed);

            book.Floor = 3;
            Assert.False(book.Changed);
            book.Floor = 99;
            Assert.Equal(Tiers.Last, book.Floor);
            book.Floor = -2;
            Assert.Equal(0, book.Floor);
            Assert.Equal(0, CompendiumBook.Load("floor\tlots").Floor);
        }

        [Fact]
        public void EverythingOfATierAtOrBelowTheFloorShowsAsKnown()
        {
            var troll = new Entry { Kind = Kind.Creature, Name = "Troll" };
            var boar = new Entry { Kind = Kind.Creature, Name = "Boar" };
            var wolf = new Entry { Kind = Kind.Creature, Name = "Wolf" };
            var serpent = new Entry { Kind = Kind.Creature, Name = "Serpent" };
            var hammer = new Entry { Kind = Kind.Item, Name = "Hammer" };
            var sound = new Entry { Kind = Kind.Sound, Name = "sfx_troll_idle" };
            sound.UsedBy.Add("Troll");
            var view = new CompendiumView(new EntryCatalog(new[] { troll, boar, wolf, serpent, hammer, sound }));
            var tiers = new Dictionary<string, int> { { troll.Key, 2 }, { boar.Key, 1 }, { wolf.Key, 4 }, { hammer.Key, 0 } };
            var book = new CompendiumBook();

            view.Milestones(tiers, 0);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Unknown, view.Of(boar));
            Assert.Equal(KnownLevel.Unknown, view.Of(hammer));

            view.Milestones(tiers, 2);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, view.Of(troll));
            Assert.Equal(KnownLevel.Known, view.Of(boar));
            Assert.Equal(KnownLevel.Known, view.Of(sound));
            Assert.Equal(KnownLevel.Unknown, view.Of(wolf));
            Assert.Equal(KnownLevel.Unknown, view.Of(serpent));

            view.Milestones(tiers, 1);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Unknown, view.Of(troll));
            Assert.Equal(KnownLevel.Known, view.Of(boar));
        }
    }
}
