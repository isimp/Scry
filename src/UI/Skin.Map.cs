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
    }
}
