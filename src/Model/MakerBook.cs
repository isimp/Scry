using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// One way a station makes an item: from what (each with how many a batch takes), how many
    /// a batch makes, and whether any one of the inputs is enough or all of them are needed.
    /// </summary>
    public sealed class Making
    {
        /// <summary>The station's prefab.</summary>
        public string Station = "";

        /// <summary>The item made, by prefab.</summary>
        public string Output = "";

        public readonly List<(string Item, int Amount)> Inputs = new List<(string, int)>();

        public int Makes = 1;

        /// <summary>Any one of the inputs makes it; otherwise it takes all of them.</summary>
        public bool AnyOne;
    }

    /// <summary>
    /// What stations make from what (a smelter's ore, a fermenter's mead base, an obliterator's
    /// leftovers), noted as the catalog is read and told from both sides: for an item, where it
    /// is made; for a station, what it makes. A station that turns several items into the same
    /// one, each on its own, is one way to make it from any one of them.
    /// </summary>
    public sealed class MakerBook
    {
        // By what is made and by where, in the order noted: every entry of the catalog asks once.
        private readonly Dictionary<string, List<Making>> _byOutput = new Dictionary<string, List<Making>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<Making>> _byStation = new Dictionary<string, List<Making>>(StringComparer.Ordinal);

        public void Add(Making making)
        {
            if (making == null || string.IsNullOrEmpty(making.Station) || string.IsNullOrEmpty(making.Output) || making.Inputs.Count == 0) return;
            Note(_byOutput, making.Output, making);
            Note(_byStation, making.Station, making);
        }

        private static void Note(Dictionary<string, List<Making>> index, string key, Making making)
        {
            if (!index.TryGetValue(key, out var list)) index[key] = list = new List<Making>();
            list.Add(making);
        }

        private static readonly IReadOnlyList<Making> None = new Making[0];

        /// <summary>The ways an item is made, station by station in the order noted.</summary>
        public IReadOnlyList<Making> Of(string output) =>
            output != null && _byOutput.TryGetValue(output, out var list) ? Merged(list) : None;

        /// <summary>What a station makes, item by item in the order noted.</summary>
        public IReadOnlyList<Making> At(string station) =>
            station != null && _byStation.TryGetValue(station, out var list) ? Merged(list) : None;

        public void Clear()
        {
            _byOutput.Clear();
            _byStation.Clear();
        }

        private static List<Making> Merged(IEnumerable<Making> makings)
        {
            var merged = new List<Making>();
            foreach (var making in makings)
            {
                var single = making.Inputs.Count == 1 || making.AnyOne;
                var into = single
                    ? merged.Find(m => m.Station == making.Station && m.Output == making.Output && m.Makes == making.Makes && (m.Inputs.Count == 1 || m.AnyOne))
                    : null;
                if (into == null)
                {
                    var copy = new Making { Station = making.Station, Output = making.Output, Makes = making.Makes, AnyOne = making.AnyOne };
                    copy.Inputs.AddRange(making.Inputs);
                    if (!single && merged.Exists(m => Same(m, copy))) continue;
                    merged.Add(copy);
                    continue;
                }
                foreach (var input in making.Inputs)
                {
                    if (into.Inputs.Exists(i => i.Item == input.Item)) continue;
                    into.Inputs.Add(input);
                    into.AnyOne = true;
                }
            }
            return merged;
        }

        private static bool Same(Making a, Making b) =>
            a.Station == b.Station && a.Output == b.Output && a.Makes == b.Makes && a.Inputs.SequenceEqual(b.Inputs);

        /// <summary>An item's row title: "Made in Smelter", "Made in Fermenter, makes 6", "…, from any one of these".</summary>
        public static string ItemTitle(string station, Making making)
        {
            var title = "Made in " + station;
            if (making.Makes > 1) title += $", makes {Numbers.Count(making.Makes)}";
            if (making.AnyOne) title += ", from any one of these";
            return title;
        }

        /// <summary>A station's row title: "Makes Copper from", "Makes 6 Mead from", "Makes Copper from any one of these".</summary>
        public static string StationTitle(string output, Making making)
        {
            var count = making.Makes > 1 ? $"{Numbers.Count(making.Makes)} " : "";
            return $"Makes {count}{output} from" + (making.AnyOne ? " any one of these" : "");
        }
    }
}
