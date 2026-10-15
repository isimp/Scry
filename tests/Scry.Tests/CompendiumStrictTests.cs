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
        public void AKnownCreaturesPageKeepsBackHowEachDamageLandsUntilTheCharacterHitItWithIt()
        {
            // The game's popup on a hit tells how its damage landed (Weak, Resistant, Immune, or
            // a plain number): a type so told shows, the rest read ??? under the strict rules.
            var world = new World();
            world.Book.Witness(world.Troll.Key, EntryKeys.Damage("Fire"));
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);

            Assert.False(world.Explorer.HidesDamage("Fire"));
            Assert.True(world.Explorer.HidesDamage("Frost"));

            // Not under the loose rules, and not on a page that is no creature's.
            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.HidesDamage("Frost"));
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Hide);
            Assert.False(world.Explorer.HidesDamage("Frost"));
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
        public void SeeingTwoThingsGoTogetherTellsNoLevelAgain()
        {
            // A pair seen in play makes nothing more or less known, so the whole catalog is not
            // gone through again for it, as it is for something learned.
            var world = new World().Rule(CompendiumRule.Strict);
            var version = world.View.Version;

            world.Book.Witness(world.Coins.Key, world.Troll.Key);
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(version, world.View.Version);

            world.Book.Raise(world.Squeal.Key, KnownLevel.Known);
            world.Rule(CompendiumRule.Strict);
            Assert.NotEqual(version, world.View.Version);
        }

        [Fact]
        public void UnderStrictAPairSeenShowsAtOnceOnThePageOpenAndInSearch()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);
            Assert.True(world.Explorer.LeavesOut(world.Coins.Key));
            var stamp = world.Explorer.KnowStamp;

            world.Book.Witness(world.Coins.Key, world.Troll.Key);
            world.Rule(CompendiumRule.Strict);

            Assert.False(world.Explorer.LeavesOut(world.Coins.Key));
            // What the panel keeps of the page by the explorer's stamp is made again.
            Assert.NotEqual(stamp, world.Explorer.KnowStamp);

            world.Explorer.Text = "drops:trollhide";
            Assert.DoesNotContain(world.Troll, world.Explorer.Results);
            world.Book.Witness(world.Hide.Key, world.Troll.Key);
            world.Rule(CompendiumRule.Strict);
            Assert.Contains(world.Troll, world.Explorer.Results);
        }

        [Fact]
        public void UnderLooseAPairSeenChangesNothingShown()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            var stamp = world.Explorer.KnowStamp;

            world.Book.Witness(world.Coins.Key, world.Troll.Key);
            world.Rule(CompendiumRule.Loose);

            Assert.Equal(stamp, world.Explorer.KnowStamp);
        }

        [Fact]
        public void APartsPageKeepsTheOwnerItIsPartOf()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Grunt);

            Assert.True(world.Explorer.LeavingOut);
            Assert.False(world.Explorer.LeavesOut(world.Troll.Key));
            Assert.True(world.Explorer.LeavesOut(world.Coins.Key));
        }

        [Fact]
        public void UnderStrictASearchFindsAKnownEntryOnlyByWhatWasSeenGoTogether()
        {
            // The troll was seen to drop its hide, never coins; its kind and parts are plain to see.
            var world = new World();
            world.Troll.Biomes = new[] { "BlackForest", "Mountain" };
            world.Troll.Components = new[] { "Character" };
            var forest = E("BlackForest", Kind.Biome);
            var all = new[] { world.Troll, world.Hide, world.Coins, world.Grunt, forest }.ToList();
            var explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            world.Book.Witness(world.Troll.Key, world.Hide.Key);
            world.Book.Witness(world.Troll.Key, forest.Key);
            view.Tell(world.Book, CompendiumRule.Strict);
            explorer.Know(view);

            explorer.Text = "drops:trollhide";
            Assert.Contains(world.Troll, explorer.Results);
            explorer.Text = "drops:coins";
            Assert.DoesNotContain(world.Troll, explorer.Results);
            explorer.Text = "biome:blackforest";
            Assert.Contains(world.Troll, explorer.Results);
            explorer.Text = "biome:mountain";
            Assert.DoesNotContain(world.Troll, explorer.Results);
            explorer.Text = "has:character";
            Assert.Contains(world.Troll, explorer.Results);
            explorer.Text = "playedby:troll";
            Assert.Contains(world.Grunt, explorer.Results);

            // Below the floor it is found as the loose rules find it.
            view.Milestones(new Dictionary<string, int> { { world.Troll.Key, 2 } }, 2);
            view.Tell(world.Book, CompendiumRule.Strict);
            explorer.Know(view);
            explorer.Text = "drops:coins";
            Assert.Contains(world.Troll, explorer.Results);
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
        public void OnlyAKnownOwnerBringsItsPartsAlongUnderEitherRules()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Unknown, world.View.Of(world.Squeal));

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

            // A new pair is noticed by what shows the strict rules, without telling every level again.
            var version = book.Version;
            var pairs = book.PairsVersion;
            book.Witness("Odd\tname", "Troll");
            Assert.NotEqual(pairs, book.PairsVersion);
            Assert.Equal(version, book.Version);
            var back = CompendiumBook.Load(book.Save());
            Assert.True(back.Witnessed("Troll", "TrollHide"));
            Assert.True(back.Witnessed("Troll", "Odd\tname"));
            Assert.Equal(2, back.Pairs);
            Assert.Equal(0, CompendiumBook.Load("saw\tTroll\nsaw\t\tTroll\nsaw\tTroll\tTroll").Pairs);
        }
    }
}
