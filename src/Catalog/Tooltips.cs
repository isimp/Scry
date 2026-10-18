using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// An item's tooltip as the game builds it with every mod's hooks (<c>ItemData.GetTooltip</c>,
    /// asked as the inventory asks) for a fresh one of the prefab at a quality, durability full, in
    /// this world, each line marked where a mod added it against the game's own
    /// (<see cref="UnmoddedTooltip"/>, <see cref="TooltipText"/>), laid out as a card
    /// (<see cref="TooltipLayout"/>), with the mods hooking it. Kept until the world is left,
    /// what mods were seen or set to give changes, or one of the character's skill levels does.
    /// </summary>
    internal static class Tooltips
    {
        internal sealed class Shown
        {
            /// <summary>The item's name as the game shows it, heading the card.</summary>
            public string Name = "";

            public List<(string Line, bool FromMods)> Lines = new List<(string, bool)>();
            public List<TooltipPart> Parts = new List<TooltipPart>();

            /// <summary>Each run of parts a mod added, first and last.</summary>
            public List<(int First, int Last)> Runs = new List<(int, int)>();

            public IReadOnlyList<string> Mods = new List<string>();
            public bool FromMods => TooltipText.AnyFromMods(Lines);
        }

        private static readonly Dictionary<(string Prefab, int Quality), Shown> Kept = new Dictionary<(string, int), Shown>();
        private static (int Worn, int Settings, int Skills) _keptWith;

        /// <summary>The mods hooking the tooltip, named once for every tooltip kept.</summary>
        private static IReadOnlyList<string> _mods;

        /// <summary>The tooltip of a fresh item of this prefab at this quality; null for no item or one the tooltip fails for.</summary>
        public static Shown Of(GameObject prefab, int quality)
        {
            var drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop.OrNull()?.m_itemData?.m_shared == null) return null;
            // The game words a tooltip at the character's skills (a block, a hit's range, a shield's absorbing), so a level changed words it again.
            if (_keptWith != (WornWatch.Version, ModConfigs.Version, YourSkills.Version))
            {
                Forget();
                _keptWith = (WornWatch.Version, ModConfigs.Version, YourSkills.Version);
            }
            if (Kept.TryGetValue((prefab.name, quality), out var kept)) return kept;
            if (!Guard.Each(Feature.TooltipFromMods, "items' tooltips", prefab.name, () => Make(prefab, drop, quality), out kept)) kept = null;
            Kept[(prefab.name, quality)] = kept;
            return kept;
        }

        private static Shown Make(GameObject prefab, ItemDrop drop, int quality)
        {
            var item = drop.m_itemData.Clone();
            item.m_quality = quality;
            item.m_durability = item.GetMaxDurability(quality);
            item.m_stack = 1;
            item.m_dropPrefab = prefab;
            item.m_worldLevel = Game.m_worldLevel;
            // The inventory asks the item itself, which asks the game's static form, so a mod
            // hooking either is in.
            var withMods = item.GetTooltip();
            string own = null;
            Guard.Each(Feature.TooltipFromMods, "the game's own tooltip", prefab.name, () => own = UnmoddedTooltip.Of(item, quality, false, Game.m_worldLevel, -1, false));
            var lines = TooltipText.Marked(Localized(withMods), own != null ? Localized(own) : null);
            var parts = TooltipLayout.Parts(lines, Localized(item.m_shared.m_description));
            return new Shown
            {
                Name = Localized(item.m_shared.m_name),
                Lines = lines,
                Parts = parts,
                Runs = TooltipLayout.ModRuns(parts),
                Mods = _mods ?? (_mods = ModHooks.Hooking(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetTooltip))),
            };
        }

        private static string Localized(string text) => Localization.instance != null ? Localization.instance.Localize(text) : text;

        /// <summary>Forgets every tooltip, for a new world.</summary>
        public static void Forget()
        {
            Kept.Clear();
            _mods = null;
        }
    }
}
