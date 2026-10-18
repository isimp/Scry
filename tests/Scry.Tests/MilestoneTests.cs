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
        public void ALinkOnAPageToWhatOnlyTheFloorShowsIsToldApartByItsKey()
        {
            // A chip or link on another page naming it is marked as its row and page are.
            var troll = new Entry { Kind = Kind.Creature, Name = "Troll" };
            var boar = new Entry { Kind = Kind.Creature, Name = "Boar" };
            var all = new List<Entry> { troll, boar };
            var explorer = new Explorer(all, new Favourites(System.IO.Path.Combine(TestCatalog.TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            book.Raise(boar.Key, KnownLevel.Known);
            view.Milestones(new Dictionary<string, int> { { troll.Key, 2 }, { boar.Key, 1 } }, 2);
            view.Tell(book, CompendiumRule.Loose);
            explorer.Know(view);

            Assert.True(explorer.ShownByFloor(troll.Key));
            Assert.False(explorer.ShownByFloor(boar.Key));
            Assert.False(explorer.ShownByFloor("NotInTheCatalog"));
            Assert.False(explorer.ShownByFloor(null));

            // Peeking at everything, nothing is told apart; off, the floor shows nothing.
            explorer.Peeking = true;
            Assert.False(explorer.ShownByFloor(troll.Key));
            explorer.Peeking = false;
            Assert.True(explorer.ShownByFloor(troll.Key));
            view.Tell(book, CompendiumRule.Off);
            explorer.Know(view);
            Assert.False(explorer.ShownByFloor(troll.Key));
        }

        [Fact]
        public void WhatShowsKnownOnlyBecauseOfTheFloorIsToldApart()
        {
            // The floor shows things as known that the character never met: the panel marks them.
            var troll = new Entry { Kind = Kind.Creature, Name = "Troll" };
            var boar = new Entry { Kind = Kind.Creature, Name = "Boar" };
            var neck = new Entry { Kind = Kind.Creature, Name = "Neck" };
            var greydwarf = new Entry { Kind = Kind.Creature, Name = "Greydwarf" };
            var trollSound = new Entry { Kind = Kind.Sound, Name = "sfx_troll_idle" };
            var hit = new Entry { Kind = Kind.Sound, Name = "sfx_hit" };
            trollSound.UsedBy.Add("Troll");
            hit.UsedBy.Add("Troll");
            hit.UsedBy.Add("Boar");
            var view = new CompendiumView(new EntryCatalog(new[] { troll, boar, neck, greydwarf, trollSound, hit }));
            var tiers = new Dictionary<string, int> { { troll.Key, 2 }, { boar.Key, 1 }, { neck.Key, 1 }, { greydwarf.Key, 3 }, { hit.Key, 1 } };
            var book = new CompendiumBook();
            book.Raise(boar.Key, KnownLevel.Known);
            book.Raise(neck.Key, KnownLevel.Seen);

            view.Milestones(tiers, 2);
            view.Tell(book, CompendiumRule.Loose);

            Assert.True(view.ByFloor(troll));
            Assert.True(view.ByFloor(trollSound));
            // Seen, its page opens only by the floor.
            Assert.True(view.ByFloor(neck));
            // Known by the character, or along with what it knows, below the floor or not: not the floor's doing.
            Assert.False(view.ByFloor(boar));
            Assert.False(view.ByFloor(hit));
            Assert.False(view.ByFloor(greydwarf));
            Assert.Equal(KnownLevel.Unknown, view.Of(greydwarf));

            view.Milestones(tiers, 0);
            view.Tell(book, CompendiumRule.Loose);
            Assert.False(view.ByFloor(troll));
            Assert.False(view.ByFloor(neck));
            view.Milestones(tiers, 2);
            view.Tell(book, CompendiumRule.Off);
            Assert.False(view.ByFloor(troll));
        }

        [Fact]
        public void TheFloorBringsAlongNothingOfATierAboveIt()
        {
            // A torch of the first tier is known by a floor of the second, and the sound it plays
            // with it; the status effect it links to, of the third tier, waits for its own tier.
            // What the character knows itself still brings along all it owns.
            var torch = new Entry { Kind = Kind.Item, Name = "Torch" };
            var lantern = new Entry { Kind = Kind.Item, Name = "Lantern" };
            var warmth = new Entry { Kind = Kind.StatusEffect, Name = "Warm" };
            var glow = new Entry { Kind = Kind.StatusEffect, Name = "Glow" };
            var crackle = new Entry { Kind = Kind.Sound, Name = "sfx_torch" };
            crackle.UsedBy.Add("Torch");
            torch.Links.Add(new Link { Group = "Status effects", Target = warmth.Key });
            lantern.Links.Add(new Link { Group = "Status effects", Target = glow.Key });
            var view = new CompendiumView(new EntryCatalog(new[] { torch, lantern, warmth, glow, crackle }));
            // The sound has the torch's tier, as the tiers give what is played its owner's.
            var tiers = new Dictionary<string, int> { { torch.Key, 1 }, { crackle.Key, 1 }, { warmth.Key, 3 }, { lantern.Key, 4 }, { glow.Key, 4 } };
            var book = new CompendiumBook();
            book.Raise(lantern.Key, KnownLevel.Known);

            view.Milestones(tiers, 2);
            view.Tell(book, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.Known, view.Of(torch));
            Assert.Equal(KnownLevel.Known, view.Of(crackle));
            Assert.True(view.ByFloor(crackle));
            Assert.NotEqual(KnownLevel.Known, view.Of(warmth));
            Assert.Equal(KnownLevel.Known, view.Of(glow));

            Assert.False(view.Owns(torch, warmth));

            // A floor reaching the effect's tier: the torch owns it again.
            view.Milestones(tiers, 3);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, view.Of(warmth));
            Assert.True(view.ByFloor(warmth));
            Assert.True(view.Owns(torch, warmth));
        }

        [Fact]
        public void TheFloorNeverBringsAlongACreatureALinkNamesWithNoTierOfItsOwn()
        {
            // A creature with no tier of its own, named by a link of something the floor opens, is
            // never brought along by the floor: a creature is a thing of its own. The character
            // meeting it does, and what the character knows itself still brings along what it owns.
            var torch = new Entry { Kind = Kind.Item, Name = "Torch" };
            var lantern = new Entry { Kind = Kind.Item, Name = "Lantern" };
            var odin = new Entry { Kind = Kind.Creature, Name = "odin" };
            var glint = new Entry { Kind = Kind.Effect, Name = "vfx_glint" };
            torch.Links.Add(new Link { Group = "Creatures", Target = odin.Key });
            glint.UsedBy.Add("Lantern");
            var view = new CompendiumView(new EntryCatalog(new[] { torch, lantern, odin, glint }));
            var tiers = new Dictionary<string, int> { { torch.Key, 1 }, { lantern.Key, 4 } };
            var book = new CompendiumBook();
            book.Raise(lantern.Key, KnownLevel.Known);

            view.Milestones(tiers, 2);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, view.Of(torch));
            Assert.NotEqual(KnownLevel.Known, view.Of(odin));
            Assert.False(view.ByFloor(odin));
            Assert.False(view.Owns(torch, odin));
            // Known by the character itself, the lantern still brings along what it plays.
            Assert.Equal(KnownLevel.Known, view.Of(glint));
            Assert.False(view.ByFloor(glint));

            // Met, it is known like anything else.
            book.Raise(odin.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, view.Of(odin));
            Assert.False(view.ByFloor(odin));
        }

        [Fact]
        public void ABossStoneNeverBringsAlongItsPower()
        {
            // A stone gives its power only for the boss's trophy: knowing the stone, by the
            // character or by the floor, tells of the power without making it known. The power
            // is known once chosen, or by a floor that reaches its own tier.
            var stone = new Entry { Kind = Kind.Piece, Name = "BossStone_Bonemass" };
            var power = new Entry { Kind = Kind.StatusEffect, Name = "GP_Bonemass" };
            var fire = new Entry { Kind = Kind.Piece, Name = "fire_pit" };
            var burning = new Entry { Kind = Kind.StatusEffect, Name = "Burning" };
            stone.Links.Add(new Link { Group = "Status effects", Target = power.Key, Notes = { "guardian power" } });
            fire.Links.Add(new Link { Group = "Status effects", Target = burning.Key, Notes = { "fire" } });
            var view = new CompendiumView(new EntryCatalog(new[] { stone, power, fire, burning }));
            var book = new CompendiumBook();
            book.Raise(stone.Key, KnownLevel.Known);
            book.Raise(fire.Key, KnownLevel.Known);

            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.HeardOf, view.Of(power));
            Assert.Equal(KnownLevel.Known, view.Of(burning));

            var floored = new CompendiumView(new EntryCatalog(new[] { stone, power }));
            var tiers = new Dictionary<string, int> { { stone.Key, 1 }, { power.Key, 3 } };
            floored.Milestones(tiers, 2);
            floored.Tell(new CompendiumBook(), CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, floored.Of(stone));
            Assert.Equal(KnownLevel.HeardOf, floored.Of(power));
            floored.Milestones(tiers, 3);
            floored.Tell(new CompendiumBook(), CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, floored.Of(power));

            // Raised to known while the view stands, the stone tells of its power and no more.
            var raised = new CompendiumView(new EntryCatalog(new[] { stone, power }));
            var later = new CompendiumBook();
            raised.Tell(later, CompendiumRule.Loose);
            later.Raise(stone.Key, KnownLevel.Known);
            raised.Tell(later, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.HeardOf, raised.Of(power));
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
