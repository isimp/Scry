using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CensusTests
    {
        // The world's server knows every place the world will ever have: once a session it tells
        // how many of each kind this world holds, never where, and its answers on a ground tell
        // how many of each kind lie there. With *The whole world* row at Counted, a place's page
        // says how many this world holds and the Locations tab counts against the kinds it holds;
        // how many lie in your ground shows whenever that ground's row shows anything.

        private const int MapSize = 64;
        private const float PixelSize = 12f;

        private static (float X, float Z) At(int px, int py) => ((px - MapSize / 2) * PixelSize, (py - MapSize / 2) * PixelSize);

        private static ExploredGrid Explored(params (int X, int Y)[] pixels)
        {
            var bits = new bool[MapSize * MapSize];
            foreach (var (x, y) in pixels) bits[y * MapSize + x] = true;
            return ExploredGrid.FromMap(MapSize, PixelSize, i => bits[i]);
        }

        private static readonly (string Kind, float X, float Z)[] Places =
        {
            ("Crypt2", At(5, 5).X, At(5, 5).Z),
            ("TrollCave02", At(40, 40).X, At(40, 40).Z),
            ("Crypt2", At(41, 41).X, At(41, 41).Z),
            ("Crypt2", At(60, 2).X, At(60, 2).Z),
            ("Dragonqueen", At(20, 50).X, At(20, 50).Z),
        };

        [Fact]
        public void TheServerCountsEachKindOfPlaceTheWorldHolds()
        {
            var census = MapCatchUp.Census(Places);
            Assert.Equal(3, census["Crypt2"]);
            Assert.Equal(1, census["TrollCave02"]);
            Assert.Equal(1, census["Dragonqueen"]);
            Assert.Equal(3, census.Count);
            Assert.Empty(MapCatchUp.Census(new (string, float, float)[] { ("", 0f, 0f) }));
        }

        [Fact]
        public void AGroundsAnswerCountsEachKindLyingInItInTheOrderFirstFound()
        {
            var grid = Explored((5, 5), (40, 40), (41, 41));
            var counted = MapCatchUp.Counted(grid, Places);
            Assert.Equal(new[] { ("Crypt2", 2), ("TrollCave02", 1) }, counted);
            Assert.Equal(counted.Select(c => c.Kind), MapCatchUp.Kinds(grid, Places));
        }

        [Fact]
        public void AWholeAnswerOnAGroundReplacesItsCountsAndTheGroundNewSinceAddsToThem()
        {
            var counts = new PlaceCounts();
            counts.Took(WorldSource.Explored, new[] { ("Crypt2", 2), ("TrollCave02", 1) }, whole: true);
            Assert.Equal(2, counts.In(WorldSource.Explored, "Crypt2"));
            counts.Took(WorldSource.Explored, new[] { ("Crypt2", 1) }, whole: false);
            Assert.Equal(3, counts.In(WorldSource.Explored, "Crypt2"));
            counts.Took(WorldSource.Explored, new[] { ("TrollCave02", 1) }, whole: true);
            Assert.Equal(0, counts.In(WorldSource.Explored, "Crypt2"));
            Assert.Equal(1, counts.In(WorldSource.Explored, "TrollCave02"));
            counts.Took(WorldSource.SharedMaps, new[] { ("Crypt2", 1) }, whole: true);
            Assert.Equal(1, counts.In(WorldSource.SharedMaps, "Crypt2"));
            // Before any answer, how many is not known.
            Assert.Equal(-1, new PlaceCounts().In(WorldSource.Explored, "Crypt2"));

            Assert.Equal(-1, counts.Held("Crypt2"));
            Assert.False(counts.CensusKnown);
            counts.Census(new Dictionary<string, int> { ["Crypt2"] = 14, ["Hildir_cave"] = 0 });
            Assert.True(counts.CensusKnown);
            Assert.Equal(14, counts.Held("Crypt2"));
            Assert.Equal(0, counts.Held("Hildir_cave"));
            // A kind counted none is no kind the world holds.
            Assert.Equal(new[] { "Crypt2" }, counts.HeldKinds);
            var version = counts.Version;
            counts.Forget();
            Assert.False(counts.CensusKnown);
            Assert.Equal(-1, counts.In(WorldSource.Explored, "TrollCave02"));
            Assert.True(counts.Version > version);
        }

        [Fact]
        public void APlacesPageSaysHowManyThisWorldHoldsAndHowManyLieInYourGround()
        {
            Assert.Equal("This world holds 14 · 3 lie in ground you explored · 1 in shared maps", CensusWords.Line(14, 3, 1));
            Assert.Equal("This world holds 14 · 1 lies in ground you explored", CensusWords.Line(14, 1, -1));
            Assert.Equal("This world holds 14", CensusWords.Line(14, 0, 0));
            Assert.Equal("This world holds none of these", CensusWords.Line(0, -1, -1));
            Assert.Equal("3 lie in ground you explored · 1 in shared maps", CensusWords.Line(-1, 3, 1));
            Assert.Equal("1 in shared maps", CensusWords.Line(-1, 0, 1));
            Assert.Null(CensusWords.Line(-1, -1, -1));
            Assert.Null(CensusWords.Line(-1, 0, 0));
        }

        [Fact]
        public void TheLocationsTabCountsAgainstTheKindsThisWorldHolds()
        {
            Assert.Equal("41 kinds of place found, of the 63 this world holds; the game and its mods can place 250", CensusWords.TabTip(41, 63, 250));
        }

        private static Entry Place(string name, string label) => new Entry { Kind = Kind.Location, Name = name, Components = new[] { "Location" }, FoundIn = new[] { label } };

        private static Entry Room(string name, string label) => new Entry { Kind = Kind.Location, Name = name, Components = new[] { "Room" }, FoundIn = new[] { label } };

        [Fact]
        public void APlaceTheWorldHoldsAndItsRoomsAreHeldTheRestNot()
        {
            var crypt = Place("Crypt2", "Burial chambers");
            var room = Room("crypt_room1", "Burial chambers");
            var haldor = Place("Vendor_BlackForest", "Haldor");
            var otherRoom = Room("cave_room1", "Frost caves");
            var held = PlaceCensus.Held(new[] { crypt, room, haldor, otherRoom, new Entry { Kind = Kind.Creature, Name = "Troll" } }, kind => kind == "Crypt2");
            Assert.Contains(crypt, held);
            Assert.Contains(room, held);
            Assert.DoesNotContain(haldor, held);
            // A room of a dungeon the world holds none of is not held either.
            Assert.DoesNotContain(otherRoom, held);
            Assert.Equal(2, held.Count);
        }

        [Fact]
        public void WithTheCensusTheLocationsTabCountsOnlyWhatTheWorldHolds()
        {
            var crypt = Place("Crypt2", "Burial chambers");
            var room = Room("crypt_room1", "Burial chambers");
            var haldor = Place("Vendor_BlackForest", "Haldor");
            var troll = new Entry { Kind = Kind.Creature, Name = "Troll" };
            var explorer = new Explorer(new[] { crypt, room, haldor, troll }, new Favourites(System.IO.Path.Combine(TestCatalog.TempDir(), "favourites.txt")));
            var book = new CompendiumBook();
            book.Raise(crypt.Key, KnownLevel.Seen);
            book.Raise(haldor.Key, KnownLevel.Seen);
            book.Raise(troll.Key, KnownLevel.Seen);
            var view = new CompendiumView(explorer.Entries);
            view.Tell(book, CompendiumRule.Loose);
            explorer.Know(view);
            Assert.Equal((2, 3), (explorer.FoundOf(Kind.Location), explorer.TotalOf(Kind.Location)));

            var held = new HashSet<Entry> { crypt, room };
            explorer.WorldHolds(held, 1);
            Assert.Equal((1, 2), (explorer.FoundOf(Kind.Location), explorer.TotalOf(Kind.Location)));
            Assert.Equal(3, explorer.AllOf(Kind.Location));
            // Only places count against the census.
            Assert.Equal((1, 1), (explorer.FoundOf(Kind.Creature), explorer.TotalOf(Kind.Creature)));
            // Handed again unchanged, nothing is counted afresh; none counts every kind again.
            var stamp = explorer.KnowStamp;
            explorer.WorldHolds(held, 1);
            Assert.Equal(stamp, explorer.KnowStamp);
            // Other places under the same count, as from a census of its own, are counted anew.
            explorer.WorldHolds(new HashSet<Entry> { haldor }, 1);
            Assert.Equal((1, 1), (explorer.FoundOf(Kind.Location), explorer.TotalOf(Kind.Location)));
            explorer.WorldHolds(held, 1);
            explorer.WorldHolds(null, 2);
            Assert.Equal((2, 3), (explorer.FoundOf(Kind.Location), explorer.TotalOf(Kind.Location)));
        }
    }
}
