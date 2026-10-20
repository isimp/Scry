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
        // With insides, a source shows what anyone on this server walked inside its dungeons: the
        // rooms walked, each naming all it holds, the place naming what they hold, and a dungeon
        // walked through whole naming all its place holds.

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

            private readonly Dictionary<WorldSource, List<DungeonMap>> _walks = new Dictionary<WorldSource, List<DungeonMap>>();
            private int _walksVersion;

            /// <summary>A dungeon of the crypt lying in a source's ground, the rooms named walked on this server, handed to the view; told on the next telling.</summary>
            public Setup WalkedOnServer(WorldSource source, params Entry[] walked)
            {
                var rooms = new[] { Walked.Name, NotWalked.Name };
                var map = new DungeonMap { Location = Crypt.Name, Rooms = new List<(int, Vec3, Vec3)>() };
                foreach (var room in rooms) map.Rooms.Add((Hash(room), new Vec3(0f, 0f, 0f), new Vec3(0f, 0f, 0f)));
                foreach (var room in walked) map.Walked.Add(System.Array.IndexOf(rooms, room.Name));
                if (!_walks.TryGetValue(source, out var held)) _walks[source] = held = new List<DungeonMap>();
                held.Add(map);
                View.WalkedOnServer(ServerWalks.Of(s => _walks.TryGetValue(s, out var maps) ? maps : new List<DungeonMap>(), _ => null, Hash), ++_walksVersion);
                return this;
            }

            public Setup Tell(CompendiumRule rule = CompendiumRule.Strict)
            {
                View.Tell(Book, rule);
                return this;
            }
        }

        private static int Hash(string name) => System.StringComparer.Ordinal.GetHashCode(name);

        [Fact]
        public void TheBoxsHintSaysTheMarkItDrawsIsWhatASourceShows()
        {
            // Drawn with the source's own mark, a folded map, rather than the hint's compass.
            Assert.Equal("What these show is marked with this on its row and page; your book keeps only what you met, so turning one down takes it all back.", CompendiumWords.SourcesHint);
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
        public void MarkedPlacesNeverGoBelowSeenYourOwnGroundCanBeTurnedOff()
        {
            // Marked places are written into the book as seen as they come: lower would show
            // nothing less. Your own ground is never written into it, so Off takes it back.
            var book = new CompendiumBook();
            book.Show(WorldSource.Explored, SourceLevel.Off);
            book.Show(WorldSource.Marked, SourceLevel.Off);
            Assert.Equal(SourceLevel.Off, book.Shows(WorldSource.Explored));
            Assert.Equal(SourceLevel.Seen, book.Shows(WorldSource.Marked));
            Assert.Equal(SourceLevel.Off, CompendiumBook.Load(book.Save()).Shows(WorldSource.Explored));
            book.Show(WorldSource.Explored, SourceLevel.Seen);
            Assert.DoesNotContain("shows\tExplored", book.Save());
            book.Show(WorldSource.SharedMaps, SourceLevel.Off);
            Assert.Equal(SourceLevel.Off, book.Shows(WorldSource.SharedMaps));
            Assert.Equal(new[] { SourceLevel.Off, SourceLevel.Seen, SourceLevel.Known, SourceLevel.WithInsides }, WorldSources.Offered(WorldSource.Explored));
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
        public void UnderStrictWithInsidesBringsTheRoomsWalkedOnThisServerInThatSourcesDungeons()
        {
            var s = new Setup().Found(WorldSource.Explored, "Crypt2").Shows(WorldSource.Explored, SourceLevel.Known, CompendiumRule.Strict);
            Assert.Equal(KnownLevel.Known, s.View.Of(s.Walked));
            Assert.True(s.View.Of(s.NotWalked) < KnownLevel.Seen);

            // Nobody on this server walked the room: with insides shows no more of it.
            s.Shows(WorldSource.Explored, SourceLevel.WithInsides, CompendiumRule.Strict);
            Assert.True(s.View.Of(s.NotWalked) < KnownLevel.Seen);

            // Walked in a dungeon of another source's ground: still nothing here.
            s.WalkedOnServer(WorldSource.SharedMaps, s.NotWalked).Tell();
            Assert.True(s.View.Of(s.NotWalked) < KnownLevel.Seen);

            s.WalkedOnServer(WorldSource.Explored, s.NotWalked).Tell();
            Assert.Equal(KnownLevel.Known, s.View.Of(s.NotWalked));
            Assert.Equal(WorldSource.Explored, s.View.ByWorld(s.NotWalked));

            // Below With insides, the walks show nothing more.
            s.Shows(WorldSource.Explored, SourceLevel.Known, CompendiumRule.Strict);
            Assert.True(s.View.Of(s.NotWalked) < KnownLevel.Seen);
        }

        [Fact]
        public void NewWalksOnThisServerCountTheViewAgain()
        {
            var s = new Setup().Found(WorldSource.Explored, "Crypt2").Shows(WorldSource.Explored, SourceLevel.WithInsides, CompendiumRule.Strict);
            var counts = s.View.WholeCountsOf(WholeCause.Rooms);
            s.WalkedOnServer(WorldSource.Explored, s.NotWalked).Tell();
            Assert.Equal(counts + 1, s.View.WholeCountsOf(WholeCause.Rooms));
            // Handed again unchanged, nothing is counted afresh.
            s.Tell();
            Assert.Equal(counts + 1, s.View.WholeCountsOf(WholeCause.Rooms));
        }

        [Fact]
        public void WalksHandedAnewUnderTheSameCountAreToldAgain()
        {
            // As a view handed walks of its own, its count started afresh.
            var s = new Setup().Found(WorldSource.Explored, "Crypt2").Shows(WorldSource.Explored, SourceLevel.WithInsides, CompendiumRule.Strict);
            var map = new DungeonMap { Location = s.Crypt.Name, Rooms = new List<(int, Vec3, Vec3)> { (Hash(s.NotWalked.Name), new Vec3(0f, 0f, 0f), new Vec3(0f, 0f, 0f)) } };
            s.View.WalkedOnServer(ServerWalks.Of(_ => new DungeonMap[0], _ => null, Hash), 7);
            s.Tell();
            map.Walked.Add(0);
            s.View.WalkedOnServer(ServerWalks.Of(at => at == WorldSource.Explored ? new[] { map } : new DungeonMap[0], _ => null, Hash), 7);
            s.Tell();
            Assert.Equal(KnownLevel.Known, s.View.Of(s.NotWalked));
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
        public void ADungeonTheCharacterKnowsGainsTheRoomsWalkedOnThisServerWithInsides()
        {
            var s = new Setup().Found(WorldSource.Explored, "Crypt2");
            s.Book.Raise(s.Crypt.Key, KnownLevel.Known);
            s.WalkedOnServer(WorldSource.Explored, s.NotWalked);
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
            var read = CompendiumBook.Load("shows\tMarked\tOff\nshows\tSharedMaps\tKnown\nshows\tNowhere\tSeen\nshows\tOnEveryMap\tEverything\n");
            Assert.Equal(SourceLevel.Seen, read.Shows(WorldSource.Marked));
            Assert.Equal(SourceLevel.Known, read.Shows(WorldSource.SharedMaps));
            Assert.Equal(SourceLevel.Off, read.Shows(WorldSource.OnEveryMap));
        }

        [Fact]
        public void TheExploredMapKeepsItsPlacesAsYourOwnGroundShownAsChosen()
        {
            // Kept as found in this world's ground, never in the book: seen as the source starts,
            // marked as its, and gone with it off.
            var s = new Setup();
            MapCatchUp.Apply(s.Book, World, new[] { "Crypt2" });
            Assert.Equal(KnownLevel.Unknown, s.Book.Level(s.Crypt.Key));
            Assert.Equal(new[] { "Crypt2" }, Sorted(s.Book.Found(World, WorldSource.Explored)));
            s.Tell(CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Seen, s.View.Of(s.Crypt));
            Assert.Equal(WorldSource.Explored, s.View.ByWorld(s.Crypt));
            s.Shows(WorldSource.Explored, SourceLevel.Off);
            Assert.True(s.View.Of(s.Crypt) < KnownLevel.Seen);
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
            Assert.Equal(new[] { "Ground you explored", "Shared maps", "Places marked for you", "Places on every map", "The whole world" },
                System.Array.ConvertAll(WorldSources.All, CompendiumWords.SourceName));
            Assert.Equal(new[] { "Off", "Counted", "Seen", "Known", "With insides" },
                System.Array.ConvertAll((SourceLevel[])System.Enum.GetValues(typeof(SourceLevel)), CompendiumWords.LevelName));
            foreach (var source in WorldSources.All) Assert.False(string.IsNullOrEmpty(CompendiumWords.SourceTip(source)));
            foreach (SourceLevel level in System.Enum.GetValues(typeof(SourceLevel))) Assert.False(string.IsNullOrEmpty(CompendiumWords.LevelTip(level)));
            Assert.Equal("Known, with what anyone on this server walked inside: those rooms and what they hold. A dungeon nobody walked shows only its entrance.", CompendiumWords.LevelTip(SourceLevel.WithInsides));
            // Your own ground and marked places offer no Off, as they show at least seen.
            Assert.Equal(new[] { SourceLevel.Off, SourceLevel.Seen, SourceLevel.Known, SourceLevel.WithInsides }, WorldSources.Offered(WorldSource.Explored));
            Assert.Equal(new[] { SourceLevel.Off, SourceLevel.Seen, SourceLevel.Known, SourceLevel.WithInsides }, WorldSources.Offered(WorldSource.SharedMaps));
        }

        [Fact]
        public void TheWholeWorldIsOffOrCountedAndNoOtherSourceIsCounted()
        {
            Assert.Equal(new[] { SourceLevel.Off, SourceLevel.Counted }, WorldSources.Offered(WorldSource.WholeWorld));
            Assert.Equal(new[] { SourceLevel.Seen, SourceLevel.Known, SourceLevel.WithInsides }, WorldSources.Offered(WorldSource.Marked));
            Assert.Equal(new[] { SourceLevel.Off, SourceLevel.Seen, SourceLevel.Known, SourceLevel.WithInsides }, WorldSources.Offered(WorldSource.OnEveryMap));
            Assert.False(WorldSources.HasGround(WorldSource.WholeWorld));
            var book = new CompendiumBook();
            Assert.Equal(SourceLevel.Off, book.Shows(WorldSource.WholeWorld));
            book.Show(WorldSource.WholeWorld, SourceLevel.Known);
            Assert.Equal(SourceLevel.Off, book.Shows(WorldSource.WholeWorld));
            book.Show(WorldSource.SharedMaps, SourceLevel.Counted);
            Assert.Equal(SourceLevel.Off, book.Shows(WorldSource.SharedMaps));
            book.Show(WorldSource.WholeWorld, SourceLevel.Counted);
            Assert.Equal(SourceLevel.Counted, CompendiumBook.Load(book.Save()).Shows(WorldSource.WholeWorld));
            // A level a source does not offer, as written by hand, is read as its least.
            var read = CompendiumBook.Load("shows\tSharedMaps\tCounted\nshows\tWholeWorld\tWithInsides\n");
            Assert.Equal(SourceLevel.Off, read.Shows(WorldSource.SharedMaps));
            Assert.Equal(SourceLevel.Off, read.Shows(WorldSource.WholeWorld));
            Assert.Equal("Every kind of place this world holds is heard of, with how many there are; none is seen until you find it.", CompendiumWords.LevelTip(SourceLevel.Counted));
            Assert.Equal("Every kind of place this world holds, by the server's census of it: how many of each there are, never where.", CompendiumWords.SourceTip(WorldSource.WholeWorld));
        }

        [Fact]
        public void CountedMakesEveryKindTheWorldHoldsHeardOfMarkedAsTheCensus()
        {
            var s = new Setup().Found(WorldSource.WholeWorld, "Crypt2").Shows(WorldSource.WholeWorld, SourceLevel.Counted);
            Assert.Equal(KnownLevel.HeardOf, s.View.Of(s.Crypt));
            Assert.Equal(WorldSource.WholeWorld, s.View.ByWorld(s.Crypt));
            Assert.Equal(KnownLevel.Unknown, s.Book.Level(s.Crypt.Key));
            // Nothing it brings along: heard of is no more than a name.
            Assert.True(s.View.Of(s.Glow) < KnownLevel.HeardOf || s.View.ByWorld(s.Glow) == null);
            Assert.Equal("Heard of from this world's census, not yet met by this character", CompendiumWords.ByWorld(WorldSource.WholeWorld, KnownLevel.HeardOf));

            // Seen for real, or shown seen by another source, it stays so and is not the census's.
            var t = new Setup().Found(WorldSource.WholeWorld, "Crypt2").Found(WorldSource.SharedMaps, "Crypt2");
            t.Book.Show(WorldSource.SharedMaps, SourceLevel.Seen);
            t.Shows(WorldSource.WholeWorld, SourceLevel.Counted);
            Assert.Equal(KnownLevel.Seen, t.View.Of(t.Crypt));
            Assert.Equal(WorldSource.SharedMaps, t.View.ByWorld(t.Crypt));
            t.Shows(WorldSource.WholeWorld, SourceLevel.Off);
            Assert.Equal(KnownLevel.Seen, t.View.Of(t.Crypt));
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

        /// <summary>
        /// A place holding a creature never met, in one of its rooms, a ghost in its own outside, and
        /// a skill never learned, its source shown as given, the rooms named walked on this server
        /// in that source's ground; the explorer handed what it shows.
        /// </summary>
        private static (Explorer Explorer, Setup Setup, Entry Draugr, Entry Skill) Holding(WorldSource source, SourceLevel level, params string[] walkedOnServer)
        {
            var s = new Setup();
            var draugr = new Entry { Kind = Kind.Creature, Name = "Draugr" };
            var skill = new Entry { Kind = Kind.Skill, Name = "Swords" };
            var ghost = new Entry { Kind = Kind.Creature, Name = "Ghost" };
            s.Crypt.Links.Add(new Link { Group = "Its spawn points place", Target = draugr.Key });
            s.Crypt.Links.Add(new Link { Group = "Its spawn points place", Target = ghost.Key });
            s.NotWalked.Links.Add(new Link { Group = "Its spawn points place", Target = draugr.Key });
            s.Crypt.Links.Add(new Link { Group = "Trains", Target = skill.Key });
            var explorer = new Explorer(new[] { s.Crypt, s.Walked, s.NotWalked, s.Haldor, s.Glow, draugr, skill, ghost }, new Favourites(System.IO.Path.Combine(TestCatalog.TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            view.RoomsWalked(name => name == "sunkencrypt_Room1", 1);
            view.InWorld(World);
            var rooms = new[] { s.Walked.Name, s.NotWalked.Name };
            var map = new DungeonMap { Location = s.Crypt.Name, Rooms = new List<(int, Vec3, Vec3)>() };
            foreach (var room in rooms) map.Rooms.Add((Hash(room), new Vec3(0f, 0f, 0f), new Vec3(0f, 0f, 0f)));
            foreach (var room in walkedOnServer) map.Walked.Add(System.Array.IndexOf(rooms, room));
            view.WalkedOnServer(ServerWalks.Of(at => at == source ? new[] { map } : new DungeonMap[0], _ => null, Hash), 1);
            s.Book.FoundIn(World, source, new[] { "Crypt2", "Vendor_BlackForest" });
            s.Book.Show(source, level);
            view.Tell(s.Book, CompendiumRule.Strict);
            explorer.Know(view);
            return (explorer, s, draugr, skill);
        }

        private const string Ghost = "Ghost";

        [Fact]
        public void APlaceShownWithItsInsidesNamesWhatItsRoomsWalkedOnThisServerHold()
        {
            var (explorer, s, draugr, skill) = Holding(WorldSource.SharedMaps, SourceLevel.WithInsides, "sunkencrypt_Room2");
            Assert.True(explorer.LevelOf(draugr) < KnownLevel.Seen);
            explorer.Select(s.Crypt);
            Assert.False(explorer.LeavesOut(draugr.Key));
            // Its example stands every creature its walked rooms place.
            Assert.False(explorer.NotMet(draugr.Key));
            // What only its own outside holds waits for the dungeon to be walked through whole.
            Assert.True(explorer.LeavesOut(EntryKeys.For(Kind.Creature, Ghost)));
            // A skill never learned is named nowhere, all it holds or not.
            Assert.True(explorer.LeavesOut(skill.Key));
            Assert.True(explorer.NotMet(skill.Key));
        }

        [Fact]
        public void WhatAWalkedRoomHoldsByWhatWasReadOfItIsNamedOnItsPlacesPage()
        {
            // A room's creatures and loot come from what was read of it, not from links of its own.
            var s = new Setup();
            var draugr = new Entry { Kind = Kind.Creature, Name = "Draugr" };
            var chest = new Entry { Kind = Kind.Item, Name = "Ruby" };
            var explorer = new Explorer(new[] { s.Crypt, s.Walked, s.NotWalked, s.Haldor, s.Glow, draugr, chest }, new Favourites(System.IO.Path.Combine(TestCatalog.TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            view.InWorld(World);
            view.RoomsHold(room => room == s.NotWalked ? new[] { "Draugr", "Ruby", "nothing_read" } : System.Array.Empty<string>());
            var rooms = new[] { s.Walked.Name, s.NotWalked.Name };
            var map = new DungeonMap { Location = s.Crypt.Name, Rooms = new List<(int, Vec3, Vec3)>() };
            foreach (var room in rooms) map.Rooms.Add((Hash(room), new Vec3(0f, 0f, 0f), new Vec3(0f, 0f, 0f)));
            map.Walked.Add(1);
            view.WalkedOnServer(ServerWalks.Of(at => at == WorldSource.SharedMaps ? new[] { map } : new DungeonMap[0], _ => null, Hash), 1);
            s.Book.FoundIn(World, WorldSource.SharedMaps, new[] { "Crypt2" });
            s.Book.Show(WorldSource.SharedMaps, SourceLevel.WithInsides);
            view.Tell(s.Book, CompendiumRule.Loose);
            explorer.Know(view);
            explorer.Select(s.Crypt);
            Assert.False(explorer.LeavesOut(draugr.Key));
            Assert.False(explorer.LeavesOut(chest.Key));
            Assert.False(explorer.NotMet(draugr.Key));
        }

        [Fact]
        public void APlaceNobodyOnThisServerWalkedNamesOnlyWhatWasMetWithInsides()
        {
            var (explorer, s, draugr, _) = Holding(WorldSource.SharedMaps, SourceLevel.WithInsides);
            explorer.Select(s.Crypt);
            Assert.True(explorer.LeavesOut(draugr.Key));
            Assert.True(explorer.NotMet(draugr.Key));
        }

        [Fact]
        public void ARoomWalkedOnThisServerNamesAllItHoldsOneNotWalkedOnlyWhatWasMet()
        {
            var (explorer, s, draugr, _) = Holding(WorldSource.SharedMaps, SourceLevel.WithInsides, "sunkencrypt_Room2");
            explorer.Select(s.NotWalked);
            Assert.False(explorer.LeavesOut(draugr.Key));
            Assert.False(explorer.NotMet(draugr.Key));

            var (other, t, creature, _) = Holding(WorldSource.SharedMaps, SourceLevel.WithInsides, "sunkencrypt_Room1");
            other.Select(t.NotWalked);
            Assert.True(other.NotMet(creature.Key));
        }

        [Fact]
        public void ADungeonWalkedThroughWholeOnThisServerNamesAllItsPlaceHolds()
        {
            var (explorer, s, draugr, skill) = Holding(WorldSource.SharedMaps, SourceLevel.WithInsides, "sunkencrypt_Room1", "sunkencrypt_Room2");
            explorer.Select(s.Crypt);
            Assert.False(explorer.LeavesOut(EntryKeys.For(Kind.Creature, Ghost)));
            Assert.False(explorer.LeavesOut(draugr.Key));
            Assert.True(explorer.LeavesOut(skill.Key));
        }

        [Fact]
        public void APlaceShownKnownOrAnyOtherPageNamesOnlyWhatWasMet()
        {
            var (known, s, draugr, _) = Holding(WorldSource.SharedMaps, SourceLevel.Known);
            known.Select(s.Crypt);
            Assert.True(known.LeavesOut(draugr.Key));
            Assert.True(known.NotMet(draugr.Key));

            // A page the source brings along, but no place shown with its insides: its effect.
            var (insides, t, other, _) = Holding(WorldSource.SharedMaps, SourceLevel.WithInsides);
            insides.Select(t.Glow);
            Assert.True(insides.LeavesOut(other.Key));
            Assert.True(insides.NotMet(other.Key));
        }

        [Fact]
        public void WhatAPlaceHoldsIsNamedOnlyOnItsPagesNotSeenElsewhere()
        {
            var (explorer, s, draugr, _) = Holding(WorldSource.SharedMaps, SourceLevel.WithInsides);
            explorer.Select(s.Crypt);
            Assert.True(explorer.LevelOf(draugr) < KnownLevel.Seen);
            Assert.False(explorer.Reaches(draugr.Key));
            Assert.Equal(0, explorer.FoundOf(Kind.Creature));
        }

        [Fact]
        public void OnlyWalksOnThisServerUnderWithInsidesNameAllARoomOrAPlaceHolds()
        {
            var s = new Setup().Found(WorldSource.SharedMaps, "Crypt2");
            s.WalkedOnServer(WorldSource.SharedMaps, s.NotWalked);
            s.Shows(WorldSource.SharedMaps, SourceLevel.WithInsides, CompendiumRule.Strict);
            Assert.True(s.View.HoldsNamed(s.NotWalked));
            Assert.False(s.View.HoldsNamed(s.Walked));
            Assert.False(s.View.HoldsNamed(s.Crypt));
            Assert.False(s.View.HoldsNamed(s.Haldor));
            // Every room walked: the dungeon is walked through whole.
            s.WalkedOnServer(WorldSource.SharedMaps, s.Walked, s.NotWalked).Tell();
            Assert.True(s.View.HoldsNamed(s.Crypt));
            s.Shows(WorldSource.SharedMaps, SourceLevel.Known, CompendiumRule.Strict);
            Assert.False(s.View.HoldsNamed(s.Crypt));
            Assert.False(s.View.HoldsNamed(s.NotWalked));
        }

        [Fact]
        public void WithInsidesIsOfferedOnlyWhereAPlaceFoundHasInsides()
        {
            // Dimmed elsewhere, its tip naming what would: a row of traders, altars and the start has none.
            var dungeons = new HashSet<string> { "Crypt2", "Hildir_cave" };
            Assert.False(WorldSources.HasInsides(new[] { "Vendor_BlackForest", "StartTemple" }, dungeons.Contains));
            Assert.True(WorldSources.HasInsides(new[] { "StartTemple", "Hildir_cave" }, dungeons.Contains));
            Assert.False(WorldSources.HasInsides(new string[0], dungeons.Contains));
            Assert.Equal("None of the places in this ground has insides yet.", CompendiumWords.NoInsides(WorldSource.Explored));
            Assert.Equal("None of the places marked for you in this world has insides yet. The Queen's lair and Hildir's crypt, cave and tower would.", CompendiumWords.NoInsides(WorldSource.Marked));
            Assert.Equal("None of the places every map shows in this world has insides yet. Hildir's crypt, cave and tower show once she sends you to them.", CompendiumWords.NoInsides(WorldSource.OnEveryMap));
        }

        private static List<string> Sorted(IEnumerable<string> kinds)
        {
            var list = new List<string>(kinds);
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }
    }
}
