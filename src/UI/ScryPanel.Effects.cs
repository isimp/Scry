using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The Effects section and the Plays in rows: effect lists, what they are made of and the clips they go with.</summary>
    internal static partial class ScryPanel
    {
        private static string _effectFilter = "";
        private static Entry _effectsEntry;
        private static List<GameObject> _effectsCarried;
        private static List<KeyValuePair<string, EffectList>> _effects;

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
            List<KeyValuePair<string, EffectList>> lists;
            if (ReferenceEquals(entry, _effectsEntry) && ReferenceEquals(carried, _effectsCarried) && _effects != null) lists = _effects;
            else
            {
                var cacheKey = entry.Key + "|" + (carried == null ? "all" : string.Join(",", carried.Select(c => c.name)));
                if (!EffectCache.TryGetValue(cacheKey, out lists))
                {
                    lists = carried == null ? Previews.PrefabLists(prefab) : Previews.PrefabLists(prefab, carried);
                    EffectCache[cacheKey] = lists;
                }
                _effectsEntry = entry;
                _effectsCarried = carried;
                _effects = lists;
            }
            if (lists.Count == 0) return y;

            y = SectionHeading("EFFECTS", width, y, null, "effects", lists.Count);
            if (IsFolded("effects")) return y;
            var rowH = U(26f);

            if (lists.Count > 12 || _effectFilter.Length > 0)
            {
                var field = new Rect(0f, y, Mathf.Min(width, U(260f)), U(28f));
                _effectFilter = FilterField(EffectControl, _effectFilter, field);
                y += U(36f);
            }

            // A long list shows its first chips and one for the rest; while filtering, every match.
            var flow = new ChipFlow(0f, width, y, rowH, U(5f), U(5f));
            var matching = _effectFilter.Length > 0 ? lists.Where(p => p.Key.IndexOf(_effectFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList() : lists;
            _firstEffect = matching.Count > 0 ? matching[0] : default;
            var count = _effectFilter.Length > 0 ? matching.Count : ShownOf("effects", matching.Count);
            for (var i = 0; i < count; i++)
            {
                var pair = matching[i];
                var w = Mathf.Min(width, Skin.Width(Skin.Chip, pair.Key) + U(8f));
                var at = flow.Place(w);
                var chip = new Rect(at.X, at.Y, w, rowH);
                if (OutOfSight(chip)) continue;
                var playing = Previews.Playing.IsPlaying(pair.Value);
                if (GUI.Button(chip, pair.Key, Skin.Fitted(playing ? Skin.ChipOn : Skin.Chip, chip)))
                {
                    if (playing) Previews.Stop(pair.Value);
                    else Previews.PlayEffectList(pair.Key, pair.Value);
                }
                if (playing && chip.Contains(Event.current.mousePosition)) AskTip("fx-stop:" + pair.Key, "Playing; click to stop it");
                if (chip.Contains(Event.current.mousePosition))
                {
                    AskTip("fx:" + pair.Key, Naming.Lines(EffectSlots.NamesPlayed(pair.Value)));
                }
            }
            if (_effectFilter.Length == 0) MoreChip("effects", matching.Count, FirstChips, width, ref flow);
            y = flow.Below;

            // What the list played last is made of, each part lit while its copy plays.
            var last = lists.FirstOrDefault(l => ReferenceEquals(l.Value, Previews.Playing.Last));
            if (last.Value != null)
            {
                y += U(10f);
                y = Members(explorer, PanelWords.In(last.Key), last.Value, EffectSlots.NamesPlayed(last.Value), null, width, y);

                // The clips it goes with, to play from here, once they are worked out.
                if (Previews.ClipsSorting)
                {
                    GUI.Label(new Rect(0f, y + U(2f), width, U(20f)), PanelWords.Waiting("Finding the clips it goes with", Time.unscaledTime), Skin.DimLabel);
                    y += U(24f);
                }
                else
                {
                    var withClips = Previews.ClipsOfList(last.Value);
                    if (withClips.Count > 0) y = ClipLinks(withClips, width, y + U(2f));
                }
            }

            return y + U(14f);
        }

        /// <summary>
        /// Chips for the clips an effect list goes with, each said how where it is not played
        /// with it (found by name, heard around), each playing its clip.
        /// </summary>
        private static float ClipLinks(List<(AnimationClip Clip, string How)> clips, float width, float y)
        {
            GUI.Label(new Rect(0f, y, width, U(20f)), ClipWords.With(clips.Count), Skin.DimLabel);
            var flow = new ChipFlow(0f, width, y + U(24f), U(26f), U(5f), U(5f));
            var playing = Previews.PlayingClip();
            foreach (var (clip, how) in clips)
            {
                var text = ClipWords.Row(clip.name, how);
                var on = playing == clip;
                var style = on ? Skin.ChipOn : Skin.Chip;
                var w = Mathf.Min(width, Skin.Width(style, text) + U(8f));
                var at = flow.Place(w);
                var chip = new Rect(at.X, at.Y, w, flow.RowHeight);
                if (GUI.Button(chip, text, Skin.Fitted(style, chip)))
                {
                    if (on) Previews.StopClip();
                    else
                    {
                        Previews.PlayClip(clip);
                        Previews.LastClip = clip;
                    }
                }
            }
            return flow.RowBottom + U(6f);
        }

        /// <summary>The effect list the filter shows first, for Enter in the filter box.</summary>
        private static KeyValuePair<string, EffectList> _firstEffect;

        private static void PlayFirstEffect()
        {
            if (_firstEffect.Value == null) return;
            Previews.PlayEffectList(_firstEffect.Key, _firstEffect.Value);
        }

        /// <summary>
        /// The parts of a list as chips: each goes to its prefab, and is lit while the copy of it
        /// the list last started still plays. The selected prefab itself is shown but not a link.
        /// </summary>
        private static float Members(Explorer explorer, string title, object list, IReadOnlyList<string> members, string self, float width, float y)
        {
            if (title != null)
            {
                GUI.Label(new Rect(0f, y, width, U(20f)), title, Skin.DimLabel);
                y += U(24f);
            }
            var flow = new ChipFlow(0f, width, y, U(26f), U(5f), U(5f));
            var key = MembersKey(title, list);
            var count = ShownOf(key, members.Count);
            for (var i = 0; i < count; i++)
            {
                var member = members[i];
                var go = member != self && InCatalog(explorer, member);
                var w = Mathf.Min(width, LinkChipWidth(member, go));
                var at = flow.Place(w);
                var chip = new Rect(at.X, at.Y, w, flow.RowHeight);
                if (OutOfSight(chip)) continue;
                var lit = Previews.Playing.IsPlaying(list, member);
                if (LinkChip(chip, member, KindOf(explorer, member), lit, go)) Go(explorer, member);
                if (go && chip.Contains(Event.current.mousePosition)) AskTip("member:" + member, PanelWords.GoTo(member, lit));
            }
            MoreChip(key, members.Count, FirstChips, width, ref flow);
            // No row is kept for parts there are none of.
            return members.Count > 0 ? flow.Below + U(6f) : flow.Below;
        }

        private static readonly Dictionary<(string, object), string> MemberKeys = new Dictionary<(string, object), string>();

        /// <summary>The key a list of members' "more" is kept under, made once for each list rather than every event.</summary>
        private static string MembersKey(string title, object list)
        {
            if (MemberKeys.TryGetValue((title, list), out var key)) return key;
            if (MemberKeys.Count > 5000) MemberKeys.Clear();
            return MemberKeys[(title, list)] = "members:" + (title ?? "") + (list != null ? Stored.Count(list.GetHashCode()) : "");
        }

        private static readonly List<string> OwnerKeys = new List<string>();

        private static string OwnersKey(int index)
        {
            while (OwnerKeys.Count <= index) OwnerKeys.Add("owners:" + Stored.Count(OwnerKeys.Count));
            return OwnerKeys[index];
        }

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
                foreach (var row in _playsInRows)
                {
                    row.Names = ChipNames.Apart(row.Owners.Select(o => (o.Key, ShownName(explorer, o.Key, o.Shown))).ToList());
                }
            }
            var rows = _playsInRows;
            if (rows.Count == 0) return y;

            y = SectionHeading("PLAYS IN", width, y, null, "playsin", rows.Count);
            if (IsFolded("playsin")) return y;
            // Each row is a list of its own, so fewer show before the rest are asked for.
            const int firstRows = 8;
            var rowH = U(26f);
            var index = 0;

            foreach (var row in rows.Take(ShownOf("playsin", rows.Count, firstRows)))
            {
                // A faint rule between rows, so where one list ends and the next begins is plain.
                if (index > 0)
                {
                    Skin.Fill(new Rect(0f, y, width, U(1f)), Skin.Outline);
                    y += U(10f);
                }
                index++;
                var list = row.List as EffectList;
                var playing = list != null && Previews.Playing.IsPlaying(list);

                // Play, what it is for, and who plays it.
                var x = 0f;
                var playText = "\u25B6 Play";
                var playW = Skin.Width(Skin.Chip, playText) + U(12f);
                var playChip = new Rect(x, y, playW, rowH);
                if (list != null && GUI.Button(playChip, playText, Skin.Fitted(playing ? Skin.ChipOn : Skin.Chip, playChip)))
                {
                    if (playing) Previews.Stop(list);
                    else Previews.PlayWhole(entry, list);
                }
                if (new Rect(x, y, playW, rowH).Contains(Event.current.mousePosition)) AskTip("playrow:" + row.Label + row.Owners[0].Shown, "Play the whole list together");
                x += playW + U(8f);
                var labelW = Mathf.Min(width - x, Skin.Width(Skin.Label, row.Label) + U(4f));
                GUI.Label(new Rect(x, y, labelW, rowH), row.Label, Skin.Label);
                x += labelW + U(8f);

                const int firstOwners = 4;
                var ownersKey = OwnersKey(index);
                var shownOwners = Mathf.Min(ShownOf(ownersKey, row.Owners.Count, firstOwners), row.Owners.Count);
                var owners = new ChipFlow(0f, width, y, rowH, U(5f), U(5f), start: x);
                for (var o = 0; o < shownOwners; o++)
                {
                    var owner = row.Owners[o];
                    var shown = row.Names != null && o < row.Names.Length ? row.Names[o] : ShownName(explorer, owner.Key, owner.Shown);
                    var go = owner.Key != null && InCatalog(explorer, owner.Key);
                    var w = Mathf.Min(width, LinkChipWidth(shown, go));
                    var at = owners.Place(w);
                    var chip = new Rect(at.X, at.Y, w, rowH);
                    var kind = KindOf(explorer, owner.Key);
                    if (LinkChip(chip, shown, kind, false, go)) Go(explorer, owner.Key);
                    if (go && chip.Contains(Event.current.mousePosition)) AskTip("owner:" + owner.Key, PanelWords.GoTo(shown));
                }
                MoreChip(ownersKey, row.Owners.Count, firstOwners, width, ref owners);
                y = owners.RowBottom + U(5f);

                // What plays along.
                if (list != null) y = Members(explorer, null, list, row.Members, entry.Name, width, y);
                y += U(6f);
            }

            var more = new ChipFlow(0f, width, y, rowH, U(5f), U(5f));
            MoreChip("playsin", rows.Count, firstRows, width, ref more);
            return more.Below + U(14f);
        }

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

        /// <summary>Lets go of the effect lists made of the world left, and of what they were last drawn for.</summary>
        private static void ForgetEffects()
        {
            EffectCache.Clear();
            _effectsEntry = null;
            _effectsCarried = null;
            _effects = null;
            ShownNames.Clear();
            _playsInRows = new List<PlaysInRow>();
            _playsInFor = null;
            _shownFor = null;
            MemberKeys.Clear();
            _firstEffect = default;
        }
    }
}
