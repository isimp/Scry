using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>An item seen in a creature's loot: in how many kills, how many in all, and the fewest and most at a time.</summary>
    public sealed class SeenDrop
    {
        public string Item = "";
        public int Times, Total, Least, Most;
    }

    /// <summary>
    /// What creatures are seen to drop as you play, whichever mod put it there: each death whose
    /// loot came out on this machine is a kill of its creature, and each item in the loot is
    /// counted with the kills it came in and how many at a time. Kept from session to session as
    /// text (<see cref="Save"/>, <see cref="Load"/>). Items seen equally often are in the order of
    /// their names, so the order is the same after reading it back.
    /// </summary>
    public sealed class SeenDrops
    {
        private readonly Dictionary<string, int> _kills = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, Dictionary<string, SeenDrop>> _drops = new Dictionary<string, Dictionary<string, SeenDrop>>(StringComparer.Ordinal);

        /// <summary>Whether anything was recorded since it was last saved.</summary>
        public bool Changed { get; private set; }

        /// <summary>A kill of a creature and its loot, an item counted once for the kill however often it came.</summary>
        public void Record(string creature, IEnumerable<(string Item, int Amount)> loot)
        {
            if (string.IsNullOrEmpty(creature)) return;
            _kills.TryGetValue(creature, out var kills);
            _kills[creature] = kills + 1;
            Changed = true;

            var amounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (item, amount) in loot ?? Enumerable.Empty<(string, int)>())
            {
                if (string.IsNullOrEmpty(item) || amount <= 0) continue;
                amounts.TryGetValue(item, out var so);
                amounts[item] = so + amount;
            }
            if (amounts.Count == 0) return;
            if (!_drops.TryGetValue(creature, out var items)) _drops[creature] = items = new Dictionary<string, SeenDrop>(StringComparer.Ordinal);
            foreach (var pair in amounts)
            {
                if (!items.TryGetValue(pair.Key, out var drop))
                {
                    items[pair.Key] = drop = new SeenDrop { Item = pair.Key, Least = pair.Value, Most = pair.Value };
                }
                drop.Times++;
                drop.Total += pair.Value;
                drop.Least = Math.Min(drop.Least, pair.Value);
                drop.Most = Math.Max(drop.Most, pair.Value);
            }
        }

        public int Kills(string creature) => creature != null && _kills.TryGetValue(creature, out var kills) ? kills : 0;

        /// <summary>What a creature was seen to drop, the most often first.</summary>
        public IReadOnlyList<SeenDrop> Of(string creature)
        {
            if (creature == null || !_drops.TryGetValue(creature, out var items)) return new List<SeenDrop>();
            return items.Values.OrderByDescending(d => d.Times).ThenBy(d => d.Item, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>The creatures an item was seen dropped by, the most often first, each with its kills.</summary>
        public IReadOnlyList<(string Creature, SeenDrop Drop, int Kills)> Sources(string item)
        {
            var sources = new List<(string, SeenDrop, int)>();
            foreach (var pair in _drops)
            {
                if (pair.Value.TryGetValue(item, out var drop)) sources.Add((pair.Key, drop, Kills(pair.Key)));
            }
            return sources.OrderByDescending(s => s.Item2.Times).ThenBy(s => s.Item1, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Everything seen, a line a creature and a line for each item it dropped.</summary>
        public string Save()
        {
            var text = new StringBuilder();
            foreach (var creature in _kills.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                text.Append("kills\t").Append(creature).Append('\t').Append(Stored.Count(_kills[creature])).Append('\n');
                foreach (var drop in Of(creature))
                {
                    text.Append("drop\t").Append(creature).Append('\t').Append(drop.Item);
                    foreach (var n in new[] { drop.Times, drop.Total, drop.Least, drop.Most }) text.Append('\t').Append(Stored.Count(n));
                    text.Append('\n');
                }
            }
            Changed = false;
            return text.ToString();
        }

        /// <summary>What was saved; a line that cannot be read is passed over.</summary>
        public static SeenDrops Load(string text)
        {
            var seen = new SeenDrops();
            if (string.IsNullOrEmpty(text)) return seen;
            foreach (var line in text.Split('\n'))
            {
                var parts = line.TrimEnd('\r').Split('\t');
                if (parts[0] == "kills" && parts.Length == 3 && parts[1].Length > 0 && Int(parts[2], out var kills))
                {
                    seen._kills[parts[1]] = kills;
                }
                else if (parts[0] == "drop" && parts.Length == 7 && parts[1].Length > 0 && parts[2].Length > 0
                         && Int(parts[3], out var times) && Int(parts[4], out var total) && Int(parts[5], out var least) && Int(parts[6], out var most))
                {
                    if (!seen._drops.TryGetValue(parts[1], out var items)) seen._drops[parts[1]] = items = new Dictionary<string, SeenDrop>(StringComparer.Ordinal);
                    items[parts[2]] = new SeenDrop { Item = parts[2], Times = times, Total = total, Least = least, Most = most };
                }
            }
            return seen;
        }

        private static bool Int(string text, out int value) => Stored.TryCount(text, out value) && value >= 0;
    }

    /// <summary>How what was seen dropping is told.</summary>
    public static class SeenWords
    {
        /// <summary>The row of a creature's loot seen in play, with how many kills it is from.</summary>
        public static string Title(int kills) => $"Seen dropping in your play ({Kills(kills)})";

        /// <summary>How often an item came, and how many at a time when that is more than one.</summary>
        public static string Amount(SeenDrop drop, int kills)
        {
            var told = $"in {Numbers.Count(drop.Times)} of {Kills(kills)}";
            if (drop.Most <= 1) return told;
            return drop.Least == drop.Most ? $"{told}, {Numbers.Count(drop.Most)} each time" : $"{told}, {Numbers.Count(drop.Least)} to {Numbers.Count(drop.Most)} each time";
        }

        /// <summary>An item's chip in a creature's row, whose title tells the kills: how many at a time when more than one, and in how many kills.</summary>
        public static string Chip(SeenDrop drop, int kills)
        {
            var often = $"{Numbers.Count(drop.Times)} of {Numbers.Count(kills)}";
            if (drop.Most <= 1) return often;
            return drop.Least == drop.Most ? $"{Numbers.Count(drop.Most)}, {often}" : $"{Numbers.Count(drop.Least)} to {Numbers.Count(drop.Most)}, {often}";
        }

        /// <summary>An item's line among where it comes from.</summary>
        public static string Line(string creature, SeenDrop drop, int kills) => $"Seen dropped by {creature} in your play, {Amount(drop, kills)}";

        private static string Kills(int kills) => kills == 1 ? "1 kill" : $"{Numbers.Count(kills)} kills";
    }
}
