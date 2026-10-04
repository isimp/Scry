using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A game effect list's slots, read one way. The game plays a list's switched-on slots, each
    /// its prefab where the slot says (<c>EffectList.Create</c>); a slot can also be switched off,
    /// or left with no prefab. Whatever plays a list or tells of it (who plays a prefab, what a
    /// thing leaves behind, what a place holds) reads <see cref="Plays"/>, so a switched-off slot
    /// counts nowhere; only telling why a list showed nothing reads <see cref="Names"/>, every
    /// slot with a prefab. A slot that <see cref="Shows"/> is one played on what plays it rather
    /// than a whole model of its own. Nothing else reads a list's slots.
    /// </summary>
    internal static class EffectSlots
    {
        /// <summary>A list's slots in its order; none for no list.</summary>
        public static EffectList.EffectData[] Of(EffectList list) => list?.m_effectPrefabs ?? System.Array.Empty<EffectList.EffectData>();

        /// <summary>Whether the game plays a slot: switched on, with a prefab.</summary>
        public static bool Plays(EffectList.EffectData slot) => slot != null && slot.m_enabled && slot.m_prefab != null;

        /// <summary>Whether a slot names a prefab, played or not, for telling why a list showed nothing.</summary>
        public static bool Names(EffectList.EffectData slot) => slot != null && slot.m_prefab != null;

        /// <summary>Whether a slot the game plays shows on what plays it: not a whole model standing on its own (<see cref="PrefabShapes.IsWholeModel"/>).</summary>
        public static bool Shows(EffectList.EffectData slot) => Plays(slot) && !PrefabShapes.IsWholeModel(slot.m_prefab);

        /// <summary>Whether the game plays anything of a list.</summary>
        public static bool PlaysAny(EffectList list)
        {
            foreach (var slot in Of(list)) if (Plays(slot)) return true;
            return false;
        }

        /// <summary>Whether a list plays anything that shows on what plays it.</summary>
        public static bool ShowsAny(EffectList list)
        {
            foreach (var slot in Of(list)) if (Shows(slot)) return true;
            return false;
        }

        /// <summary>The prefabs a list plays, by name, each once, in its order.</summary>
        public static List<string> NamesPlayed(EffectList list)
        {
            var names = new List<string>();
            foreach (var slot in Of(list))
            {
                if (Plays(slot) && !names.Contains(slot.m_prefab.name)) names.Add(slot.m_prefab.name);
            }
            return names;
        }
    }
}
