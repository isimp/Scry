using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class WorldSourcesTests
    {
        // The player chooses what a world shows as known, source by source: the ground the
        // character explored, maps shared at a cartography table, places a Vegvisir or a rune
        // stone marked, and the places every map shows. Each source's places show at the level
        // chosen, laid over the book as the floor is, never written into it: marked as the
        // source's, so turning a source down takes it all back. The places are kept per world.

        private const string World = "1234";
        private const string OtherWorld = "5678";

        private static Entry Place(string name, string label) => new Entry { Kind = Kind.Location, Name = name, Components = new[] { "Location" }, FoundIn = new[] { label } };

        private static Entry Room(string name, string label) => new Entry { Kind = Kind.Location, Name = name, Components = new[] { "Room" }, FoundIn = new[] { label } };

        private sealed class Setup
        {
            public readonly Entry Crypt = Place("Crypt2", "Burial chambers");
            public readonly Entry Walked = Room("sunkencrypt_Room1", "Burial chambers");
            public readonly Entry NotWalked = Room("sunkencrypt_Room2", "Burial chambers");
            public readonly Entry Haldor = Place("Vendor_BlackForest", "Haldor");
            public readonly Entry Glow = new Entry { Kind = Kind.Effect, Name = "fx_crypt_glow" };
            public readonly CompendiumBook Book = new CompendiumBook();
            public readonly CompendiumView View;

            public Setup()
            {
                Crypt.Links.Add(new Link { Group = "Effects", Target = Glow.Key });
                View = new CompendiumView(new EntryCatalog(new[] { Crypt, Walked, NotWalked, Haldor, Glow }));
                View.RoomsWalked(name => name == "sunkencrypt_Room1", 1);
                View.InWorld(World);
            }

            public Setup Found(WorldSource source, params string[] kinds)
            {
                Book.FoundIn(World, source, kinds);
                return this;
            }

            public Setup Shows(WorldSource source, SourceLevel level, CompendiumRule rule = CompendiumRule.Loose)
            {
                Book.Show(source, level);
                View.Tell(Book, rule);
                return this;
            }
        }

        [Fact]
        public void YourOwnGroundAndMarkedPlacesStartSeenTheNewSourcesOff()
        {
            var book = new CompendiumBook();
            Assert.Equal(SourceLevel.Seen, book.Shows(WorldSource.Explored));
            Assert.Equal(SourceLevel.Seen, book.Shows(WorldSource.Marked));
            Assert.Equal(SourceLevel.Off, book.Shows(WorldSource.SharedMaps));
            Assert.Equal(SourceLevel.Off, book.Shows(WorldSource.OnEveryMap));
        }

        [Fact]
        public void YourOwnGroundAndMarkedPlacesNeverGoBelowSeen()
        {
            // The book holds them as seen already, written as they came: lower would show nothing less.
            var book = new CompendiumBook();
            book.Show(WorldSource.Explored, SourceLevel.Off);
            book.Show(WorldSource.Marked, SourceLevel.Off);
            Assert.Equal(SourceLevel.Seen, book.Shows(WorldSource.Explored));
            Assert.Equal(SourceLevel.Seen, book.Shows(WorldSource.Marked));
            book.Show(WorldSource.SharedMaps, SourceLevel.Off);
            Assert.Equal(SourceLevel.Off, book.Shows(WorldSource.SharedMaps));
        }

        [Fact]
        public void ASourceAtSeenShowsItsPlacesSeenWithoutWritingTheBook()
        {
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2").Shows(WorldSource.SharedMaps, SourceLevel.Seen);
            Assert.Equal(KnownLevel.Seen, s.View.Of(s.Crypt));
            Assert.Equal(WorldSource.SharedMaps, s.View.ByWorld(s.Crypt));
            Assert.Equal(KnownLevel.Unknown, s.Book.Level(s.Crypt.Key));
        }

        [Fact]
        public void ASourceOffShowsNothingOfIt()
        {
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2").Shows(WorldSource.SharedMaps, SourceLevel.Off);
            Assert.Equal(KnownLevel.Unknown, s.View.Of(s.Crypt));
            Assert.Null(s.View.ByWorld(s.Crypt));
        }

        [Fact]
        public void AtKnownAPlaceOpensAndBringsAlongWhatAKnownPlaceDoes()
        {
            var s = new Setup().Found(WorldSource.OnEveryMap, "Crypt2").Shows(WorldSource.OnEveryMap, SourceLevel.Known);
            Assert.Equal(KnownLevel.Known, s.View.Of(s.Crypt));
            Assert.Equal(KnownLevel.Known, s.View.Of(s.Glow));
            Assert.Equal(WorldSource.OnEveryMap, s.View.ByWorld(s.Glow));
        }

        [Fact]
        public void UnderStrictKnownBringsTheRoomsWalkedWithInsidesEveryRoom()
        {
            var s = new Setup().Found(WorldSource.Explored, "Crypt2").Shows(WorldSource.Explored, SourceLevel.Known, CompendiumRule.Strict);
            Assert.Equal(KnownLevel.Known, s.View.Of(s.Walked));
            Assert.True(s.View.Of(s.NotWalked) < KnownLevel.Seen);

            s.Shows(WorldSource.Explored, SourceLevel.WithInsides, CompendiumRule.Strict);
            Assert.Equal(KnownLevel.Known, s.View.Of(s.NotWalked));
            Assert.Equal(WorldSource.Explored, s.View.ByWorld(s.NotWalked));
        }

        [Fact]
        public void TurningASourceDownTakesBackAllItShowed()
        {
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2").Shows(WorldSource.SharedMaps, SourceLevel.WithInsides);
            Assert.Equal(KnownLevel.Known, s.View.Of(s.NotWalked));

            s.Shows(WorldSource.SharedMaps, SourceLevel.Off);
            Assert.Equal(KnownLevel.Unknown, s.View.Of(s.Crypt));
            Assert.True(s.View.Of(s.NotWalked) < KnownLevel.Seen);
            Assert.True(s.View.Of(s.Glow) < KnownLevel.Seen);
            Assert.Null(s.View.ByWorld(s.Glow));
        }

        [Fact]
        public void PlacesFoundInAnotherWorldShowNothingHere()
        {
            var s = new Setup();
            s.Book.FoundIn(OtherWorld, WorldSource.SharedMaps, new[] { "Crypt2" });
            s.Shows(WorldSource.SharedMaps, SourceLevel.Known);
            Assert.Equal(KnownLevel.Unknown, s.View.Of(s.Crypt));

            s.View.InWorld(OtherWorld);
            s.View.Tell(s.Book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, s.View.Of(s.Crypt));
        }

        [Fact]
        public void WhatTheCharacterMetIsItsOwnAndNotMarked()
        {
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2");
            s.Book.Raise(s.Crypt.Key, KnownLevel.Known);
            s.Shows(WorldSource.SharedMaps, SourceLevel.Known);
            Assert.Equal(KnownLevel.Known, s.View.Of(s.Crypt));
            Assert.Null(s.View.ByWorld(s.Crypt));
        }

        [Fact]
        public void TheStrongestSourceShowsAPlaceFoundInSeveral()
        {
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2").Found(WorldSource.OnEveryMap, "Crypt2");
            s.Book.Show(WorldSource.SharedMaps, SourceLevel.Seen);
            s.Shows(WorldSource.OnEveryMap, SourceLevel.Known);
            Assert.Equal(KnownLevel.Known, s.View.Of(s.Crypt));
            Assert.Equal(WorldSource.OnEveryMap, s.View.ByWorld(s.Crypt));
        }

        [Fact]
        public void APlaceFoundInSeveralSourcesIsNamedByTheStrongest()
        {
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2").Found(WorldSource.Explored, "Crypt2");
            s.Book.Show(WorldSource.SharedMaps, SourceLevel.Known);
            s.Shows(WorldSource.Explored, SourceLevel.WithInsides, CompendiumRule.Strict);
            Assert.Equal(WorldSource.Explored, s.View.ByWorld(s.Crypt));
        }

        [Fact]
        public void ADungeonTheCharacterKnowsGainsItsRoomsNotWalkedWithInsides()
        {
            var s = new Setup().Found(WorldSource.Explored, "Crypt2");
            s.Book.Raise(s.Crypt.Key, KnownLevel.Known);
            s.Shows(WorldSource.Explored, SourceLevel.WithInsides, CompendiumRule.Strict);
            Assert.Null(s.View.ByWorld(s.Crypt));
            Assert.Equal(KnownLevel.Known, s.View.Of(s.NotWalked));
            Assert.Equal(WorldSource.Explored, s.View.ByWorld(s.NotWalked));
            Assert.Null(s.View.ByWorld(s.Walked));
        }

        [Fact]
        public void TellingAgainInTheSameWorldCountsNothingAfresh()
        {
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2").Shows(WorldSource.SharedMaps, SourceLevel.Seen);
            var whole = s.View.WholeCounts;
            s.View.InWorld(World);
            s.View.Tell(s.Book, CompendiumRule.Loose);
            Assert.Equal(whole, s.View.WholeCounts);
        }

        [Fact]
        public void MeetingAPlaceForRealMakesItTheBooksOwn()
        {
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2", "Vendor_BlackForest");
            s.Book.Show(WorldSource.SharedMaps, SourceLevel.Seen);
            s.View.Tell(s.Book, CompendiumRule.Loose);

            // Learned on top of what was told: known for real, it is no longer the source's.
            s.Book.Raise(s.Crypt.Key, KnownLevel.Known);
            s.View.Tell(s.Book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Known, s.View.Of(s.Crypt));
            Assert.Null(s.View.ByWorld(s.Crypt));

            // Only heard of for real, the source still shows it seen.
            s.Book.Raise(s.Haldor.Key, KnownLevel.HeardOf);
            s.View.Tell(s.Book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Seen, s.View.Of(s.Haldor));
            Assert.Equal(WorldSource.SharedMaps, s.View.ByWorld(s.Haldor));

            // Seen for real, it is the book's.
            s.Book.Raise(s.Haldor.Key, KnownLevel.Seen);
            s.View.Tell(s.Book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Seen, s.View.Of(s.Haldor));
            Assert.Null(s.View.ByWorld(s.Haldor));
        }

        [Fact]
        public void PlacesFoundAndChoicesAreKeptWithTheBook()
        {
            var book = new CompendiumBook();
            book.FoundIn(World, WorldSource.SharedMaps, new[] { "Crypt2", "Vendor_BlackForest" });
            book.FoundIn(World, WorldSource.Marked, new[] { "Eikthyrnir" });
            book.FoundIn(OtherWorld, WorldSource.OnEveryMap, new[] { "StartTemple" });
            book.Show(WorldSource.SharedMaps, SourceLevel.Known);
            book.Show(WorldSource.Explored, SourceLevel.WithInsides);
            Assert.True(book.Changed);

            var read = CompendiumBook.Load(book.ToSave()());
            Assert.Equal(new[] { "Crypt2", "Vendor_BlackForest" }, Sorted(read.Found(World, WorldSource.SharedMaps)));
            Assert.Equal(new[] { "Eikthyrnir" }, Sorted(read.Found(World, WorldSource.Marked)));
            Assert.Equal(new[] { "StartTemple" }, Sorted(read.Found(OtherWorld, WorldSource.OnEveryMap)));
            Assert.Empty(read.Found(World, WorldSource.OnEveryMap));
            Assert.Equal(SourceLevel.Known, read.Shows(WorldSource.SharedMaps));
            Assert.Equal(SourceLevel.WithInsides, read.Shows(WorldSource.Explored));
            Assert.Equal(SourceLevel.Off, read.Shows(WorldSource.OnEveryMap));
        }

        [Fact]
        public void ASavedChoiceBelowWhatASourceAllowsIsReadAsTheLeast()
        {
            var read = CompendiumBook.Load("shows\tExplored\tOff\nshows\tSharedMaps\tKnown\nshows\tNowhere\tSeen\nshows\tOnEveryMap\tEverything\n");
            Assert.Equal(SourceLevel.Seen, read.Shows(WorldSource.Explored));
            Assert.Equal(SourceLevel.Known, read.Shows(WorldSource.SharedMaps));
            Assert.Equal(SourceLevel.Off, read.Shows(WorldSource.OnEveryMap));
        }

        [Fact]
        public void TheExploredMapKeepsItsPlacesAsYourOwnGround()
        {
            // Written as seen as before, and kept as found in this world's ground for the choice laid over it.
            var book = new CompendiumBook();
            MapCatchUp.Apply(book, World, new[] { "Crypt2" });
            Assert.Equal(KnownLevel.Seen, book.Level(EntryKeys.For(Kind.Location, "Crypt2")));
            Assert.Equal(new[] { "Crypt2" }, Sorted(book.Found(World, WorldSource.Explored)));
        }

        [Fact]
        public void APlaceFoundAgainChangesNothing()
        {
            var book = new CompendiumBook();
            book.FoundIn(World, WorldSource.SharedMaps, new[] { "Crypt2" });
            book.ToSave();
            var version = book.SourcesVersion;
            book.FoundIn(World, WorldSource.SharedMaps, new[] { "Crypt2" });
            book.Show(WorldSource.SharedMaps, SourceLevel.Off);
            Assert.False(book.Changed);
            Assert.Equal(version, book.SourcesVersion);
        }

        [Fact]
        public void AChoiceOrAPlaceFoundCountsTheViewAgain()
        {
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2").Shows(WorldSource.SharedMaps, SourceLevel.Seen);
            var whole = s.View.WholeCountsOf(WholeCause.World);
            s.Found(WorldSource.SharedMaps, "Vendor_BlackForest");
            s.View.Tell(s.Book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Seen, s.View.Of(s.Haldor));
            Assert.Equal(whole + 1, s.View.WholeCountsOf(WholeCause.World));
        }

        [Fact]
        public void WhatASourceShowsReadsWholeAsWhatTheFloorShowsDoes()
        {
            // One meaning for every mark: shown by the player's choice, a page shows whole under either rules.
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2").Shows(WorldSource.SharedMaps, SourceLevel.Known, CompendiumRule.Strict);
            Assert.True(s.View.Whole(s.Crypt));
            Assert.False(s.View.Whole(s.Haldor));
            Assert.False(s.View.InsidesShown(s.Crypt));
            s.Shows(WorldSource.SharedMaps, SourceLevel.WithInsides, CompendiumRule.Strict);
            Assert.True(s.View.InsidesShown(s.Crypt));
            s.Shows(WorldSource.SharedMaps, SourceLevel.Known, CompendiumRule.Strict);
            Assert.False(s.View.InsidesShown(s.Crypt));
        }

        [Fact]
        public void AMarkSaysWhichSourceShowsItAndHow()
        {
            Assert.Equal("Shown as known from your explored ground, not yet met by this character", CompendiumWords.ByWorld(WorldSource.Explored, KnownLevel.Known));
            Assert.Equal("Shown as seen from shared maps, not yet met by this character", CompendiumWords.ByWorld(WorldSource.SharedMaps, KnownLevel.Seen));
            Assert.Equal("shown as known from the places every map shows", CompendiumWords.CameLine(null, -1, null, owner: null, byFloor: false, known: true, byWorld: WorldSource.OnEveryMap));
        }

        [Fact]
        public void TheBoxNamesEachSourceAndWhatEachLevelDoes()
        {
            Assert.Equal(new[] { "Ground you explored", "Shared maps", "Places marked for you", "Places on every map" },
                System.Array.ConvertAll(WorldSources.All, CompendiumWords.SourceName));
            Assert.Equal(new[] { "Off", "Seen", "Known", "With insides" },
                System.Array.ConvertAll((SourceLevel[])System.Enum.GetValues(typeof(SourceLevel)), CompendiumWords.LevelName));
            foreach (var source in WorldSources.All) Assert.False(string.IsNullOrEmpty(CompendiumWords.SourceTip(source)));
            foreach (SourceLevel level in System.Enum.GetValues(typeof(SourceLevel))) Assert.False(string.IsNullOrEmpty(CompendiumWords.LevelTip(level)));
            // Your own ground and marked places offer no Off, as they show at least seen.
            Assert.Equal(new[] { SourceLevel.Seen, SourceLevel.Known, SourceLevel.WithInsides }, WorldSources.Offered(WorldSource.Explored));
            Assert.Equal(new[] { SourceLevel.Off, SourceLevel.Seen, SourceLevel.Known, SourceLevel.WithInsides }, WorldSources.Offered(WorldSource.SharedMaps));
        }

        [Fact]
        public void ChipsAndLinksToWhatASourceShowsAreMarkedWithItsTip()
        {
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2").Shows(WorldSource.SharedMaps, SourceLevel.Seen);
            var explorer = new Explorer(new[] { s.Crypt, s.Walked, s.NotWalked, s.Haldor, s.Glow }, new Favourites(System.IO.Path.Combine(TestCatalog.TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            view.InWorld(World);
            view.Tell(s.Book, CompendiumRule.Loose);
            explorer.Know(view);
            Assert.True(explorer.ShownByChoice(s.Crypt.Key));
            Assert.False(explorer.ShownByChoice(s.Haldor.Key));
            Assert.Equal(CompendiumWords.ByWorld(WorldSource.SharedMaps, KnownLevel.Seen) + "\n" + "Goes to Crypt", explorer.ChoiceTip(s.Crypt.Key, "Goes to Crypt"));
            Assert.Equal("Goes to Haldor", explorer.ChoiceTip(s.Haldor.Key, "Goes to Haldor"));
        }

        private static List<string> Sorted(IEnumerable<string> kinds)
        {
            var list = new List<string>(kinds);
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }
    }
}
