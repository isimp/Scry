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

        public void Add(string key, Unlock kind, string target)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(target)) return;
            if (!_opens.TryGetValue((key, kind), out var targets)) _opens[(key, kind)] = targets = new List<string>();
            if (!targets.Contains(target)) targets.Add(target);
            _keys.Add(key);
        }

        public IReadOnlyList<string> Of(string key, Unlock kind) =>
            _opens.TryGetValue((key, kind), out var targets) ? targets : (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Whether anything waits for the key.</summary>
        public bool Any(string key) => key != null && _keys.Contains(key);

        public void Clear()
        {
            _opens.Clear();
            _keys.Clear();
        }
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
