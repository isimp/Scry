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
            y = SectionHeading(PanelWords.Heading("ANIMATIONS", clips.Count), width, y, null, "animations");
            if (IsFolded("animations")) return y;

            // The ground footsteps sound on, when the creature sounds different on some.
            var grounds = Previews.Grounds(explorer.Selected?.Source as GameObject);
            if (grounds.Count > 1)
            {
                var names = grounds.Select(g => g == FootStep.GroundMaterial.Default ? "Plain" : g == FootStep.GroundMaterial.GenericGround ? "Ground" : Naming.FieldLabel(g.ToString())).ToList();
                var chosen = Segments("Ground", names, Mathf.Max(0, grounds.IndexOf(Previews.StepGround)), width, labelW, ref y);
                if (chosen >= 0) Previews.StepGround = grounds[chosen];
            }

            var x = 0f;
            var rowH = U(26f);

            var stopEnabled = GUI.enabled;
            GUI.enabled = stopEnabled && playing != null;
            if (GUI.Button(new Rect(x, y, U(60f), rowH), "Stop", Skin.Chip) && playing != null) Previews.StopClip();
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

            // The clip playing, to pause and scrub through, like a sound. Its row stays, greyed out,
            // while none plays, so starting one moves nothing below it.
            {
                float time = 0f, length = 0f;
                var timed = playing != null && Previews.ClipPosition(out time, out length);
                var enabled = GUI.enabled;
                GUI.enabled = enabled && timed;
                var pauseW = U(84f);
                if (GUI.Button(new Rect(0f, y, pauseW, rowH), PanelWords.Pause(timed && Previews.ClipPaused), timed && Previews.ClipPaused ? Skin.ChipOn : Skin.Chip)) Previews.PauseClip(!Previews.ClipPaused);
                var readout = timed ? ClipWords.Readout(time, length) : "";
                var readW = Skin.Width(Skin.DimLabel, "00.00 / 00.00 s") + U(6f);
                var picked = TimeBar(pauseW + U(10f), y, Mathf.Max(U(40f), width - pauseW - readW - U(20f)), rowH, time, length);
                if (picked.HasValue && timed) Previews.SeekClip(picked.Value);
                GUI.Label(new Rect(width - readW, y, readW, rowH), readout, Skin.DimLabel);
                GUI.enabled = enabled;
                y += rowH + U(10f);
            }

            // Grouped by what they do: the attacks (of what is held, on a person with hundreds of
            // clips), then those that play sounds or effects, then the silent; within each, those
            // Scry knows what they are first. While that is still being worked out, they are
            // listed as they come, and it says so.
            var started = Timing.Start();
            var sorting = Previews.ClipsSorting;
            var shown = ClipRows(clips, sorting);
            var ownNow = Previews.AnimatorClipNow();
            Timing.Add("clips rows", started);
            // Each group shows its first chips and one for the rest (a person has hundreds of
            // clips); while filtering, every match.
            var totals = new Dictionary<int, int>();
            foreach (var row in shown) totals[row.Group] = totals.TryGetValue(row.Group, out var n) ? n + 1 : 1;
            var filtering = _clipFilter.Length > 0;
            _firstClip = shown.Count > 0 ? shown[0].Clip : null;
            var group = -1;
            var inGroup = 0;
            var limit = 0;
            var flow = new ChipFlow(0f, width, y, rowH, U(5f), U(5f));
            foreach (var row in shown)
            {
                var clip = row.Clip;
                if (row.Group != group)
                {
                    if (group >= 0 && !filtering) MoreChip("clips:" + Stored.Count(group), totals[group], FirstChips, width, ref flow);
                    y = flow.Below;
                    if (flow.InRow) y += U(4f);
                    group = row.Group;
                    inGroup = 0;
                    limit = filtering ? totals[group] : ShownOf("clips:" + Stored.Count(group), totals[group]);
                    GUI.Label(new Rect(0f, y, width, U(20f)), group < 3 ? ClipHeadings[group] : PanelWords.Waiting("Working out what each clip plays", Time.unscaledTime), Skin.DimLabel);
                    y += U(22f);
                    flow = new ChipFlow(0f, width, y, rowH, U(5f), U(5f));
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
                if (GUI.Button(chip, text, style))
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
            if (group >= 0 && !filtering) MoreChip("clips:" + Stored.Count(group), totals[group], FirstChips, width, ref flow);
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

        private static readonly Dictionary<string, string> NoTags = new Dictionary<string, string>();

        private static readonly string[] ClipHeadings = { "Attacks", "With sounds or effects", "Silent" };

        /// <summary>A clip's chip as the Animations section lists it: its name, its text and its group (3 while still being worked out).</summary>
        private sealed class ClipRow
        {
            public AnimationClip Clip;
            public string Name;
            public string Text;
            public int Group;
        }

        private static List<ClipRow> _clipRows = new List<ClipRow>();
        private static List<AnimationClip> _rowsClips;
        private static IReadOnlyDictionary<string, string> _rowsTags;
        private static bool _rowsSorting;
        private static string _rowsFilter;

        /// <summary>
        /// The clips' chips in the order they are listed, filtered. Worked out again only when the
        /// clips, what is known of them or the filter change, not on every event the panel draws.
        /// </summary>
        private static List<ClipRow> ClipRows(List<AnimationClip> clips, bool sorting)
        {
            var tags = sorting ? NoTags : Previews.ClipTags();
            if (ReferenceEquals(clips, _rowsClips) && ReferenceEquals(tags, _rowsTags) && sorting == _rowsSorting && _clipFilter == _rowsFilter) return _clipRows;

            var rows = new List<ClipRow>(clips.Count);
            var names = Previews.ClipNames();
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                var name = i < names.Count ? names[i] : clip.name;
                var tagged = tags.TryGetValue(name, out var tag);
                var text = ClipWords.Row(name, tag);
                if (_clipFilter.Length > 0 && text.IndexOf(_clipFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var group = sorting ? 3 : tagged && ClipWords.IsAttack(tag) ? 0 : Previews.ClipSounds(clip) ? 1 : 2;
                rows.Add(new ClipRow { Clip = clip, Name = name, Text = text, Group = group * 2 + (tagged ? 0 : 1) });
            }
            rows = rows.OrderBy(r => r.Group).ToList();
            foreach (var row in rows) row.Group /= 2;

            _clipRows = rows;
            _rowsClips = clips;
            _rowsTags = tags;
            _rowsSorting = sorting;
            _rowsFilter = _clipFilter;
            return rows;
        }

        /// <summary>Lets go of the clip rows made of the world left, and of the clip they were last drawn for.</summary>
        private static void ForgetAnimations()
        {
            _clipRows = new List<ClipRow>();
            _rowsClips = null;
            _rowsTags = null;
            _firstClip = null;
        }
    }
}
