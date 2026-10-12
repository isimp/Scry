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
    /// part, or under another room's collider beside the copy, goes unmet; a record keeps what the
    /// copy it was probed on met, with other parts and rooms about it, so a surface found by one
    /// of them only, within <see cref="Hidden"/> under something else the other met, is the same floor.
    /// </summary>
    internal sealed class FloorCheck
    {
        public const float SameHeight = 0.01f;

        /// <summary>How far under something else a ray met a surface can go unmet: the ray's step past a surface, with the rays' error.</summary>
        public const float Hidden = 0.02f + SameHeight;

        /// <summary>What only the record told, and only the rays found, each to the millimetre.</summary>
        public readonly List<string> OnlyKept = new List<string>();
        public readonly List<string> OnlyRays = new List<string>();

        private string _place = "";

        public bool Same => OnlyKept.Count == 0 && OnlyRays.Count == 0;

        /// <summary>What differed, in a line naming the place: how much of each, and the first few.</summary>
        public string Told => _place + ": only kept " + Stored.Count(OnlyKept.Count) + " (" + string.Join("; ", OnlyKept.Take(3)) + "), only by rays " + Stored.Count(OnlyRays.Count) + " (" + string.Join("; ", OnlyRays.Take(3)) + ")";

        /// <summary>
        /// Checks the roll of <paramref name="present"/> parts, with its spawn points' <paramref name="keys"/>,
        /// as <paramref name="kept"/> tells it and as its <paramref name="rays"/> found it; <paramref name="met"/>,
        /// when given, is everything each fresh ray met by its place in the grid's list, a part of the
        /// copy or another copy's collider (-1).
        /// </summary>
        public static FloorCheck Of(FloorRecord kept, FloorRecord rays, ISet<int> present, IList<string> keys, IReadOnlyDictionary<int, List<(float Height, int Part)>> met = null)
        {
            var check = new FloorCheck { _place = kept.Prefab };
            string Told(FloorLanding l) => Stored.Count(l.I) + "," + Stored.Count(l.J) + " at " + Stored.Number(l.Height, 3) + (l.Open ? " open" : "");

            var rayOf = new Dictionary<(int, int), int>();
            for (var ray = 0; ray < kept.Grid.Count; ray++) rayOf[(kept.Grid[ray].I, kept.Grid[ray].J)] = ray;
            bool Under(FloorLanding l, IEnumerable<(float Height, int Part)> others) =>
                others.Any(o => o.Part != l.Part && o.Height - l.Height >= 0f && o.Height - l.Height <= Hidden);
            bool HiddenFromRays(FloorLanding l) => met != null && rayOf.TryGetValue((l.I, l.J), out var ray) && met.TryGetValue(ray, out var hits) && Under(l, hits);
            bool HiddenFromKept(FloorLanding l) => met != null && rayOf.TryGetValue((l.I, l.J), out var ray) && Under(l, kept.On(ray, present).Select(s => (s.Height, s.Part)));

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
            }
            return check;
        }
    }
}
