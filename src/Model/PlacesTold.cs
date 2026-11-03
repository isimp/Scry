using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// How a server tells players the places lying in ground they explored or saw on a shared map
    /// but never came near: not at all, the 128 m square each stands in (<see cref="PlaceSquares"/>),
    /// or where each stands (<see cref="PlacesToldBy"/>). The server's own setting; in single
    /// player, or hosting, the player's.
    /// </summary>
    internal enum PlacesTold
    {
        Off,
        Squares,
        Dots,
    }

    /// <summary>
    /// The places a server tells where they stand, on <see cref="PlacesTold.Dots"/>: those whose
    /// centre lies in the ground asked about, each by its kind and where it stands to the metre,
    /// sent after the squares, so a player's older Scry, which stops reading after them, keeps
    /// to the squares.
    /// </summary>
    internal static class PlacesToldBy
    {
        /// <summary>What a packed answer's spots begin with, so a part of another make is read as none.</summary>
        private const int Magic = 0x53505431;

        /// <summary>The setting read from an older config's on-and-off squares switch: left off, nothing told, until the setting is set anew.</summary>
        public static PlacesTold FromOld(bool? squaresOnMap, PlacesTold now) =>
            squaresOnMap == false && now == PlacesTold.Squares ? PlacesTold.Off : now;

        /// <summary>The places whose centre lies in explored ground, each by its kind and where it stands, in the order given.</summary>
        public static List<(string Kind, int X, int Z)> SpotsOf(ExploredGrid grid, IEnumerable<(string Kind, float X, float Z)> places)
        {
            var spots = new List<(string Kind, int X, int Z)>();
            foreach (var (kind, x, z) in places)
                if (!string.IsNullOrEmpty(kind) && grid.Explored(x, z)) spots.Add((kind, (int)Math.Round(x), (int)Math.Round(z)));
            return spots;
        }

        /// <summary>The spots as an answer sends them, each by its kind's place among the kinds it names, so a kind's name crosses the wire once.</summary>
        public static byte[] PackSpots(IReadOnlyList<string> kinds, IReadOnlyList<(string Kind, int X, int Z)> spots)
        {
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < kinds.Count; i++) if (kinds[i] != null && !index.ContainsKey(kinds[i])) index[kinds[i]] = i;
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write(Magic);
                var told = new List<(int, int, int)>(spots.Count);
                foreach (var (kind, x, z) in spots) if (kind != null && index.TryGetValue(kind, out var at)) told.Add((at, x, z));
                writer.Write(told.Count);
                foreach (var (at, x, z) in told)
                {
                    writer.Write(at);
                    writer.Write(x);
                    writer.Write(z);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>The spots an answer sent, read against the kinds it named; none where the part is broken or of another make.</summary>
        public static List<(string Kind, int X, int Z)> UnpackSpots(byte[] data, IReadOnlyList<string> kinds)
        {
            if (data == null || data.Length < 8) return null;
            using (var reader = new System.IO.BinaryReader(new System.IO.MemoryStream(data)))
            {
                if (reader.ReadInt32() != Magic) return null;
                var count = reader.ReadInt32();
                // Read only as long as it says: what follows is exactly its spots, or none is read.
                if (count < 0 || count > PlaceSquares.MostSent || data.Length != 8 + count * 12) return null;
                var spots = new List<(string Kind, int X, int Z)>(count);
                for (var i = 0; i < count; i++)
                {
                    var at = reader.ReadInt32();
                    var x = reader.ReadInt32();
                    var z = reader.ReadInt32();
                    if (at >= 0 && at < kinds.Count) spots.Add((kinds[at], x, z));
                }
                return spots;
            }
        }

        /// <summary>The spots told, but those where a mark stands already: one seen, one the game marks, a dungeon walked (<see cref="GameMarks.SamePlace"/>).</summary>
        public static List<(int X, int Z)> Besides(IReadOnlyList<(int X, int Z)> told, IReadOnlyList<(int X, int Z)> marked)
        {
            var besides = new List<(int X, int Z)>();
            foreach (var (x, z) in told)
            {
                var near = false;
                foreach (var (mx, mz) in marked)
                {
                    if ((mx - x) * (mx - x) + (mz - z) * (mz - z) > GameMarks.SamePlace * GameMarks.SamePlace) continue;
                    near = true;
                    break;
                }
                if (!near) besides.Add((x, z));
            }
            return besides;
        }
    }
}
