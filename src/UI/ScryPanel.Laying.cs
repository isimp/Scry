using System;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A page opened for the first time, drawn while it is laid out over the frames after the
    /// click (<see cref="PageStaging"/>): each part not laid yet stands as its skeleton, faint bars
    /// in its shape with a band of light sweeping over them (<see cref="Skin.Skeleton"/>), and
    /// fades in as it is laid; the section line and the finds chip stand as skeletons until the
    /// page is whole and fade in together then; the stage says while it still lays its ground's grass.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>How long a part laid takes to fade in, in milliseconds.</summary>
        private const double FadeMs = 180;

        /// <summary>A section of the page drawn once its turn to be laid has come, fading in then; nothing until then.</summary>
        private static float Laid(string part, float y, Func<float, float> draw)
        {
            if (!Staging.Lay()) return y;
            var unit = Staging.LastUnit;
            var after = Section(part, y, draw);
            FadeIn(new Rect(0f, y, _sideWidth, after - y), Skin.Backdrop, Staging.Shown(unit, FadeMs));
            return after;
        }

        /// <summary>
        /// Covers what was just drawn with what lies under it, less as it is shown: a part laid
        /// fades in from its background. Nothing once shown whole.
        /// </summary>
        private static void FadeIn(Rect area, Color under, double shown)
        {
            if (shown >= 1.0 || area.height <= 0f) return;
            Skin.Fill(area, Skin.Alpha(under, (float)(1.0 - shown)));
        }

        /// <summary>The same over a rounded box, its corners kept.</summary>
        private static void FadeInBox(Rect box, Color under, double shown)
        {
            if (shown >= 1.0 || box.height <= 0f) return;
            Skin.Box(box, Skin.Alpha(under, (float)(1.0 - shown)));
        }

        /// <summary>A box's skeleton: the box, a short bar where its heading goes and a longer one under it.</summary>
        private static float SkeletonBox(float width, float y, float height)
        {
            var top = y + U(6f);
            Skin.Box(new Rect(0f, top, width, height), Skin.TopicFill);
            var pad = U(12f);
            Skin.Skeleton(new Rect(pad, top + U(12f), (width - pad * 2f) * 0.22f, U(11f)));
            Skin.Skeleton(new Rect(pad, top + U(32f), (width - pad * 2f) * 0.55f, U(11f)));
            CountDrawn(PanelPart.Waiting);
            return top + height + U(4f);
        }

        /// <summary>A row of headline tiles' skeleton, two bars in each.</summary>
        private static float SkeletonTiles(float width, float y, int tiles)
        {
            var gap = U(8f);
            var w = (width - gap * (tiles - 1)) / tiles;
            var height = U(46f);
            for (var i = 0; i < tiles; i++)
            {
                var x = i * (w + gap);
                Skin.Box(new Rect(x, y, w, height), Skin.TopicFill);
                Skin.Skeleton(new Rect(x + U(10f), y + U(10f), w * 0.45f, U(10f)));
                Skin.Skeleton(new Rect(x + U(10f), y + U(26f), w * 0.7f, U(10f)));
            }
            CountDrawn(PanelPart.Waiting);
            return y + height + U(9f);
        }

        /// <summary>The facts' section while they are worked out: its heading, then the You box and a row of tiles as skeletons.</summary>
        private static float WaitingFacts(float width, float y)
        {
            GUI.Label(new Rect(0f, y, width, U(20f)), "IN THE GAME", Skin.Heading);
            y += U(26f);
            y = SkeletonBox(width, y - U(6f), U(52f));
            return SkeletonTiles(width, y + U(2f), 3) + U(14f);
        }

        /// <summary>The topics while their plan is worked out, under the You box: a row of tiles as skeletons.</summary>
        private static float WaitingPlan(float width, float y) => SkeletonTiles(width, y, 3) + U(14f);

        /// <summary>A topic not laid yet: its box's skeleton where it will stand.</summary>
        private static float WaitingTopic(string heading, float width, float y)
        {
            if (heading == null) return SkeletonTiles(width, y + U(6f), 3);
            return SkeletonBox(width, y, U(56f));
        }

        /// <summary>The page's end while sections below are still to be laid: one box's skeleton.</summary>
        private static float WaitingRest(float width, float y) => Staging.HeldAny ? SkeletonBox(width, y, U(52f)) : y;

        /// <summary>The stage's caption while its ground's grass is scattered, with how far it has got.</summary>
        private static void GrassCaption(Rect stage)
        {
            var biome = Stage.GroundUnderfoot;
            if (!Stage.GrassLaying || biome == null || Event.current.type != EventType.Repaint) return;
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
            CountDrawn(PanelPart.GrassCaption);
        }
    }
}
