using System;

namespace Scry
{
    /// <summary>
    /// How the stage frames what it shows. The camera stands far enough off for what is framed to
    /// fill the picture, nearer or farther as it is zoomed and never under a floor's cut; it
    /// glides to what it is to frame, out quickly so nothing leaves the picture and back in
    /// slowly once it settles; and the floor, the grid and the sky reach past what is framed. The
    /// stage measures what it shows and places the camera; these say how far.
    /// </summary>
    internal static class StageFraming
    {
        /// <summary>How far out an effect's particles are followed, in metres.</summary>
        public const float EffectReach = 25f;

        /// <summary>How far past its own size a model's framing takes in what it plays, as a share of that size.</summary>
        public const float ModelReach = 1.6f;

        /// <summary>How quickly the framing widens, or goes to a floor stepped to, per second.</summary>
        private const float WidenRate = 5f;

        /// <summary>How quickly the framing narrows once what is framed settles, per second.</summary>
        private const float NarrowRate = 1f;

        /// <summary>How quickly the middle of the framing glides to what it is to frame, per second.</summary>
        private const float CentreRate = 6f;

        /// <summary>How far left of the model the person stands, in metres.</summary>
        public const float PersonGap = 0.4f;

        /// <summary>
        /// How far off a sphere of a radius fills the picture's height, seen through a field of view
        /// in degrees; 0 while nothing is framed.
        /// </summary>
        public static float FramedDistance(float radius, float fieldOfView) =>
            radius > 0f ? radius / (float)Math.Sin(fieldOfView * 0.5 * Math.PI / 180.0) : 0f;

        /// <summary>How far off the camera stands: the framed distance zoomed, and never nearer than keeps it over a floor's cut.</summary>
        public static float Distance(float radius, float fieldOfView, float zoom, float overCut) =>
            Math.Max(FramedDistance(radius, fieldOfView) * zoom, overCut);

        /// <summary>How near and how far the picture draws: from a hundredth of the way (a centimetre at least) to well past what is framed.</summary>
        public static (float Near, float Far) Clipping(float distance, float radius) =>
            (Math.Max(0.01f, distance * 0.01f), distance + radius * 6f + 10f);

        /// <summary>
        /// How far a model's framing reaches: what it and what it played reach, up to
        /// <see cref="ModelReach"/> times its own size, or up to <see cref="EffectReach"/> metres
        /// following an effect's particles as they spread.
        /// </summary>
        public static float ModelRadius(float own, float reach, bool followEffect) =>
            Math.Min(reach, followEffect ? EffectReach : own * ModelReach);

        /// <summary>The framing's radius after some seconds of gliding to what it is to frame: out quickly, back in slowly, to a floor stepped to quickly either way.</summary>
        public static float Glide(float current, float wanted, bool toFloor, float seconds) =>
            current + (wanted - current) * Share(toFloor || wanted > current ? WidenRate : NarrowRate, seconds);

        /// <summary>How much of the way to what it is to frame the framing's middle goes in some seconds.</summary>
        public static float CentreShare(float seconds) => Share(CentreRate, seconds);

        /// <summary>Whether the framing is still on its way: its radius off by more than half a percent (a centimetre at least), or its middle by more than two centimetres.</summary>
        public static bool StillGliding(float radius, float wanted, float centreOff) =>
            Math.Abs(radius - wanted) > Math.Max(0.01f, wanted * 0.005f) || centreOff > 0.02f;

        /// <summary>How wide the grid is: whole tens of metres, ten at least, twice what is framed across, so a five-metre line runs under the middle.</summary>
        public static float GridMetres(float radius) => Math.Max(10f, (float)Math.Ceiling(radius * 4f / 10f) * 10f);

        /// <summary>How wide the plain floor is, well past what is framed.</summary>
        public static float FloorSize(float radius) => radius * 3.2f;

        /// <summary>How tall the picture is at a distance from the camera, seen through a field of view in degrees.</summary>
        public static float Tall(float distance, float fieldOfView) =>
            2f * distance * (float)Math.Tan(fieldOfView * 0.5 * Math.PI / 180.0);

        /// <summary>How tall the sky is at its depth: the picture's height there, with a little to spare at its edges.</summary>
        public static float SkyHeight(float depth, float fieldOfView) => Tall(depth, fieldOfView) * 1.05f;

        /// <summary>How far a floor opened is framed out from its middle: as far as what stands on it reaches, two metres at least.</summary>
        public static float FloorReach(float across) => Math.Max(2f, across);

        /// <summary>Where the person stands across: its right side <see cref="PersonGap"/> left of the model's left side.</summary>
        public static float PersonX(float modelLeft, float personHalfWidth) => modelLeft - PersonGap - personHalfWidth;

        /// <summary>The share of the way gone in some seconds at a rate, slowing as it nears; all of it, however long a frame takes.</summary>
        private static float Share(float rate, float seconds) => 1f - (float)Math.Exp(-rate * seconds);
    }
}
