using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class CompendiumLooseTests
    {
        /// <summary>The one line a section ends in that left so many out, all never met under the loose rules and all met under the strict ones.</summary>
        private static string Line(Explorer explorer, string topic, int count, bool alone = false) =>
            explorer.LeftOutLines(topic, explorer.LeavingOut ? 0 : count, explorer.LeavingOut ? count : 0, alone).Select(l => l.Line).FirstOrDefault();

        // Under the loose rules a known page shows whole, but of what it names only what the
        // character has met, seen or come to know: the rest is left out and counted, and no
        // search finds the page by it. A known windmill does not tell what it grinds before the
        // grain was met. What the floor shows counts as met; off and peeking name everything.

        private sealed class World
        {
            public readonly Entry Windmill = E("windmill", Kind.Piece, "Windmill");
            public readonly Entry Barley = E("Barley", Kind.Item, "Barley");
            public readonly Entry Flour = E("BarleyFlour", Kind.Item, "Barley Flour");
            public readonly Entry Stone = E("Stone", Kind.Item, "Stone");
            public readonly Entry Wood = E("Wood", Kind.Item, "Wood");
            public readonly Entry Neck = E("Neck", Kind.Creature, "Neck");
            public readonly Entry Plains = E("Plains", Kind.Biome, "Plains");
            public readonly Entry Mod = E("Monstrum", Kind.Mod);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                Windmill.AddTermLink("gives", Flour);
                Windmill.AddTermLink("needs", Barley);
                Windmill.AddTermLink("needs", Stone);
                Neck.Biomes = new[] { "Plains" };
                var all = new[] { Windmill, Barley, Flour, Stone, Wood, Neck, Plains, Mod }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                Book.Raise(Windmill.Key, KnownLevel.Known);
                Book.Raise(Stone.Key, KnownLevel.Known);
                Book.Raise(Wood.Key, KnownLevel.Seen);
                Book.Raise(Neck.Key, KnownLevel.Known);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        [Fact]
        public void WhatHingesOnAThingNotMetIsLeftOutWholeNotNamedAsUnknown()
        {
            // A lox's riding hinges on its saddle: stamina and speed when ridden, and a saddled look
            // on the stage, would tell it can be ridden. Until the saddle is met, seen counting, the
            // whole topic and the look are left out, not a –– where the saddle would be named.
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Neck);
            Assert.True(world.Explorer.HidesHinged(world.Barley.Key));
            Assert.False(world.Explorer.HidesHinged(world.Wood.Key));
            Assert.False(world.Explorer.HidesHinged(world.Stone.Key));
            Assert.False(world.Explorer.HidesHinged(null));
            // The strict rules likewise; with the compendium off, everything shows.
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Neck);
            Assert.True(world.Explorer.HidesHinged(world.Barley.Key));
            world.Rule(CompendiumRule.Off);
            world.Explorer.Select(world.Neck);
            Assert.False(world.Explorer.HidesHinged(world.Barley.Key));
        }

        [Fact]
        public void AGearLookShowsOnlyOnceEverythingItWearsWasMet()
        {
            // A creature's looks on the stage: no gear, a set of Stone met, a set holding Barley
            // never met. The set of Barley would show and name it, so it is left out; with the
            // compendium off every look shows.
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Neck);
            IReadOnlyList<string> ItemsOf(int look) => look == 1 ? new[] { world.Stone.Key } : look == 2 ? new[] { world.Stone.Key, world.Barley.Key } : new string[0];
            Assert.Equal(new[] { 0, 1 }, world.Explorer.LooksShown(3, ItemsOf));
            world.Rule(CompendiumRule.Off);
            world.Explorer.Select(world.Neck);
            Assert.Equal(new[] { 0, 1, 2 }, world.Explorer.LooksShown(3, ItemsOf));
        }

        [Fact]
        public void AKnownWindmillDoesNotTellWhatItGrindsBeforeTheGrainWasMet()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Windmill);

            Assert.True(world.Explorer.NamesOnlyMet);
            Assert.True(world.Explorer.LeavesOut(world.Barley.Key));
            Assert.True(world.Explorer.LeavesOut(world.Flour.Key));
            // The numbers the game never shows stay on a loose page: only names are left out.
            Assert.False(world.Explorer.LeavingOut);

            // Seen, the grain is named; the flour still waits.
            world.Book.Raise(world.Barley.Key, KnownLevel.Seen);
            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.LeavesOut(world.Barley.Key));
            Assert.True(world.Explorer.LeavesOut(world.Flour.Key));
        }

        [Fact]
        public void WhatTheCharacterHasSeenOrKnowsIsNamed()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Windmill);

            Assert.False(world.Explorer.LeavesOut(world.Stone.Key));
            Assert.False(world.Explorer.LeavesOut(world.Wood.Key));
            Assert.False(world.Explorer.LeavesOut(world.Windmill.Key));
            Assert.False(world.Explorer.LeavesOut(world.Mod.Key));
            Assert.False(world.Explorer.LeavesOut("NotInTheCatalog"));
            Assert.False(world.Explorer.LeavesOut(null));
        }

        [Fact]
        public void WhatTheFloorShowsCountsAsMet()
        {
            var world = new World();
            world.View.Milestones(new Dictionary<string, int> { { world.Barley.Key, 1 } }, 1);
            world.Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Windmill);

            Assert.False(world.Explorer.LeavesOut(world.Barley.Key));
            Assert.True(world.Explorer.LeavesOut(world.Flour.Key));
        }

        [Fact]
        public void OffPeekingAndAClosedPageNameEverything()
        {
            var world = new World().Rule(CompendiumRule.Off);
            world.Explorer.Select(world.Windmill);
            Assert.False(world.Explorer.NamesOnlyMet);
            Assert.False(world.Explorer.LeavesOut(world.Barley.Key));

            world.Rule(CompendiumRule.Loose);
            world.Explorer.Peeking = true;
            Assert.False(world.Explorer.NamesOnlyMet);
            Assert.False(world.Explorer.LeavesOut(world.Barley.Key));
            world.Explorer.Peeking = false;

            // Wood, only seen: its page is closed, nothing on it to leave out.
            world.Explorer.Select(world.Wood);
            Assert.False(world.Explorer.NamesOnlyMet);
            Assert.False(world.Explorer.LeavesOut(world.Barley.Key));
        }

        [Fact]
        public void AModsPageListsWhatItAddsByItsOwnRule()
        {
            // A mod's page lists only what was seen of what it adds already, and nothing else.
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Mod);

            Assert.False(world.Explorer.NamesOnlyMet);
            Assert.False(world.Explorer.LeavesOut(world.Barley.Key));
        }

        [Fact]
        public void UnderStrictWhatWasNeverMetIsLeftOutBelowTheFloorToo()
        {
            // Below the floor a strict page shows as a loose one does: whole, naming what was met.
            var world = new World();
            world.View.Milestones(new Dictionary<string, int> { { world.Windmill.Key, 1 } }, 1);
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Windmill);

            Assert.False(world.Explorer.LeavingOut);
            Assert.True(world.Explorer.NamesOnlyMet);
            Assert.True(world.Explorer.LeavesOut(world.Barley.Key));
            Assert.False(world.Explorer.LeavesOut(world.Stone.Key));
        }

        [Fact]
        public void UnderLooseASearchThroughWhatAPageNamesFindsNothingByWhatWasNotMet()
        {
            var world = new World().Rule(CompendiumRule.Loose);

            world.Explorer.Text = "gives:flour";
            Assert.DoesNotContain(world.Windmill, world.Explorer.Results);
            world.Explorer.Text = "needs:barley";
            Assert.DoesNotContain(world.Windmill, world.Explorer.Results);
            world.Explorer.Text = "needs:stone";
            Assert.Contains(world.Windmill, world.Explorer.Results);
            world.Explorer.Text = "biome:plains";
            Assert.DoesNotContain(world.Neck, world.Explorer.Results);

            world.Book.Raise(world.Flour.Key, KnownLevel.Seen);
            world.Book.Raise(world.Plains.Key, KnownLevel.Seen);
            world.Rule(CompendiumRule.Loose);
            world.Explorer.Text = "gives:flour";
            Assert.Contains(world.Windmill, world.Explorer.Results);
            world.Explorer.Text = "biome:plains";
            Assert.Contains(world.Neck, world.Explorer.Results);

            // Off, everything is found by everything it names.
            world.Rule(CompendiumRule.Off);
            world.Explorer.Text = "needs:barley";
            Assert.Contains(world.Windmill, world.Explorer.Results);
        }

        [Fact]
        public void UnderLooseAPlaceNotMetFindsNothingAndANumberFindsAsTheGameHasIt()
        {
            // A troll is found in a kind of place one of whose locations was met, not in one never
            // met; how it takes fire, a number the loose rules keep whole, finds it unhit.
            var book = new CompendiumBook();
            var troll = E("Troll", Kind.Creature, "Troll");
            troll.FoundIn = new[] { "Troll cave", "Burial chambers" };
            troll.SetTermWords("weak", new[] { "fire" });
            var cave = new Entry { Kind = Kind.Location, Name = "TrollCave02", Components = new[] { "Location" }, FoundIn = new[] { "Troll cave" } };
            var crypt = new Entry { Kind = Kind.Location, Name = "Crypt2", Components = new[] { "Location" }, FoundIn = new[] { "Burial chambers" } };
            var explorer = new Explorer(new[] { troll, cave, crypt }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            book.Raise(troll.Key, KnownLevel.Known);
            book.Raise(cave.Key, KnownLevel.Seen);
            view.Tell(book, CompendiumRule.Loose);
            explorer.Know(view);

            explorer.Text = "in:trollcave";
            Assert.Contains(troll, explorer.Results);
            explorer.Text = "in:burial";
            Assert.DoesNotContain(troll, explorer.Results);
            explorer.Text = "weak:fire";
            Assert.Contains(troll, explorer.Results);
        }

        [Fact]
        public void WhatWasLeftOutIsCountedInTheWordsOfTheRules()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Windmill);
            // Every section counts what it left out as not yet met, a creature's places by what they are.
            Assert.Equal("2 more not yet met", Line(world.Explorer, "making", 2));
            Assert.Equal("1 more place it lives not yet found", Line(world.Explorer, "lives", 1));
            Assert.Null(Line(world.Explorer, "making", 0));

            world.Rule(CompendiumRule.Strict);
            Assert.Equal("2 more not yet seen fall", Line(world.Explorer, "loot", 2));
        }

        [Fact]
        public void UnderStrictASectionSaysApartWhatToMeetAndWhatToSeeTogether()
        {
            // What was never met and what was met but not seen with the page wait for different
            // things, so each gets its own line and its hover says the way to it.
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Windmill);
            Assert.Equal(new[]
            {
                ("9 more not yet met", CompendiumWords.MeetFirstTip(strict: true)),
                ("3 more not yet seen together", CompendiumWords.TogetherTip("fight")),
            }, world.Explorer.LeftOutLines("fight", unmet: 9, apart: 3));
            // Alone, where the section names nothing else; only one line where only one kind was left out.
            Assert.Equal(new[] { ("2 not yet seen fall", CompendiumWords.TogetherTip("loot")) }, world.Explorer.LeftOutLines("loot", 0, 2, alone: true));
            Assert.Equal(new[] { ("1 more not yet met", CompendiumWords.MeetFirstTip(strict: true)) }, world.Explorer.LeftOutLines("from", 1, 0));
            // Where it lives: a place never found apart from a place found but where it was not met.
            Assert.Equal(new[]
            {
                ("2 more places it lives not yet found", CompendiumWords.MeetFirstTip(strict: true)),
                ("1 more place it lives not yet met in", CompendiumWords.TogetherTip("lives")),
            }, world.Explorer.LeftOutLines("lives", 2, 1));
            Assert.Empty(world.Explorer.LeftOutLines("fight", 0, 0));

            // Loose leaves out only what was never met, in one line for every section.
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(new[] { ("2 more not yet met", CompendiumWords.MeetFirstTip(strict: false)) }, world.Explorer.LeftOutLines("loot", 2, 0));
            Assert.Equal(new[] { ("3 more not yet met", CompendiumWords.MeetFirstTip(strict: false)) }, world.Explorer.LeftOutLines("loot", 2, 1));
            Assert.Equal(new[] { ("2 more not yet met", CompendiumWords.MeetFirstTip(strict: false)) }, world.Explorer.LeftOutLines("making", 2, 0));
            Assert.Empty(world.Explorer.LeftOutLines("making", 0, 0));
        }

        [Fact]
        public void ACardCountsWhatItLeftOutByWhetherEachWasMet()
        {
            // A room's or raid's card has the keys it left out: each counted as never met or as met but not seen with it.
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Windmill);
            Assert.Equal(new[] { "1 more not yet met", "2 more not yet seen here" },
                world.Explorer.LeftOutCounts("room", new[] { world.Barley.Key, world.Stone.Key, world.Neck.Key }));
            Assert.Equal(new[] { "1 not yet met" }, world.Explorer.LeftOutCounts("raid", new[] { world.Barley.Key }, alone: true));
            Assert.Empty(world.Explorer.LeftOutCounts("room", new string[0]));
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(new[] { "3 more not yet met" }, world.Explorer.LeftOutCounts("room", new[] { world.Barley.Key, world.Stone.Key, world.Neck.Key }));
        }

        [Fact]
        public void UnderEitherRulesEverySectionSaysHowManyItLeftOut()
        {
            // Strict leaves out what was not seen together with the page; each section says how
            // many at its end, so the page never goes quiet on what it keeps back.
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Windmill);
            Assert.Equal("2 more not yet seen together", Line(world.Explorer, "making", 2));
            Assert.Equal("1 more not yet seen give it", Line(world.Explorer, "from", 1));
            // A section with no topic of its own, as LINKED, counts too; one left with nothing else says it alone.
            Assert.Equal("11 not yet seen together", Line(world.Explorer, null, 11, alone: true));
            Assert.Null(Line(world.Explorer, "making", 0));
            // Loose leaves out only what was never met, and says so in every section just the same.
            world.Rule(CompendiumRule.Loose);
            Assert.Equal("2 more not yet met", Line(world.Explorer, "making", 2));
            Assert.Equal("1 more not yet met", Line(world.Explorer, "from", 1));
            Assert.Equal("11 not yet met", Line(world.Explorer, null, 11, alone: true));
            Assert.Null(Line(world.Explorer, "making", 0));
        }

        [Fact]
        public void UnderLooseEveryNameThePageCouldLeaveOutUnmetIsOneOfItsFinds()
        {
            // The loose title chip counts how much of what the page names was met: the grain never
            // met is a find still left out, the stone met is one found, game-shown or not. The
            // page's own, a mod's and one not in the catalog are never left out, so no finds.
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Windmill);
            Assert.True(world.Explorer.Hideable(world.Barley.Key));
            Assert.True(world.Explorer.LeavesOut(world.Barley.Key));
            Assert.True(world.Explorer.Hideable(world.Stone.Key));
            Assert.True(world.Explorer.Hideable(world.Stone.Key, gameShown: true));
            Assert.False(world.Explorer.LeavesOut(world.Stone.Key));
            Assert.True(world.Explorer.Hideable(world.Wood.Key));
            foreach (var key in new[] { world.Windmill.Key, world.Mod.Key, "NotInTheCatalog", null })
            {
                Assert.False(world.Explorer.Hideable(key));
                Assert.False(world.Explorer.LeavesOut(key));
            }
            // Its numbers stay whole: names are the only finds.
            Assert.False(world.Explorer.KeepsBack(EntryKeys.Did(Deed.Killed)));
        }

        [Fact]
        public void AKnownPageCountsWhatItNamesUnderLooseAndWhatItKeepsBackUnderStrict()
        {
            // Under the loose rules a known page counts the names it could leave out (met or not
            // yet); under the strict rules everything it keeps back. Off, peeking, a page only
            // seen and a mod's page count nothing.
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Windmill);
            Assert.True(world.Explorer.CountingMet);
            Assert.True(world.Explorer.CountingFinds);
            Assert.False(world.Explorer.LeavingOut);

            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Windmill);
            Assert.False(world.Explorer.CountingMet);
            Assert.True(world.Explorer.CountingFinds);

            world.Rule(CompendiumRule.Off);
            world.Explorer.Select(world.Windmill);
            Assert.False(world.Explorer.CountingFinds);

            world.Rule(CompendiumRule.Loose);
            world.Explorer.Peeking = true;
            Assert.False(world.Explorer.CountingFinds);
            world.Explorer.Peeking = false;
            world.Explorer.Select(world.Wood);
            Assert.False(world.Explorer.CountingFinds);
            world.Explorer.Select(world.Mod);
            Assert.False(world.Explorer.CountingFinds);
            Assert.False(world.Explorer.Hideable(world.Barley.Key));
        }

        [Fact]
        public void UnderStrictEveryNameThePageCouldLeaveOutIsOneOfItsFinds()
        {
            // A name the page could leave out counts toward the title chip whether it is left out
            // now or was seen together with the page: the grain never met, the stone met but not
            // seen with the windmill. The page's own, a mod's, one the game shows and one not in
            // the catalog are no finds, and none of them is ever left out.
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Windmill);
            Assert.True(world.Explorer.Hideable(world.Barley.Key));
            Assert.True(world.Explorer.LeavesOut(world.Barley.Key));
            Assert.True(world.Explorer.Hideable(world.Stone.Key));
            Assert.True(world.Explorer.LeavesOut(world.Stone.Key));
            foreach (var (key, gameShown) in new[] { (world.Windmill.Key, false), (world.Mod.Key, false), (world.Stone.Key, true), ("NotInTheCatalog", false), ((string)null, false) })
            {
                Assert.False(world.Explorer.Hideable(key, gameShown));
                Assert.False(world.Explorer.LeavesOut(key, gameShown));
            }
        }

        [Fact]
        public void ARoomCardCountsWhatWasNotMetWithNoTopicOfItsOwn()
        {
            // A dungeon room's card asks for its room: the creatures and loot of the room not met
            // are counted on it under either rule, a count the player can act on by going in.
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Windmill);
            Assert.Equal("7 not yet met", Line(world.Explorer, "room", 7, alone: true));
            Assert.Equal("2 more not yet met", Line(world.Explorer, "room", 2));
            Assert.Null(Line(world.Explorer, "room", 0));
            world.Rule(CompendiumRule.Strict);
            Assert.Equal("7 not yet seen here", Line(world.Explorer, "room", 7, alone: true));
        }

        [Fact]
        public void ACreatureNotYetMetIsKnownAsSuchWhateverThePageShown()
        {
            // The stage leaves a creature not yet met off a place's example, its page open or not.
            var world = new World();
            var troll = E("Troll", Kind.Creature, "Troll");
            var boar = E("Boar", Kind.Creature, "Boar");
            world.Book.Raise(boar.Key, KnownLevel.Seen);
            var all = new[] { world.Windmill, world.Neck, troll, boar }.ToList();
            var explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            view.Tell(world.Book, CompendiumRule.Loose);
            explorer.Know(view);

            Assert.True(explorer.NotMet(troll.Key));
            Assert.False(explorer.NotMet(world.Neck.Key));
            Assert.False(explorer.NotMet(boar.Key));
            Assert.False(explorer.NotMet("NotInTheCatalog"));
            explorer.Select(world.Windmill);
            Assert.True(explorer.NotMet(troll.Key));

            explorer.Peeking = true;
            Assert.False(explorer.NotMet(troll.Key));
            explorer.Peeking = false;
            view.Tell(world.Book, CompendiumRule.Off);
            explorer.Know(view);
            Assert.False(explorer.NotMet(troll.Key));
        }
    }
}
