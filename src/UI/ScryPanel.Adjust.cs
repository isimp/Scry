using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The Adjust section (loudness, size, stars, wear, look, gear rows), just under what can be
    /// done with the selection, and the Animations section, which can hold hundreds of clips and
    /// so comes after what the entry is in the game.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>
        /// Whether the model can be adjusted: without the stage only while its copy is in the
        /// world. How loud previews play can always be, for sounds as for everything else.
        /// </summary>
        private static bool Adjustable(Entry entry, bool withStage, out bool staged)
        {
            staged = Stage.IsStaged(entry);
            var projectile = entry.Kind == Kind.Projectile;
            var modelInWorld = Previews.InWorld && Previews.IsModel(entry);
            return (withStage || modelInWorld || projectile) && (staged || projectile);
        }

        private static float Animations(Explorer explorer, Entry entry, float width, float y, bool withStage)
        {
            var model = Adjustable(entry, withStage, out var staged);
            var clips = model && staged && entry.Kind != Kind.StatusEffect && entry.Kind != Kind.Raid ? Previews.Clips() : NoClips;
            if (clips.Count == 0) return y;
            y = Clips(explorer, clips, explorer.Modifiers, width, U(_compact ? 100f : 120f), y);
            return IsFolded("animations") ? y : y + U(10f);
        }

        private static float Adjust(Explorer explorer, Entry entry, float width, float y, bool withStage)
        {
            var modifiers = explorer.Modifiers;
            var projectile = entry.Kind == Kind.Projectile;
            var model = Adjustable(entry, withStage, out var staged);

            // Volume and size are the header's (ScryPanel.Slide); the section shows only with something else to adjust.
            var stars = model && entry.Kind == Kind.Creature && modifiers.MaxLevel > 1;
            if (!stars && !(model && modifiers.WearAvailable) && !(model && modifiers.LookAvailable) && !projectile) return y;

            // Reset is offered only while the section is open and something differs from the prefab.
            var open = !IsFolded("adjust");
            var changed = !modifiers.IsDefault || (entry.Source is GameObject rolled && Gear.LoadoutChanged(rolled));
            y = SectionHeading("ADJUST", width, y, open && changed ? () =>
            {
                modifiers.Reset();
                if (entry.Source is GameObject reset) Gear.ResetLoadout(reset);
                Previews.Rebuild();
            } : (Action)null, "adjust");
            var labelW = U(_compact ? 100f : 120f);

            if (open && stars)
            {
                var names = new List<string>();
                for (var level = 1; level <= modifiers.MaxLevel; level++) names.Add(level == 1 ? "No stars" : level == 2 ? "1 star" : $"{Numbers.Count(level - 1)} stars");
                var chosen = Segments("Stars", names, modifiers.Level - 1, width, labelW, ref y);
                if (chosen >= 0) modifiers.Level = chosen + 1;
            }

            if (open && model && modifiers.WearAvailable)
            {
                var chosen = Segments("Wear", new List<string> { "New", "Worn", "Broken" }, (int)modifiers.Wear, width, labelW, ref y);
                if (chosen >= 0) modifiers.Wear = (Wear)chosen;
            }

            if (open && model && modifiers.LookAvailable)
            {
                var chosen = Segments("Look", new List<string>(modifiers.LookNames), modifiers.Look, width, labelW, ref y);
                if (chosen >= 0) modifiers.Look = chosen;
                if (modifiers.Look > 0 && entry.Source is GameObject creature && Scry.Variants.IsGear(creature)) LoadoutRows(creature, modifiers.Look, width, labelW, ref y);
            }

            if (open && projectile)
            {
                Previews.ProjectileSpeed = SliderRow("Speed", $"{Numbers.Count(Mathf.RoundToInt(Previews.ProjectileSpeed))} m/s", Previews.ProjectileSpeed, 5f, 120f, width, labelW, ref y);
            }

            // Space after it only when open: a folded heading already leaves the same gap as every other.
            return open ? y + U(10f) : y;
        }

        private static readonly List<AnimationClip> NoClips = new List<AnimationClip>();
        /// <summary>
        /// The weapon, shield and armour a creature can roll, one row each, and the extras it may
        /// be given, each put on or taken off. Only rows with a choice are shown.
        /// </summary>
        private static void LoadoutRows(GameObject creature, int look, float width, float labelW, ref float y)
        {
            var loadout = Gear.LoadoutOf(creature);
            loadout.Carrying(Gear.SetWeapons(creature, look));
            var rows = new[] { Loadout.Row.Holding, Loadout.Row.Weapon, Loadout.Row.Shield, Loadout.Row.Armour };
            var labels = new[] { "Holding", "Weapon", "Shield", "Armour" };
            for (var i = 0; i < rows.Length; i++)
            {
                if (!loadout.Offered(rows[i])) continue;
                var names = loadout.Options(rows[i]).Select(ItemName).ToList();
                for (var n = 0; n < names.Count; n++)
                {
                    if (names.Count(other => other == names[n]) > 1) names[n] = names[n] + " (" + loadout.Options(rows[i])[n] + ")";
                }

                // A shield the weapon leaves no hand for is shown put away, and still chosen for later.
                var held = loadout.Held(rows[i]);
                var was = GUI.color;
                if (!held) GUI.color = Skin.Alpha(was, was.a * 0.4f);
                var top = y;
                var chosen = Segments(labels[i], names, loadout.Chosen(rows[i]), width, labelW, ref y);
                GUI.color = was;
                if (!held && new Rect(0f, top, width, y - top).Contains(Event.current.mousePosition))
                {
                    AskTip("shield-away", "Not worn: the weapon takes both hands. Pick a one-handed weapon to wear it.");
                }
                if (chosen < 0 || chosen == loadout.Chosen(rows[i])) continue;
                loadout.Choose(rows[i], chosen);
                Previews.Rebuild();
            }

            if (loadout.Extras.Count == 0) return;
            var extras = loadout.Extras.Select(e => ItemName(e.Name)).ToList();
            var clicked = Toggles("Extras", extras, loadout.ExtraOn, width, labelW, ref y);
            if (clicked < 0) return;
            loadout.ToggleExtra(clicked);
            Previews.Rebuild();
        }

        /// <summary>An item's name as the game shows it, or "Nothing" for an empty choice.</summary>
        private static string ItemName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return "Nothing";
            var shared = GamePrefabs.Item(prefabName).OrNull()?.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
            var shown = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
            return shown.Length > 0 ? shown : prefabName;
        }
    }
}
