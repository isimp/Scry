using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The Animations section: clips, their timeline and what each plays.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>
        /// Every animation clip the creature has, each played directly on the copy. The one playing
        /// is lit; Stop hands the copy back to its own animations.
        /// </summary>
        private static float Clips(Explorer explorer, List<AnimationClip> clips, Modifiers modifiers, float width, float labelW, float y)
        {
            _groundsFor = explorer.Selected;
            var playing = Previews.PlayingClip();
            y = SectionHeading($"ANIMATIONS  {clips.Count}", width, y, null, "animations");
            if (IsFolded("animations")) return y;

            var speed = SliderRow("Speed", $"×{modifiers.AnimationSpeed.ToString("0.0", CultureInfo.InvariantCulture)}", modifiers.AnimationSpeed, 0f, Modifiers.MaxAnimationSpeed, width, labelW, ref y);
            if (!Mathf.Approximately(speed, modifiers.AnimationSpeed)) modifiers.AnimationSpeed = speed;

            // The ground footsteps sound on, when the creature sounds different on some.
            var grounds = Previews.Grounds(_groundsFor?.Source as GameObject);
            if (grounds.Count > 1)
            {
                var names = grounds.Select(g => g == FootStep.GroundMaterial.Default ? "Plain" : g == FootStep.GroundMaterial.GenericGround ? "Ground" : Naming.FieldLabel(g.ToString())).ToList();
                var chosen = Segments("Ground", names, Mathf.Max(0, grounds.IndexOf(Previews.StepGround)), width, labelW, ref y);
                if (chosen >= 0) Previews.StepGround = grounds[chosen];
            }

            var x = 0f;
            var rowH = U(26f);

            if (GUI.Button(new Rect(x, y, U(84f), rowH), Previews.LoopClips ? "Repeat on" : "Repeat off", Previews.LoopClips ? Skin.ChipOn : Skin.Chip)) Previews.ToggleLoopClips();
            x += U(90f);
            if (playing != null && GUI.Button(new Rect(x, y, U(60f), rowH), "Stop", Skin.Chip)) Previews.StopClip();
            x += U(66f);

            if (clips.Count > 12)
            {
                if (width - x < U(150f))
                {
                    x = 0f;
                    y += rowH + U(6f);
                }
                var field = new Rect(x, y - U(1f), Mathf.Min(width - x, U(260f)), U(28f));
                GUI.SetNextControlName(ClipControl);
                _clipFilter = GUI.TextField(field, _clipFilter, 40, Skin.Field);
                if (string.IsNullOrEmpty(_clipFilter)) GUI.Label(field, "Filter", Skin.Placeholder);
            }
            y += rowH + U(10f);

            // The clip playing, to pause and scrub through, like a sound.
            if (playing != null && Previews.ClipPosition(out var time, out var length))
            {
                var pauseW = U(84f);
                if (GUI.Button(new Rect(0f, y, pauseW, rowH), Previews.ClipPaused ? "Resume" : "Pause", Previews.ClipPaused ? Skin.ChipOn : Skin.Chip)) Previews.PauseClip(!Previews.ClipPaused);
                var readout = $"{time.ToString("0.00", CultureInfo.InvariantCulture)} / {length.ToString("0.00", CultureInfo.InvariantCulture)} s";
                var readW = Skin.DimLabel.CalcSize(new GUIContent(readout)).x + U(6f);
                var slider = new Rect(pauseW + U(10f), y + (rowH - U(14f)) / 2f, Mathf.Max(U(40f), width - pauseW - readW - U(20f)), U(14f));
                var picked = GUI.HorizontalSlider(slider, time, 0f, length);
                if (!Mathf.Approximately(picked, time)) Previews.SeekClip(picked);
                GUI.Label(new Rect(width - readW, y, readW, rowH), readout, Skin.DimLabel);
                y += rowH + U(10f);
            }

            // What Scry knows a clip to be comes first, the attacks of what is held before all:
            // among a person's hundreds of clips, those of the item tried on.
            x = 0f;
            var tags = Previews.ClipTags();
            int Rank(AnimationClip c) => !tags.TryGetValue(c.name, out var t) ? 2 : t.StartsWith("attack") ? 0 : 1;
            foreach (var clip in clips.OrderBy(Rank).ToList())
            {
                // Named by the modelers; what it is follows, as far as Scry saw.
                var text = tags.TryGetValue(clip.name, out var tag) ? clip.name + "  ·  " + tag : clip.name;
                if (_clipFilter.Length > 0 && text.IndexOf(_clipFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                var on = playing == clip;
                var style = on ? Skin.ChipOn : Skin.Chip;
                var w = Mathf.Min(width, style.CalcSize(new GUIContent(text)).x + U(8f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                var chip = new Rect(x, y, w, rowH);
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
                    AskTip("clip:" + clip.name, $"{clip.name}\n{clip.length.ToString("0.0", CultureInfo.InvariantCulture)} s{(clip.isLooping ? ", loops" : "")}");
                }
                x += w + U(5f);
            }
            if (x > 0f) y += rowH;

            var last = Previews.LastClip;
            if (last != null && clips.Contains(last))
            {
                var members = Previews.ClipMembers(last).ToArray();
                if (members.Length > 0)
                {
                    y += U(10f);
                    y = Members(explorer, "In " + last.name + ":", last, members, null, width, y);
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
    }
}
