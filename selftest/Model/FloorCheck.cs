using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// For the self-test: whether what a place's record tells of a roll is what that roll's own
    /// rays found (<c>FloorProbe.Checked</c>): each surface in the same cell, as open, at about
    /// the same height, and each spawn point's creature on about the same ground. A ray's hit is
    /// off by a millimetre or so where a dungeon's rooms stand, high over the world, and the copy
    /// stands elsewhere each time, so heights within <see cref="SameHeight"/> are the same. A ray
    /// steps two centimetres past each surface it meets, so a surface less than that under another
    /// part goes unmet; a record keeps what the rolls it was probed on met, with other parts about
    /// it, so a surface found by one of them only, within <see cref="Hidden"/> under something of
    /// another part the other met (or, given, another copy's collider: -1), is the same floor.
    /// </summary>
    internal sealed class FloorCheck
    {
        public const float SameHeight = 0.01f;

        /// <summary>How far under something else a ray met a surface can go unmet: the ray's step past a surface, with the rays' error.</summary>
        public const float Hidden = 0.02f + SameHeight;

        /// <summary>What only the record told, and only the rays found, each to the millimetre.</summary>
        public readonly List<string> OnlyKept = new List<string>();
        public readonly List<string> OnlyRays = new List<string>();

        /// <summary>The parts whose surfaces differ, by name, each once.</summary>
        public readonly List<string> Parts = new List<string>();

        private string _place = "";

        public bool Same => OnlyKept.Count == 0 && OnlyRays.Count == 0;

        /// <summary>How many cells may differ for the difference to be minor.</summary>
        public const int MinorCells = 2;

        /// <summary>
        /// Whether what differs is a cell or two and no creature's ground: a ray meets a curved
        /// part's mesh, or misses it, by its last digit at a seam, while a part found in one and
        /// not the other differs across many cells.
        /// </summary>
        public bool Minor => !Same && _grounds == 0 && _cells.Count <= MinorCells;

        private readonly HashSet<(int I, int J)> _cells = new HashSet<(int, int)>();
        private int _grounds;

        /// <summary>How many surfaces the first differing cell's fresh ray met, -1 where not told: a ray stops after its most.</summary>
        private int _metAtFirst = -1;

        /// <summary>What differed, in a line naming the place: how much of each, and the first few.</summary>
        public string Told => _place + ": only kept " + Stored.Count(OnlyKept.Count) + " (" + string.Join("; ", OnlyKept.Take(3)) + "), only by rays " + Stored.Count(OnlyRays.Count) + " (" + string.Join("; ", OnlyRays.Take(3)) + ")"
                              + (Parts.Count > 0 ? "; parts " + string.Join(", ", Parts.Take(4)) : "")
                              + (_metAtFirst >= 0 ? "; its ray met " + Stored.Count(_metAtFirst) : "");

        /// <summary>
        /// Checks the roll of <paramref name="present"/> parts, with its spawn points' <paramref name="keys"/>,
        /// as <paramref name="kept"/> tells it and as its <paramref name="rays"/> found it; <paramref name="met"/>,
        /// when given, is everything each fresh ray met by its place in the grid's list, a part of the
        /// copy or another copy's collider (-1).
        /// </summary>
        public static FloorCheck Of(FloorRecord kept, FloorRecord rays, ISet<int> present, IList<string> keys, IReadOnlyDictionary<int, List<(float Height, int Part)>> met = null)
        {
            var check = new FloorCheck { _place = kept.Prefab };
            var rayOf = new Dictionary<(int, int), int>();
            for (var ray = 0; ray < kept.Grid.Count; ray++) rayOf[(kept.Grid[ray].I, kept.Grid[ray].J)] = ray;
            string Told(FloorLanding l)
            {
                var part = l.Part >= 0 && l.Part < kept.Parts.Count ? kept.Parts[l.Part].Name : "?";
                if (!check.Parts.Contains(part)) check.Parts.Add(part);
                check._cells.Add((l.I, l.J));
                if (check._metAtFirst < 0 && met != null && rayOf.TryGetValue((l.I, l.J), out var at) && met.TryGetValue(at, out var hits)) check._metAtFirst = hits.Count;
                return Stored.Count(l.I) + "," + Stored.Count(l.J) + " at " + Stored.Number(l.Height, 3) + (l.Open ? " open" : "");
            }

            bool Under(FloorLanding l, IEnumerable<(float Height, int Part)> others) =>
                others.Any(o => o.Part != l.Part && o.Height - l.Height >= 0f && o.Height - l.Height <= Hidden);
            bool HiddenFromRays(FloorLanding l) => met != null && rayOf.TryGetValue((l.I, l.J), out var ray) && met.TryGetValue(ray, out var hits) && Under(l, hits);
            // The record keeps a part from the roll it was first seen in, where another part may
            // have hidden it, whether or not that one is there now.
            bool HiddenFromKept(FloorLanding l) => met != null && rayOf.TryGetValue((l.I, l.J), out var ray) && Under(l, kept.On(ray).Select(s => (s.Height, s.Part)));

            var unmatched = rays.Landings(present);
            foreach (var landing in kept.Landings(present))
            {
                var match = unmatched.FindIndex(l => l.I == landing.I && l.J == landing.J && l.Open == landing.Open && Math.Abs(l.Height - landing.Height) <= SameHeight);
                if (match >= 0) unmatched.RemoveAt(match);
                else if (!HiddenFromRays(landing)) check.OnlyKept.Add(Told(landing));
            }
            check.OnlyRays.AddRange(unmatched.Where(l => !HiddenFromKept(l)).Select(Told));

            string Ground(string key, bool on, float height) => "spawn " + key + (on ? " on " + Stored.Number(height, 3) : " on nothing");
            foreach (var key in keys)
            {
                var was = kept.GroundUnder(key, present, out var wasGround);
                var now = rays.GroundUnder(key, present, out var nowGround);
                if (was == now && (!was || Math.Abs(wasGround - nowGround) <= SameHeight)) continue;
                check.OnlyKept.Add(Ground(key, was, wasGround));
                check.OnlyRays.Add(Ground(key, now, nowGround));
                check._grounds++;
            }
            return check;
        }
    }
}
