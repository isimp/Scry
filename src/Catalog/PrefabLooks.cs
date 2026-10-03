using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The looks a prefab can be switched between, when the game switches them by script: a
    /// plant growing or grown, a fire unlit or lit, a portal unconnected, connected or open, a door
    /// or chest shut or open, a smelter, windmill, fermenter, sap collector or crafting station at
    /// work, something picked or not, a creature with or without its gear or saddle, an item in
    /// each of its styles. Only one kind is offered per prefab, in that order. Read off the prefab
    /// when it is first selected; the previews put a copy in a look (<c>Variants</c>).
    /// </summary>
    internal static class PrefabLooks
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static PrefabLooks() => WorldCaches.Register(nameof(PrefabLooks), Forget);

        internal enum Sort { None, Growth, Fire, Portal, Door, Chest, Windmill, Smelter, Fermenter, Sap, Station, Picked, Gear, Saddle, Style }

        internal sealed class Found
        {
            public Sort Sort;
            public string[] Names = System.Array.Empty<string>();
            public int Default;
        }

        private static readonly Dictionary<GameObject, Found> Known = new Dictionary<GameObject, Found>();

        /// <summary>The looks on offer, and the one shown first.</summary>
        public static string[] Of(GameObject prefab, out int defaultLook)
        {
            var found = Describe(prefab);
            defaultLook = found.Default;
            return found.Names;
        }

        /// <summary>Whether a prefab's looks are about the gear it wears.</summary>
        public static bool IsGear(GameObject prefab) => Describe(prefab).Sort == Sort.Gear;

        /// <summary>
        /// Forgets the looks worked out for each prefab, for a world that was left: they are keyed
        /// by prefab, and a world's prefabs can be gone or changed by the next one.
        /// </summary>
        public static void Forget() => Known.Clear();

        /// <summary>A fire with a low flame: unlit, low, lit.</summary>
        private static readonly string[] FireLooksWithLow = { "Unlit", "Low", "Lit" };

        /// <summary>A fire: unlit, lit.</summary>
        private static readonly string[] FireLooks = { "Unlit", "Lit" };

        /// <summary>A portal: unconnected, connected, open.</summary>
        private static readonly string[] PortalLooks = { "Unconnected", "Connected", "Open" };

        /// <summary>A door: shut, open either way.</summary>
        private static readonly string[] DoorLooks = { "Shut", "Open", "Open the other way" };

        /// <summary>A chest: shut, open.</summary>
        private static readonly string[] ChestLooks = { "Shut", "Open" };

        /// <summary>A windmill: still, turning.</summary>
        private static readonly string[] WindmillLooks = { "Still", "Turning" };

        /// <summary>A smelter: cold, loaded, working.</summary>
        private static readonly string[] SmelterLooks = { "Cold", "Loaded", "Working" };

        /// <summary>A fermenter: empty, fermenting, ready.</summary>
        private static readonly string[] FermenterLooks = { "Empty", "Fermenting", "Ready" };

        /// <summary>A sap collector: idle, working, full.</summary>
        private static readonly string[] SapLooks = { "Idle", "Working", "Full" };

        /// <summary>A station with a fire: cold, lit, in use.</summary>
        private static readonly string[] StationFireLooks = { "Cold", "Fire lit", "In use" };

        /// <summary>A station: idle, in use.</summary>
        private static readonly string[] StationLooks = { "Idle", "In use" };

        /// <summary>What is picked: ready, picked.</summary>
        private static readonly string[] PickedLooks = { "Ready", "Picked" };

        /// <summary>A creature that takes a saddle: without, saddled.</summary>
        private static readonly string[] SaddleLooks = { "No saddle", "Saddled" };

        public static Found Describe(GameObject prefab)
        {
            if (prefab == null) return new Found();
            if (Known.TryGetValue(prefab, out var known)) return known;

            var found = new Found();
            if (!Guard.Each("looks", prefab.name, () =>
            {
                    var plant = prefab.GetComponentInChildren<Plant>(true);
                    var fire = prefab.GetComponentInChildren<Fireplace>(true);
                    var pickable = prefab.GetComponentInChildren<Pickable>(true);
                    var portal = prefab.GetComponentInChildren<TeleportWorld>(true);
                    var door = prefab.GetComponentInChildren<global::Door>(true);
                    var chest = prefab.GetComponentInChildren<Container>(true);
                    var windmill = prefab.GetComponentInChildren<global::Windmill>(true);
                    var smelter = prefab.GetComponentInChildren<global::Smelter>(true);
                    var fermenter = prefab.GetComponentInChildren<Fermenter>(true);
                    var sap = prefab.GetComponentInChildren<SapCollector>(true);
                    var station = prefab.GetComponentInChildren<CraftingStation>(true);
                    var tameable = prefab.GetComponentInChildren<Tameable>(true);
                    var drop = prefab.GetComponent<ItemDrop>();

                    if (plant != null && plant.m_grownPrefabs != null && plant.m_grownPrefabs.Length > 0)
                    {
                        var names = new List<string> { "Growing" };
                        for (var i = 0; i < plant.m_grownPrefabs.Length; i++)
                        {
                            names.Add(plant.m_grownPrefabs.Length == 1 ? "Grown" : $"Grown {Numbers.Count(i + 1)}");
                        }
                        found = new Found { Sort = Sort.Growth, Names = names.ToArray() };
                    }
                    else if (fire != null && fire.m_enabledObject != null)
                    {
                        var hasLow = fire.m_enabledObjectLow != null && fire.m_enabledObjectHigh != null;
                        found = new Found
                        {
                            Sort = Sort.Fire,
                            Names = hasLow ? FireLooksWithLow : FireLooks,
                            Default = hasLow ? 2 : 1,
                        };
                    }
                    else if (portal != null && (portal.m_model != null || portal.m_target_found != null))
                    {
                        found = new Found { Sort = Sort.Portal, Names = PortalLooks, Default = 2 };
                    }
                    else if (door != null && Animators.Main(prefab) != null)
                    {
                        found = new Found { Sort = Sort.Door, Names = DoorLooks };
                    }
                    else if (chest != null && (chest.m_open != null || chest.m_closed != null))
                    {
                        found = new Found { Sort = Sort.Chest, Names = ChestLooks };
                    }
                    else if (windmill != null && windmill.m_propeller != null)
                    {
                        found = new Found { Sort = Sort.Windmill, Names = WindmillLooks, Default = 1 };
                    }
                    else if (smelter != null)
                    {
                        found = new Found { Sort = Sort.Smelter, Names = SmelterLooks, Default = 2 };
                    }
                    else if (fermenter != null && fermenter.m_topObject != null)
                    {
                        found = new Found { Sort = Sort.Fermenter, Names = FermenterLooks, Default = 1 };
                    }
                    else if (sap != null && (sap.m_workingEffect != null || sap.m_notEmptyEffect != null))
                    {
                        found = new Found { Sort = Sort.Sap, Names = SapLooks, Default = 1 };
                    }
                    else if (station != null && (station.m_inUseObject != null || station.m_haveFireObject != null))
                    {
                        found = new Found
                        {
                            Sort = Sort.Station,
                            Names = station.m_haveFireObject != null ? StationFireLooks : StationLooks,
                        };
                    }
                    else if (pickable != null && pickable.m_hideWhenPicked != null)
                    {
                        found = new Found { Sort = Sort.Picked, Names = PickedLooks };
                    }
                    else if (PrefabGear.Sets(prefab, out var sets) && sets.Count > 0)
                    {
                        var names = new List<string> { "No gear" };
                        names.AddRange(sets);
                        found = new Found { Sort = Sort.Gear, Names = names.ToArray(), Default = 1 };
                    }
                    else if (tameable != null && tameable.m_saddle != null)
                    {
                        found = new Found { Sort = Sort.Saddle, Names = SaddleLooks };
                    }
                    else if (drop != null && drop.m_itemData?.m_shared != null && drop.m_itemData.m_shared.m_variants > 1)
                    {
                        var names = new List<string>();
                        for (var i = 0; i < drop.m_itemData.m_shared.m_variants; i++) names.Add($"Style {Numbers.Count(i + 1)}");
                        found = new Found { Sort = Sort.Style, Names = names.ToArray() };
                    }
            })) found = new Found();

            Known[prefab] = found;
            return found;
        }
    }
}
