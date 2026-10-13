using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class CompendiumListTests
    {
        // With the compendium on, the list and the search show what the character has met or
        // heard of; a kind's tab with nothing typed shows what it has not met yet as silhouettes,
        // so the tab reads as a collection to complete, and counts what was found of all there is.

        private sealed class World
        {
            public readonly Entry Troll = E("Troll", Kind.Creature);
            public readonly Entry Boar = E("Boar", Kind.Creature);
            public readonly Entry Fenring = E("Fenring", Kind.Creature);
            public readonly Entry Hide = E("TrollHide", Kind.Item);
            public readonly Entry Splash = E("fx_fenring_splash", Kind.Effect);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                Troll.Components = new[] { "Character" };
                Boar.Components = new[] { "Character" };
                Troll.AddTermLink("drops", Hide);
                var all = new[] { Troll, Boar, Fenring, Hide, Splash }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                Book.Raise(Troll.Key, KnownLevel.Known);
                Book.Raise(Boar.Key, KnownLevel.Seen);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        [Fact]
        public void WithTheCompendiumOffEverythingIsListedAsBefore()
        {
            var world = new World().Rule(CompendiumRule.Off);

            Assert.Equal(5, world.Explorer.Results.Count);
            Assert.False(world.Explorer.CompendiumOn);
            Assert.Equal(KnownLevel.Known, world.Explorer.LevelOf(world.Fenring));
        }

        [Fact]
        public void UnderLooseTheListHoldsOnlyWhatTheCharacterHasSeenOrKnows()
        {
            // The troll's hide is named on the troll's page, heard of: that is no reason to list it.
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;

            Assert.True(explorer.CompendiumOn);
            Assert.Equal(KnownLevel.HeardOf, explorer.LevelOf(world.Hide));
            Assert.Equal(new[] { world.Boar, world.Troll }.OrderBy(e => e.Name), explorer.Results.OrderBy(e => e.Name));

            explorer.Text = "fenring";
            Assert.Empty(explorer.Results);
            explorer.Text = "troll";
            Assert.Equal(new[] { world.Troll }, explorer.Results.ToArray());
        }

        [Fact]
        public void ASearchTermTellingWhatAPageHoldsFindsOnlyWhatIsKnown()
        {
            // The boar was only seen: its page is closed, so has: and drops: cannot find it,
            // nor tell by leaving it out that it would not match. Its kind is plain to see.
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;

            explorer.Text = "has:character";
            Assert.Equal(new[] { world.Troll }, explorer.Results.ToArray());
            explorer.Text = "-has:nothing kind:creature";
            Assert.Equal(new[] { world.Troll }, explorer.Results.ToArray());
            explorer.Text = "kind:creature";
            Assert.Equal(2, explorer.Results.Count);
        }

        [Fact]
        public void AKindsTabWithNothingTypedListsWhatIsNotMetYetAsSilhouettes()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;

            explorer.KindFilter = Kind.Creature;
            Assert.Contains(world.Fenring, explorer.Results);
            Assert.Equal(KnownLevel.Unknown, explorer.LevelOf(world.Fenring));
            // What is only heard of is a silhouette there too.
            explorer.KindFilter = Kind.Item;
            Assert.Equal(new[] { world.Hide }, explorer.Results.ToArray());
            explorer.KindFilter = Kind.Creature;

            // Typing finds only what is met; the kinds that show no silhouettes leave the unknown out.
            explorer.Text = "f";
            Assert.DoesNotContain(world.Fenring, explorer.Results);
            explorer.Text = "";
            explorer.KindFilter = Kind.Effect;
            Assert.DoesNotContain(world.Splash, explorer.Results);
            explorer.KindFilter = null;
            Assert.DoesNotContain(world.Fenring, explorer.Results);
        }

        [Fact]
        public void EachKindCountsWhatWasFoundOfAllThereIs()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            explorer.KindFilter = Kind.Creature;

            // The tab's own count is what the character has met; silhouettes are not counted as found.
            Assert.Equal(2, explorer.CountOf(Kind.Creature));
            Assert.Equal(2, explorer.FoundOf(Kind.Creature));
            Assert.Equal(3, explorer.TotalOf(Kind.Creature));
            // Heard of is neither listed nor found.
            Assert.Equal(0, explorer.CountOf(Kind.Item));
            Assert.Equal(0, explorer.FoundOf(Kind.Item));
            Assert.Equal(1, explorer.TotalOf(Kind.Item));
        }

        [Fact]
        public void TheListFollowsWhatTheCharacterLearns()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            Assert.DoesNotContain(world.Fenring, world.Explorer.Results);

            world.Book.Raise(world.Fenring.Key, KnownLevel.Seen);
            world.Rule(CompendiumRule.Loose);

            Assert.Contains(world.Fenring, world.Explorer.Results);
            Assert.Equal(3, world.Explorer.FoundOf(Kind.Creature));
        }

        [Fact]
        public void AMendedSearchNeverOffersANameTheCharacterDoesNotKnow()
        {
            var off = new World().Rule(CompendiumRule.Off);
            off.Explorer.Text = "fenrinh";
            Assert.Contains(off.Fenring, off.Explorer.Results);

            var loose = new World().Rule(CompendiumRule.Loose);
            loose.Explorer.Text = "fenrinh";
            Assert.Empty(loose.Explorer.Results);
            Assert.Empty(loose.Explorer.Mended);
        }

        [Fact]
        public void ASlipIsMendedToANameTheCharacterKnowsBeforeOneItDoesNot()
        {
            // "wolg" is one slip from the wolf, not met, and starts like the wolverine, met.
            var wolf = E("Wolf", Kind.Creature);
            var wolverine = E("Wolverine", Kind.Creature);
            var explorer = new Explorer(new[] { wolf, wolverine }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            book.Raise(wolverine.Key, KnownLevel.Seen);

            explorer.Text = "wolg";
            Assert.Equal(new[] { wolf }, explorer.Results.ToArray());

            view.Tell(book, CompendiumRule.Loose);
            explorer.Know(view);
            Assert.Equal(new[] { wolverine }, explorer.Results.ToArray());
        }

        [Fact]
        public void AMendedSearchFindsOnlyWhatTheCharacterKnows()
        {
            var world = new World();
            var ghost = E("Troll_Ghost", Kind.Creature);
            var explorer = new Explorer(new[] { world.Troll, ghost }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            view.Tell(world.Book, CompendiumRule.Loose);
            explorer.Know(view);

            explorer.Text = "trolk";

            Assert.Equal(new[] { world.Troll }, explorer.Results.ToArray());
        }

        [Fact]
        public void TheStageShowsOnlyWhatTheCharacterHasSeen()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;

            explorer.Select(world.Boar);
            Assert.Same(world.Boar, explorer.Shown);
            explorer.Select(world.Hide);
            Assert.Null(explorer.Shown);
            explorer.KindFilter = Kind.Creature;
            explorer.Select(world.Fenring);
            Assert.Null(explorer.Shown);

            world.Rule(CompendiumRule.Off);
            Assert.Same(world.Fenring, explorer.Shown);
        }

        [Fact]
        public void ASilhouetteSelectedComesOnStageOnceTheCharacterSeesIt()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            explorer.KindFilter = Kind.Creature;
            explorer.Select(world.Fenring);
            var version = explorer.SelectionVersion;

            world.Book.Raise(world.Troll.Key, KnownLevel.Known);
            world.Book.Raise(world.Splash.Key, KnownLevel.Seen);
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(version, explorer.SelectionVersion);

            world.Book.Raise(world.Fenring.Key, KnownLevel.Seen);
            world.Rule(CompendiumRule.Loose);
            Assert.NotEqual(version, explorer.SelectionVersion);
            Assert.Same(world.Fenring, explorer.Shown);

            // Shown once: learning something else leaves it as it is.
            version = explorer.SelectionVersion;
            world.Book.Raise(world.Hide.Key, KnownLevel.Seen);
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(version, explorer.SelectionVersion);

            // Something else picked meanwhile is shown once, not again when more is learned.
            explorer.Select(world.Boar);
            version = explorer.SelectionVersion;
            world.Book.Raise(world.Splash.Key, KnownLevel.Known);
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(version, explorer.SelectionVersion);
        }

        [Fact]
        public void GoingToSomethingNotMetLeadsNowhereButWhatWasHeardOfOpensAmongItsKindsSilhouettes()
        {
            // A heard-of name on a page leads to its closed page, in its kind's tab where it is listed.
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Text = "troll";

            Assert.False(world.Explorer.Jump(world.Fenring.Key));
            Assert.True(world.Explorer.Jump(world.Hide.Key));
            Assert.Same(world.Hide, world.Explorer.Selected);
            Assert.Equal(Kind.Item, world.Explorer.KindFilter);
            Assert.Equal("", world.Explorer.Text);
            Assert.Contains(world.Hide, world.Explorer.Results);
            Assert.False(world.Explorer.Jump(world.Splash.Key));
        }

        [Fact]
        public void AHeardOfThingOfAKindShowingNoSilhouettesLeadsNowhere()
        {
            // Such kinds list nothing not seen, so there is nowhere to open it.
            var troll = E("Troll", Kind.Creature);
            var rock = E("troll_groundslam_rock", Kind.Other);
            troll.AddTermLink("spawns", rock);
            var explorer = new Explorer(new[] { troll, rock }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Loose);
            explorer.Know(view);

            Assert.Equal(KnownLevel.HeardOf, explorer.LevelOf(rock));
            Assert.False(explorer.Jump(rock.Key));
        }
    }
}
