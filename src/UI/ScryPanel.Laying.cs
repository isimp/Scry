using System;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A page opened for the first time, drawn while it is laid out over the frames after the
    /// click (<see cref="PageStaging"/>): each part not laid yet keeps its place with a line saying
    /// it is being laid or waits, the finds chip counts once the page is whole, and the stage says
    /// while it still lays its ground's grass.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>A section of the page drawn once its turn to be laid has come; nothing until then.</summary>
        private static float Laid(string part, float y, Func<float, float> draw) => Staging.Lay() ? Section(part, y, draw) : y;

        /// <summary>The line saying a part is being laid or waits: the accent's bar, faint words.</summary>
        private static float WaitingLine(string said, float x, float width, float y)
        {
            Skin.Fill(new Rect(x, y + U(3f), U(2f), U(16f)), Skin.Alpha(Skin.Accent, 0.6f));
            GUI.Label(new Rect(x + U(10f), y, Mathf.Max(0f, width - U(10f)), U(22f)), Skin.Fit(Skin.FaintLabel, said, width - U(10f)), Skin.FaintLabel);
            CountDrawn(PanelPart.Waiting);
            return y + U(22f);
        }

        /// <summary>The facts' section while they are worked out: its heading and what it is doing.</summary>
        private static float WaitingFacts(float width, float y)
        {
            GUI.Label(new Rect(0f, y, width, U(20f)), "IN THE GAME", Skin.Heading);
            y += U(26f);
            return WaitingLine(PanelWords.WorkingOutFacts, 0f, width, y) + U(14f);
        }

        /// <summary>A topic not laid yet: its box and heading where it will stand, and whether it is being laid or waits.</summary>
        private static float WaitingTopic(string heading, float width, float y)
        {
            var top = y + U(6f);
            var height = U(56f);
            Skin.Box(new Rect(0f, top, width, height), Skin.TopicFill);
            var pad = U(10f);
            if (heading != null) GUI.Label(new Rect(pad, top + U(6f), width - pad * 2f, U(20f)), heading, Skin.TopicName);
            WaitingLine(PanelWords.Laying(Staging.HeldFirst), pad, width - pad * 2f, top + U(30f));
            return top + height + U(4f);
        }

        /// <summary>The line at the page's end while sections below are still to be laid.</summary>
        private static float WaitingRest(float width, float y) => Staging.HeldAny ? WaitingLine(PanelWords.LayingTheRest, 0f, width, y + U(4f)) : y;

        /// <summary>The stage's caption while its ground's grass is scattered, with how far it has got.</summary>
        private static void GrassCaption(Rect stage)
        {
            var biome = Stage.GroundUnderfoot;
            if (!Stage.GrassScattering || biome == null || Event.current.type != EventType.Repaint) return;
            var text = PanelWords.LayingGrass(biome);
            var height = U(20f);
            var bar = U(28f);
            var width = Skin.Width(Skin.Small, text) + bar + U(24f);
            var at = new Rect(stage.x + U(10f), stage.yMax - height - U(8f), Mathf.Min(width, stage.width - U(20f)), height);
            Skin.Box(at, Skin.Alpha(Skin.Backdrop, 0.6f));
            var track = new Rect(at.x + U(9f), at.y + (height - U(4f)) / 2f, bar, U(4f));
            Skin.Fill(track, Skin.Alpha(Skin.Accent, 0.25f));
            Skin.Fill(new Rect(track.x, track.y, track.width * Stage.GrassScattered, track.height), Skin.Accent);
            Skin.LabelIn(new Rect(track.xMax + U(6f), at.y, at.xMax - track.xMax - U(9f), height), Skin.Fit(Skin.Small, text, at.xMax - track.xMax - U(9f)), Skin.Small, Skin.Faint);
            CountDrawn(PanelPart.Waiting);
        }
    }
}
