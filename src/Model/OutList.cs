using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>Where something Scry put in the world is: the order is the list's, what stays longest first.</summary>
    public enum OutPlace { Shown, Pinned, Sound, Status, Playing }

    /// <summary>One line of what is out in the world: its place, the prefab it is of, which of several, and how many.</summary>
    public struct OutRow
    {
        public OutPlace Place;
        public string Key;

        /// <summary>Which of the copies of this prefab in this place, counted from 0, for taking away just that one.</summary>
        public int Nth;

        public int Count;
    }

    /// <summary>
    /// The lines of what Scry has in the world, for taking things away one at a time. A pinned
    /// copy is a line of its own, so one of two can go; what plays for a while (an effect, a
    /// projectile, what a felled tree leaves) is one line per prefab with how many are out.
    /// </summary>
    public static class OutList
    {
        public static List<OutRow> Rows(IEnumerable<(OutPlace Place, string Key)> things)
        {
            var rows = new List<OutRow>();
            var seen = new Dictionary<(OutPlace, string), int>();
            foreach (var (place, key) in things)
            {
                seen.TryGetValue((place, key), out var before);
                seen[(place, key)] = before + 1;
                if (place == OutPlace.Playing && before > 0)
                {
                    var at = rows.FindIndex(r => r.Place == place && r.Key == key);
                    var row = rows[at];
                    row.Count++;
                    rows[at] = row;
                    continue;
                }
                rows.Add(new OutRow { Place = place, Key = key, Nth = before, Count = 1 });
            }
            return rows.OrderBy(r => (int)r.Place).ToList();
        }
    }
}
