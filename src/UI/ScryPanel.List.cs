using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The catalog list, its rows and icons, and the help card over it.</summary>
    internal static partial class ScryPanel
    {
        private static readonly string[][] HelpLines =
        {
            new[] { "troll", "Names containing it, in the game's words or the prefab's. Best matches first." },
            new[] { "troll hat", "Every word has to match." },
            new[] { "-ragdoll", "A minus leaves out whatever matches." },
            new[] { "kind:creature", "Only one kind: creature, item, piece, projectile, effect, sound, se (status effect), other." },
            new[] { "has:aoe", "Prefabs with a part of that type, such as has:light, has:pickable, has:fireplace." },
            new[] { "biome:swamp", "What spawns or grows in that biome." },
            new[] { "mod:epic", "What a mod added, by the start or any part of its name." },
            new[] { "used:troll", "The sounds and effects a prefab plays." },
            new[] { "station:forge3", "What is made at that station, here what a forge at level 3 can make. station:forge for any level, station:hand for what needs none." },
            new[] { "-has:ragdoll kind:c", "Terms combine, can be left out with a minus, and can be shortened." },
        };

        /// <summary>How to search, shown in place of the list while the ? button is on.</summary>
        private static Vector2 _helpScroll;
        private static float _helpHeight;

        private static void HelpCard(Rect rect)
        {
            Skin.Box(rect, Skin.Panel);
            var closeH = U(30f);
            var area = new Rect(rect.x + U(4f), rect.y + U(6f), rect.width - U(8f), rect.height - closeH - U(20f));
            var view = new Rect(0f, 0f, area.width - U(14f), Mathf.Max(_helpHeight, area.height));
            _helpScroll = GUI.BeginScrollView(area, _helpScroll, view, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);

            var x = U(14f);
            var width = view.width - U(24f);
            var y = U(10f);

            GUI.Label(new Rect(x, y, width, U(26f)), "How to search", Skin.Big);
            y += U(34f);

            // Side by side when there is room, the example above its meaning when not.
            var stacked = width < U(420f);
            var keyW = stacked ? width : Mathf.Min(U(190f), width * 0.42f);
            foreach (var line in HelpLines)
            {
                var boxW = Mathf.Min(keyW, Skin.Width(Skin.Label, line[0]) + U(16f));
                Skin.Box(new Rect(x - U(4f), y - U(1f), boxW, U(24f)), Skin.Raised);
                GUI.Label(new Rect(x + U(4f), y, boxW - U(8f), U(22f)), line[0], Skin.Label);

                var textX = stacked ? x : x + keyW + U(10f);
                var textY = stacked ? y + U(28f) : y + U(2f);
                var textW = stacked ? width : width - keyW - U(10f);
                var height = Skin.Height(Skin.DimWrap, line[1], textW);
                GUI.Label(new Rect(textX, textY, textW, height), line[1], Skin.DimWrap);
                y = Mathf.Max(y + U(24f), textY + height) + U(12f);
            }

            const string more = "The star shows only favourites, Recent what you looked at last. The kind tabs, Game or Mods, and all of the above work together.";
            var moreH = Skin.Height(Skin.DimWrap, more, width);
            GUI.Label(new Rect(x, y, width, moreH), more, Skin.DimWrap);
            if (Event.current.type == EventType.Repaint) _helpHeight = y + moreH + U(12f);

            GUI.EndScrollView();

            if (GUI.Button(new Rect(rect.xMax - U(96f), rect.yMax - closeH - U(10f), U(80f), closeH), "Close", Skin.Button)) _help = false;
        }

        private static void List(Explorer explorer, Rect rect)
        {
            if (_help)
            {
                HelpCard(rect);
                return;
            }

            Skin.Box(rect, Skin.Panel);
            var inner = new Rect(rect.x + U(4f), rect.y + U(6f), rect.width - U(8f), rect.height - U(12f));
            var results = explorer.Results;
            var rowH = U(34f);
            _rowsInView = Mathf.Max(1, Mathf.FloorToInt(inner.height / rowH));

            if (results.Count == 0)
            {
                var message = explorer.FavouritesOnly && explorer.Favourites.Keys.Count == 0
                    ? "No favourites yet. Star something to keep it here."
                    : "Nothing matches.";
                GUI.Label(inner, message, Skin.CenterDim);
                return;
            }

            if (_reveal && Event.current.type == EventType.Repaint)
            {
                _reveal = false;
                var index = explorer.SelectedIndex;
                if (index >= 0)
                {
                    var top = index * rowH;
                    if (top < _listScroll.y) _listScroll.y = top;
                    else if (top + rowH > _listScroll.y + inner.height) _listScroll.y = top + rowH - inner.height;
                }
            }

            var view = new Rect(0f, 0f, inner.width - U(14f), results.Count * rowH);
            _listScroll = GUI.BeginScrollView(inner, _listScroll, view, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);

            var first = Mathf.Max(0, Mathf.FloorToInt(_listScroll.y / rowH));
            var last = Mathf.Min(results.Count - 1, first + _rowsInView + 1);
            var visible = new Rect(0f, _listScroll.y, view.width, inner.height);
            for (var i = first; i <= last; i++)
            {
                Row(explorer, results[i], new Rect(0f, i * rowH, view.width, rowH), i == explorer.SelectedIndex, visible);
            }

            GUI.EndScrollView();
        }

        private static void Row(Explorer explorer, Entry entry, Rect rect, bool selected, Rect visible)
        {
            var e = Event.current;
            var hover = rect.Contains(e.mousePosition) && visible.Contains(e.mousePosition) && _drag == Drag.None;
            var inner = new Rect(rect.x + U(2f), rect.y + U(1f), rect.width - U(4f), rect.height - U(2f));

            if (selected) Skin.Box(inner, Skin.AccentSoft);
            else if (hover) Skin.Box(inner, Skin.Hover);
            if (selected) Skin.Fill(new Rect(inner.x, inner.y + U(8f), U(3f), inner.height - U(16f)), Skin.Accent);

            var icon = new Rect(inner.x + U(10f), inner.y + (inner.height - U(24f)) / 2f, U(24f), U(24f));
            DrawIcon(entry, icon);

            var favourite = explorer.Favourites.Contains(entry);
            var star = new Rect(inner.xMax - U(28f), inner.y + (inner.height - U(18f)) / 2f, U(18f), U(18f));
            if (favourite) Skin.Icon(star, Skin.Star, Skin.Accent);
            else if (hover) Skin.Icon(star, Skin.StarHollow, star.Contains(e.mousePosition) ? Skin.Accent : Skin.Faint);

            var textX = icon.xMax + U(10f);
            var textW = star.x - U(8f) - textX;
            var primary = string.IsNullOrEmpty(entry.DisplayName) ? entry.Name : entry.DisplayName;
            var secondary = string.IsNullOrEmpty(entry.DisplayName) || entry.DisplayName == entry.Name ? "" : entry.Name;

            var nameStyle = Skin.RowName;
            var was = nameStyle.normal.textColor;
            if (entry.Empty) nameStyle.normal.textColor = Skin.Faint;
            var fullW = Skin.Width(nameStyle, primary);
            var nameW = Mathf.Min(textW, fullW);
            GUI.Label(new Rect(textX, inner.y, nameW, inner.height), primary, nameStyle);
            nameStyle.normal.textColor = was;

            var cut = fullW > textW;
            if (secondary.Length > 0)
            {
                var room = textW - nameW - U(8f);
                if (room > U(40f))
                {
                    GUI.Label(new Rect(textX + nameW + U(8f), inner.y + U(1f), room, inner.height), secondary, Skin.RowSub);
                    cut |= Skin.Width(Skin.RowSub, secondary) > room;
                }
                else
                {
                    cut = true;
                }
            }

            if (hover && cut && !star.Contains(e.mousePosition))
            {
                AskTip(entry.Key, secondary.Length > 0 ? primary + "\n" + secondary : primary);
            }

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition) && visible.Contains(e.mousePosition))
            {
                if (star.Contains(e.mousePosition))
                {
                    explorer.ToggleFavourite(entry);
                }
                else
                {
                    explorer.Select(entry);
                    if (e.clickCount == 2) Primary(entry);
                }
                e.Use();
            }
        }

        private static void DrawIcon(Entry entry, Rect rect)
        {
            if (entry.Icon is Sprite sprite && sprite != null && sprite.texture != null && Event.current.type == EventType.Repaint)
            {
                try
                {
                    var t = sprite.texture;
                    var r = sprite.textureRect;
                    var uv = new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height);
                    var aspect = r.width / Mathf.Max(1f, r.height);
                    var fit = aspect >= 1f
                        ? new Rect(rect.x, rect.y + (rect.height - rect.height / aspect) / 2f, rect.width, rect.height / aspect)
                        : new Rect(rect.x + (rect.width - rect.width * aspect) / 2f, rect.y, rect.width * aspect, rect.height);
                    GUI.DrawTextureWithTexCoords(fit, t, uv, true);
                    return;
                }
                catch
                {
                    // A sprite packed in a way that has no simple rectangle; the mark below stands in.
                }
            }

            var color = Skin.KindColor(entry.Kind);
            Skin.Box(rect, new Color(color.r, color.g, color.b, 0.20f));
            var style = Skin.Glyph;
            var was = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(rect, Skin.KindMark(entry.Kind), style);
            style.normal.textColor = was;
        }
    }
}
