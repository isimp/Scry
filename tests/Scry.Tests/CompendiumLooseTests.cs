using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class CompendiumLooseTests
    {
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
            // whole topic and the look are left out, not a ??? where the saddle would be named.
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
            // Only its loot and where it lives count what was left out: counts the player can act on.
            Assert.Null(world.Explorer.LeftOutLine("making", 2));
            Assert.Equal("1 more place it lives not yet met in", world.Explorer.LeftOutLine("lives", 1));
            Assert.Null(world.Explorer.LeftOutLine("making", 0));

            world.Rule(CompendiumRule.Strict);
            Assert.Equal("2 more not yet seen fall", world.Explorer.LeftOutLine("loot", 2));
        }

        [Fact]
        public void ARoomCardCountsWhatWasNotMetWithNoTopicOfItsOwn()
        {
            // A dungeon room's card asks for its room: the creatures and loot of the room not met
            // are counted on it under either rule, a count the player can act on by going in. A
            // section with no topic counts nothing.
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Windmill);
            Assert.Equal("7 not yet met", world.Explorer.LeftOutLine("room", 7, alone: true));
            Assert.Equal("2 more not yet met", world.Explorer.LeftOutLine("room", 2));
            Assert.Null(world.Explorer.LeftOutLine("room", 0));
            Assert.Null(world.Explorer.LeftOutLine(null, 7));
            world.Rule(CompendiumRule.Strict);
            Assert.Equal("7 not yet seen here", world.Explorer.LeftOutLine("room", 7, alone: true));
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
