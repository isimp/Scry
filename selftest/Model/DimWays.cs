using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// For the self-test: the ways of dimming (<see cref="DimWay"/>) compared, each place with the
    /// share of their brightness its dimmed rooms keep in each way (<see cref="DimLook"/>), and the
    /// ways dark enough in every place.
    /// </summary>
    internal static class DimWays
    {
        /// <summary>The ways in the order they are told.</summary>
        public static readonly DimWay[] All = { DimWay.Tint, DimWay.Colours, DimWay.Light, DimWay.Plain };

        /// <summary>What a way is called in the self-test's notes.</summary>
        public static string Name(DimWay way)
        {
            switch (way)
            {
                case DimWay.Tint: return "its tint";
                case DimWay.Colours: return "every colour";
                case DimWay.Light: return "light at 30%";
                default: return "one plain dark material";
            }
        }

        /// <summary>A place with the share of brightness its rooms keep dimmed each way, or that the way was not read.</summary>
        public static string Line(string place, IReadOnlyDictionary<DimWay, float?> kept) =>
            place + ": " + string.Join(", ", All.Select(way => Name(way) + " " + (kept.TryGetValue(way, out var share) && share is float s ? Numbers.Percent(s) : "not read")));

        /// <summary>The ways dark enough (<see cref="DimLook.DarkEnough"/>) in every place read; none with no place.</summary>
        public static List<DimWay> DarkEverywhere(IEnumerable<IReadOnlyDictionary<DimWay, float?>> places)
        {
            var read = places.ToList();
            if (read.Count == 0) return new List<DimWay>();
            return All.Where(way => read.All(kept => kept.TryGetValue(way, out var share) && DimLook.DarkEnough(share))).ToList();
        }

        /// <summary>The ways dark enough in every place, told.</summary>
        public static string Summary(IEnumerable<IReadOnlyDictionary<DimWay, float?>> places)
        {
            var dark = DarkEverywhere(places);
            return dark.Count == 0 ? "no way of dimming is dark enough in every place" : "dark enough in every place: " + string.Join(", ", dark.Select(Name));
        }
    }
}
