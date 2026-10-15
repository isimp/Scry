using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MapCatchUpTests
    {
        // A character met before the compendium kept a book has explored much of its world: the
        // places in the ground it explored count as seen, once per world. The game's map is a
        // square of pixels, each so many metres wide, the world's centre in its middle.

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
        public void APlaceCountsWhereTheGroundAroundItWasExplored()
        {
            // Cells of about 48 m: four pixels of 12 m a side, any of them explored marks the cell.
            var grid = Explored((5, 5));
            Assert.Equal(4, grid.Factor);

            Assert.True(grid.Explored(At(5, 5).X, At(5, 5).Z));
            Assert.True(grid.Explored(At(6, 7).X, At(6, 7).Z));
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
            Assert.Equal(5, ExploredGrid.FromMap(10, 10f, _ => false).Factor);
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
            var grid = Explored((5, 5), (40, 40));
            var places = new[]
            {
                ("Crypt2", At(5, 5).X, At(5, 5).Z),
                ("Crypt2", At(40, 40).X, At(40, 40).Z),
                ("TrollCave02", At(41, 41).X, At(41, 41).Z),
                ("Dragonqueen", At(20, 50).X, At(20, 50).Z),
            };

            Assert.Equal(new[] { "Crypt2", "TrollCave02" }, MapCatchUp.Kinds(grid, places));
        }

        [Fact]
        public void WhatTheMapTellsIsWrittenAsSeenQuietlyOncePerWorld()
        {
            // Seen before the book was kept: nothing newly learned, and what is known stays known.
            var book = new CompendiumBook();
            book.Raise(EntryKeys.For(Kind.Location, "TrollCave02"), KnownLevel.Known);
            var learned = book.Learned;

            var raised = MapCatchUp.Apply(book, "123", new[] { "Crypt2", "TrollCave02" });

            Assert.Equal(1, raised);
            Assert.Equal(KnownLevel.Seen, book.Level(EntryKeys.For(Kind.Location, "Crypt2")));
            Assert.Equal(KnownLevel.Known, book.Level(EntryKeys.For(Kind.Location, "TrollCave02")));
            Assert.Equal(learned, book.Learned);
            Assert.True(book.Mapped("123"));
            Assert.False(book.Mapped("456"));
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
        public void TheBiomesUnderExploredGroundAreWrittenAsSeenQuietly()
        {
            var book = new CompendiumBook();
            book.Raise(EntryKeys.For(Kind.Biome, "Meadows"), KnownLevel.Known, quietly: true);
            book.Save();

            Assert.Equal(1, MapCatchUp.ApplyBiomes(book, new[] { "Meadows", "Swamp" }));

            Assert.Equal(KnownLevel.Known, book.Level(EntryKeys.For(Kind.Biome, "Meadows")));
            Assert.Equal(KnownLevel.Seen, book.Level(EntryKeys.For(Kind.Biome, "Swamp")));
            Assert.Empty(book.Lately);
        }
    }
}
