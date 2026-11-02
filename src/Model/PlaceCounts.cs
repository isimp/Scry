using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// How many of each kind of place the world played holds, by the server's census, and how
    /// many lie in each ground, by its answers: a whole answer on a ground replaces its counts,
    /// one on the ground new since adds to them; and the squares those places stand in
    /// (<see cref="PlaceSquares"/>), kept the same way, with what the server said of them. Held
    /// for the session, the world's own.
    /// </summary>
    internal sealed class PlaceCounts
    {
        private Dictionary<string, int> _census;
        private readonly Dictionary<WorldSource, Dictionary<string, int>> _in = new Dictionary<WorldSource, Dictionary<string, int>>();
        private readonly Dictionary<WorldSource, Dictionary<string, Dictionary<(int X, int Z), int>>> _squares = new Dictionary<WorldSource, Dictionary<string, Dictionary<(int X, int Z), int>>>();
        private readonly Dictionary<WorldSource, SquaresHeard> _heard = new Dictionary<WorldSource, SquaresHeard>();

        private static readonly IReadOnlyDictionary<(int X, int Z), int> NoSquares = new Dictionary<(int X, int Z), int>();

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

        /// <summary>Takes in the squares a ground's answer told: a whole answer in place of those before, one on the ground new since on top of them.</summary>
        public void TookSquares(WorldSource source, IEnumerable<PlaceSquare> squares, bool whole)
        {
            if (whole || !_squares.TryGetValue(source, out var kinds)) _squares[source] = kinds = new Dictionary<string, Dictionary<(int X, int Z), int>>(StringComparer.Ordinal);
            foreach (var square in squares)
            {
                if (string.IsNullOrEmpty(square.Kind) || square.Count <= 0) continue;
                if (!kinds.TryGetValue(square.Kind, out var of)) kinds[square.Kind] = of = new Dictionary<(int X, int Z), int>();
                var key = (square.X, square.Z);
                of[key] = of.TryGetValue(key, out var n) ? n + square.Count : square.Count;
            }
            _heard[source] = SquaresHeard.Told;
            Version++;
        }

        /// <summary>No answer came on a ground (no Scry heard on the server, or an answer given up on): squares told before stay, else none are said to come.</summary>
        public void NoAnswer(WorldSource source)
        {
            if (_heard.TryGetValue(source, out var heard) && heard == SquaresHeard.Told) return;
            _heard[source] = SquaresHeard.NoScry;
            Version++;
        }

        /// <summary>The server told no squares for a ground, and why.</summary>
        public void SquaresNot(WorldSource source, SquaresHeard why)
        {
            _squares.Remove(source);
            _heard[source] = why;
            Version++;
        }

        /// <summary>
        /// The squares holding a kind in the grounds shown, with how many each holds: a square told
        /// for both grounds holds the more of the two, as a place in your own ground and on a shared
        /// map is the same place; none before an answer.
        /// </summary>
        public IReadOnlyDictionary<(int X, int Z), int> Squares(string kind, Func<WorldSource, bool> shown)
        {
            Dictionary<(int X, int Z), int> squares = null;
            foreach (var pair in _squares)
            {
                if (kind == null || !shown(pair.Key) || !pair.Value.TryGetValue(kind, out var of)) continue;
                if (squares == null) squares = new Dictionary<(int X, int Z), int>();
                foreach (var square in of) squares[square.Key] = squares.TryGetValue(square.Key, out var n) ? Math.Max(n, square.Value) : square.Value;
            }
            return squares ?? NoSquares;
        }

        /// <summary>What the server said of squares for the grounds shown: told where any told them, else why not, nothing yet before an answer.</summary>
        public SquaresHeard Heard(Func<WorldSource, bool> shown)
        {
            var heard = SquaresHeard.Waiting;
            foreach (var pair in _heard)
            {
                if (!shown(pair.Key)) continue;
                if (pair.Value == SquaresHeard.Told) return SquaresHeard.Told;
                if (pair.Value > heard) heard = pair.Value;
            }
            return heard;
        }

        /// <summary>On leaving a world: nothing is known of the next.</summary>
        public void Forget()
        {
            _census = null;
            _in.Clear();
            _squares.Clear();
            _heard.Clear();
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
        public static string TabTip(int found, int held, int all)
        {
            if (TabTips.TryGet((found, held, all), out var tip)) return tip;
            tip = $"{Numbers.Count(found)} kinds of place found, of the {Numbers.Count(held)} this world holds; the game and its mods can place {Numbers.Count(all)}";
            TabTips.Set((found, held, all), tip);
            return tip;
        }

        /// <summary>The tips by their counts, kept: the tabs are laid out every frame.</summary>
        private static readonly RecentCache<(int, int, int), string> TabTips = new RecentCache<(int, int, int), string>(64);
    }
}
