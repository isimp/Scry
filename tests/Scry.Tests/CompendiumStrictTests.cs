using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class CompendiumStrictTests
    {
        // Strict shows what a known entry is, and of how it connects only what the character saw
        // go together: the drops it saw fall, the places it met a creature in, an item made at a
        // station. What was not witnessed is left out silently; a page's own parts (its sounds,
        // effects, projectiles) always show; below the milestone floor a page shows whole.

        private sealed class World
        {
            public readonly Entry Troll = E("Troll", Kind.Creature);
            public readonly Entry Boar = E("Boar", Kind.Creature);
            public readonly Entry Hide = E("TrollHide", Kind.Item);
            public readonly Entry Coins = E("Coins", Kind.Item);
            public readonly Entry Grunt = E("sfx_troll_grunt", Kind.Sound);
            public readonly Entry Squeal = E("sfx_boar_squeal", Kind.Sound);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                Troll.AddTermLink("drops", Hide);
                Troll.AddTermLink("drops", Coins);
                Grunt.UsedBy.Add("Troll");
                Squeal.UsedBy.Add("Boar");
                var all = new[] { Troll, Boar, Hide, Coins, Grunt, Squeal }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                Book.Raise(Troll.Key, KnownLevel.Known);
                Book.Raise(Boar.Key, KnownLevel.Seen);
                Book.Raise(Hide.Key, KnownLevel.Known);
                Book.Raise(Coins.Key, KnownLevel.Known);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        [Fact]
        public void AKnownPageLeavesOutWhatTheCharacterDidNotSeeGoTogether()
        {
            var world = new World();
            world.Book.Witness(world.Hide.Key, world.Troll.Key);
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);

            Assert.False(world.Explorer.LeavesOut(world.Hide.Key));
            Assert.True(world.Explorer.LeavesOut(world.Coins.Key));
            Assert.False(world.Explorer.LeavesOut(world.Grunt.Key));
            Assert.False(world.Explorer.LeavesOut(world.Troll.Key));
            Assert.False(world.Explorer.LeavesOut("NotInTheCatalog"));
        }

        [Fact]
        public void LooseOffAndAClosedPageLeaveNothingOut()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Troll);
            Assert.False(world.Explorer.LeavesOut(world.Coins.Key));

            world.Rule(CompendiumRule.Off);
            Assert.False(world.Explorer.LeavesOut(world.Coins.Key));

            // The boar, only seen: its page is closed, nothing on it to leave out.
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Boar);
            Assert.False(world.Explorer.LeavesOut(world.Coins.Key));
        }

        [Fact]
        public void AModsPageAndTheModThatAddedSomethingAreNeverLeftOut()
        {
            // A mod is always known; no pair is ever seen with one.
            var world = new World();
            var mod = E("Monstrum", Kind.Mod);
            var all = new[] { world.Troll, world.Coins, mod }.ToList();
            var explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            view.Tell(world.Book, CompendiumRule.Strict);
            explorer.Know(view);

            explorer.Select(world.Troll);
            Assert.True(explorer.LeavingOut);
            Assert.False(explorer.LeavesOut(mod.Key));
            Assert.True(explorer.LeavesOut(world.Coins.Key));

            explorer.Select(mod);
            Assert.False(explorer.LeavingOut);
            Assert.False(explorer.LeavesOut(world.Coins.Key));
        }

        [Fact]
        public void BelowTheFloorAPageShowsWhole()
        {
            var world = new World();
            world.View.Milestones(new Dictionary<string, int> { { world.Troll.Key, 2 } }, 2);
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);

            Assert.False(world.Explorer.LeavingOut);
            Assert.False(world.Explorer.LeavesOut(world.Coins.Key));
        }

        [Fact]
        public void UnderStrictOnlyAKnownOwnerBringsItsPartsAlong()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, world.View.Of(world.Squeal));

            world.Rule(CompendiumRule.Strict);
            Assert.Equal(KnownLevel.Unknown, world.View.Of(world.Squeal));
            Assert.Equal(KnownLevel.Known, world.View.Of(world.Grunt));
            Assert.True(world.View.Owns(world.Troll, world.Grunt));
            Assert.False(world.View.Owns(world.Grunt, world.Troll));
        }

        [Fact]
        public void WhatWasSeenGoTogetherIsKeptEitherWayRound()
        {
            var book = new CompendiumBook();
            Assert.True(book.Witness("Troll", "TrollHide"));
            Assert.True(book.Witnessed("TrollHide", "Troll"));
            Assert.False(book.Witnessed("Troll", "Coins"));
            book.Save();

            Assert.False(book.Witness("TrollHide", "Troll"));
            Assert.False(book.Changed);
            Assert.False(book.Witness("Troll", "Troll"));
            Assert.False(book.Witness("", "Troll"));

            var version = book.Version;
            book.Witness("Odd\tname", "Troll");
            Assert.NotEqual(version, book.Version);
            var back = CompendiumBook.Load(book.Save());
            Assert.True(back.Witnessed("Troll", "TrollHide"));
            Assert.True(back.Witnessed("Troll", "Odd\tname"));
            Assert.Equal(2, back.Pairs);
            Assert.Equal(0, CompendiumBook.Load("saw\tTroll\nsaw\t\tTroll\nsaw\tTroll\tTroll").Pairs);
        }
    }
}
