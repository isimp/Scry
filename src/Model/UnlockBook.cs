using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>What waits for a world key, kind by kind.</summary>
    internal enum Unlock
    {
        /// <summary>A raid that may come once the key is set (<c>RandomEvent.m_requiredGlobalKeys</c>).</summary>
        RaidStarts,

        /// <summary>A raid that stops coming once it is set (<c>m_notRequiredGlobalKeys</c>).</summary>
        RaidEnds,

        /// <summary>A creature the world's spawning or a spawner then places (<c>m_requiredGlobalKey</c>).</summary>
        Spawns,

        /// <summary>A creature a spawner then stops placing (<c>CreatureSpawner.m_blockingGlobalKey</c>).</summary>
        StopsSpawning,

        /// <summary>An item traders then sell (<c>Trader.TradeItem.m_requiredGlobalKey</c>).</summary>
        Sells,
    }

    /// <summary>
    /// What each world key opens, kept as the catalog reads raids, spawn lists, spawners and
    /// traders, so a creature whose defeat sets a key (<c>Character.m_defeatSetGlobalKey</c>) can
    /// tell what follows it. Each thing is kept once, in the order read.
    /// </summary>
    internal sealed class UnlockBook
    {
        private readonly Dictionary<(string Key, Unlock Kind), List<string>> _opens = new Dictionary<(string, Unlock), List<string>>();
        private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The traders selling each ware once a key is set, by the key and the ware.</summary>
        private readonly Dictionary<(string Key, string Ware), List<string>> _sellers = new Dictionary<(string, string), List<string>>();

        /// <summary>Keeps a thing waiting for a key; a ware with the trader selling it so (<paramref name="seller"/>).</summary>
        public void Add(string key, Unlock kind, string target, string seller = null)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(target)) return;
            if (!_opens.TryGetValue((key, kind), out var targets)) _opens[(key, kind)] = targets = new List<string>();
            if (!targets.Contains(target)) targets.Add(target);
            _keys.Add(key);
            if (kind != Unlock.Sells || string.IsNullOrEmpty(seller)) return;
            if (!_sellers.TryGetValue((key, target), out var sellers)) _sellers[(key, target)] = sellers = new List<string>();
            if (!sellers.Contains(seller)) sellers.Add(seller);
        }

        public IReadOnlyList<string> Of(string key, Unlock kind) =>
            _opens.TryGetValue((key, kind), out var targets) ? targets : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Whether anything waits for the key.</summary>
        public bool Any(string key) => key != null && _keys.Contains(key);

        /// <summary>
        /// Whether the character saw a key's setting go together with what waits for it: a ware
        /// seen on the store of a trader selling it once the key is set (<paramref name="paired"/>,
        /// trader and ware), as a store lists it only then (<c>Trader.GetAvailableItems</c>), or a
        /// raid lived through (<paramref name="livedThrough"/>), as the game starts one only while
        /// every key it waits for is set (<c>RandEventSystem.HaveGlobalKeys</c>). Nothing else that
        /// waits for a key shows it was set (<see cref="UnlockWays.SeenTogether"/>).
        /// </summary>
        public bool SeenOpened(string key, string target, Func<string, string, bool> paired, Func<string, bool> livedThrough)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(target)) return false;
            if (_sellers.TryGetValue((key, target), out var sellers))
            {
                foreach (var seller in sellers) if (paired(seller, target)) return true;
            }
            return _opens.TryGetValue((key, Unlock.RaidStarts), out var raids) && raids.Contains(target) && livedThrough(target);
        }

        public void Clear()
        {
            _opens.Clear();
            _keys.Clear();
            _sellers.Clear();
        }
    }

    /// <summary>Which of what waits for a key the character can see go together with the key's setting (<see cref="UnlockBook.SeenOpened"/>).</summary>
    internal static class UnlockWays
    {
        /// <summary>
        /// A ware, seen on its store, and a raid that may come, lived through; a raid that stops, a
        /// creature that then spawns or stops spawning shows nothing of the key, so under the strict
        /// rules its row is left out whole.
        /// </summary>
        public static bool SeenTogether(Unlock kind) => kind == Unlock.Sells || kind == Unlock.RaidStarts;
    }

    /// <summary>How a creature's page titles what its defeat opens.</summary>
    internal static class UnlockWords
    {
        public static string Title(Unlock kind, int count) => Naming.Counted(Opens(kind), count);

        private static string Opens(Unlock kind)
        {
            switch (kind)
            {
                case Unlock.RaidStarts: return "After it falls, raids that may come";
                case Unlock.RaidEnds: return "After it falls, raids that stop";
                case Unlock.Spawns: return "After it falls, these spawn";
                case Unlock.StopsSpawning: return "After it falls, spawners stop placing these";
                default: return "After it falls, traders sell";
            }
        }
    }
}
