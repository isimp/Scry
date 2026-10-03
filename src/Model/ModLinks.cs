using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What a loaded mod declares of other mods, and the package it was installed from.</summary>
    internal sealed class ModFacts
    {
        public string Name = "", Guid = "", Package = "";

        /// <summary>The ids of the mods it needs, of those it works with when there, and of those it will not run with.</summary>
        public List<string> Hard = new List<string>(), Soft = new List<string>(), Incompatible = new List<string>();

        /// <summary>The packages its package depends on, each with its version ("Author-Name-1.2.3").</summary>
        public List<string> PackageDeps = new List<string>();
    }

    /// <summary>A mod's ties to the other mods loaded, each by its name, in the order of their names.</summary>
    internal sealed class ModRelations
    {
        public readonly List<string> Needs = new List<string>(), NeededBy = new List<string>();
        public readonly List<string> WorksWith = new List<string>(), WorkedWithBy = new List<string>();

        /// <summary>The mods it will not run with: by name when loaded, else by id.</summary>
        public readonly List<string> WillNotRunWith = new List<string>();
    }

    /// <summary>
    /// How the mods loaded tie together, from what each declares (BepInEx's dependencies and
    /// incompatibilities, by id) and from its package's dependencies (by package, which brings
    /// every mod that package holds). A mod needed is not also one it works with, and no mod
    /// needs itself or a mod of its own package. Ids and packages of no mod loaded (BepInEx
    /// itself, a library with no plugin) are left out, but a mod it will not run with is told
    /// by its id.
    /// </summary>
    internal static class ModLinks
    {
        public static Dictionary<string, ModRelations> Of(IEnumerable<ModFacts> mods)
        {
            var all = mods.Where(m => m != null && !string.IsNullOrEmpty(m.Name)).ToList();
            var byGuid = new Dictionary<string, ModFacts>(StringComparer.Ordinal);
            foreach (var mod in all) if (!string.IsNullOrEmpty(mod.Guid) && !byGuid.ContainsKey(mod.Guid)) byGuid[mod.Guid] = mod;

            var needs = new Dictionary<string, HashSet<string>>();
            var works = new Dictionary<string, HashSet<string>>();
            foreach (var mod in all)
            {
                var need = new HashSet<string>(StringComparer.Ordinal);
                foreach (var guid in mod.Hard) if (guid != null && byGuid.TryGetValue(guid, out var other)) need.Add(other.Name);
                foreach (var dependency in mod.PackageDeps)
                {
                    var package = ModManifest.Package(dependency);
                    if (package.Length == 0 || string.Equals(package, mod.Package, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (var other in all) if (string.Equals(other.Package, package, StringComparison.OrdinalIgnoreCase)) need.Add(other.Name);
                }
                need.Remove(mod.Name);
                needs[mod.Name] = need;

                var work = new HashSet<string>(StringComparer.Ordinal);
                foreach (var guid in mod.Soft) if (guid != null && byGuid.TryGetValue(guid, out var other) && !need.Contains(other.Name)) work.Add(other.Name);
                work.Remove(mod.Name);
                works[mod.Name] = work;
            }

            var relations = new Dictionary<string, ModRelations>(StringComparer.Ordinal);
            foreach (var mod in all) relations[mod.Name] = new ModRelations();
            foreach (var mod in all)
            {
                var own = relations[mod.Name];
                own.Needs.AddRange(Ordered(needs[mod.Name]));
                own.WorksWith.AddRange(Ordered(works[mod.Name]));
                own.NeededBy.AddRange(Ordered(all.Where(m => needs[m.Name].Contains(mod.Name)).Select(m => m.Name)));
                own.WorkedWithBy.AddRange(Ordered(all.Where(m => works[m.Name].Contains(mod.Name)).Select(m => m.Name)));
                own.WillNotRunWith.AddRange(Ordered(mod.Incompatible.Where(g => !string.IsNullOrEmpty(g)).Select(g => byGuid.TryGetValue(g, out var other) ? other.Name : g)));
            }
            return relations;
        }

        private static IEnumerable<string> Ordered(IEnumerable<string> names) => names.Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase);
    }
}
