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
        public void APlaceSeenOnlyFromOutsideKeepsItsInsideBack()
        {
            // A dungeon seen from outside, by sight or on the explored map, shows its outside
            // alone; once entered it is known, and its inside shows. Off or peeking, every one does.
            var crypt = E("Crypt2", Kind.Location);
            var sunken = E("SunkenCrypt4", Kind.Location);
            var explorer = new Explorer(new[] { crypt, sunken }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            book.Raise(crypt.Key, KnownLevel.Seen);
            book.Raise(sunken.Key, KnownLevel.Known);

            view.Tell(book, CompendiumRule.Off);
            explorer.Know(view);
            Assert.True(explorer.InsideKnown(crypt));

            foreach (var rule in new[] { CompendiumRule.Loose, CompendiumRule.Strict })
            {
                view.Tell(book, rule);
                explorer.Know(view);
                Assert.False(explorer.InsideKnown(crypt));
                Assert.True(explorer.InsideKnown(sunken));
            }

            explorer.Peeking = true;
            Assert.True(explorer.InsideKnown(crypt));
            explorer.Peeking = false;
            Assert.False(explorer.InsideKnown(null));
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
        public void PeekingShowsOnlyThePageLookedAtWholeTheListStaysAsTheCharacterKnowsIt()
        {
            // For checking something, or helping a friend: the page in front of you shows whole and
            // its links go anywhere, while the list, its counts and the search stay as the character
            // knows them, an unmet thing found only by its whole name in quotes. What was looked at
            // meanwhile is no step back after.
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            explorer.Select(world.Troll);

            explorer.Peeking = true;
            Assert.True(explorer.Peeking);
            Assert.False(explorer.CompendiumOn);
            Assert.True(explorer.CompendiumSet);
            Assert.Equal(2, explorer.Results.Count);
            Assert.Equal(2, explorer.FoundOf(Kind.Creature));
            Assert.True(explorer.Reaches(world.Fenring.Key));
            Assert.Equal(KnownLevel.Known, explorer.LevelOf(world.Fenring));
            explorer.Text = "fenr";
            Assert.Empty(explorer.Results);
            explorer.Text = "\"fenring\"";
            Assert.Equal(new[] { world.Fenring }, explorer.Results);
            explorer.Text = "";
            // A link followed opens the page of what was not met, whole.
            explorer.Select(world.Fenring);
            Assert.Same(world.Fenring, explorer.Selected);
            explorer.Select(world.Boar);

            explorer.Peeking = false;
            Assert.True(explorer.CompendiumOn);
            Assert.Equal(2, explorer.Results.Count);
            Assert.False(explorer.Reaches(world.Fenring.Key));
            Assert.True(explorer.Back());
            Assert.Same(world.Troll, explorer.Selected);
        }

        [Fact]
        public void ALearnedRowOpensItsPageAtWhatChanged()
        {
            // The troll was fought, the boar only seen; then the troll's health was found out.
            var world = new World().Rule(CompendiumRule.Strict);
            var explorer = world.Explorer;
            Assert.Equal(NewsArrival.You(CompendiumWords.FirstSeenLabel), explorer.ArrivalOf(world.Boar).Spot);
            world.Book.FoundOut(world.Troll.Key, EntryKeys.Did(Deed.Killed));
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(EntryKeys.Did(Deed.Killed), explorer.ArrivalOf(world.Troll).Spot);
            // Nothing to arrive at with the compendium off.
            world.Rule(CompendiumRule.Off);
            Assert.Null(explorer.ArrivalOf(world.Troll));
        }

        [Fact]
        public void TheEyePeeksAtThePageShownAloneAndGoingToAnotherEndsIt()
        {
            // A look at the one page in front of you: it shows whole, the list and the search stay as
            // the character knows them, and going to any other entry from the list ends it; a step
            // back to it peeks at it again.
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            explorer.Select(world.Troll);
            explorer.Select(world.Boar);

            explorer.PeekPage();
            Assert.True(explorer.Peeking);
            Assert.False(explorer.PeekingEvery);
            Assert.Same(world.Boar, explorer.PeekedPage);
            Assert.False(explorer.CompendiumOn);
            Assert.Equal(KnownLevel.Known, explorer.LevelOf(world.Boar));
            explorer.Text = "\"fenring\"";
            Assert.Empty(explorer.Results);
            explorer.Text = "";
            Assert.Equal(2, explorer.Results.Count);

            explorer.Select(world.Troll);
            Assert.False(explorer.Peeking);
            Assert.Null(explorer.PeekedPage);
            Assert.True(explorer.CompendiumOn);

            // Stepping back to a page left while peeked at peeks at it again, and forward to a page left as known shows that as known.
            Assert.True(explorer.Back());
            Assert.Same(world.Boar, explorer.Selected);
            Assert.True(explorer.Peeking);
            Assert.Same(world.Boar, explorer.PeekedPage);
            Assert.Equal(KnownLevel.Known, explorer.LevelOf(world.Boar));
            Assert.True(explorer.Forward());
            Assert.Same(world.Troll, explorer.Selected);
            Assert.False(explorer.Peeking);
            Assert.Equal(KnownLevel.Seen, explorer.LevelOf(world.Boar));
        }

        [Fact]
        public void StoppingThePeekForgetsWhichPagesOfTheHistoryWerePeekedAt()
        {
            // The strip's cross, or the eye again, stops the peek for good: a step back to a page
            // left while peeked at shows it as the character knows it.
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            explorer.Select(world.Troll);
            explorer.Select(world.Boar);
            explorer.PeekPage();
            Assert.True(explorer.Jump(world.Fenring.Key));
            explorer.Peeking = false;
            Assert.True(explorer.Back());
            Assert.Same(world.Boar, explorer.Selected);
            Assert.False(explorer.Peeking);
            Assert.Equal(KnownLevel.Seen, explorer.LevelOf(world.Boar));
        }

        [Fact]
        public void ALinkFollowedFromAPagePeekedAtToAThingNotMetPeeksAtThatOneInTurn()
        {
            // The page peeked at shows whole, so its links lead anywhere: one to a thing the
            // character has not met opens that page peeked at in turn, staying while the list is
            // asked again though it is not listed; a step back or forward moves the peek with it,
            // while going to a page met by the list ends the peek, and what was looked at
            // meanwhile is no step after.
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            explorer.Select(world.Troll);
            explorer.PeekPage();

            Assert.True(explorer.Jump(world.Fenring.Key));
            Assert.Same(world.Fenring, explorer.Selected);
            Assert.Same(world.Fenring, explorer.PeekedPage);
            explorer.Text = "boar";
            explorer.Text = "";
            Assert.Same(world.Fenring, explorer.Selected);

            Assert.True(explorer.Back());
            Assert.Same(world.Troll, explorer.Selected);
            Assert.True(explorer.Peeking);
            Assert.Same(world.Troll, explorer.PeekedPage);
            Assert.True(explorer.CanGoForward);
            Assert.True(explorer.Forward());
            Assert.Same(world.Fenring, explorer.Selected);
            Assert.Same(world.Fenring, explorer.PeekedPage);

            Assert.True(explorer.Back());
            explorer.Select(world.Boar);
            Assert.False(explorer.Peeking);
            Assert.Null(explorer.PeekedPage);
            Assert.False(explorer.CanGoForward);
        }

        [Fact]
        public void PeekingAtEveryPageAPageNotMetStaysShownWhileTheListIsAskedAgain()
        {
            // A link followed while peeking at every page keeps its page when the list is asked
            // again, as the character's book catches up; stopping the peek closes it.
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            explorer.Select(world.Troll);
            explorer.Peeking = true;
            Assert.True(explorer.Jump(world.Fenring.Key));
            explorer.Text = "boar";
            explorer.Text = "";
            Assert.Same(world.Fenring, explorer.Selected);

            explorer.Peeking = false;
            Assert.Null(explorer.Selected);
        }

        [Fact]
        public void APeekAtOnePageWidensToEveryPageOrStops()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            explorer.Select(world.Boar);
            explorer.PeekPage();

            // The strip's chip: every page shows whole until the panel closes, going elsewhere or not.
            explorer.Peeking = true;
            Assert.True(explorer.PeekingEvery);
            Assert.Null(explorer.PeekedPage);
            explorer.Text = "\"fenring\"";
            Assert.Equal(new[] { world.Fenring }, explorer.Results);
            explorer.Text = "";
            explorer.Select(world.Troll);
            Assert.True(explorer.PeekingEvery);

            // The strip's cross, or the eye again: the compendium as it was.
            explorer.Peeking = false;
            explorer.PeekPage();
            explorer.Peeking = false;
            Assert.False(explorer.Peeking);
            Assert.Null(explorer.PeekedPage);
            Assert.True(explorer.CompendiumOn);
        }

        [Fact]
        public void WithNoPageShownTheEyePeeksAtEveryPageAndWithTheCompendiumOffAtNone()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.PeekPage();
            Assert.True(world.Explorer.PeekingEvery);

            var off = new World().Rule(CompendiumRule.Off);
            off.Explorer.Select(off.Troll);
            off.Explorer.PeekPage();
            Assert.False(off.Explorer.Peeking);
            Assert.Null(off.Explorer.PeekedPage);
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
        public void WhileTheLearnedListIsShownEachOtherTabCountsWhatItWouldShow()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            // Seen long ago: listed, but not among what was learned lately.
            world.Book.Raise(world.Fenring.Key, KnownLevel.Seen, quietly: true);
            world.Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            var all = explorer.CountAll;
            var creatures = explorer.CountOf(Kind.Creature);

            explorer.LearnedOnly = true;
            Assert.Equal(new[] { world.Boar, world.Troll }, explorer.Results.ToArray());
            Assert.True(all > explorer.Results.Count);
            Assert.Equal((all, creatures), (explorer.CountAll, explorer.CountOf(Kind.Creature)));
        }

        [Fact]
        public void TheLearnedTabCountsWhatItWouldListWhicheverTabIsShown()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;
            Assert.Equal(2, explorer.LearnedCount);
            explorer.KindFilter = Kind.Item;
            Assert.Equal(2, explorer.LearnedCount);

            // Only heard of, it is not counted, as it is not listed.
            world.Book.Raise(world.Fenring.Key, KnownLevel.Seen);
            world.Book.Raise(world.Hide.Key, KnownLevel.HeardOf);
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(3, explorer.LearnedCount);

            // With the compendium off there is no such tab.
            world.Rule(CompendiumRule.Off);
            Assert.Equal(0, explorer.LearnedCount);
        }

        [Fact]
        public void ASearchCountsOnTheLearnedTabOnlyWhatItFindsThere()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            // Seen long ago: found by a search, but not among what was learned lately.
            world.Book.Raise(world.Fenring.Key, KnownLevel.Seen, quietly: true);
            world.Rule(CompendiumRule.Loose);
            var explorer = world.Explorer;

            explorer.Text = "troll";
            Assert.Equal(1, explorer.LearnedCount);
            // Found, but nothing of it learned lately: the tab has nothing to show, so it is not drawn.
            explorer.Text = "fenring";
            Assert.Equal(1, explorer.CountAll);
            Assert.Equal(0, explorer.LearnedCount);
            explorer.Text = "";
            Assert.Equal(2, explorer.LearnedCount);

            // Something learned that the catalog no longer holds, as a removed mod's thing, is not counted.
            world.Book.Raise("RemovedModThing", KnownLevel.Known);
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(2, explorer.LearnedCount);
        }

        [Fact]
        public void ANoticeTellsWhatWasLearnedSinceItLastToldOfWhatWasMet()
        {
            // What was only heard of is not named; with the compendium off or peeking, nothing is told.
            var world = new World().Rule(CompendiumRule.Loose);
            var notice = new LearnedNotice();

            Assert.Equal(new[] { world.Boar, world.Troll }, notice.New(world.Explorer));
            notice.Told();
            Assert.Empty(notice.New(world.Explorer));

            world.Book.Raise(world.Fenring.Key, KnownLevel.Seen);
            world.Book.Raise(world.Hide.Key, KnownLevel.HeardOf);
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(new[] { world.Fenring }, notice.New(world.Explorer));

            world.Explorer.Peeking = true;
            Assert.Empty(notice.New(world.Explorer));
            world.Explorer.Peeking = false;
            world.Rule(CompendiumRule.Off);
            Assert.Empty(notice.New(world.Explorer));
        }

        [Fact]
        public void EachCharactersNoticeKeepsItsOwnPlace()
        {
            // The self-test shows a made-up character for a while; the one played is not told again.
            var world = new World().Rule(CompendiumRule.Loose);
            var notice = new LearnedNotice();
            notice.New(world.Explorer);
            notice.Told();

            var other = new CompendiumBook();
            other.Raise(world.Fenring.Key, KnownLevel.Known);
            var otherView = new CompendiumView(world.Explorer.Entries);
            otherView.Tell(other, CompendiumRule.Loose);
            world.Explorer.Know(otherView);
            Assert.Equal(new[] { world.Fenring }, notice.New(world.Explorer));

            world.Explorer.Know(world.View);
            Assert.Empty(notice.New(world.Explorer));
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
