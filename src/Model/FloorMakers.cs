using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What each floor found stands on, for the self-test to tell: the parts whose colliders its
    /// rays landed on within half a metre of it, the two landed on most first, with their shares;
    /// a floor no ray landed near is the location's own ground (<see cref="PlaceView.WithGround"/>).
    /// </summary>
    public static class FloorMakers
    {
        public const float Near = 0.5f;
        public const int Most = 2;

        public static List<string> Tell(IReadOnlyList<float> heights, IReadOnlyList<string> names, IReadOnlyList<float> floors)
        {
            var told = new List<string>();
            foreach (var floor in floors)
            {
                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                var total = 0;
                for (var i = 0; i < heights.Count && i < names.Count; i++)
                {
                    if (Math.Abs(heights[i] - floor) > Near) continue;
                    counts.TryGetValue(names[i], out var n);
                    counts[names[i]] = n + 1;
                    total++;
                }
                if (total == 0)
                {
                    told.Add("the ground");
                    continue;
                }
                told.Add(string.Join(", ", counts.OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal).Take(Most)
                    .Select(c => c.Key + " " + ((int)Math.Round(c.Value * 100.0 / total, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + "%")));
            }
            return told;
        }
    }
}
