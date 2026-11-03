using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The game's pin names on its large map, as Scry's own words on the map are written in them:
    /// a copy of what the game makes a pin's name from, its words set by the name the game's own
    /// code sets them by, as the game writes them with TextMeshPro, which Scry does not build against.
    /// </summary>
    internal static class PinNames
    {
        /// <summary>What the game makes a pin's name from (<c>Minimap.m_pinNamePrefab</c>).</summary>
        private static readonly AccessTools.FieldRef<Minimap, GameObject> Prefab = AccessTools.FieldRefAccess<Minimap, GameObject>("m_pinNamePrefab");

        /// <summary>A copy of the game's pin name in a layer, hidden; none where the map has none to copy.</summary>
        public static GameObject Make(Minimap map, Transform layer, string name)
        {
            var named = map != null ? Prefab(map) : null;
            if (named == null) return null;
            var made = Object.Instantiate(named, layer);
            made.name = name;
            made.SetActive(false);
            return made;
        }

        /// <summary>The component that writes a name's words; none.</summary>
        public static Component Text(GameObject label) =>
            label == null ? null : label.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && IsText(c.GetType()));

        /// <summary>Sets a name's words.</summary>
        public static void SetText(GameObject label, string said)
        {
            var text = Text(label);
            if (text == null) return;
            text.GetType().GetProperty("text")?.SetValue(text, said);
        }

        /// <summary>A name's words filling what holds it, centred on one line at a size, as a count on a tab is written: the name and every part of it under it stretched over it, as the words stand on a part of their own.</summary>
        public static void Centred(GameObject label, float size)
        {
            foreach (var rect in label.GetComponentsInChildren<RectTransform>(true))
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }
            var text = Text(label);
            if (text == null) return;
            var type = text.GetType();
            type.GetProperty("fontSize")?.SetValue(text, size);
            type.GetProperty("enableWordWrapping")?.SetValue(text, false);
            // TextAlignmentOptions.Center, by its number, as Scry does not build against TextMeshPro.
            if (type.GetProperty("alignment") is System.Reflection.PropertyInfo alignment && alignment.PropertyType.IsEnum)
                alignment.SetValue(text, System.Enum.ToObject(alignment.PropertyType, 514));
        }

        /// <summary>Whether a component writes with TextMeshPro (<c>TMP_Text</c> or what derives from it).</summary>
        private static bool IsText(System.Type type)
        {
            for (var t = type; t != null; t = t.BaseType) if (t.Name == "TMP_Text") return true;
            return false;
        }
    }
}
