using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>What an item is used for, in the order the panel lists the kinds.</summary>
    public enum UseKind
    {
        /// <summary>An ingredient of a recipe; the place is the crafting station, or "hand".</summary>
        Crafts,

        /// <summary>
        /// An upgrade kit a recipe names, which the game asks for only at an upgrade station, to
        /// take what the recipe makes past its top quality; no place.
        /// </summary>
        UpgradesPastTop,

        /// <summary>Part of the cost of a piece; the place is the station it is built near, if any.</summary>
        Builds,

        /// <summary>Turned into another item by a station (a smelter, a kiln, a fermenter); the place is the station.</summary>
        TurnsInto,

        /// <summary>Burnt by a station or a fire.</summary>
        Fuels,

        /// <summary>Eaten by a creature.</summary>
        EatenBy,
    }

    /// <summary>One use of a kind, at one place: what it goes into, and how many of the item each takes.</summary>
    public sealed class UseGroup
    {
        public UseKind Kind;

        /// <summary>Where it happens, a prefab name; null where it happens nowhere in particular.</summary>
        public string Place;

        public readonly List<(string Target, int Amount)> Targets = new List<(string, int)>();
    }

    /// <summary>
    /// What each item is used for: the recipes it goes into, the pieces it builds, what stations
    /// turn it into, what burns it and what eats it. Noted while the catalog is read, each use
    /// once, and told back per item grouped by kind and place, always in the same order.
    /// </summary>
    public sealed class UseBook
    {
        private readonly Dictionary<string, List<UseGroup>> _uses = new Dictionary<string, List<UseGroup>>(StringComparer.Ordinal);
        private readonly HashSet<(string, UseKind, string, string)> _noted = new HashSet<(string, UseKind, string, string)>();

        /// <summary>Notes that an item is used, as a kind of use, for a target, taking an amount of it, at a place.</summary>
        public void Add(string item, UseKind kind, string target, int amount, string place = null)
        {
            if (string.IsNullOrEmpty(item) || string.IsNullOrEmpty(target) || item == target) return;
            if (!_noted.Add((item, kind, target, place))) return;

            if (!_uses.TryGetValue(item, out var groups)) _uses[item] = groups = new List<UseGroup>();
            var group = groups.Find(g => g.Kind == kind && g.Place == place);
            if (group == null)
            {
                group = new UseGroup { Kind = kind, Place = place };
                groups.Add(group);
            }
            group.Targets.Add((target, amount));
        }

        /// <summary>An item's uses, by kind in the order of <see cref="UseKind"/>, then by place in the order first noted.</summary>
        public IReadOnlyList<UseGroup> Of(string item)
        {
            if (item == null || !_uses.TryGetValue(item, out var groups)) return Array.Empty<UseGroup>();
            var ordered = new List<UseGroup>(groups);
            var firstSeen = new Dictionary<UseGroup, int>();
            for (var i = 0; i < groups.Count; i++) firstSeen[groups[i]] = i;
            ordered.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : firstSeen[a].CompareTo(firstSeen[b]));
            return ordered;
        }

        /// <summary>How many things an item is used for, all kinds together.</summary>
        public int CountOf(string item)
        {
            if (item == null || !_uses.TryGetValue(item, out var groups)) return 0;
            var count = 0;
            foreach (var group in groups) count += group.Targets.Count;
            return count;
        }

        public void Clear()
        {
            _uses.Clear();
            _noted.Clear();
        }
    }
}
