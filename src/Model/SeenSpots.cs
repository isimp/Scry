using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// Where a character saw each resource or place it may want to find again, for Show on map to
    /// dot on the large map: one spot a thing, by world and kind, to the metre. The same thing
    /// seen again, or opened into its broken form where it stood, is the same spot (within
    /// <see cref="SameSpot"/>); a thing gone takes its own spot away. Kept from session to session
    /// as text (<see cref="Save"/>, <see cref="Load"/>), a line a spot, in a fixed order so it reads
    /// back the same.
    /// </summary>
    internal sealed class SeenSpots
    {
        /// <summary>How near, in metres, a thing seen stands to a kept spot of its kind to be that spot.</summary>
        public const float SameSpot = 1.5f;

        /// <summary>The first line of the text, telling this kind of file and its make.</summary>
        private const string Header = "scry seen spots 1";

        /// <summary>The side of a bucket the spots are kept in, the game's zone, so a look for the same spot reads few.</summary>
        private const int Bucket = 64;

        /// <summary>The spots, by world, then kind, then the bucket they stand in.</summary>
        private readonly Dictionary<string, Dictionary<string, Dictionary<long, List<(int X, int Z)>>>> _spots =
            new Dictionary<string, Dictionary<string, Dictionary<long, List<(int X, int Z)>>>>(StringComparer.Ordinal);

        /// <summary>Whether anything differs from what was last saved or loaded.</summary>
        public bool Changed { get; private set; }

        /// <summary>How many spots are kept, in every world.</summary>
        public int Count => _spots.Values.Sum(kinds => kinds.Values.Sum(buckets => buckets.Values.Sum(list => list.Count)));

        /// <summary>A thing of a kind seen standing at x and z in a world: kept as a spot unless one of its kind is kept there already; whether it was new.</summary>
        public bool Saw(string world, string kind, float x, float z)
        {
            if (Find(world, kind, x, z, out _, out _) || world == null || kind == null) return false;
            var at = (X: (int)Math.Round(x), Z: (int)Math.Round(z));
            if (!_spots.TryGetValue(world, out var kinds)) _spots[world] = kinds = new Dictionary<string, Dictionary<long, List<(int, int)>>>(StringComparer.Ordinal);
            if (!kinds.TryGetValue(kind, out var buckets)) kinds[kind] = buckets = new Dictionary<long, List<(int, int)>>();
            var key = BucketOf(at.X, at.Z);
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<(int, int)>();
            list.Add(at);
            Changed = true;
            return true;
        }

        /// <summary>A thing of a kind gone from x and z in a world: its spot taken away; whether one was kept.</summary>
        public bool Gone(string world, string kind, float x, float z)
        {
            if (!Find(world, kind, x, z, out var list, out var index)) return false;
            list.RemoveAt(index);
            Changed = true;
            return true;
        }

        /// <summary>The spots of a kind kept in a world, in a fixed order.</summary>
        public IReadOnlyList<(int X, int Z)> Of(string world, string kind)
        {
            if (world == null || kind == null || !_spots.TryGetValue(world, out var kinds) || !kinds.TryGetValue(kind, out var buckets)) return Array.Empty<(int, int)>();
            return Ordered(buckets).ToList();
        }

        /// <summary>Every spot of a world within a reach of x and z, the reach itself counting, with its kind: what to look for where the character stands.</summary>
        public IEnumerable<(string Kind, int X, int Z)> Near(string world, float x, float z, float reach)
        {
            if (world == null || !_spots.TryGetValue(world, out var kinds)) yield break;
            var most = reach * reach;
            foreach (var kind in kinds)
            {
                foreach (var key in BucketsAround(x, z, reach))
                {
                    if (!kind.Value.TryGetValue(key, out var list)) continue;
                    foreach (var spot in list)
                    {
                        var dx = spot.X - x;
                        var dz = spot.Z - z;
                        if (dx * dx + dz * dz <= most) yield return (kind.Key, spot.X, spot.Z);
                    }
                }
            }
        }

        /// <summary>Takes away a world's spots, or every spot where no world is named.</summary>
        public void Forget(string world = null)
        {
            if (world == null) _spots.Clear();
            else _spots.Remove(world);
            Changed = true;
        }

        /// <summary>The spots as text, worlds, kinds and spots in a fixed order; what is saved is no longer changed.</summary>
        public string Save()
        {
            var text = new StringBuilder().AppendLine(Header);
            foreach (var world in _spots.Keys.OrderBy(w => w, StringComparer.Ordinal))
            {
                foreach (var kind in _spots[world].OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    foreach (var spot in Ordered(kind.Value))
                    {
                        text.Append(Stored.Escaped(world)).Append('\t').Append(Stored.Escaped(kind.Key)).Append('\t')
                            .Append(Stored.Count(spot.X)).Append('\t').Append(Stored.Count(spot.Z)).Append('\n');
                    }
                }
            }
            Changed = false;
            return text.ToString();
        }

        /// <summary>Spots read back from their text; none from text of another make; a line that does not read is passed over.</summary>
        public static SeenSpots Load(string text)
        {
            var spots = new SeenSpots();
            var lines = (text ?? "").Split('\n');
            if (lines.Length == 0 || lines[0].TrimEnd('\r') != Header) return spots;
            for (var i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].TrimEnd('\r').Split('\t');
                if (fields.Length != 4 || !Stored.TryUnescaped(fields[0], out var world) || !Stored.TryUnescaped(fields[1], out var kind)) continue;
                if (!Stored.TryCount(fields[2], out var x) || !Stored.TryCount(fields[3], out var z)) continue;
                spots.Saw(world, kind, x, z);
            }
            spots.Changed = false;
            return spots;
        }

        /// <summary>Whether a spot of a kind is kept within <see cref="SameSpot"/> of x and z, and where in its bucket's list.</summary>
        private bool Find(string world, string kind, float x, float z, out List<(int X, int Z)> list, out int index)
        {
            list = null;
            index = -1;
            if (world == null || kind == null || !_spots.TryGetValue(world, out var kinds) || !kinds.TryGetValue(kind, out var buckets)) return false;
            foreach (var key in BucketsAround(x, z, SameSpot))
            {
                if (!buckets.TryGetValue(key, out var candidates)) continue;
                for (var i = 0; i < candidates.Count; i++)
                {
                    var dx = candidates[i].X - x;
                    var dz = candidates[i].Z - z;
                    if (dx * dx + dz * dz > SameSpot * SameSpot) continue;
                    list = candidates;
                    index = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>The buckets a reach around x and z touches.</summary>
        private static IEnumerable<long> BucketsAround(float x, float z, float reach)
        {
            var (fromX, toX) = (FloorBucket(x - reach), FloorBucket(x + reach));
            var (fromZ, toZ) = (FloorBucket(z - reach), FloorBucket(z + reach));
            for (var bx = fromX; bx <= toX; bx++)
                for (var bz = fromZ; bz <= toZ; bz++) yield return Key(bx, bz);
        }

        private static long BucketOf(int x, int z) => Key(FloorBucket(x), FloorBucket(z));

        private static int FloorBucket(float at) => (int)Math.Floor(at / Bucket);

        private static long Key(int bx, int bz) => ((long)bx << 32) | (uint)bz;

        /// <summary>A kind's spots in a fixed order: by x, then z.</summary>
        private static IEnumerable<(int X, int Z)> Ordered(Dictionary<long, List<(int X, int Z)>> buckets) =>
            buckets.Values.SelectMany(list => list).OrderBy(s => s.X).ThenBy(s => s.Z);
    }

    /// <summary>The rules of what a character's sight records for Show on map.</summary>
    internal static class SeenOnMap
    {
        /// <summary>How far a thing is seen for the map at most where the world sets nothing else, as far as the map itself uncovers around you.</summary>
        public const float DefaultReach = 50f;

        /// <summary>How far a thing of a size is seen for the map: as far as its size is seen (<see cref="Sighting.Reach"/>), no farther than the world allows; none where it allows none.</summary>
        public static float Reach(float size, float worldReach) => worldReach <= 0f ? 0f : Math.Min(Sighting.Reach(size), worldReach);

        /// <summary>Whether a thing seen is recorded: nothing a Wishbone finds, as a buried vein or chest, until it is opened and carries no beacon.</summary>
        public static bool Records(bool foundByWishbone) => !foundByWishbone;
    }
}
