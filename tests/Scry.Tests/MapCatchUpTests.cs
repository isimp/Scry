using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MapCatchUpTests
    {
        // A character met before the compendium kept a book has explored much of its world: the
        // places in the ground it explored count as seen, once per world, and again as it explores
        // more. The game's map is a square of pixels, each so many metres wide, the world's centre
        // in its middle; places are told by the map's own pixels, about 12 m.

        private const int MapSize = 64;
        private const float PixelSize = 12f;

        /// <summary>The world position of a map pixel's centre, as the game maps positions to pixels.</summary>
        private static (float X, float Z) At(int px, int py) => ((px - MapSize / 2) * PixelSize, (py - MapSize / 2) * PixelSize);

        private static ExploredGrid Explored(params (int X, int Y)[] pixels)
        {
            var bits = new bool[MapSize * MapSize];
            foreach (var (x, y) in pixels) bits[y * MapSize + x] = true;
            return ExploredGrid.FromMap(MapSize, PixelSize, i => bits[i]);
        }

        [Fact]
        public void APlaceCountsOnlyWhereTheGroundAtItWasExplored()
        {
            // Cells of one map pixel, 12 m a side: the pixel next to an explored one is not.
            var grid = Explored((5, 5));
            Assert.Equal(1, grid.Factor);

            Assert.True(grid.Explored(At(5, 5).X, At(5, 5).Z));
            Assert.False(grid.Explored(At(6, 5).X, At(6, 5).Z));
            Assert.False(grid.Explored(At(6, 7).X, At(6, 7).Z));
            Assert.False(grid.Explored(At(8, 5).X, At(8, 5).Z));
            Assert.False(grid.Explored(At(30, 30).X, At(30, 30).Z));
            Assert.False(grid.Explored(1e6f, 0f));
            Assert.False(grid.Explored(-1e6f, -1e6f));
        }

        [Fact]
        public void APositionFallsInThePixelNearestItAsTheGameMapsIt()
        {
            // 7.6 pixels in from the edge is the eighth pixel, in the third cell.
            var grid = Explored((8, 8));
            var x = (7.6f - MapSize / 2) * PixelSize;
            Assert.True(grid.Explored(x, x));
            Assert.False(grid.Explored(x - PixelSize, x));
            Assert.Equal(1, ExploredGrid.FromMap(10, 10f, _ => false).Factor);
            Assert.Equal(5, ExploredGrid.FromMap(10, 10f, _ => false, ExploredGrid.BiomeCellMetres).Factor);
        }

        [Fact]
        public void TheWorldsCentreIsTheMapsMiddle()
        {
            var grid = Explored((MapSize / 2, MapSize / 2));
            Assert.True(grid.Explored(0f, 0f));
            Assert.False(grid.Explored(0f, 100f));
        }

        [Fact]
        public void ACoarseMapWithWidePixelsIsNotMadeCoarser()
        {
            var grid = ExploredGrid.FromMap(16, 64f, i => i == 0);
            Assert.Equal(1, grid.Factor);
            Assert.True(grid.Explored(-8 * 64f, -8 * 64f));
            Assert.False(grid.Explored(-7 * 64f, -8 * 64f));
        }

        [Fact]
        public void TheGridIsSentAsBytesAndReadBackTheSame()
        {
            var grid = Explored((5, 5), (40, 12), (63, 63));
            var back = ExploredGrid.Unpack(grid.Pack());

            Assert.NotNull(back);
            Assert.Equal(grid.Factor, back.Factor);
            for (var y = 0; y < MapSize; y++)
            {
                for (var x = 0; x < MapSize; x++) Assert.Equal(grid.Explored(At(x, y).X, At(x, y).Z), back.Explored(At(x, y).X, At(x, y).Z));
            }
        }

        [Fact]
        public void AGridOfCoarserCellsAsAnOlderScrySendsItIsReadAsSent()
        {
            // Cells of about 48 m, four pixels a side, any of them explored marking the cell: what a
            // client's older Scry sends, which the server still answers.
            var bits = new bool[MapSize * MapSize];
            bits[5 * MapSize + 5] = true;
            var coarse = ExploredGrid.FromMap(MapSize, PixelSize, i => bits[i], ExploredGrid.BiomeCellMetres);
            Assert.Equal(4, coarse.Factor);
            var back = ExploredGrid.Unpack(coarse.Pack());
            Assert.Equal(4, back.Factor);
            Assert.True(back.Explored(At(6, 7).X, At(6, 7).Z));
            Assert.False(back.Explored(At(8, 5).X, At(8, 5).Z));
            Assert.Equal(1, back.Count);
        }

        [Fact]
        public void TheGroundNewSinceTheLastAskIsAGridOfItsOwn()
        {
            // Asked again as the map grows, only the ground explored since goes, its places' kinds alone answered.
            var before = Explored((5, 5), (6, 5));
            var now = Explored((5, 5), (6, 5), (40, 40), (41, 41));
            var fresh = now.NewSince(before);
            Assert.Equal(2, fresh.Count);
            Assert.False(fresh.Explored(At(5, 5).X, At(5, 5).Z));
            Assert.True(fresh.Explored(At(40, 40).X, At(40, 40).Z));
            Assert.Equal(4, now.Count);
            var places = new[] { ("Crypt2", At(5, 5).X, At(5, 5).Z), ("TrollCave02", At(41, 41).X, At(41, 41).Z) };
            Assert.Equal(new[] { "TrollCave02" }, MapCatchUp.Kinds(fresh, places));

            // Against nothing asked before, or a grid of another map, all of it is new.
            Assert.Equal(4, now.NewSince(null).Count);
            var other = ExploredGrid.FromMap(32, PixelSize, i => i == 0);
            Assert.Equal(4, now.NewSince(other).Count);
            Assert.Equal(0, now.NewSince(now).Count);
        }

        [Fact]
        public void TheMapIsAskedAgainOnceItGrewByEnoughAtMostOnceAMinute()
        {
            Assert.False(MapGrowth.Due(MapGrowth.FewestNew - 1, 600f));
            Assert.False(MapGrowth.Due(MapGrowth.FewestNew, MapGrowth.Every - 1f));
            Assert.True(MapGrowth.Due(MapGrowth.FewestNew, MapGrowth.Every));
            Assert.Equal(60f, MapGrowth.Every);
            Assert.Equal(25, MapGrowth.FewestNew);
        }

        [Fact]
        public void BytesThatAreNoGridReadAsNone()
        {
            Assert.Null(ExploredGrid.Unpack(null));
            Assert.Null(ExploredGrid.Unpack(new byte[3]));
            var packed = Explored((5, 5)).Pack();
            Assert.Null(ExploredGrid.Unpack(packed.Take(packed.Length - 1).ToArray()));
            packed[0] ^= 0xFF;
            Assert.Null(ExploredGrid.Unpack(packed));
        }

        [Fact]
        public void TheKindsOfPlaceInExploredGroundAreEachToldOnce()
        {
            var grid = Explored((5, 5), (40, 40), (41, 41));
            var places = new[]
            {
                ("Crypt2", At(5, 5).X, At(5, 5).Z),
                ("Crypt2", At(40, 40).X, At(40, 40).Z),
                ("TrollCave02", At(41, 41).X, At(41, 41).Z),
                ("Dragonqueen", At(20, 50).X, At(20, 50).Z),
                ("Eikthyrnir", At(42, 41).X, At(42, 41).Z),
            };

            // The altar a pixel past explored ground is not told.
            Assert.Equal(new[] { "Crypt2", "TrollCave02" }, MapCatchUp.Kinds(grid, places));
        }

        [Fact]
        public void WhatTheMapTellsIsKeptAsYourGroundNeverWrittenIntoTheBook()
        {
            // The game explores a ring of 100 m around the character whatever it saw, so the places
            // in it are kept as found in its own ground, shown as Ground you explored says, and are
            // no news; the book's seen stays what the character saw. What is known stays known.
            var book = new CompendiumBook();
            book.Raise(EntryKeys.For(Kind.Location, "TrollCave02"), KnownLevel.Known);
            var mark = book.Learned;

            var found = MapCatchUp.Apply(book, "123", new[] { "Crypt2", "TrollCave02" });

            Assert.Equal(2, found);
            Assert.Equal(KnownLevel.Unknown, book.Level(EntryKeys.For(Kind.Location, "Crypt2")));
            Assert.Equal(KnownLevel.Known, book.Level(EntryKeys.For(Kind.Location, "TrollCave02")));
            Assert.Empty(book.LearnedSince(mark));
            Assert.Null(book.FoundOutOf(EntryKeys.For(Kind.Location, "Crypt2")));
            Assert.Equal(new[] { "Crypt2", "TrollCave02" }, book.Found("123", WorldSource.Explored).OrderBy(k => k, System.StringComparer.Ordinal).ToArray());
            Assert.Equal(0, MapCatchUp.Apply(book, "123", new[] { "Crypt2" }));
            Assert.True(book.Mapped("123"));
            Assert.False(book.Mapped("456"));
        }

        [Fact]
        public void TheBiomesUnderTheMapAreFoundInYourGroundNeverWrittenIntoTheBook()
        {
            // As the kinds of place are: shown while the source is on, taken back with it.
            var book = new CompendiumBook();
            Assert.Equal(1, MapCatchUp.ApplyBiomes(book, "123", new[] { "Swamp" }));
            Assert.Equal(0, MapCatchUp.ApplyBiomes(book, "123", new[] { "Swamp" }));
            Assert.Equal(KnownLevel.Unknown, book.Level(EntryKeys.For(Kind.Biome, "Swamp")));
            Assert.Contains(EntryKeys.For(Kind.Biome, "Swamp"), book.Found("123", WorldSource.Explored));
            Assert.Equal(0, MapCatchUp.ApplyBiomes(book, null, new[] { "Swamp" }));
        }

        [Fact]
        public void ABookThatTookBiomesOffTheMapGivesThemBackOnce()
        {
            // A biome only ever seen off the map leaves the book and its Learned list, and its worlds
            // are read again so it comes back as found in your ground; one seen by sight, or known, stays.
            var swamp = EntryKeys.For(Kind.Biome, "Swamp");
            var mountain = EntryKeys.For(Kind.Biome, "Mountain");
            var meadows = EntryKeys.For(Kind.Biome, "Meadows");
            var book = new CompendiumBook { Now = () => new Moment(3, "Meadows") };
            foreach (var key in new[] { swamp, mountain })
            {
                book.Raise(key, KnownLevel.Seen, sighted: false);
                book.NoteNews(key, EntryKeys.OnMap);
            }
            book.Raise(mountain, KnownLevel.Known);
            book.Raise(meadows, KnownLevel.Seen);
            book.NoteNews(meadows, EntryKeys.OnMap);
            book.MarkMapped("123");
            var old = string.Join("\n", book.Save().Split('\n').Where(line => !line.StartsWith("biomesapart", System.StringComparison.Ordinal)));

            var read = CompendiumBook.Load(old);
            Assert.Equal(KnownLevel.Unknown, read.Level(swamp));
            Assert.Equal(KnownLevel.Known, read.Level(mountain));
            Assert.Equal(KnownLevel.Seen, read.Level(meadows));
            Assert.DoesNotContain(swamp, read.Lately);
            Assert.False(read.Mapped("123"));
            Assert.True(read.Changed);
            // Given back once: the book says so from then on.
            Assert.Contains("biomesapart", read.Save());
        }

        [Fact]
        public void ABookThatTookPlacesOffTheMapGivesThemBackOnce()
        {
            // Places only ever seen off the map leave the book and its Learned list, and its worlds
            // are read again so they come back as found in your ground; a place seen by sight, a
            // known one stay. Done once: what is seen off the map later is not undone.
            var crypt = EntryKeys.For(Kind.Location, "Crypt2");
            var cave = EntryKeys.For(Kind.Location, "TrollCave02");
            var camp = EntryKeys.For(Kind.Location, "GoblinCamp2");
            var swamp = EntryKeys.For(Kind.Biome, "Swamp");
            var book = new CompendiumBook { Now = () => new Moment(3, "Meadows") };
            foreach (var key in new[] { crypt, cave, camp, swamp })
            {
                book.Raise(key, KnownLevel.Seen, sighted: false);
                book.NoteNews(key, EntryKeys.OnMap);
            }
            book.Raise(EntryKeys.For(Kind.Location, "Seen1"), KnownLevel.Seen);
            var sighted = EntryKeys.For(Kind.Location, "Sighted2");
            book.Raise(sighted, KnownLevel.Seen);
            book.NoteNews(sighted, EntryKeys.OnMap);
            book.Raise(camp, KnownLevel.Known);
            book.MarkMapped("123");
            var old = string.Join("\n", book.Save().Split('\n').Where(line => !line.StartsWith("mapapart", System.StringComparison.Ordinal) && !line.StartsWith("biomesapart", System.StringComparison.Ordinal)));

            var read = CompendiumBook.Load(old);
            Assert.Equal(KnownLevel.Unknown, read.Level(crypt));
            Assert.Equal(KnownLevel.Unknown, read.Level(cave));
            Assert.Equal(KnownLevel.Known, read.Level(camp));
            // The biome only ever seen off the map is given back by the biomes' own giving back.
            Assert.Equal(KnownLevel.Unknown, read.Level(swamp));
            Assert.Equal(KnownLevel.Seen, read.Level(EntryKeys.For(Kind.Location, "Seen1")));
            Assert.Equal(KnownLevel.Seen, read.Level(sighted));
            Assert.DoesNotContain(crypt, read.Lately);
            Assert.DoesNotContain(swamp, read.Lately);
            Assert.False(read.Mapped("123"));
            Assert.True(read.Changed);

            // Once given back, a place seen off the map again stays as it is saved.
            read.Raise(crypt, KnownLevel.Seen, sighted: false);
            read.NoteNews(crypt, EntryKeys.OnMap);
            Assert.Equal(KnownLevel.Seen, CompendiumBook.Load(read.Save()).Level(crypt));
        }

        [Fact]
        public void EveryExploredCellIsToldByAPointInItAndNoOtherIs()
        {
            var grid = Explored((5, 5), (40, 9));
            var centres = grid.ExploredCentres().ToList();
            Assert.Equal(2, centres.Count);
            Assert.All(centres, c => Assert.True(grid.Explored(c.X, c.Z)));
            Assert.Empty(Explored().ExploredCentres());
        }

        [Fact]
        public void TheBiomesUnderExploredGroundAreEachToldOnce()
        {
            // Biomes the character's own exploring crossed are seen, as the map shows their ground.
            var grid = Explored((5, 5), (6, 6), (20, 20), (40, 9), (50, 50));
            string BiomeAt(float x, float z) => x < 0f ? "Meadows" : z < 0f ? "BlackForest" : "Swamp";

            var biomes = MapCatchUp.Biomes(grid, BiomeAt);

            Assert.Equal(new[] { "BlackForest", "Meadows", "Swamp" }, biomes.OrderBy(b => b, System.StringComparer.Ordinal));
            Assert.Empty(MapCatchUp.Biomes(Explored(), BiomeAt));
            Assert.DoesNotContain("None", MapCatchUp.Biomes(grid, (x, z) => "None"));
        }

        [Fact]
        public void TheBiomesUnderExploredGroundAreNoNewsAndLeaveWhatTheBookKnowsAlone()
        {
            // Found in the ground, not written: a biome known stays known, one new to the book stays out of it and of Learned.
            var book = new CompendiumBook();
            book.Raise(EntryKeys.For(Kind.Biome, "Meadows"), KnownLevel.Known, quietly: true);
            book.Save();

            Assert.Equal(2, MapCatchUp.ApplyBiomes(book, "123", new[] { "Meadows", "Swamp" }));

            Assert.Equal(KnownLevel.Known, book.Level(EntryKeys.For(Kind.Biome, "Meadows")));
            Assert.Equal(KnownLevel.Unknown, book.Level(EntryKeys.For(Kind.Biome, "Swamp")));
            Assert.Empty(book.Lately);
            Assert.Null(book.FoundOutOf(EntryKeys.For(Kind.Biome, "Swamp")));
            Assert.True(book.Changed);
        }

        [Fact]
        public void TheNoticeCountsWhatTheMapShowedInOneLine()
        {
            // A map read can bring hundreds of places: counted, not named; the rest named as ever.
            Assert.Equal("Your explored map showed 240 places", JournalWords.News(new NewsLine[0], 240, 0));
            Assert.Equal("Your explored map showed 1 place and 2 biomes", JournalWords.News(new NewsLine[0], 1, 2));
            Assert.Equal("Your explored map showed 1 biome", JournalWords.News(new NewsLine[0], 0, 1));
            var troll = new NewsLine("Troll", NewsHow.Known, null, 14);
            Assert.Equal("Day 14: came to know Troll and Wood", JournalWords.News(new[] { troll, new NewsLine("Wood", NewsHow.Known, null, 14) }, 0, 0));
            Assert.Equal("Day 14: came to know Troll, and your explored map showed 12 places", JournalWords.News(new[] { troll }, 12, 0));
        }
    }
}
