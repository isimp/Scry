using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The Adjust section: size, level, look, gear rows and switches.</summary>
    internal static partial class ScryPanel
    {
        private static float Adjust(Explorer explorer, Entry entry, float width, float y, bool withStage)
        {
            var modifiers = explorer.Modifiers;
            var staged = Stage.IsStaged(entry);
            var projectile = entry.Kind == Kind.Projectile;
            var modelInWorld = Previews.InWorld && Previews.IsModel(entry);
            var clips = staged ? Previews.Clips() : new List<AnimationClip>();

            // Without the stage there is nothing to adjust unless the copy is in the world.
            if (!withStage && !modelInWorld && !projectile) return y;
            if (!staged && !projectile) return y;

            y = SectionHeading("ADJUST", width, y, () =>
            {
                modifiers.Reset();
                if (entry.Source is GameObject reset) Gear.ResetLoadout(reset);
                Previews.Rebuild();
            }, "adjust");
            var labelW = U(_compact ? 100f : 120f);
            var open = !IsFolded("adjust");

            if (open && staged)
            {
                // Size on a curve, so the range from a tenth to ten times is usable end to end.
                var logScale = Mathf.Log10(modifiers.Scale);
                var picked = SliderRow("Size", $"×{modifiers.Scale.ToString("0.00", CultureInfo.InvariantCulture)}", logScale, -1f, 1f, width, labelW, ref y);
                if (!Mathf.Approximately(picked, logScale)) modifiers.Scale = Mathf.Pow(10f, picked);
            }

            if (open && entry.Kind == Kind.Creature && modifiers.MaxLevel > 1)
            {
                var names = new List<string>();
                for (var level = 1; level <= modifiers.MaxLevel; level++) names.Add(level == 1 ? "No stars" : level == 2 ? "1 star" : $"{level - 1} stars");
                var chosen = Segments("Level", names, modifiers.Level - 1, width, labelW, ref y);
                if (chosen >= 0) modifiers.Level = chosen + 1;
            }

            if (open && modifiers.WearAvailable)
            {
                var chosen = Segments("Wear", new List<string> { "New", "Worn", "Broken" }, (int)modifiers.Wear, width, labelW, ref y);
                if (chosen >= 0) modifiers.Wear = (Wear)chosen;
            }

            if (open && modifiers.LookAvailable)
            {
                var chosen = Segments("Look", new List<string>(modifiers.LookNames), modifiers.Look, width, labelW, ref y);
                if (chosen >= 0) modifiers.Look = chosen;
                if (modifiers.Look > 0 && entry.Source is GameObject creature && Scry.Variants.IsGear(creature)) LoadoutRows(creature, modifiers.Look, width, labelW, ref y);
            }

            if (open && projectile)
            {
                Previews.ProjectileSpeed = SliderRow("Speed", $"{Mathf.RoundToInt(Previews.ProjectileSpeed)} m/s", Previews.ProjectileSpeed, 5f, 120f, width, labelW, ref y);
            }

            if (clips.Count > 0)
            {
                y += U(6f);
                y = Clips(explorer, clips, modifiers, width, labelW, y);
            }

            return y + U(10f);
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
                var w = style.CalcSize(new GUIContent(names[i])).x + U(10f);
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
