using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>A sound's timeline and its variants.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>
        /// Where the playing sound is in its clip, as a bar that can be dragged to any point, which
        /// is what makes long clips such as music usable. Its row stays, greyed out, while nothing
        /// plays, so playing a sound moves nothing below it.
        /// </summary>
        private static float Timeline(float width, float y)
        {
            float time = 0f, length = 0f;
            var timed = Previews.SoundPosition(out time, out length);
            var enabled = GUI.enabled;
            GUI.enabled = enabled && timed;
            try
            {
                return TimelineRow(width, y, time, length, timed);
            }
            finally
            {
                GUI.enabled = enabled;
            }
        }

        private static float TimelineRow(float width, float y, float time, float length, bool timed)
        {
            var rowH = U(26f);
            var labelW = U(52f);
            GUI.Label(new Rect(0f, y, labelW, rowH), Clock(time), Skin.Label);
            var picked = TimeBar(labelW, y, width - labelW * 2f - U(8f), rowH, time, length);
            if (picked.HasValue && timed) Previews.SeekSound(picked.Value);

            GUI.Label(new Rect(width - labelW, y, labelW, rowH), timed ? Clock(length) : "", Skin.DimRight);

            return y + rowH + U(14f);
        }

        private static string Clock(float seconds) => SoundWords.Clock(seconds);

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

            y = SectionHeading(PanelWords.Heading("VARIANTS", clips.Count), width, y, null, "variants");
            if (IsFolded("variants")) return y;
            var now = Previews.SoundClipNow();
            var flow = new ChipFlow(0f, width, y, U(28f), U(5f), U(5f));

            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                var text = SoundWords.Variant(i, clip.name);
                var style = clip == now ? Skin.ChipOn : Skin.Chip;
                var w = Mathf.Min(width, Skin.Width(style, text) + U(8f));
                var at = flow.Place(w);
                var chip = new Rect(at.X, at.Y, w, flow.RowHeight);
                if (OutOfSight(chip)) continue;
                if (GUI.Button(chip, text, style)) Previews.PlaySound(entry, clip);
                if (chip.Contains(Event.current.mousePosition))
                {
                    AskTip("variant:" + clip.name, SoundWords.VariantTip(clip.name, clip.length));
                }
            }

            return flow.RowBottom + U(16f);
        }

        /// <summary>Lets go of the sound variants found in the world left.</summary>
        private static void ForgetSounds()
        {
            VariantCache.Clear();
        }
    }
}
