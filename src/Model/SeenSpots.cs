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

        /// <summary>How many spots a world keeps at most, so a record never grows without end.</summary>
        public const int MostAWorld = 20000;

        /// <summary>How many spots each world keeps now.</summary>
        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);

        private readonly int _most;

        /// <summary>Spots kept, a world keeping at most <paramref name="most"/>.</summary>
        public SeenSpots(int most = MostAWorld) => _most = most;

        /// <summary>Whether anything differs from what was last saved or loaded.</summary>
        public bool Changed { get; private set; }

        /// <summary>Counts up whenever a spot is kept, taken away or forgotten, for what is drawn of them to be worked out again.</summary>
        public int Version { get; private set; }

        /// <summary>How many spots are kept, in every world.</summary>
        [ForTest]
        public int Count => _counts.Values.Sum();

        /// <summary>How many spots a world keeps.</summary>
        public int CountIn(string world) => world != null && _counts.TryGetValue(world, out var count) ? count : 0;

        /// <summary>
        /// A thing of a kind seen standing at x and z in a world: kept as a spot unless one of its
        /// kind is kept there already or the world keeps its most already; whether it was new.
        /// </summary>
        public bool Saw(string world, string kind, float x, float z)
        {
            if (world == null || kind == null || Find(world, kind, x, z, out _, out _)) return false;
            if (_counts.TryGetValue(world, out var count) && count >= _most) return false;
            _counts[world] = count + 1;
            var at = (X: (int)Math.Round(x), Z: (int)Math.Round(z));
            if (!_spots.TryGetValue(world, out var kinds)) _spots[world] = kinds = new Dictionary<string, Dictionary<long, List<(int, int)>>>(StringComparer.Ordinal);
            if (!kinds.TryGetValue(kind, out var buckets)) kinds[kind] = buckets = new Dictionary<long, List<(int, int)>>();
            var key = BucketOf(at.X, at.Z);
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<(int, int)>();
            list.Add(at);
            Changed = true;
            Version++;
            return true;
        }

        /// <summary>How many spots of a kind a world keeps, counted without listing them, as a page asks each time it is drawn.</summary>
        public int CountOf(string world, string kind)
        {
            if (world == null || kind == null || !_spots.TryGetValue(world, out var kinds) || !kinds.TryGetValue(kind, out var buckets)) return 0;
            var count = 0;
            foreach (var list in buckets.Values) count += list.Count;
            return count;
        }

        /// <summary>Whether a spot of a kind is kept at x and z in a world already (within <see cref="SameSpot"/>), so a thing seen there needs no further look.</summary>
        public bool Has(string world, string kind, float x, float z) => Find(world, kind, x, z, out _, out _);

        /// <summary>A thing of a kind gone from x and z in a world: its spot taken away; whether one was kept.</summary>
        public bool Gone(string world, string kind, float x, float z)
        {
            if (!Find(world, kind, x, z, out var list, out var index)) return false;
            list.RemoveAt(index);
            _counts[world]--;
            Changed = true;
            Version++;
            return true;
        }

        /// <summary>The spots of a kind kept in a world, in a fixed order.</summary>
        public IReadOnlyList<(int X, int Z)> Of(string world, string kind)
        {
            if (world == null || kind == null || !_spots.TryGetValue(world, out var kinds) || !kinds.TryGetValue(kind, out var buckets)) return Array.Empty<(int, int)>();
            return Ordered(buckets).ToList();
        }

        /// <summary>Every spot of a world within a reach of x and z, the reach itself counting, with its kind: what to look for where the character stands.</summary>
        [ForTest]
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
            if (world == null)
            {
                _spots.Clear();
                _counts.Clear();
            }
            else
            {
                _spots.Remove(world);
                _counts.Remove(world);
            }
            Changed = true;
            Version++;
        }

        /// <summary>The spots as text, worlds, kinds and spots in a fixed order; what is saved is no longer changed.</summary>
        public string Save() => ToSave()();

        /// <summary>
        /// The spots copied as they are now, and what makes their text from that copy (<see cref="Save"/>),
        /// to run off the game's frame while play goes on; what is copied is no longer changed.
        /// </summary>
        public Func<string> ToSave()
        {
            var copy = new List<(string World, string Kind, (int X, int Z)[] Spots)>();
            foreach (var world in _spots)
                foreach (var kind in world.Value) copy.Add((world.Key, kind.Key, kind.Value.Values.SelectMany(list => list).ToArray()));
            Changed = false;
            return () =>
            {
                var text = new StringBuilder().Append(Header).Append('\n');
                foreach (var (world, kind, spots) in copy.OrderBy(c => c.World, StringComparer.Ordinal).ThenBy(c => c.Kind, StringComparer.Ordinal))
                {
                    foreach (var spot in spots.OrderBy(s => s.X).ThenBy(s => s.Z))
                    {
                        text.Append(Stored.Escaped(world)).Append('\t').Append(Stored.Escaped(kind)).Append('\t')
                            .Append(Stored.Count(spot.X)).Append('\t').Append(Stored.Count(spot.Z)).Append('\n');
                    }
                }
                return text.ToString();
            };
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
            // Looped in place, not through BucketsAround, so the sight's look for a kept spot makes no garbage.
            for (var bx = FloorBucket(x - SameSpot); bx <= FloorBucket(x + SameSpot); bx++)
            {
                for (var bz = FloorBucket(z - SameSpot); bz <= FloorBucket(z + SameSpot); bz++)
                {
                    if (!buckets.TryGetValue(Key(bx, bz), out var candidates)) continue;
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

        /// <summary>What lies everywhere: what a thing gives that makes it no find of its own.</summary>
        private static readonly HashSet<string> Everywhere = new HashSet<string>(StringComparer.Ordinal) { "Wood", "Stone" };

        /// <summary>
        /// Whether a kind of thing is worth finding again on the map: a place; a thing picked, mined
        /// or tapped (<paramref name="gatheredBy"/>) that gives something besides wood and stone, as a
        /// berry bush, flint, a mushroom, an ore deposit or an ancient root. Not a branch, a stone, a plain rock, a
        /// tree, a shrub or a log, which stand everywhere, nor a crop only ever grown from planting
        /// (<paramref name="planted"/>: what a sapling or a seed grows into, <c>Plant.m_grownPrefabs</c>,
        /// that the world never places wild), which stands where the player planted it. A kind
        /// growing both wild and from planting, as a seed carrot, is one, kept off tilled ground
        /// (<see cref="InAGarden"/>).
        /// </summary>
        public static bool Findable(bool location, Gathered gatheredBy, IEnumerable<string> gives, bool planted = false) =>
            location || (!planted && gatheredBy != Gathered.None && gives.Any(item => !Everywhere.Contains(item)));

        /// <summary>Whether a thing stands in a garden and is no find there: a kind a planted sapling or seed grows into, standing on tilled ground, where the game grows a planted one (<c>Plant.m_needCultivatedGround</c>).</summary>
        public static bool InAGarden(bool grownFromPlanting, bool tilled) => grownFromPlanting && tilled;

        /// <summary>
        /// Whether a thing that breaks is a deposit mined: the pickaxe's damage counts against it
        /// and every other damage is turned away (<c>Destructible.m_damages</c>), as a tin or an
        /// obsidian deposit; not a pot or a stump anything breaks.
        /// </summary>
        public static bool OnlyAPickaxeBreaks(Degree pickaxe, IEnumerable<Degree> others) =>
            !TurnedAway(pickaxe) && others.All(TurnedAway);

        private static bool TurnedAway(Degree degree) => degree == Degree.Immune || degree == Degree.Ignore;

        /// <summary>A kept kind's dot on the minimap as a share of its pins' size, before the dots' size setting.</summary>
        public const float MiniDot = 0.8f;

        /// <summary>How big a kept kind's dot stands on the minimap: a share of its pins' size, scaled as the settings ask.</summary>
        public static float MiniDotSize(float pinSizeSmall, float scale) => pinSizeSmall * MiniDot * scale;

        /// <summary>How many marks of the kinds kept stand on the map at once, the nearest, so the map stays quick.</summary>
        public const int MostKept = 2000;

        /// <summary>The marks nearest x and z, at most <paramref name="most"/>, nearest first; all where they are fewer.</summary>
        public static List<(int X, int Z)> NearestOf(IReadOnlyList<(int X, int Z)> marks, float x, float z, int most)
        {
            if (marks.Count <= most) return marks.ToList();
            return marks.OrderBy(m => (m.X - x) * (m.X - x) + (m.Z - z) * (m.Z - z)).Take(most).ToList();
        }

        /// <summary>The spot nearest a point: how far, and how far east and north of the point; none where there is no spot.</summary>
        public static (float Distance, float Dx, float Dz)? Nearest(IReadOnlyList<(int X, int Z)> spots, float x, float z)
        {
            (float, float, float)? nearest = null;
            var best = float.MaxValue;
            foreach (var spot in spots)
            {
                var dx = spot.X - x;
                var dz = spot.Z - z;
                var away = dx * dx + dz * dz;
                if (away >= best) continue;
                best = away;
                nearest = ((float)Math.Sqrt(away), dx, dz);
            }
            return nearest;
        }

        /// <summary>Whether what a character's sight kept may stand on the map at all: a world keeping a map, its server remembering what is seen.</summary>
        public static bool Remembers(bool noMap, float reach) => !noMap && reach > 0f;

        /// <summary>
        /// Why Show on map shows nothing now: a world with no map, or a server remembering nothing
        /// seen (<see cref="Remembers"/>), shows none at all, neither spots kept before nor
        /// squares, as nothing kept on the map stands then; spots kept show while any are kept,
        /// recording on or off; with none, recording off, an unreadable file, or none seen yet says
        /// why. The server's squares of a place stand apart from what the character keeps, and show
        /// whatever it keeps.
        /// </summary>
        public static MapBlock Block(bool noMap, float reach, bool recording, bool unreadable, int count, int squares = 0)
        {
            if (noMap) return MapBlock.NoMap;
            if (reach <= 0f) return MapBlock.NoReach;
            if (squares > 0 || count > 0) return MapBlock.None;
            if (!recording) return MapBlock.RecordingOff;
            return unreadable ? MapBlock.Unreadable : MapBlock.NoneSeen;
        }

        /// <summary>The game's large map's nearest and farthest zoom (<c>Minimap.m_minZoom</c>, <c>m_maxZoom</c>), the share of the world it shows across.</summary>
        public const float NearestZoom = 0.01f, FarthestZoom = 1f;

        /// <summary>
        /// How big a dot is drawn, ring and all (<see cref="DotAt"/>): a pin and a half at the
        /// map's nearest zoom, smaller as it zooms out, to a whole pin at the whole world, so a dot
        /// stands as big as the pins it is among.
        /// </summary>
        public static float DotSize(float pinSize, float zoom)
        {
            var clamped = Math.Min(FarthestZoom, Math.Max(NearestZoom, zoom));
            var t = (float)(Math.Log(clamped / NearestZoom) / Math.Log(FarthestZoom / NearestZoom));
            return pinSize * (1.5f + (1f - 1.5f) * t);
        }

        /// <summary>How many of the marks nearest the character the map opens to hold, and the room left round the farthest of them.</summary>
        public const int FitNearest = 3;
        public const float FitMargin = 1.15f;

        /// <summary>
        /// The zoom the large map opens at for a kind shown, centred on the character: as it is
        /// where the nearest marks (<see cref="FitNearest"/>) stand in its window, else out just far
        /// enough to hold them, never nearer and never past the whole world. The window shows the
        /// zoom's share of the map's height and that times its <paramref name="aspect"/> across
        /// (<c>Minimap.CenterMap</c>); <paramref name="mapMetres"/> is the map's side.
        /// </summary>
        public static float FitZoom(float zoom, IEnumerable<(float Dx, float Dz)> marks, float mapMetres, float aspect)
        {
            var nearest = marks.OrderBy(m => m.Dx * m.Dx + m.Dz * m.Dz).Take(FitNearest);
            var needed = 0f;
            foreach (var (dx, dz) in nearest)
            {
                needed = Math.Max(needed, 2f * Math.Abs(dz) / mapMetres);
                needed = Math.Max(needed, 2f * Math.Abs(dx) / (mapMetres * aspect));
            }
            return Math.Min(FarthestZoom, Math.Max(zoom, needed * FitMargin));
        }

        /// <summary>
        /// What a dot shows at a distance from its middle, as a share of its radius: the kind's
        /// colour inside a dark edge, the map between it and a light ring, and a dark halo either
        /// side of the ring so it reads on light ground and dark. No pin of the game is ringed, so
        /// a ringed dot reads as Scry's among them.
        /// </summary>
        public static DotPart DotAt(float share)
        {
            if (share <= 0.41f) return DotPart.Disc;
            if (share <= 0.57f) return DotPart.Edge;
            if (share <= 0.71f) return DotPart.Clear;
            if (share <= 0.78f) return DotPart.Halo;
            if (share <= 0.93f) return DotPart.Ring;
            return share <= 1f ? DotPart.Halo : DotPart.Clear;
        }

        /// <summary>
        /// How far a point of a dot lies from its middle as a share of the way to its edge, by its
        /// mark's shape (<see cref="KeptMarks"/>), x and y from -1 to 1 across the dot, y up: the
        /// circle by its radius, the diamond, the square and the point-up triangle each reaching
        /// the whole at its own edge, every one of them, ring and halo, inside the picture.
        /// </summary>
        public static float ShapeShare(MarkShape shape, float x, float y)
        {
            switch (shape)
            {
                case MarkShape.Diamond: return System.Math.Abs(x) + System.Math.Abs(y);
                case MarkShape.Square: return System.Math.Max(System.Math.Abs(x), System.Math.Abs(y)) * 1.12f;
                case MarkShape.Triangle:
                    // Its three sides, the bottom and the two rising to the point at the top.
                    var lean = 1.7320508f * System.Math.Abs(x);
                    return System.Math.Max(-2f * y, y + lean);
                default: return (float)System.Math.Sqrt(x * x + y * y);
            }
        }

        /// <summary>How bright the player's other pins stand while a kind is shown, of what they were.</summary>
        public const float OthersDimmed = 1f / 3f;

        /// <summary>How many metres of ground a dot spans, at least, for marks to stand plain where the settings ask: some 60, a few dots to a place, far out.</summary>
        public const float PlainFrom = 60f;

        /// <summary>How big a plain mark stands of the full one: its middle, the kind's shape and edge, without the ring.</summary>
        public const float PlainShare = 0.6f;

        /// <summary>How bright the kinds not shown alone stand while one is (<see cref="MapLook"/>).</summary>
        public const float SoloOthers = 0.2f;

        /// <summary>
        /// How much of the dark veil covers the map's picture while a kind is shown, under every
        /// mark: the map then looks half as bright. The game blends in linear colour, where a veil
        /// of a half leaves about three quarters of what one sees; this much leaves a half.
        /// </summary>
        public const float MapDarkened = 0.78f;

        /// <summary>A pin layer's see-through while a kind is shown, from what it was.</summary>
        public static float Dimmed(float before, float share = OthersDimmed) => before * share;

        /// <summary>
        /// How bright Scry's own marks on a dimmed layer stand: as the layer was before, with every
        /// group above it that fades the map, from the layer up, as Unity reckons a group's
        /// see-through, ending at one that ignores those above it.
        /// </summary>
        public static float AsShown(IEnumerable<(float Alpha, bool IgnoresParents)> fromLayerUp)
        {
            var alpha = 1f;
            foreach (var (each, ignores) in fromLayerUp)
            {
                alpha *= each;
                if (ignores) break;
            }
            return alpha;
        }
    }

    /// <summary>The parts of a dot on the map, from its middle out (<see cref="SeenOnMap.DotAt"/>).</summary>
    internal enum DotPart
    {
        Clear,
        Disc,
        Edge,
        Halo,
        Ring,
    }

    /// <summary>How a thing is gathered, as far as finding it again goes: picked, mined (a deposit or its broken form), tapped (a root a tap built on it draws from), or none of these.</summary>
    internal enum Gathered
    {
        None,
        Picked,
        Mined,
        Tapped,
    }
}
