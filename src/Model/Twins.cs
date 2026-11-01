using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Variants of a thing that go by one name, its variant's number aside (<see cref="Naming.WithoutVariant"/>),
    /// with the same values, as a tree's health, tier, modifiers and drops: one thing to find out,
    /// so what was found out of one holds for each.
    /// </summary>
    internal static class Twins
    {
        private static readonly IReadOnlyList<string> None = Array.Empty<string>();

        /// <summary>Each thing's twins among those given, by key, its name and the signature of its values; none for one with none.</summary>
        public static Func<string, IReadOnlyList<string>> Of(IEnumerable<(string Key, string Name, long Signature)> things)
        {
            var twins = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var group in things.GroupBy(t => (Naming.WithoutVariant(t.Name), t.Signature)))
            {
                var keys = group.Select(t => t.Key).Distinct().ToList();
                if (keys.Count < 2) continue;
                foreach (var key in keys) twins[key] = keys.Where(k => k != key).ToList();
            }
            return key => key != null && twins.TryGetValue(key, out var found) ? found : None;
        }
    }
}
