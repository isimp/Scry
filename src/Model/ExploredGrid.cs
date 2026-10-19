using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Where a character explored a world, coarser than the game's explored map: each cell covers
    /// <see cref="Factor"/> by <see cref="Factor"/> of the map's pixels, about 48 m a side, and
    /// counts as explored where any of them is. Made from the game's map as the minimap keeps it
    /// (pixels per side, metres per pixel, a pixel's bit at y times the side plus x), so a world
    /// position falls in the pixel the game would mark for it; packed to bytes for the server
    /// that knows where the world's places are, which never sends them back.
    /// </summary>
    internal sealed class ExploredGrid
    {
        /// <summary>About how wide a cell is, in metres: places are compared with it, never drawn.</summary>
        private const float CellMetres = 48f;

        private const int Magic = 0x53435259;

        private readonly bool[] _cells;

        private ExploredGrid(int mapSize, float pixelSize, int factor, bool[] cells)
        {
            MapSize = mapSize;
            PixelSize = pixelSize;
            Factor = factor;
            Side = (mapSize + factor - 1) / factor;
            _cells = cells;
        }

        /// <summary>The game's map: pixels per side.</summary>
        public int MapSize { get; }

        /// <summary>The game's map: metres per pixel.</summary>
        public float PixelSize { get; }

        /// <summary>How many of the map's pixels a cell takes along each side.</summary>
        public int Factor { get; }

        /// <summary>Cells per side.</summary>
        public int Side { get; }

        /// <summary>Reads the game's explored map, given its size, its pixels' width and whether the pixel at an index was explored.</summary>
        public static ExploredGrid FromMap(int mapSize, float pixelSize, Func<int, bool> explored)
        {
            var factor = Math.Max(1, (int)Math.Round(CellMetres / pixelSize));
            var side = (mapSize + factor - 1) / factor;
            var cells = new bool[side * side];
            for (var y = 0; y < mapSize; y++)
            {
                for (var x = 0; x < mapSize; x++)
                {
                    if (explored(y * mapSize + x)) cells[y / factor * side + x / factor] = true;
                }
            }
            return new ExploredGrid(mapSize, pixelSize, factor, cells);
        }

        /// <summary>Whether the ground at a world position was explored, by the pixel the game maps it to; nothing off the map was.</summary>
        public bool Explored(float x, float z)
        {
            var px = (int)Math.Round(x / PixelSize + MapSize / 2);
            var py = (int)Math.Round(z / PixelSize + MapSize / 2);
            if (px < 0 || px >= MapSize || py < 0 || py >= MapSize) return false;
            return _cells[py / Factor * Side + px / Factor];
        }

        /// <summary>A world position in each explored cell, its middle, as <see cref="Explored"/> reads it back.</summary>
        public IEnumerable<(float X, float Z)> ExploredCentres()
        {
            for (var i = 0; i < _cells.Length; i++)
            {
                if (!_cells[i]) continue;
                var px = Math.Min(MapSize - 1, i % Side * Factor + Factor / 2);
                var py = Math.Min(MapSize - 1, i / Side * Factor + Factor / 2);
                yield return ((px - MapSize / 2) * PixelSize, (py - MapSize / 2) * PixelSize);
            }
        }

        /// <summary>The grid as bytes: what it is, the map's size and pixel width, the cells' factor, then a bit for each cell.</summary>
        public byte[] Pack()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(MapSize);
                writer.Write(PixelSize);
                writer.Write(Factor);
                var bytes = new byte[(_cells.Length + 7) / 8];
                for (var i = 0; i < _cells.Length; i++) if (_cells[i]) bytes[i / 8] |= (byte)(1 << (i % 8));
                writer.Write(bytes);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>A grid read back from <see cref="Pack"/>; none from bytes that are not one whole.</summary>
        public static ExploredGrid Unpack(byte[] data)
        {
            if (data == null || data.Length < 16) return null;
            using (var reader = new BinaryReader(new MemoryStream(data)))
            {
                if (reader.ReadInt32() != Magic) return null;
                var mapSize = reader.ReadInt32();
                var pixelSize = reader.ReadSingle();
                var factor = reader.ReadInt32();
                if (mapSize <= 0 || mapSize > 1 << 14 || factor <= 0 || factor > mapSize || !(pixelSize > 0f)) return null;
                var side = (mapSize + factor - 1) / factor;
                var cells = new bool[side * side];
                if (data.Length - 16 != (cells.Length + 7) / 8) return null;
                var bytes = reader.ReadBytes((cells.Length + 7) / 8);
                for (var i = 0; i < cells.Length; i++) cells[i] = (bytes[i / 8] & (1 << (i % 8))) != 0;
                return new ExploredGrid(mapSize, pixelSize, factor, cells);
            }
        }
    }

    /// <summary>
    /// Catching up from the explored map: the kinds of place lying where the character explored
    /// count as seen, as news noted as off the explored map (<see cref="EntryKeys.OnMap"/>).
    /// </summary>
    internal static class MapCatchUp
    {
        /// <summary>The kinds of place with one at least whose centre lies in explored ground, each once, in the order first found.</summary>
        public static List<string> Kinds(ExploredGrid grid, IEnumerable<(string Kind, float X, float Z)> places)
        {
            var kinds = new List<string>();
            var told = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (kind, x, z) in places)
            {
                if (string.IsNullOrEmpty(kind) || told.Contains(kind) || !grid.Explored(x, z)) continue;
                told.Add(kind);
                kinds.Add(kind);
            }
            return kinds;
        }

        /// <summary>
        /// The biomes under the explored ground, each once, by the biome at a point in each
        /// explored cell as the world generator tells it; ground of no biome is passed over.
        /// </summary>
        public static List<string> Biomes(ExploredGrid grid, Func<float, float, string> biomeAt)
        {
            var biomes = new List<string>();
            var told = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (x, z) in grid.ExploredCentres())
            {
                var biome = biomeAt(x, z);
                if (string.IsNullOrEmpty(biome) || biome == NoBiome || !told.Add(biome)) continue;
                biomes.Add(biome);
            }
            return biomes;
        }

        /// <summary>What the game calls ground of no biome.</summary>
        private const string NoBiome = "None";

        /// <summary>Writes the biomes under the explored ground as seen, as news off the map; how many were new.</summary>
        public static int ApplyBiomes(CompendiumBook book, IEnumerable<string> biomes) =>
            biomes.Count(biome => Seen(book, EntryKeys.For(Kind.Biome, biome)));

        /// <summary>Writes the kinds of place as seen, as news off the map, and the world as caught up; how many were new.</summary>
        public static int Apply(CompendiumBook book, string world, IEnumerable<string> kinds)
        {
            var raised = 0;
            foreach (var kind in kinds)
            {
                if (Seen(book, EntryKeys.For(Kind.Location, kind))) raised++;
            }
            book.MarkMapped(world);
            return raised;
        }

        /// <summary>An entry the map shows, as news noted as off the map, no sighting; true when it was new.</summary>
        private static bool Seen(CompendiumBook book, string key)
        {
            if (!book.Raise(key, KnownLevel.Seen, sighted: false)) return false;
            book.NoteNews(key, EntryKeys.OnMap);
            return true;
        }
    }
}
