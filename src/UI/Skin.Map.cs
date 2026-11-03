using UnityEngine;

namespace Scry
{
    /// <summary>The colours Scry's marks stand in on Valheim's map.</summary>
    internal static partial class Skin
    {
        /// <summary>A place's dots on Valheim's map: a sky blue, as the places' pale stone would read white inside the dots' light ring.</summary>
        public static readonly Color MapPlace = new Color(0.35f, 0.66f, 0.94f);

        /// <summary>The colour a kind's dots stand in on Valheim's map: its list colour, a place's sky blue.</summary>
        public static Color MapDot(Kind kind) => kind == Kind.Location ? MapPlace : KindColor(kind);

        private static Texture2D _pointedRing;

        /// <summary>A thin ring round the mark the pointer rests on, on Valheim's map.</summary>
        public static Texture2D PointedRing => _pointedRing != null ? _pointedRing : _pointedRing = Shape(32, 32, (x, y) =>
        {
            var r = Mathf.Sqrt((x - 16f) * (x - 16f) + (y - 16f) * (y - 16f));
            return r <= 15.5f && r >= 13.5f;
        });
    }
}
