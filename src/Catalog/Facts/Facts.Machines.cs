using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What a ballista, trap, ship, cart or catapult does (<see cref="MachineWords"/>): what it
    /// fires and at whom, on whom it springs and how hard, how it fares at sea, what it weighs,
    /// and what it loads.
    /// </summary>
    internal sealed partial class Facts
    {
        private void Machines(GameObject prefab)
        {
            var turret = prefab.GetComponent<Turret>();
            var trap = prefab.GetComponent<Trap>();
            var ship = prefab.GetComponent<Ship>();
            var cart = prefab.GetComponent<Vagon>();
            var catapult = prefab.GetComponent<Catapult>();
            var hit = trap != null && trap.m_AOE != null ? trap.m_AOE.GetComponentInChildren<Aoe>(true) : null;
            AddAll(MachineWords.Pairs(new MachineFacts
            {
                Turret = turret == null ? null : new TurretFacts
                {
                    MaxAmmo = turret.m_maxAmmo, Enemies = turret.m_targetEnemies, Players = turret.m_targetPlayers, Tamed = turret.m_targetTamed,
                    Range = turret.m_viewDistance, Cooldown = turret.m_attackCooldown,
                },
                Trap = trap == null ? null : new TrapFacts
                {
                    Enemies = trap.m_triggeredByEnemies, Players = trap.m_triggeredByPlayers, Rearm = trap.m_rearmCooldown,
                    Damage = hit != null ? DamageFigures(hit.m_damage) : System.Array.Empty<(string, float)>(), Staggers = trap.m_forceStagger,
                },
                Ship = ship == null ? null : new ShipFacts { AshlandsReady = ship.m_ashlandsReady, CapsizedDamage = ship.m_upsideDownDmg, CapsizedEvery = ship.m_upsideDownDmgInterval },
                Cart = cart == null ? null : new CartFacts { Mass = cart.m_baseMass, LoadShare = cart.m_itemWeightMassFactor },
                Catapult = catapult == null ? null : new CatapultFacts
                {
                    ListExcludes = catapult.m_defaultIncludeAndListExclude,
                    Types = (catapult.m_includeExcludeTypesList ?? new System.Collections.Generic.List<ItemDrop.ItemData.ItemType>())
                        .Select(t => Groups.ItemTypeName(t.ToString()).ToLowerInvariant()).Distinct().ToArray(),
                    OnlyHeld = catapult.m_onlyUseIncludedProjectiles, MaxLoad = catapult.m_maxLoadStack, ThrowsWhoStandsThere = catapult.m_launchCollectArea != null,
                },
            }));

            // Rows of what they take, each item going to its page.
            if (turret != null)
            {
                PrefabChips("Fires", turret.m_allowedAmmo?.Where(a => a.m_ammo != null).Select(a => a.m_ammo.gameObject.name));
                PrefabChips(MachineWords.TrophiesTitle(turret.m_maxConfigTargets), turret.m_configTargets?.Where(t => t.m_item != null).Select(t => t.m_item.gameObject.name));
            }
            if (catapult != null)
            {
                PrefabChips("Loads these whatever their type", catapult.m_includeItemsOverride?.Where(i => i != null).Select(i => i.gameObject.name));
                PrefabChips("Never loads these", catapult.m_excludeItemsOverride?.Where(i => i != null).Select(i => i.gameObject.name));
            }
        }

        /// <summary>A row of chips of prefabs by name, each going to its page; none for none.</summary>
        private void PrefabChips(string title, System.Collections.Generic.IEnumerable<string> prefabs)
        {
            var row = new Row { Title = title };
            foreach (var name in prefabs ?? System.Linq.Enumerable.Empty<string>()) row.Items.Add(Chip(name, ""));
            if (row.Items.Count > 0) Rows.Add(row);
        }
    }
}
