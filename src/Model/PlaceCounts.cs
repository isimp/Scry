using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// How many of each kind of place the world played holds, by the server's census, and how
    /// many lie in each ground, by its answers: a whole answer on a ground replaces its counts,
    /// one on the ground new since adds to them. Held for the session, the world's own.
    /// </summary>
    internal sealed class PlaceCounts
    {
        private Dictionary<string, int> _census;
        private readonly Dictionary<WorldSource, Dictionary<string, int>> _in = new Dictionary<WorldSource, Dictionary<string, int>>();

        /// <summary>Counts up each time anything here changes.</summary>
        public int Version { get; private set; }

        /// <summary>Whether the server's census came.</summary>
        public bool CensusKnown => _census != null;

        /// <summary>The kinds of place the world holds, none before the census came.</summary>
        public IEnumerable<string> HeldKinds => _census != null ? _census.Keys : (IEnumerable<string>)Array.Empty<string>();

        /// <summary>Takes the server's census in.</summary>
        public void Census(IReadOnlyDictionary<string, int> census)
        {
            _census = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in census) if (pair.Value > 0) _census[pair.Key] = pair.Value;
            Version++;
        }

        /// <summary>How many of a kind the world holds: none where the census names none of it; -1 before it came.</summary>
        public int Held(string kind) => _census == null ? -1 : kind != null && _census.TryGetValue(kind, out var n) ? n : 0;

        /// <summary>Takes in how many of each kind lie in a ground: a whole answer in place of the counts before, one on the ground new since on top of them.</summary>
        public void Took(WorldSource source, IEnumerable<(string Kind, int Count)> counted, bool whole)
        {
            if (whole || !_in.TryGetValue(source, out var counts)) _in[source] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (kind, count) in counted)
            {
                if (string.IsNullOrEmpty(kind) || count <= 0) continue;
                counts[kind] = counts.TryGetValue(kind, out var n) ? n + count : count;
            }
            Version++;
        }

        /// <summary>How many of a kind lie in a ground: none it was not told of; -1 before any answer on it.</summary>
        public int In(WorldSource source, string kind) =>
            !_in.TryGetValue(source, out var counts) ? -1 : kind != null && counts.TryGetValue(kind, out var n) ? n : 0;

        /// <summary>On leaving a world: nothing is known of the next.</summary>
        public void Forget()
        {
            _census = null;
            _in.Clear();
            Version++;
        }
    }

    /// <summary>The entries of the places the world holds by the census, and the rooms of their dungeons, for the Locations tab to count against.</summary>
    internal static class PlaceCensus
    {
        /// <summary>The places of a kind the world holds, by its prefab's name, and every room found in what they go by (<see cref="Places.LabelOf"/>).</summary>
        public static HashSet<Entry> Held(IEnumerable<Entry> catalog, Func<string, bool> held)
        {
            var places = new HashSet<Entry>();
            var labels = new HashSet<string>(StringComparer.Ordinal);
            var rooms = new List<Entry>();
            foreach (var entry in catalog)
            {
                if (entry.Kind != Kind.Location) continue;
                if (entry.Components != null && Array.IndexOf(entry.Components, "Room") >= 0)
                {
                    rooms.Add(entry);
                    continue;
                }
                if (!held(entry.Name)) continue;
                places.Add(entry);
                var label = Places.LabelOf(entry);
                if (label != null) labels.Add(label);
            }
            foreach (var room in rooms)
            {
                foreach (var label in room.FoundIn)
                {
                    if (!labels.Contains(label)) continue;
                    places.Add(room);
                    break;
                }
            }
            return places;
        }
    }

    /// <summary>What the census and the grounds' counts say on a place's page and the Locations tab.</summary>
    internal static class CensusWords
    {
        /// <summary>
        /// The line under a place's title: how many this world holds, how many lie in ground you
        /// explored and in shared maps, each left out where not known (-1), or none: "This world
        /// holds 14 · 3 lie in ground you explored · 1 in shared maps"; none where nothing is told.
        /// </summary>
        public static string Line(int held, int explored, int shared)
        {
            if (held == 0) return "This world holds none of these";
            var parts = new List<string>();
            if (held > 0) parts.Add("This world holds " + Numbers.Count(held));
            if (explored > 0) parts.Add(Numbers.Count(explored) + (explored == 1 ? " lies" : " lie") + " in ground you explored");
            if (shared > 0) parts.Add(Numbers.Count(shared) + " in shared maps");
            return parts.Count == 0 ? null : string.Join(" \u00b7 ", parts);
        }

        /// <summary>The Locations tab's tip with the census: how many kinds of place were found, of those the world holds, of all the game and its mods can place.</summary>
        public static string TabTip(int found, int held, int all) =>
            $"{Numbers.Count(found)} kinds of place found, of the {Numbers.Count(held)} this world holds; the game and its mods can place {Numbers.Count(all)}";
    }
}
