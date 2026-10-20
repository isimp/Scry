using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Where a character explored a world, by cells of the game's explored map: each cell covers
    /// <see cref="Factor"/> by <see cref="Factor"/> of the map's pixels and counts as explored
    /// where any of them is; one pixel, about 12 m, as places are told (<see cref="PlaceCellMetres"/>),
    /// four, about 48 m, for the biomes under the ground and as an older Scry sends it. Made from
    /// the game's map as the minimap keeps it (pixels per side, metres per pixel, a pixel's bit at
    /// y times the side plus x), so a world position falls in the pixel the game would mark for
    /// it; held and packed as a bit a cell for the server that knows where the world's places
    /// are, which never sends them back, and reads the cells' size from what it is sent.
    /// </summary>
    internal sealed class ExploredGrid
    {
        /// <summary>About how wide a cell is as places are told, in metres: one pixel of the game's map.</summary>
        public const float PlaceCellMetres = 12f;

        /// <summary>About how wide a cell is for the biomes under the ground, which need no finer, and as a Scry before 12 m cells sends it.</summary>
        public const float BiomeCellMetres = 48f;

        private const int Magic = 0x53435259;

        /// <summary>The most bytes a packed grid another game sends may unpack to (<see cref="Packed"/>): a map of 8,192 pixels a side at a bit each, four times the game's own, as mods may make it bigger.</summary>
        public const int MostSent = 16 + 8192 * 8192 / 8;

        /// <summary>A bit a cell, the cell at y times the side plus x.</summary>
        private readonly byte[] _bits;

        private ExploredGrid(int mapSize, float pixelSize, int factor, byte[] bits)
        {
            MapSize = mapSize;
            PixelSize = pixelSize;
            Factor = factor;
            Side = (mapSize + factor - 1) / factor;
            _bits = bits;
            var count = 0;
            foreach (var b in bits) for (var v = b; v != 0; v &= (byte)(v - 1)) count++;
            Count = count;
        }

        /// <summary>The game's map: pixels per side.</summary>
        public int MapSize { get; }

        /// <summary>The game's map: metres per pixel.</summary>
        public float PixelSize { get; }

        /// <summary>How many of the map's pixels a cell takes along each side.</summary>
        public int Factor { get; }

        /// <summary>Cells per side.</summary>
        public int Side { get; }

        /// <summary>How many cells were explored.</summary>
        public int Count { get; }

        private static int Bytes(int side) => (side * side + 7) / 8;

        private bool Cell(int i) => (_bits[i >> 3] & (1 << (i & 7))) != 0;

        /// <summary>Reads the game's explored map, given its size, its pixels' width and whether the pixel at an index was explored, in cells about the width given.</summary>
        public static ExploredGrid FromMap(int mapSize, float pixelSize, Func<int, bool> explored, float cellMetres = PlaceCellMetres)
        {
            var factor = Math.Max(1, (int)Math.Round(cellMetres / pixelSize));
            var side = (mapSize + factor - 1) / factor;
            var bits = new byte[Bytes(side)];
            for (var y = 0; y < mapSize; y++)
            {
                for (var x = 0; x < mapSize; x++)
                {
                    if (!explored(y * mapSize + x)) continue;
                    var i = y / factor * side + x / factor;
                    bits[i >> 3] |= (byte)(1 << (i & 7));
                }
            }
            return new ExploredGrid(mapSize, pixelSize, factor, bits);
        }

        /// <summary>Whether the ground at a world position was explored, by the pixel the game maps it to; nothing off the map was.</summary>
        public bool Explored(float x, float z)
        {
            var px = (int)Math.Round(x / PixelSize + MapSize / 2);
            var py = (int)Math.Round(z / PixelSize + MapSize / 2);
            if (px < 0 || px >= MapSize || py < 0 || py >= MapSize) return false;
            return Cell(py / Factor * Side + px / Factor);
        }

        /// <summary>The ground explored here and not in an earlier grid of the same map, as a grid of its own; all of it against none, or one of another map or cells.</summary>
        public ExploredGrid NewSince(ExploredGrid before)
        {
            if (before == null || before.MapSize != MapSize || before.Factor != Factor || before.PixelSize != PixelSize) return this;
            var bits = new byte[_bits.Length];
            for (var i = 0; i < bits.Length; i++) bits[i] = (byte)(_bits[i] & ~before._bits[i]);
            return new ExploredGrid(MapSize, PixelSize, Factor, bits);
        }

        /// <summary>A world position in each explored cell, its middle, as <see cref="Explored"/> reads it back.</summary>
        public IEnumerable<(float X, float Z)> ExploredCentres()
        {
            for (var i = 0; i < Side * Side; i++)
            {
                if (!Cell(i)) continue;
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
                writer.Write(_bits);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>A grid read back from <see cref="Pack"/>, of whatever cells it was packed in; none from bytes that are not one whole.</summary>
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
                if (data.Length - 16 != Bytes(side)) return null;
                return new ExploredGrid(mapSize, pixelSize, factor, reader.ReadBytes(Bytes(side)));
            }
        }
    }

    /// <summary>When the explored map is asked about again as it grows: once enough new ground is explored, at most once a minute.</summary>
    internal static class MapGrowth
    {
        /// <summary>The least time between two asks, in seconds.</summary>
        public const float Every = 60f;

        /// <summary>The fewest new cells worth an ask, some 3,600 m² at 12 m.</summary>
        public const int FewestNew = 25;

        /// <summary>Whether the map is asked about again: enough new cells, long enough since the last ask.</summary>
        public static bool Due(int newCells, float sinceAsked) => newCells >= FewestNew && sinceAsked >= Every;
    }

    /// <summary>
    /// Catching up from the explored map: the kinds of place lying where the character explored
    /// count as seen, as news noted as off the explored map (<see cref="EntryKeys.OnMap"/>).
    /// </summary>
    internal static class MapCatchUp
    {
        /// <summary>The kinds of place with one at least whose centre lies in explored ground, each once, in the order first found.</summary>
        public static List<string> Kinds(ExploredGrid grid, IEnumerable<(string Kind, float X, float Z)> places) => Counted(grid, places).Select(c => c.Kind).ToList();

        /// <summary>The kinds of place with one at least whose centre lies in explored ground, with how many, in the order first found.</summary>
        public static List<(string Kind, int Count)> Counted(ExploredGrid grid, IEnumerable<(string Kind, float X, float Z)> places)
        {
            var counted = new List<(string Kind, int Count)>();
            var at = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (kind, x, z) in places)
            {
                if (string.IsNullOrEmpty(kind) || !grid.Explored(x, z)) continue;
                if (at.TryGetValue(kind, out var i)) counted[i] = (kind, counted[i].Count + 1);
                else
                {
                    at[kind] = counted.Count;
                    counted.Add((kind, 1));
                }
            }
            return counted;
        }

        /// <summary>How many of each kind of place the world holds, planned or built, as only its server knows: the census, never where.</summary>
        public static Dictionary<string, int> Census(IEnumerable<(string Kind, float X, float Z)> places)
        {
            var census = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (kind, _, _) in places)
            {
                if (string.IsNullOrEmpty(kind)) continue;
                census[kind] = census.TryGetValue(kind, out var n) ? n + 1 : 1;
            }
            return census;
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

        /// <summary>
        /// Writes the kinds of place as seen, as news off the map, keeps them as found in the
        /// character's own ground of the world (<see cref="WorldSource.Explored"/>), for how the
        /// player chose they show, and marks the world caught up; how many were new.
        /// </summary>
        public static int Apply(CompendiumBook book, string world, IReadOnlyCollection<string> kinds)
        {
            var raised = 0;
            foreach (var kind in kinds)
            {
                if (Seen(book, EntryKeys.For(Kind.Location, kind))) raised++;
            }
            book.FoundIn(world, WorldSource.Explored, kinds);
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
