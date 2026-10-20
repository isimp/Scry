using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for crafting stations' upgrades.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Each crafting station of the catalog links every upgrade the game counts for it: each
        /// piece of the game and its mods whose station goes by the station's name
        /// (<c>StationExtension.FindExtensions</c> compares the names), whichever prefab of the
        /// station it names. Any it misses is named; the workbench's upgrades are told.
        /// </summary>
        private static IEnumerator StationUpgradesLinked(Probe p)
        {
            var scene = ZNetScene.instance;
            if (scene == null)
            {
                p.Skip("no world is loaded");
                yield break;
            }
            var stations = scene.m_prefabs.Where(o => o != null && o.GetComponent<CraftingStation>() != null).ToList();
            var upgrades = scene.m_prefabs.Where(o => o != null && o.GetComponent<StationExtension>().OrNull()?.m_craftingStation != null).ToList();
            var counted = 0;
            var missed = new List<string>();
            foreach (var station in stations)
            {
                var entry = X.Find(station.name);
                if (entry == null) continue;
                var name = station.GetComponent<CraftingStation>().m_name;
                var linked = new HashSet<string>(entry.Links.Where(l => l.Group == LinkWords.Upgrades).Select(l => l.Target), StringComparer.Ordinal);
                foreach (var upgrade in upgrades)
                {
                    if (upgrade.GetComponent<StationExtension>().m_craftingStation.m_name != name || X.Find(upgrade.name) == null) continue;
                    counted++;
                    if (!linked.Contains(upgrade.name)) missed.Add($"{upgrade.name} of {station.name}");
                }
            }
            var workbench = X.Find("piece_workbench");
            if (workbench != null) p.Note("the workbench's upgrades: " + string.Join(", ", workbench.Links.Where(l => l.Group == LinkWords.Upgrades).Select(l => l.Target)));
            p.Check(counted > 0 && missed.Count == 0, "each crafting station links every upgrade the game counts for it, whichever prefab of the station the upgrade names",
                $"{Numbers.Count(counted)} counted; missed: {string.Join(", ", missed.Take(12))}");
        }
    }
}
