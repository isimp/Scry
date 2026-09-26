using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The looks a prefab can be switched between, when the game switches them by script: a
    /// plant growing or grown, a fire unlit or lit, something picked or not, a creature with or
    /// without its gear, an item in each of its styles. Only one kind is offered per prefab, in
    /// that order, and each is applied to a copy the way the game's own script would.
    /// </summary>
    internal static class Variants
    {
        private enum Sort { None, Growth, Fire, Picked, Gear, Style }

        private sealed class Found
        {
            public Sort Sort;
            public string[] Names = new string[0];
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

        /// <summary>What to copy for a look: a grown plant is a prefab of its own.</summary>
        public static GameObject SourceFor(GameObject prefab, int look)
        {
            var found = Describe(prefab);
            if (found.Sort != Sort.Growth || look <= 0) return prefab;

            var plant = prefab.GetComponentInChildren<Plant>(true);
            var grown = plant != null && look - 1 < plant.m_grownPrefabs.Length ? plant.m_grownPrefabs[look - 1] : null;
            return grown != null ? grown : prefab;
        }

        /// <summary>Puts a copy in a look.</summary>
        public static void Apply(GameObject prefab, GameObject copy, int look)
        {
            if (prefab == null || copy == null) return;
            var found = Describe(prefab);

            switch (found.Sort)
            {
                case Sort.Fire:
                    Fire(prefab, copy, look);
                    break;
                case Sort.Picked:
                    var pickable = prefab.GetComponentInChildren<Pickable>(true);
                    var shown = Looks.Twin(prefab.transform, copy.transform, pickable.m_hideWhenPicked.transform);
                    if (shown != null) shown.gameObject.SetActive(look == 0);
                    break;
                case Sort.Gear:
                    if (look > 0) Gear.Dress(prefab, copy, look);
                    break;
                case Sort.Style:
                    Style(prefab, copy, look);
                    break;
            }
        }

        /// <summary>What a creature wears in a look; nothing when the look is not about gear.</summary>
        public static List<GameObject> GearOf(GameObject prefab, int look)
        {
            return Describe(prefab).Sort == Sort.Gear ? Gear.DressItems(prefab, look) : new List<GameObject>();
        }

        /// <summary>Whether a prefab's looks are about the gear it wears.</summary>
        public static bool IsGear(GameObject prefab) => Describe(prefab).Sort == Sort.Gear;

        private static Found Describe(GameObject prefab)
        {
            if (prefab == null) return new Found();
            if (Known.TryGetValue(prefab, out var known)) return known;

            var found = new Found();
            try
            {
                var plant = prefab.GetComponentInChildren<Plant>(true);
                var fire = prefab.GetComponentInChildren<Fireplace>(true);
                var pickable = prefab.GetComponentInChildren<Pickable>(true);
                var drop = prefab.GetComponent<ItemDrop>();

                if (plant != null && plant.m_grownPrefabs != null && plant.m_grownPrefabs.Length > 0)
                {
                    var names = new List<string> { "Growing" };
                    for (var i = 0; i < plant.m_grownPrefabs.Length; i++)
                    {
                        names.Add(plant.m_grownPrefabs.Length == 1 ? "Grown" : $"Grown {i + 1}");
                    }
                    found = new Found { Sort = Sort.Growth, Names = names.ToArray() };
                }
                else if (fire != null && fire.m_enabledObject != null)
                {
                    var hasLow = fire.m_enabledObjectLow != null && fire.m_enabledObjectHigh != null;
                    found = new Found
                    {
                        Sort = Sort.Fire,
                        Names = hasLow ? new[] { "Unlit", "Low", "Lit" } : new[] { "Unlit", "Lit" },
                        Default = hasLow ? 2 : 1,
                    };
                }
                else if (pickable != null && pickable.m_hideWhenPicked != null)
                {
                    found = new Found { Sort = Sort.Picked, Names = new[] { "Ready", "Picked" } };
                }
                else if (Gear.Sets(prefab, out var sets) && sets.Count > 0)
                {
                    var names = new List<string> { "No gear" };
                    names.AddRange(sets);
                    found = new Found { Sort = Sort.Gear, Names = names.ToArray(), Default = 1 };
                }
                else if (drop != null && drop.m_itemData?.m_shared != null && drop.m_itemData.m_shared.m_variants > 1)
                {
                    var names = new List<string>();
                    for (var i = 0; i < drop.m_itemData.m_shared.m_variants; i++) names.Add($"Style {i + 1}");
                    found = new Found { Sort = Sort.Style, Names = names.ToArray() };
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogDebug($"Scry could not read the looks of {prefab.name}: {ex.Message}");
                found = new Found();
            }

            Known[prefab] = found;
            return found;
        }

        /// <summary>As <c>Fireplace.UpdateState</c> shows a fire by whether it burns and how brightly.</summary>
        private static void Fire(GameObject prefab, GameObject copy, int look)
        {
            var fire = prefab.GetComponentInChildren<Fireplace>(true);
            var hasLow = fire.m_enabledObjectLow != null && fire.m_enabledObjectHigh != null;
            var burning = look > 0;
            var low = hasLow && look == 1;

            void Set(GameObject part, bool on)
            {
                if (part == null) return;
                var twin = Looks.Twin(prefab.transform, copy.transform, part.transform);
                if (twin != null) twin.gameObject.SetActive(on);
            }

            Set(fire.m_enabledObject, burning);
            if (hasLow)
            {
                Set(fire.m_enabledObjectHigh, burning && !low);
                Set(fire.m_enabledObjectLow, burning && low);
            }
            Set(fire.m_fullObject, burning && !low);
            Set(fire.m_halfObject, low);
            Set(fire.m_emptyObject, !burning);
        }

        /// <summary>As <c>ItemStyle.Setup</c> picks an item's style through the game's material manager.</summary>
        private static void Style(GameObject prefab, GameObject copy, int look)
        {
            if (MaterialMan.instance == null) return;
            foreach (var style in prefab.GetComponentsInChildren<ItemStyle>(true))
            {
                var twin = Looks.Twin(prefab.transform, copy.transform, style.transform);
                if (twin != null) MaterialMan.instance.SetValue(twin.gameObject, ShaderProps._Style, look, true);
            }
        }
    }
}
