using System;
using System.Collections.Generic;
using System.Globalization;
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
            var clips = model && staged && entry.Kind != Kind.StatusEffect ? Previews.Clips() : NoClips;
            if (clips.Count == 0) return y;
            y = Clips(explorer, clips, explorer.Modifiers, width, U(_compact ? 100f : 120f), y);
            return IsFolded("animations") ? y : y + U(10f);
        }

        private static float Adjust(Explorer explorer, Entry entry, float width, float y, bool withStage)
        {
            var modifiers = explorer.Modifiers;
            var projectile = entry.Kind == Kind.Projectile;
            var model = Adjustable(entry, withStage, out var staged);

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

            if (open) Volume(modifiers, width, labelW, ref y);

            if (open && model && staged)
            {
                // Size on a curve, so the range from a tenth to ten times is usable end to end.
                var logScale = Mathf.Log10(modifiers.Scale);
                var picked = SliderRow("Size", $"×{modifiers.Scale.ToString("0.00", CultureInfo.InvariantCulture)}", logScale, -1f, 1f, width, labelW, ref y);
                if (!Mathf.Approximately(picked, logScale)) modifiers.Scale = Mathf.Pow(10f, picked);
            }

            if (open && model && entry.Kind == Kind.Creature && modifiers.MaxLevel > 1)
            {
                var names = new List<string>();
                for (var level = 1; level <= modifiers.MaxLevel; level++) names.Add(level == 1 ? "No stars" : level == 2 ? "1 star" : $"{level - 1} stars");
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
                Previews.ProjectileSpeed = SliderRow("Speed", $"{Mathf.RoundToInt(Previews.ProjectileSpeed)} m/s", Previews.ProjectileSpeed, 5f, 120f, width, labelW, ref y);
            }

            // Space after it only when open: a folded heading already leaves the same gap as every other.
            return open ? y + U(10f) : y;
        }

        private static readonly List<AnimationClip> NoClips = new List<AnimationClip>();
        /// <summary>
        /// How loud the selection's previews play, from silent to twice the game's own, in steps of
        /// 5%, heard as it moves. It is the selection's own, like its size: the next one starts at
        /// the game's loudness again, so a sound turned up never carries over to one that plays as
        /// soon as it is selected.
        /// </summary>
        private static void Volume(Modifiers modifiers, float width, float labelW, ref float y)
        {
            var now = modifiers.Volume;
            var picked = SliderRow("Volume", $"{Mathf.RoundToInt(now * 100f)}%", now, 0f, Modifiers.MaxVolume, width, labelW, ref y);
            picked = Mathf.Round(picked * 20f) / 20f;
            if (!Mathf.Approximately(picked, now))
            {
                modifiers.Volume = picked;
                Loudness.Gain = modifiers.Volume;
            }
        }

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
                if (!held) GUI.color = new Color(was.r, was.g, was.b, was.a * 0.4f);
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
            var shared = Looks.Prefab(prefabName)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            var shown = shared != null ? CatalogBuilder.Localize(shared.m_name) : "";
            return shown.Length > 0 ? shown : prefabName;
        }

        /// <summary>Like <see cref="Segments"/>, but any number can be on; returns the one clicked.</summary>
        private static int Toggles(string label, List<string> names, Func<int, bool> on, float width, float labelW, ref float y)
        {
            var rowH = U(28f);
            FitLabel(new Rect(0f, y, labelW - U(6f), rowH), label, Skin.DimLabel, 10f);
            var x = labelW;
            var clicked = -1;
            for (var i = 0; i < names.Count; i++)
            {
                var style = on(i) ? Skin.SegmentOn : Skin.Segment;
                var w = Skin.Width(style, names[i]) + U(10f);
                if (x + w > width && x > labelW)
                {
                    x = labelW;
                    y += rowH + U(4f);
                }
                if (GUI.Button(new Rect(x, y, w, rowH), names[i], style)) clicked = i;
                x += w + U(4f);
            }
            y += rowH + U(8f);
            return clicked;
        }
    }
}
