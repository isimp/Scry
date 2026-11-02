using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>A 128 m square of the world (<see cref="PlaceSquares"/>) and how many places of a kind stand in it, as the server tells it; never where inside.</summary>
    internal readonly struct PlaceSquare
    {
        public PlaceSquare(string kind, int x, int z, int count)
        {
            Kind = kind;
            X = x;
            Z = z;
            Count = count;
        }

        public string Kind { get; }

        /// <summary>The square's place on the world's grid, east and north, by <see cref="PlaceSquares.Index"/>.</summary>
        public int X { get; }
        public int Z { get; }

        public int Count { get; }
    }

    /// <summary>What an answer on a ground told: the kinds of place lying in it with how many of each, and the squares they stand in, or why there are none.</summary>
    internal sealed class GroundTold
    {
        public GroundTold(List<(string Kind, int Count)> counted, SquaresHeard heard, List<PlaceSquare> squares)
        {
            Counted = counted;
            Heard = heard;
            Squares = squares ?? new List<PlaceSquare>();
        }

        public List<(string Kind, int Count)> Counted { get; }
        public SquaresHeard Heard { get; }
        public List<PlaceSquare> Squares { get; }

        /// <summary>Worked out where this game knows where places stand: the counts, and the squares where it gives them.</summary>
        public static GroundTold Worked(ExploredGrid grid, IReadOnlyCollection<(string Kind, float X, float Z)> places, bool squares) =>
            new GroundTold(MapCatchUp.Counted(grid, places), squares ? SquaresHeard.Told : SquaresHeard.Off, squares ? PlaceSquares.Of(grid, places) : null);
    }

    /// <summary>What the server said of squares for a ground: nothing yet this session, that it runs no Scry, or a version that tells none or tells them otherwise than this one reads, that it gives none, or the squares.</summary>
    internal enum SquaresHeard
    {
        Waiting,
        NoScry,
        OtherScry,
        Off,
        Told,
    }

    /// <summary>
    /// Show on map's squares (B): for each kind of place, the 128 m squares of the world's own grid
    /// that hold one in ground the character explored or saw on a shared map, as only the server
    /// knows where places stand; worked out with the counts of each kind in a ground
    /// (<see cref="MapCatchUp.Counted"/>) and sent with them, never a place's position. The map
    /// shades a square to say one stands somewhere inside, leaving out those whose places the
    /// character all saw itself, which its dots show.
    /// </summary>
    internal static class PlaceSquares
    {
        /// <summary>
        /// How wide a square is, in metres: two of the world's 64 m zones a side
        /// (<c>ZoneSystem.c_ZoneSize</c>), and the game places at most one place in a zone, so a
        /// square says one stands in one of four zones, near enough to walk to and still to look for.
        /// </summary>
        public const float Metres = 128f;

        /// <summary>The square a world coordinate falls in, along one axis: 0 from the world's middle to 128 m east or north, -1 just west or south of it.</summary>
        public static int Index(float metres) => (int)Math.Floor(metres / Metres);

        /// <summary>The squares holding places whose centre lies in explored ground, a square a kind with how many of it, in the order first found.</summary>
        public static List<PlaceSquare> Of(ExploredGrid grid, IEnumerable<(string Kind, float X, float Z)> places)
        {
            var squares = new List<PlaceSquare>();
            var at = new Dictionary<(string, int, int), int>();
            foreach (var (kind, x, z) in places)
            {
                if (string.IsNullOrEmpty(kind) || !grid.Explored(x, z)) continue;
                var key = (kind, Index(x), Index(z));
                if (at.TryGetValue(key, out var i))
                {
                    var square = squares[i];
                    squares[i] = new PlaceSquare(kind, square.X, square.Z, square.Count + 1);
                }
                else
                {
                    at[key] = squares.Count;
                    squares.Add(new PlaceSquare(kind, key.Item2, key.Item3, 1));
                }
            }
            return squares;
        }

        /// <summary>The squares to shade: each with how many of its places the character did not see itself, those it saw all of left out.</summary>
        public static List<(int X, int Z, int Left)> Shown(IReadOnlyDictionary<(int X, int Z), int> squares, IReadOnlyList<(int X, int Z)> seen)
        {
            var seenIn = new Dictionary<(int, int), int>();
            if (seen != null)
            {
                foreach (var (x, z) in seen)
                {
                    var key = (Index(x), Index(z));
                    seenIn[key] = seenIn.TryGetValue(key, out var n) ? n + 1 : 1;
                }
            }
            var shown = new List<(int, int, int)>();
            foreach (var pair in squares)
            {
                var left = pair.Value - (seenIn.TryGetValue(pair.Key, out var n) ? n : 0);
                if (left > 0) shown.Add((pair.Key.X, pair.Key.Z, left));
            }
            return shown;
        }

        /// <summary>
        /// Where a square stands on the large map's pin layer, its lower left corner and its size,
        /// as the game places its pins (<see cref="MapMarks.Place"/>); false where none of it is in
        /// the window the map shows.
        /// </summary>
        public static bool OnMap(int x, int z, float pixelSize, int textureSize, (float XMin, float YMin, float Width, float Height) window, (float Width, float Height) layer, out (float X, float Y, float W, float H) at)
        {
            MapMarks.Place(new Vec3(x * Metres, 0f, z * Metres), pixelSize, textureSize, window, layer, out var low);
            MapMarks.Place(new Vec3((x + 1) * Metres, 0f, (z + 1) * Metres), pixelSize, textureSize, window, layer, out var high);
            at = (low.X, low.Y, high.X - low.X, high.Y - low.Y);
            return high.X > 0f && low.X < layer.Width && high.Y > 0f && low.Y < layer.Height;
        }

        /// <summary>
        /// The map's pixels under a square, for its fog mask: the first one along each side, how many
        /// cover it whole, and where the square begins and how much of them it spans, as shares of
        /// them, so the mask lies on the square as the fog lies on the map.
        /// </summary>
        public static (int FromX, int FromY, int Side, float U, float V, float Span) MaskPixels(int x, int z, float pixelSize, int textureSize)
        {
            var half = textureSize / 2;
            var startX = x * Metres / pixelSize + half;
            var startY = z * Metres / pixelSize + half;
            var fromX = (int)Math.Floor(startX);
            var fromY = (int)Math.Floor(startY);
            var across = Metres / pixelSize;
            var side = (int)Math.Ceiling(across) + 1;
            return (fromX, fromY, side, (startX - fromX) / side, (startY - fromY) / side, across / side);
        }

        /// <summary>What a packed answer's squares begin with, so a squares part of another make is read as none; the grid's size follows it, so squares on another grid are too.</summary>
        private const int Magic = 0x53515231;

        /// <summary>The most squares one answer may hold: every square of the biggest world a mod makes, with room for several kinds in each.</summary>
        public const int MostSent = 1 << 20;

        /// <summary>
        /// The squares as an answer sends them, after the kinds it names and the grid's size: each
        /// by its kind's place among them, its square and its count, so a kind's name crosses the
        /// wire once.
        /// </summary>
        public static byte[] Pack(IReadOnlyList<string> kinds, IReadOnlyList<PlaceSquare> squares)
        {
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < kinds.Count; i++) if (kinds[i] != null && !index.ContainsKey(kinds[i])) index[kinds[i]] = i;
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write((int)Metres);
                var told = new List<(int, PlaceSquare)>(squares.Count);
                foreach (var square in squares) if (square.Kind != null && index.TryGetValue(square.Kind, out var at)) told.Add((at, square));
                writer.Write(told.Count);
                foreach (var (at, square) in told)
                {
                    writer.Write(at);
                    writer.Write(square.X);
                    writer.Write(square.Z);
                    writer.Write(square.Count);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>An answer's squares part where the server gives none.</summary>
        public static byte[] PackOff()
        {
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write((int)Metres);
                writer.Write(-1);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// The squares an answer sent, read against the kinds it named, with what the server said:
        /// the squares, that it gives none, or, where they are broken, another make's or on another
        /// grid, none read and the server's Scry another than this one.
        /// </summary>
        public static (SquaresHeard Heard, List<PlaceSquare> Squares) Unpack(byte[] data, IReadOnlyList<string> kinds)
        {
            var squares = new List<PlaceSquare>();
            if (data == null || data.Length < 12) return (SquaresHeard.OtherScry, squares);
            using (var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(data)))
            {
                if (reader.ReadInt32() != Magic || reader.ReadInt32() != (int)Metres) return (SquaresHeard.OtherScry, squares);
                var count = reader.ReadInt32();
                if (count < 0) return (SquaresHeard.Off, squares);
                // Read only as long as it says: what follows is exactly its squares, or none is read.
                if (count > MostSent || data.Length != 12 + count * 16) return (SquaresHeard.OtherScry, squares);
                for (var i = 0; i < count; i++)
                {
                    var at = reader.ReadInt32();
                    var x = reader.ReadInt32();
                    var z = reader.ReadInt32();
                    var n = reader.ReadInt32();
                    if (at < 0 || at >= kinds.Count || n <= 0) continue;
                    squares.Add(new PlaceSquare(kinds[at], x, z, n));
                }
            }
            return (SquaresHeard.Told, squares);
        }

        /// <summary>How many places the squares shaded hold that the character did not see.</summary>
        public static int More(IReadOnlyList<(int X, int Z, int Left)> shown)
        {
            var more = 0;
            foreach (var square in shown) more += square.Left;
            return more;
        }
    }
}
