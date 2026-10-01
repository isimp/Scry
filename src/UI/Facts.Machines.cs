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
            if (turret != null)
            {
                var fires = new Row { Title = "Fires" };
                if (turret.m_allowedAmmo != null) foreach (var ammo in turret.m_allowedAmmo) if (ammo.m_ammo != null) fires.Items.Add(Chip(ammo.m_ammo.gameObject.name, ""));
                if (fires.Items.Count > 0) Rows.Add(fires);
                Add("Holds", $"{turret.m_maxAmmo} shots");
                Add("Shoots", MachineWords.Shoots(turret.m_targetEnemies, turret.m_targetPlayers, turret.m_targetTamed, turret.m_viewDistance));
                Add("Shoots every", Naming.Duration(turret.m_attackCooldown));
                // Given trophies, it shoots only their creatures (Turret.UseItem, UpdateTarget).
                var trophies = new Row { Title = $"Given these trophies, up to {turret.m_maxConfigTargets} at once, it shoots only their creatures" };
                if (turret.m_configTargets != null) foreach (var target in turret.m_configTargets) if (target.m_item != null) trophies.Items.Add(Chip(target.m_item.gameObject.name, ""));
                if (trophies.Items.Count > 0) Rows.Add(trophies);
            }

            var trap = prefab.GetComponent<Trap>();
            if (trap != null)
            {
                Add("Springs on", MachineWords.Springs(trap.m_triggeredByEnemies, trap.m_triggeredByPlayers));
                Add("Rearms after", Naming.Duration(trap.m_rearmCooldown));
                var hit = trap.m_AOE != null ? trap.m_AOE.GetComponentInChildren<Aoe>(true) : null;
                var damage = hit != null ? Damages(hit.m_damage) : "";
                if (damage.Length > 0) Add("Damage", damage);
                if (trap.m_forceStagger) Add("Staggers", "whoever it hits");
            }

            var ship = prefab.GetComponent<Ship>();
            if (ship != null)
            {
                Add("Ashlands seas", MachineWords.Ashlands(ship.m_ashlandsReady));
                Add("Capsized", MachineWords.Capsized(ship.m_upsideDownDmg, ship.m_upsideDownDmgInterval));
            }

            var cart = prefab.GetComponent<Vagon>();
            if (cart != null) Add("Weighs", MachineWords.CartWeight(cart.m_baseMass, cart.m_itemWeightMassFactor));

            var catapult = prefab.GetComponent<Catapult>();
            if (catapult != null)
            {
                var types = (catapult.m_includeExcludeTypesList ?? new System.Collections.Generic.List<ItemDrop.ItemData.ItemType>())
                    .Select(t => Groups.ItemTypeName(t.ToString()).ToLowerInvariant()).Distinct().ToArray();
                Add("Loads", MachineWords.Loads(catapult.m_defaultIncludeAndListExclude, types, catapult.m_onlyUseIncludedProjectiles));
                if (catapult.m_maxLoadStack > 1) Add("At a time", $"up to {catapult.m_maxLoadStack}");
                var always = new Row { Title = "Loads these whatever their type" };
                if (catapult.m_includeItemsOverride != null) foreach (var item in catapult.m_includeItemsOverride) if (item != null) always.Items.Add(Chip(item.gameObject.name, ""));
                if (always.Items.Count > 0) Rows.Add(always);
                var never = new Row { Title = "Never loads these" };
                if (catapult.m_excludeItemsOverride != null) foreach (var item in catapult.m_excludeItemsOverride) if (item != null) never.Items.Add(Chip(item.gameObject.name, ""));
                if (never.Items.Count > 0) Rows.Add(never);
                if (catapult.m_launchCollectArea != null) Add("Also throws", "whoever stands where it is loaded");
            }
        }
    }
}
