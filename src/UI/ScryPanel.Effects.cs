using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The Effects section and the Plays in rows: effect lists, what they are made of and the clips they go with.</summary>
    internal static partial class ScryPanel
    {
        private static readonly Dictionary<string, List<KeyValuePair<string, EffectList>>> EffectCache =
            new Dictionary<string, List<KeyValuePair<string, EffectList>>>();

        /// <summary>
        /// Every effect list the prefab carries, played on the stage copy and on the copy in the
        /// world: a creature's hits and death, a piece's placing and breaking, an item's swings.
        /// </summary>
        private static float Effects(Explorer explorer, Entry entry, float width, float y, bool withStage)
        {
            if (!(entry.Source is GameObject prefab) || entry.Kind == Kind.Sound || entry.Kind == Kind.Effect) return y;
            if (!withStage && !(Previews.InWorld && Previews.IsModel(entry))) return y;

            // A creature's chips follow what it has on: the weapon in its hand, not the rest.
            var carried = Previews.CarriedNow(entry);
            var cacheKey = entry.Key + "|" + (carried == null ? "all" : string.Join(",", carried.Select(c => c.name)));
            if (!EffectCache.TryGetValue(cacheKey, out var lists))
            {
                lists = carried == null ? Previews.PrefabLists(prefab) : Previews.PrefabLists(prefab, carried);
                EffectCache[cacheKey] = lists;
            }
            if (lists.Count == 0) return y;

            y = SectionHeading($"EFFECTS  {lists.Count}", width, y, null, "effects");
            if (IsFolded("effects")) return y;
            var rowH = U(26f);

            if (lists.Count > 12 || _effectFilter.Length > 0)
            {
                var field = new Rect(0f, y, Mathf.Min(width, U(260f)), U(28f));
                GUI.SetNextControlName(EffectControl);
                _effectFilter = GUI.TextField(field, _effectFilter, 40, Skin.Field);
                if (string.IsNullOrEmpty(_effectFilter)) GUI.Label(field, "Filter", Skin.Placeholder);
                y += U(36f);
            }

            var x = 0f;
            foreach (var pair in lists)
            {
                if (_effectFilter.Length > 0 && pair.Key.IndexOf(_effectFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var w = Mathf.Min(width, Skin.Chip.CalcSize(new GUIContent(pair.Key)).x + U(8f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                var chip = new Rect(x, y, w, rowH);
                var playing = Previews.Playing.IsPlaying(pair.Value);
                if (GUI.Button(chip, pair.Key, playing ? Skin.ChipOn : Skin.Chip))
                {
                    if (playing) Previews.Stop(pair.Value);
                    else Previews.PlayEffectList(pair.Key, pair.Value);
                }
                if (playing && chip.Contains(Event.current.mousePosition)) AskTip("fx-stop:" + pair.Key, "Playing; click to stop it");
                if (chip.Contains(Event.current.mousePosition))
                {
                    var names = pair.Value.m_effectPrefabs.Where(d => d?.m_prefab != null).Select(d => d.m_prefab.name);
                    AskTip("fx:" + pair.Key, string.Join("\n", names));
                }
                x += w + U(5f);
            }
            if (x > 0f) y += rowH;

            // What the list played last is made of, each part lit while its copy plays.
            var last = lists.FirstOrDefault(l => ReferenceEquals(l.Value, Previews.Playing.Last));
            if (last.Value != null)
            {
                y += U(10f);
                y = Members(explorer, "In " + last.Key + ":", last.Value, Members(last.Value), null, width, y);

                // The clips it goes with, to play from here.
                var withClips = Previews.ClipsOfList(last.Value);
                if (withClips.Count > 0) y = ClipLinks(withClips, width, y + U(2f));
            }

            return y + U(14f);
        }

        /// <summary>
        /// Chips for the clips an effect list goes with, each said how where it is not played
        /// with it (found by name, heard around), each playing its clip.
        /// </summary>
        private static float ClipLinks(List<(AnimationClip Clip, string How)> clips, float width, float y)
        {
            GUI.Label(new Rect(0f, y, width, U(20f)), clips.Count > 1 ? "With its clips:" : "With its clip:", Skin.DimLabel);
            y += U(24f);
            var rowH = U(26f);
            var x = 0f;
            var playing = Previews.PlayingClip();
            foreach (var (clip, how) in clips)
            {
                var text = how.Length > 0 ? clip.name + "  ·  " + how : clip.name;
                var on = playing == clip;
                var style = on ? Skin.ChipOn : Skin.Chip;
                var w = Mathf.Min(width, style.CalcSize(new GUIContent(text)).x + U(8f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                if (GUI.Button(new Rect(x, y, w, rowH), text, style))
                {
                    if (on) Previews.StopClip();
                    else
                    {
                        Previews.PlayClip(clip);
                        Previews.LastClip = clip;
                    }
                }
                x += w + U(5f);
            }
            return y + rowH + U(6f);
        }

        /// <summary>The prefabs an effect list plays, each once.</summary>
        private static string[] Members(EffectList list)
        {
            return list.m_effectPrefabs.Where(d => d != null && d.m_enabled && d.m_prefab != null).Select(d => d.m_prefab.name).Distinct().ToArray();
        }

        /// <summary>
        /// The parts of a list as chips: each goes to its prefab, and is lit while the copy of it
        /// the list last started still plays. The selected prefab itself is shown but not a link.
        /// </summary>
        private static Entry _groundsFor;

        private static float Members(Explorer explorer, string title, object list, string[] members, string self, float width, float y)
        {
            if (title != null)
            {
                GUI.Label(new Rect(0f, y, width, U(20f)), title, Skin.DimLabel);
                y += U(24f);
            }
            var x = 0f;
            var rowH = U(26f);
            foreach (var member in members)
            {
                var lit = Previews.Playing.IsPlaying(list, member);
                var go = member != self && InCatalog(explorer, member);
                var w = Mathf.Min(width, LinkChipWidth(member, go));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                var chip = new Rect(x, y, w, rowH);
                if (LinkChip(chip, member, KindOf(explorer, member), lit, go)) Go(explorer, member);
                if (go && chip.Contains(Event.current.mousePosition)) AskTip("member:" + member, "Go to " + member + (lit ? "\n(playing now)" : ""));
                x += w + U(5f);
            }
            return y + rowH + U(6f);
        }

        private static bool _allPlaysIn;
        private static Entry _playsInFor;
        private static List<PlaysInRow> _playsInRows = new List<PlaysInRow>();

        /// <summary>
        /// The effect lists a sound or effect is part of, one row per list: what it is for, who
        /// plays it, and what else it plays, all of which go where they name. Play plays the whole
        /// list: an effect's around it on the stage, a sound's where you are looking.
        /// </summary>
        private static float PlaysInSection(Explorer explorer, Entry entry, float width, float y)
        {
            if (_playsInFor != entry)
            {
                _playsInFor = entry;
                _allPlaysIn = false;

                // A list the entry itself plays is its own, not one it plays in; and it is not
                // named among what plays along.
                _playsInRows = EffectLinks.For(entry.Name)
                    .Select(r => new PlaysInRow
                    {
                        Label = r.Label, List = r.List,
                        Owners = r.Owners.Where(o => o.Key != entry.Name).ToList(),
                        Members = r.Members.Where(m => m != entry.Name).ToArray(),
                    })
                    .Where(r => r.Owners.Count > 0)
                    .ToList();
            }
            var rows = _playsInRows;
            if (rows.Count == 0) return y;

            y = SectionHeading($"PLAYS IN  {rows.Count}", width, y, null, "playsin");
            if (IsFolded("playsin")) return y;
            const int Shown = 8;
            var rowH = U(26f);

            foreach (var row in _allPlaysIn ? rows : rows.Take(Shown))
            {
                var list = row.List as EffectList;
                var playing = list != null && Previews.Playing.IsPlaying(list);

                // Play, what it is for, and who plays it.
                var x = 0f;
                var playText = "\u25B6 Play";
                var playW = Skin.Chip.CalcSize(new GUIContent(playText)).x + U(12f);
                if (list != null && GUI.Button(new Rect(x, y, playW, rowH), playText, playing ? Skin.ChipOn : Skin.Chip))
                {
                    if (playing) Previews.Stop(list);
                    else Previews.PlayWhole(entry, list);
                }
                if (new Rect(x, y, playW, rowH).Contains(Event.current.mousePosition)) AskTip("playrow:" + row.Label + row.Owners[0].Shown, "Play the whole list together");
                x += playW + U(8f);
                var labelW = Mathf.Min(width - x, Skin.Label.CalcSize(new GUIContent(row.Label)).x + U(4f));
                GUI.Label(new Rect(x, y, labelW, rowH), row.Label, Skin.Label);
                x += labelW + U(8f);

                const int Owners = 4;
                foreach (var owner in row.Owners.Take(Owners))
                {
                    var shown = ShownName(explorer, owner.Key, owner.Shown);
                    var go = owner.Key != null && CanGo(explorer, owner.Key);
                    var w = Mathf.Min(width, LinkChipWidth(shown, go));
                    if (x + w > width && x > 0f)
                    {
                        x = 0f;
                        y += rowH + U(5f);
                    }
                    var chip = new Rect(x, y, w, rowH);
                    var kind = owner.Key != null && owner.Key.StartsWith("se:", StringComparison.Ordinal) ? Kind.StatusEffect : KindOf(explorer, owner.Key);
                    if (LinkChip(chip, shown, kind, false, go)) Go(explorer, owner.Key);
                    if (go && chip.Contains(Event.current.mousePosition)) AskTip("owner:" + owner.Key, "Go to " + shown);
                    x += w + U(5f);
                }
                if (row.Owners.Count > Owners)
                {
                    var more = $"and {row.Owners.Count - Owners} more";
                    var w = Skin.DimLabel.CalcSize(new GUIContent(more)).x + U(4f);
                    if (x + w > width && x > 0f)
                    {
                        x = 0f;
                        y += rowH + U(5f);
                    }
                    var rect = new Rect(x, y, w, rowH);
                    GUI.Label(rect, more, Skin.DimLabel);
                    if (rect.Contains(Event.current.mousePosition)) AskTip("owners:" + row.Label + row.Owners[0].Shown, string.Join("\n", row.Owners.Skip(Owners).Take(30).Select(o => o.Shown)));
                }
                y += rowH + U(5f);

                // What plays along.
                if (list != null) y = Members(explorer, null, list, row.Members, entry.Name, width, y);
                y += U(6f);
            }

            if (rows.Count > Shown)
            {
                var text = _allPlaysIn ? "Show fewer" : $"Show all {rows.Count}";
                var w = Skin.Chip.CalcSize(new GUIContent(text)).x + U(8f);
                if (GUI.Button(new Rect(0f, y, Mathf.Min(width, w), rowH), text, Skin.Chip)) _allPlaysIn = !_allPlaysIn;
                y += rowH;
            }
            return y + U(14f);
        }

        /// <summary>Whether a prefab or status effect ("se:" name) is in the catalog to go to.</summary>
        private static bool CanGo(Explorer explorer, string key)
        {
            if (!key.StartsWith("se:", StringComparison.Ordinal)) return InCatalog(explorer, key);
            if (_statusFor != explorer)
            {
                _statusFor = explorer;
                StatusNames.Clear();
                foreach (var e in explorer.Catalog) if (e.Kind == Kind.StatusEffect) StatusNames.Add(e.Name);
            }
            return StatusNames.Contains(key.Substring(3));
        }

        private static readonly HashSet<string> StatusNames = new HashSet<string>();
        private static Explorer _statusFor;

        private static readonly Dictionary<string, string> ShownNames = new Dictionary<string, string>();
        private static Explorer _shownFor;

        /// <summary>A prefab's name as the game shows it, or the name given when it has none.</summary>
        private static string ShownName(Explorer explorer, string key, string fallback)
        {
            if (key == null) return fallback;
            if (_shownFor != explorer)
            {
                _shownFor = explorer;
                ShownNames.Clear();
                foreach (var e in explorer.Catalog)
                {
                    if (!string.IsNullOrEmpty(e.DisplayName) && !ShownNames.ContainsKey(e.Key)) ShownNames[e.Key] = e.DisplayName;
                }
            }
            return ShownNames.TryGetValue(key, out var shown) ? shown : fallback;
        }
    }
}
