using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The Animations section: clips, their timeline and what each plays.</summary>
    internal static partial class ScryPanel
    {
        private static string _clipFilter = "";

        /// <summary>The clip the filter shows first, for Enter in the filter box.</summary>
        private static AnimationClip _firstClip;

        private static void PlayFirstClip()
        {
            if (_firstClip == null) return;
            Previews.PlayClip(_firstClip);
            Previews.LastClip = _firstClip;
        }

        /// <summary>
        /// Every animation clip the creature has, each played directly on the copy. The one playing
        /// is lit; Stop hands the copy back to its own animations.
        /// </summary>
        private static float Clips(Explorer explorer, List<AnimationClip> clips, Modifiers modifiers, float width, float labelW, float y)
        {
            var playing = Previews.PlayingClip();
            y = SectionHeading("ANIMATIONS", width, y, null, "animations", ClipsCounted(clips));
            if (IsFolded("animations")) return y;

            var x = 0f;
            var rowH = U(26f);

            var stopEnabled = GUI.enabled;
            GUI.enabled = stopEnabled && playing != null;
            var stopChip = new Rect(x, y, U(60f), rowH);
            if (GUI.Button(stopChip, "Stop", Skin.Fitted(Skin.Chip, stopChip)) && playing != null) Previews.StopClip();
            GUI.enabled = stopEnabled;
            x += U(66f);

            // Shown while it filters, even where there are few clips to filter.
            if (clips.Count > 12 || _clipFilter.Length > 0)
            {
                if (width - x < U(150f))
                {
                    x = 0f;
                    y += rowH + U(6f);
                }
                var field = new Rect(x, y - U(1f), Mathf.Min(width - x, U(260f)), U(28f));
                _clipFilter = FilterField(ClipControl, _clipFilter, field);
            }
            y += rowH + U(10f);

            y = PlayingClipRow(playing, width, rowH, y);

            // Grouped by what they are for (ClipGroups): the attacks of what is held, moving with
            // it, blocking, jumping, getting hurt, emotes, resting, working; within each, those
            // Scry knows what they are first. While that is still being worked out, they are
            // listed as they come, and it says so.
            var started = Timing.Start();
            var sorting = Previews.ClipsSorting;
            var shown = ClipRows(clips, sorting);
            var ownNow = Previews.AnimatorClipNow();
            Timing.Add("clips rows", started);
            // Each group shows its first chips (FirstOf) and one for the rest; while filtering, every match.
            Previews.ClipGroupsNow(out var stance);
            // Under the strict rules an attack's clips wait for the attack, as its damage does: one
            // faint chip counts those that wait, in their group's place, and they count nowhere else.
            var totals = new Dictionary<int, int>();
            foreach (var row in shown) totals[row.Group] = (totals.TryGetValue(row.Group, out var n) ? n : 0) + (explorer.KeepsBackAttackClip(row.Item) ? 0 : 1);
            var waiting = shown.Count(r => explorer.KeepsBackAttackClip(r.Item));
            var filtering = _clipFilter.Length > 0;
            // Enter plays the first clip the rules let play.
            _firstClip = shown.FirstOrDefault(r => !explorer.KeepsBackAttackClip(r.Item))?.Clip;
            var group = -1;
            var inGroup = 0;
            var limit = 0;
            var flow = new ChipFlow(0f, width, y, rowH, U(5f), U(5f));
            foreach (var row in shown)
            {
                var clip = row.Clip;
                if (row.Group != group)
                {
                    if (group >= 0 && !filtering) MoreChip("clips:" + Stored.Count(group), totals[group], FirstOf(group), width, ref flow);
                    y = flow.Below;
                    if (flow.InRow) y += U(4f);
                    group = row.Group;
                    inGroup = 0;
                    limit = filtering ? totals[group] : ShownOf("clips:" + Stored.Count(group), totals[group], FirstOf(group));
                    GUI.Label(new Rect(0f, y, width, U(20f)), ClipHeading(group, totals[group], stance), Skin.DimLabel);
                    y += U(22f);
                    flow = new ChipFlow(0f, width, y, rowH, U(5f), U(5f));
                }

                if (explorer.KeepsBackAttackClip(row.Item))
                {
                    if (waiting > 0) WaitingClipsChip(waiting, width, rowH, ref flow);
                    waiting = 0;
                    continue;
                }
                if (inGroup++ >= limit) continue;

                // Named by the modelers; what it is follows, as far as Scry saw. The one the
                // animator plays on its own right now is marked.
                var text = clip == ownNow ? ClipWords.Now(row.Text) : row.Text;

                var on = playing == clip;
                var style = on ? Skin.ChipOn : Skin.Chip;
                // Rows below what shows are measured a few a frame; until then their width is told
                // from their length, which only moves rows out of sight.
                started = Timing.Start();
                var below = flow.Y > _sideVisible.yMax;
                var w = Mathf.Min(width, (below ? Skin.WidthSoon(style, text) : Skin.Width(style, text)) + U(8f));
                Timing.Add("clips measured", started);
                var at = flow.Place(w);
                var chip = new Rect(at.X, at.Y, w, rowH);
                if (OutOfSight(chip)) continue;
                ClipChip(row, chip, text, style, on, ownNow);
            }
            if (group >= 0 && !filtering) MoreChip("clips:" + Stored.Count(group), totals[group], FirstOf(group), width, ref flow);
            y = flow.Below;

            var last = Previews.LastClip;
            if (last != null && clips.Contains(last) && !sorting)
            {
                var members = Previews.ClipMembers(last).ToArray();
                if (members.Length > 0)
                {
                    y += U(10f);
                    y = Members(explorer, PanelWords.In(last.name), last, members, null, width, y);
                }

                // Paired by the clip's name alone, where the animator could not be seen to go there.
                var named = Previews.ClipByNameMembers(last).ToArray();
                if (named.Length > 0)
                {
                    y += U(members.Length > 0 ? 2f : 10f);
                    y = Members(explorer, "Found by name:", last, named, null, width, y);
                }

                // What the game does not play with the clip but plays around it: next, or now and then.
                var around = Previews.ClipAroundMembers(last).ToArray();
                if (around.Length > 0)
                {
                    y += U(members.Length > 0 || named.Length > 0 ? 2f : 10f);
                    y = Members(explorer, "Heard around it:", last, around, null, width, y);
                }
            }

            return y + U(10f);
        }

        /// <summary>How many clips the heading counts: not those it never plays, nor those it plays only with other gear in hand (<see cref="ClipLeftovers"/>, <see cref="HeldClips"/>).</summary>
        private static int ClipsCounted(List<AnimationClip> clips) =>
            clips.Count - (Previews.ClipsUnplayed()?.Count ?? 0) - (Previews.ClipsOtherGear()?.Count ?? 0);

        /// <summary>The clip playing, to pause and scrub through, like a sound. Its row stays, greyed out, while none plays, so starting one moves nothing below it.</summary>
        private static float PlayingClipRow(AnimationClip playing, float width, float rowH, float y)
        {
            float time = 0f, length = 0f;
            var timed = playing != null && Previews.ClipPosition(out time, out length);
            var enabled = GUI.enabled;
            GUI.enabled = enabled && timed;
            var pauseW = U(84f);
            var pauseChip = new Rect(0f, y, pauseW, rowH);
            if (GUI.Button(pauseChip, PanelWords.Pause(timed && Previews.ClipPaused), Skin.Fitted(timed && Previews.ClipPaused ? Skin.ChipOn : Skin.Chip, pauseChip))) Previews.PauseClip(!Previews.ClipPaused);
            var readout = timed ? ClipWords.Readout(time, length) : "";
            var readW = Skin.Width(Skin.DimLabel, "00.00 / 00.00 s") + U(6f);
            var picked = TimeBar(pauseW + U(10f), y, Mathf.Max(U(40f), width - pauseW - readW - U(20f)), rowH, time, length);
            if (picked.HasValue && timed) Previews.SeekClip(picked.Value);
            GUI.Label(new Rect(width - readW, y, readW, rowH), readout, Skin.DimLabel);
            GUI.enabled = enabled;
            y += rowH + U(10f);
            return y;
        }

        /// <summary>One faint chip counting an attack group's clips kept back under the strict rules, placed in the group's flow, its hover saying what frees them.</summary>
        private static void WaitingClipsChip(int waiting, float width, float rowH, ref ChipFlow flow)
        {
            var text = ClipWords.ClipsWait(waiting);
            var w = Mathf.Min(width, Skin.Width(Skin.Chip, text) + U(8f));
            var at = flow.Place(w);
            var chip = new Rect(at.X, at.Y, w, rowH);
            if (OutOfSight(chip)) return;
            var enabled = GUI.enabled;
            GUI.enabled = false;
            GUI.Button(chip, text, Skin.Fitted(Skin.Chip, chip));
            GUI.enabled = enabled;
            if (chip.Contains(Event.current.mousePosition)) AskTip("clips-wait", ClipWords.KeptClipsTip);
            CountDrawn(PanelPart.ClipKeptBack);
        }

        /// <summary>A clip's chip: a click plays or stops it and its hover tells its length.</summary>
        private static void ClipChip(ClipRow row, Rect chip, string text, GUIStyle style, bool on, AnimationClip ownNow)
        {
            var clip = row.Clip;
            if (GUI.Button(chip, text, Skin.Fitted(style, chip)))
            {
                if (on) Previews.StopClip();
                else
                {
                    Previews.PlayClip(clip);
                    Previews.LastClip = clip;
                }
            }
            if (chip.Contains(Event.current.mousePosition))
            {
                AskTip("clip:" + row.Name + (clip == ownNow ? ":now" : ""), ClipWords.Tip(row.Name, clip.length, clip.isLooping, clip == ownNow));
            }
        }

        private static readonly Dictionary<string, string> NoTags = new Dictionary<string, string>();

        /// <summary>
        /// A group of clips' heading: what they are for and how many (<see cref="ClipGroup"/>), moving
        /// with what is held; while being worked out, that it is; and last, while filtering, those of
        /// other gear and those it never plays.
        /// </summary>
        private static string ClipHeading(int group, int clips, int stance) =>
            group <= (int)ClipGroup.Other ? ClipWords.GroupHeading((ClipGroup)group, clips, stance) : group == OtherGearGroup ? ClipWords.OtherGearHeading
            : group == UnplayedGroup ? ClipWords.UnplayedHeading : PanelWords.Waiting("Working out what each clip plays", Time.unscaledTime);

        /// <summary>How many of a group's chips show before the one for the rest: more of its attacks and its moving than of the rest.</summary>
        private static int FirstOf(int group) => group <= (int)ClipGroup.Moving ? GroupFirstChips : FewFirstChips;

        private const int GroupFirstChips = 8;
        private const int FewFirstChips = 4;

        /// <summary>The group of the clips it plays only with other gear in hand, listed only while filtering.</summary>
        private const int OtherGearGroup = 20;

        /// <summary>The group of the clips it never plays, listed only while filtering, last.</summary>
        private const int UnplayedGroup = 21;

        /// <summary>The group of every clip while what each is for is still being worked out.</summary>
        private const int SortingGroup = 30;

        /// <summary>A clip's chip as the Animations section lists it: its name, its text and its group (a <see cref="ClipGroup"/>; else one only other gear plays, one it never plays, or any while still being worked out).</summary>
        private sealed class ClipRow
        {
            public AnimationClip Clip;
            public string Name;
            public string Text;
            public int Group;

            /// <summary>The prefab of the item whose attack the clip plays; none for a clip that is no attack's.</summary>
            public string Item;
        }

        private static List<ClipRow> _clipRows = new List<ClipRow>();
        private static List<AnimationClip> _rowsClips;
        private static IReadOnlyDictionary<string, string> _rowsTags;
        private static bool _rowsSorting;
        private static string _rowsFilter;
        private static HashSet<string> _rowsUnplayed;
        private static IReadOnlyDictionary<string, int> _rowsOtherGear;
        private static IReadOnlyDictionary<string, ClipGroup> _rowsGroups;

        /// <summary>
        /// The clips' chips in the order they are listed, filtered. Worked out again only when the
        /// clips, what is known of them or the filter change, not on every event the panel draws.
        /// </summary>
        private static List<ClipRow> ClipRows(List<AnimationClip> clips, bool sorting)
        {
            var tags = sorting ? NoTags : Previews.ClipTags();
            var unplayed = sorting ? null : Previews.ClipsUnplayed();
            var otherGear = sorting ? null : Previews.ClipsOtherGear();
            var groups = sorting ? null : Previews.ClipGroupsNow(out _);
            if (ReferenceEquals(clips, _rowsClips) && ReferenceEquals(tags, _rowsTags) && sorting == _rowsSorting && _clipFilter == _rowsFilter
                && ReferenceEquals(unplayed, _rowsUnplayed) && ReferenceEquals(otherGear, _rowsOtherGear) && ReferenceEquals(groups, _rowsGroups)) return _clipRows;
            var items = sorting ? NoTags : Previews.ClipAttackItems();

            var rows = new List<ClipRow>(clips.Count);
            var names = Previews.ClipNames();
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                var name = i < names.Count ? names[i] : clip.name;
                var tagged = tags.TryGetValue(name, out var tag);
                var text = ClipWords.Row(name, tag);
                if (_clipFilter.Length > 0 && text.IndexOf(_clipFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                // One it never plays, or plays only with other gear in hand, shows only to a filter, last, saying so.
                var never = unplayed != null && unplayed.Contains(name);
                var stance = -1;
                var withOther = !never && otherGear != null && otherGear.TryGetValue(name, out stance);
                if ((never || withOther) && _clipFilter.Length == 0) continue;
                if (never) text = ClipWords.Unplayed(text);
                else if (withOther) text = ClipWords.OtherGear(text, stance);
                var group = sorting ? SortingGroup : never ? UnplayedGroup : withOther ? OtherGearGroup
                    : groups != null && groups.TryGetValue(name, out var sorted) ? (int)sorted : tagged && ClipWords.IsAttack(tag) ? (int)ClipGroup.Attacks : (int)ClipGroup.Other;
                rows.Add(new ClipRow { Clip = clip, Name = name, Text = text, Group = group * 2 + (tagged ? 0 : 1), Item = items.TryGetValue(name, out var item) ? item : null });
            }
            rows = rows.OrderBy(r => r.Group).ToList();
            foreach (var row in rows) row.Group /= 2;

            _clipRows = rows;
            _rowsClips = clips;
            _rowsTags = tags;
            _rowsSorting = sorting;
            _rowsFilter = _clipFilter;
            _rowsUnplayed = unplayed;
            _rowsOtherGear = otherGear;
            _rowsGroups = groups;
            return rows;
        }

        /// <summary>The clips' chips as the Animations section lists them for the stage copy, filtered by the text given, with each one's group.</summary>
        [ForTest]
        internal static List<(string Text, int Group)> ClipRowsForTest(string filter)
        {
            var kept = _clipFilter;
            _clipFilter = filter ?? "";
            try
            {
                return ClipRows(Previews.Clips(), Previews.ClipsSorting).Select(r => (r.Text, r.Group)).ToList();
            }
            finally
            {
                _clipFilter = kept;
            }
        }

        /// <summary>Lets go of the clip rows made of the world left, and of the clip they were last drawn for.</summary>
        private static void ForgetAnimations()
        {
            _clipRows = new List<ClipRow>();
            _rowsClips = null;
            _rowsTags = null;
            _rowsUnplayed = null;
            _rowsOtherGear = null;
            _rowsGroups = null;
            _firstClip = null;
        }
    }
}
