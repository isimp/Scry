using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class CompendiumListTests
    {
        // With the compendium on, the list and the search show what the character has met or
        // heard of; nothing stands in for the rest, and each kind counts what was found of all
        // there is.

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
        public void AKindsTabListsOnlyWhatWasSeenOrKnownNothingStandingInForTheRest()
        {
            // No row hints at what was not seen, nor its group's name at what that group holds.
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;

            explorer.KindFilter = Kind.Creature;
            Assert.Equal(new[] { world.Boar, world.Troll }.OrderBy(e => e.Name), explorer.Results.OrderBy(e => e.Name));
            // A tab with nothing to show lists every kind's instead, as it always has: still nothing unseen.
            explorer.KindFilter = Kind.Item;
            Assert.DoesNotContain(world.Hide, explorer.Results);
            Assert.All(explorer.Results, e => Assert.True(explorer.LevelOf(e) >= KnownLevel.Seen));
            explorer.KindFilter = Kind.Effect;
            Assert.DoesNotContain(world.Splash, explorer.Results);
        }

        [Fact]
        public void EachKindCountsWhatWasFoundOfAllThereIs()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            explorer.KindFilter = Kind.Creature;

            // The tab's own count is what the character has met.
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
        public void WhatWasSelectedUnseenLeavesTheListOnceTheCompendiumIsOn()
        {
            var world = new World().Rule(CompendiumRule.Off);
            world.Explorer.Select(world.Fenring);

            world.Rule(CompendiumRule.Loose);

            Assert.Null(world.Explorer.Selected);
            Assert.Null(world.Explorer.Shown);
        }

        [Fact]
        public void GoingToSomethingNotSeenLeadsNowhereHeardOfOrNot()
        {
            // A heard-of name stays a name on the page that mentions it.
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Text = "fenring";

            Assert.False(world.Explorer.Jump(world.Fenring.Key));
            Assert.False(world.Explorer.Jump(world.Hide.Key));
            Assert.Null(world.Explorer.Selected);
            Assert.Equal("fenring", world.Explorer.Text);
            Assert.True(world.Explorer.Jump(world.Boar.Key));
            Assert.Same(world.Boar, world.Explorer.Selected);
        }

        [Fact]
        public void APageLeadsOnlyToWhatTheCharacterHasSeen()
        {
            // The troll's page names its hide, heard of: the name is told as plain text, going nowhere.
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;

            Assert.True(explorer.Reaches(world.Troll.Key));
            Assert.True(explorer.Reaches(world.Boar.Key));
            Assert.False(explorer.Reaches(world.Hide.Key));
            Assert.False(explorer.Reaches(world.Fenring.Key));
            Assert.False(explorer.Reaches("Draugr"));
            Assert.False(explorer.Reaches(null));
        }

        [Fact]
        public void WhatIsMadeFromWhatTheCharacterKnowsCanTellWhenItChanged()
        {
            // The search box's terms are read again from it.
            var world = new World().Rule(CompendiumRule.Loose);
            var stamp = world.Explorer.KnowStamp;

            world.Explorer.Know(world.View);
            Assert.Equal(stamp, world.Explorer.KnowStamp);

            world.Book.Raise(world.Fenring.Key, KnownLevel.Seen);
            world.Rule(CompendiumRule.Loose);
            Assert.NotEqual(stamp, world.Explorer.KnowStamp);
        }

        [Fact]
        public void FavouritesAndRecentListOnlyWhatTheCharacterHasSeen()
        {
            // Both were kept while the compendium was off, or by another character.
            var world = new World().Rule(CompendiumRule.Off);
            var explorer = world.Explorer;
            explorer.ToggleFavourite(world.Fenring);
            explorer.ToggleFavourite(world.Troll);
            explorer.Select(world.Fenring);
            explorer.Select(world.Boar);

            world.Rule(CompendiumRule.Loose);

            explorer.FavouritesOnly = true;
            Assert.Equal(new[] { world.Troll }, explorer.Results.ToArray());
            explorer.FavouritesOnly = false;
            explorer.RecentOnly = true;
            Assert.Equal(new[] { world.Boar }, explorer.Results.ToArray());
        }

        [Fact]
        public void GoingBackNeverLeadsToWhatTheCharacterHasNotSeen()
        {
            // Looked at with the compendium off, the fenring is no step back once it is on.
            var world = new World().Rule(CompendiumRule.Off);
            var explorer = world.Explorer;
            explorer.Select(world.Troll);
            explorer.Select(world.Fenring);
            explorer.Select(world.Hide);
            explorer.Select(world.Boar);

            world.Rule(CompendiumRule.Loose);

            Assert.True(explorer.Back());
            Assert.Same(world.Troll, explorer.Selected);
            Assert.False(explorer.CanGoBack);
            Assert.True(explorer.Forward());
            Assert.Same(world.Boar, explorer.Selected);
            Assert.False(explorer.CanGoForward);
        }

        [Fact]
        public void GoingForwardAgainLeadsOnlyToWhatTheCharacterHasSeen()
        {
            // The boar, only seen, is a step still; the fenring and the hide are not.
            var world = new World().Rule(CompendiumRule.Off);
            var explorer = world.Explorer;
            explorer.Select(world.Troll);
            explorer.Select(world.Boar);
            explorer.Select(world.Fenring);
            explorer.Select(world.Hide);
            explorer.Back();
            explorer.Back();
            explorer.Back();

            world.Rule(CompendiumRule.Loose);

            Assert.Same(world.Troll, explorer.Selected);
            Assert.True(explorer.Forward());
            Assert.Same(world.Boar, explorer.Selected);
            Assert.False(explorer.CanGoForward);
        }

        [Fact]
        public void PeekingShowsEverythingUntilItStops()
        {
            // For checking something, or helping a friend; what was looked at meanwhile is no step back after.
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            explorer.Select(world.Troll);

            explorer.Peeking = true;
            Assert.True(explorer.Peeking);
            Assert.False(explorer.CompendiumOn);
            Assert.True(explorer.CompendiumSet);
            Assert.Equal(5, explorer.Results.Count);
            Assert.True(explorer.Reaches(world.Fenring.Key));
            explorer.Select(world.Fenring);
            explorer.Select(world.Boar);

            explorer.Peeking = false;
            Assert.True(explorer.CompendiumOn);
            Assert.Equal(2, explorer.Results.Count);
            Assert.False(explorer.Reaches(world.Fenring.Key));
            Assert.True(explorer.Back());
            Assert.Same(world.Troll, explorer.Selected);
        }

        [Fact]
        public void ThereIsNoPeekingWithTheCompendiumOff()
        {
            var world = new World().Rule(CompendiumRule.Off);
            world.Explorer.Peeking = true;
            Assert.False(world.Explorer.Peeking);
            Assert.False(world.Explorer.CompendiumSet);

            // Nor does a peek left on outlast the compendium being turned off and on again.
            world.Rule(CompendiumRule.Loose);
            world.Explorer.Peeking = true;
            world.Rule(CompendiumRule.Off);
            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.Peeking);
            Assert.True(world.Explorer.CompendiumOn);
        }

        [Fact]
        public void LearnedLatelyListsWhatTheCharacterCameToMeetNewestFirst()
        {
            // What was only heard of is no more listed here than anywhere.
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;

            explorer.LearnedOnly = true;
            Assert.Equal(new[] { world.Boar, world.Troll }, explorer.Results.ToArray());

            world.Book.Raise(world.Fenring.Key, KnownLevel.Seen);
            world.Book.Raise(world.Hide.Key, KnownLevel.HeardOf);
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(new[] { world.Fenring, world.Boar, world.Troll }, explorer.Results.ToArray());

            explorer.RecentOnly = true;
            Assert.False(explorer.LearnedOnly);
            explorer.LearnedOnly = true;
            Assert.False(explorer.RecentOnly);
            explorer.SearchEverything("troll");
            Assert.False(explorer.LearnedOnly);
        }

        [Fact]
        public void WithTheCompendiumOffAPageLeadsToEverythingInTheCatalog()
        {
            var world = new World().Rule(CompendiumRule.Off);

            Assert.True(world.Explorer.Reaches(world.Fenring.Key));
            Assert.True(world.Explorer.Reaches(world.Hide.Key));
            Assert.False(world.Explorer.Reaches("Draugr"));
        }
    }
}
