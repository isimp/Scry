namespace Scry
{
    /// <summary>
    /// How a room is drawn darker on the stage: every colour its parts' shaders take, a tint, a
    /// glow or a snow colour alike, at a share of its own, its see-through kept. A shader's snow
    /// is coloured apart from its tint, so darkening the tint alone leaves a snowy room as bright.
    /// </summary>
    internal static class DimColour
    {
        /// <summary>The share of its own colour a dimmed part keeps.</summary>
        public const float Share = 0.3f;

        /// <summary>The share a part of a room on the level of the room gone into keeps: half dimmed, as near it.</summary>
        public const float NearShare = 0.45f;

        /// <summary>A colour darkened: each channel at <see cref="Share"/> of its own, its alpha as it was.</summary>
        public static (float R, float G, float B, float A) Darker(float r, float g, float b, float a) => Darker(r, g, b, a, Share);

        /// <summary>A colour darkened to another share of its own, its alpha as it was: at none, black, as the self-test films what no colour lights.</summary>
        public static (float R, float G, float B, float A) Darker(float r, float g, float b, float a, float share) => (r * share, g * share, b * share, a);
    }
}
