using System;

namespace Scry
{
    /// <summary>
    /// Where the compendium's sight looks from: told each frame where the character stands, where
    /// the eye is and which way it looks, it says whether the character or the eye moved, or the
    /// view turned, past a little since it last said so. Only then can a thing standing still
    /// come into reach or into view, so things resting wake on it (<see cref="Rounds{T}.Wake"/>).
    /// Small moves add up from where the last move was told, so creeping is told too.
    /// </summary>
    internal sealed class Viewpoint
    {
        /// <summary>How far the character or the eye may move before it counts, in metres.</summary>
        public const float Step = 0.25f;

        /// <summary>How far the view may turn before it counts, in degrees.</summary>
        public const float Turn = 2f;

        private static readonly double LeastCosine = Math.Cos(Turn * Math.PI / 180.0);

        private Vec3 _standing;
        private Vec3 _eye;

        /// <summary>Which way the view looked at the last move told; none before the first, so the first look counts.</summary>
        private Vec3 _forward;

        /// <summary>
        /// Whether the character or the eye moved more than <see cref="Step"/>, or the view turned
        /// more than <see cref="Turn"/>, since the last move told, taking where they are now as
        /// the place to measure from when so; the first look always counts, and so does a way
        /// to look that is no way at all.
        /// </summary>
        public bool Moved(Vec3 standing, Vec3 eye, Vec3 forward)
        {
            if (Vec3.Distance(standing, _standing) <= Step && Vec3.Distance(eye, _eye) <= Step && Cosine(forward, _forward) >= LeastCosine) return false;
            _standing = standing;
            _eye = eye;
            _forward = forward;
            return true;
        }

        /// <summary>The cosine of the angle between two ways, not a number where either is none.</summary>
        private static double Cosine(Vec3 a, Vec3 b)
        {
            var lengths = (double)a.Length * b.Length;
            return lengths > 0 ? Vec3.Dot(a, b) / lengths : double.NaN;
        }
    }
}
