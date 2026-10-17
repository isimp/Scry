using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The Levels view's plates: each level of a dungeon's example a floor plate seen at a slant,
    /// from the plan turned with the view, one under the other by depth, the highest first. A
    /// plate shows its depth squashed (<see cref="Squash"/>) and its far side shifted left
    /// (<see cref="Shear"/>), as a floor looked at from in front and above; the shift is taken from
    /// the whole example's far side, so a spot over another on the next level stands straight
    /// above it. All plates share one scale, as big as the box holds.
    /// </summary>
    internal sealed class LevelPlates
    {
        /// <summary>How much of its depth a plate shows, seen at a slant.</summary>
        public const float Squash = 0.45f;

        /// <summary>How far a plate's near side stands right of its far side, for each metre between them.</summary>
        public const float Shear = 0.35f;

        private readonly float[] _tops;
        private readonly (float MinUp, float MaxUp)[] _ups;
        private readonly float _left, _minRight, _maxUp;

        /// <summary>The plates' scale, in the box's units to the metre.</summary>
        public float Scale { get; }

        private LevelPlates(float scale, float left, float minRight, float maxUp, float[] tops, (float, float)[] ups)
        {
            Scale = scale;
            _left = left;
            _minRight = minRight;
            _maxUp = maxUp;
            _tops = tops;
            _ups = ups;
        }

        /// <summary>
        /// Lays the plates out in a box: <paramref name="whole"/> the whole example's reach right
        /// and up on the turned plan, <paramref name="levels"/> each level's reach up, the highest
        /// first, <paramref name="gap"/> between plates; centred in the box.
        /// </summary>
        public static LevelPlates Lay(float x, float y, float width, float height, float gap,
            (float MinRight, float MaxRight, float MinUp, float MaxUp) whole, IReadOnlyList<(float MinUp, float MaxUp)> levels)
        {
            var count = levels.Count;
            var wide = Math.Max(1f, whole.MaxRight - whole.MinRight) + Math.Max(0f, whole.MaxUp - whole.MinUp) * Shear;
            var tall = 0f;
            foreach (var (minUp, maxUp) in levels) tall += Math.Max(1f, maxUp - minUp) * Squash;
            var gaps = gap * Math.Max(0, count - 1);
            var scale = Math.Max(0f, Math.Min(width / wide, (height - gaps) / Math.Max(1f, tall)));
            var left = x + (width - wide * scale) / 2f;
            var top = y + (height - (tall * scale + gaps)) / 2f;
            var tops = new float[count];
            var ups = new (float, float)[count];
            for (var i = 0; i < count; i++)
            {
                tops[i] = top;
                ups[i] = levels[i];
                top += Math.Max(1f, levels[i].MaxUp - levels[i].MinUp) * Squash * scale + gap;
            }
            return new LevelPlates(scale, left, whole.MinRight, whole.MaxUp, tops, ups);
        }

        /// <summary>Where a spot of a level, by how far right and up it lies on the turned plan, stands in the box.</summary>
        public (float X, float Y) At(int level, float right, float up) =>
            (_left + (right - _minRight) * Scale + (_maxUp - up) * Shear * Scale, _tops[level] + (_ups[level].MaxUp - up) * Squash * Scale);

        /// <summary>The top of a level's plate in the box, its far side.</summary>
        public float PlateTop(int level) => _tops[level];

        /// <summary>The bottom of a level's plate in the box, its near side.</summary>
        public float PlateBottom(int level) => _tops[level] + Math.Max(1f, _ups[level].MaxUp - _ups[level].MinUp) * Squash * Scale;
    }
}
