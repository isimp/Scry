using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>A sound's timeline and its variants.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>
        /// Where the playing sound is in its clip, as a bar that can be dragged to any point. Shown
        /// while a sound plays or is paused, which is what makes long clips such as music usable.
        /// </summary>
        private static float Timeline(float width, float y)
        {
            if (!Previews.SoundPosition(out var time, out var length)) return y;

            var rowH = U(26f);
            var labelW = U(52f);
            GUI.Label(new Rect(0f, y, labelW, rowH), Clock(time), Skin.Label);
            var slider = new Rect(labelW, y + (rowH - U(14f)) / 2f, width - labelW * 2f - U(8f), U(14f));

            GUI.changed = false;
            var picked = GUI.HorizontalSlider(slider, time, 0f, length);
            if (GUI.changed) Previews.SeekSound(picked);

            var end = new Rect(width - labelW, y, labelW, rowH);
            var style = Skin.DimLabel;
            var anchor = style.alignment;
            style.alignment = TextAnchor.MiddleRight;
            GUI.Label(end, Clock(length), style);
            style.alignment = anchor;

            return y + rowH + U(14f);
        }

        private static string Clock(float seconds)
        {
            var whole = Mathf.Max(0, Mathf.FloorToInt(seconds));
            if (seconds < 10f) return seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
            return $"{whole / 60}:{whole % 60:00}";
        }

        private static readonly Dictionary<Entry, List<AudioClip>> VariantCache = new Dictionary<Entry, List<AudioClip>>();

        private static List<AudioClip> Variants(Entry entry)
        {
            if (!VariantCache.TryGetValue(entry, out var clips))
            {
                clips = Previews.SoundVariants(entry.Source as GameObject);
                VariantCache[entry] = clips;
            }
            return clips;
        }

        /// <summary>
        /// Every clip the sound can play. The game picks one at random each time; here each can be
        /// played on its own, and the one heard last, chosen or picked, is lit.
        /// </summary>
        private static float Variants(Entry entry, float width, float y)
        {
            var clips = Variants(entry);
            if (clips.Count < 2) return y;

            y = SectionHeading($"VARIANTS  {clips.Count}", width, y, null, "variants");
            if (IsFolded("variants")) return y;
            var now = Previews.SoundClipNow();
            var x = 0f;
            var rowH = U(28f);

            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                var text = $"{i + 1}   {clip.name}";
                var style = clip == now ? Skin.ChipOn : Skin.Chip;
                var w = Mathf.Min(width, Skin.Width(style, text) + U(8f));
                if (x + w > width && x > 0f)
                {
                    x = 0f;
                    y += rowH + U(5f);
                }
                var chip = new Rect(x, y, w, rowH);
                if (GUI.Button(chip, text, style)) Previews.PlaySound(entry, clip);
                if (chip.Contains(Event.current.mousePosition))
                {
                    AskTip("variant:" + clip.name, $"{clip.name}\n{clip.length.ToString("0.00", CultureInfo.InvariantCulture)} s");
                }
                x += w + U(5f);
            }

            return y + rowH + U(16f);
        }
    }
}
