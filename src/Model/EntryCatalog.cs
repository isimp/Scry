using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A world's catalog: its entries in the order they were read, each found by its key
    /// (<see cref="EntryKeys"/>) at once, the first read where several share one. What reads the
    /// world and what shows it find entries this way rather than going through them all, and
    /// what lists them hears when their groups change (<see cref="Regrouped"/>), as what the
    /// locations hold moves entries to other groups once read.
    /// </summary>
    internal sealed class EntryCatalog
    {
        private readonly Dictionary<string, Entry> _byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);

        public EntryCatalog(IReadOnlyList<Entry> entries)
        {
            All = entries;
            foreach (var entry in entries) if (!_byKey.ContainsKey(entry.Key)) _byKey[entry.Key] = entry;
        }

        /// <summary>Every entry, in the order read.</summary>
        public IReadOnlyList<Entry> All { get; }

        /// <summary>The entry kept under a key, or null for none.</summary>
        public Entry Find(string key) => key != null && _byKey.TryGetValue(key, out var entry) ? entry : null;

        /// <summary>Told after entries changed their groups.</summary>
        public event Action Regrouped;

        /// <summary>Says the entries' groups have changed, so what lists them lists them again.</summary>
        public void Regroup() => Regrouped?.Invoke();
    }
}
