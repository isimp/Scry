using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>How the sea holds what the stage shows over the ocean's floor.</summary>
    internal enum SeaHeld
    {
        /// <summary>Not at all: it stands on the sea's floor.</summary>
        Not,

        /// <summary>Swimming, its root its swim depth under the surface (<c>Character.UpdateSwimming</c>).</summary>
        Swimming,

        /// <summary>Riding the surface, its root at the surface less its float offset (<c>Leviathan.m_alignToWaterLevel</c>).</summary>
        AtSurface,
    }

    /// <summary>
    /// Where the sea's water lies on the stage over the ocean's ground, and how deep it is. What
    /// the water holds keeps the height the game holds it at against the surface, over a sea as
    /// deep as the world places it and never shallower than the depth the game's water shows
    /// deepest at, so the camera looks down onto open water; what it does not hold stands on the
    /// sea's floor in the shallows, the water no higher than its middle, so the camera looking
    /// down at its middle stays over the surface.
    /// </summary>
    internal readonly struct SeaHold
    {
        /// <summary>The deepest the shallows under what the sea does not hold are.</summary>
        public const float Shallows = 4f;

        /// <summary>The depth the game's water shows its deepest at (<c>WaterVolume.DetectWaterDepth</c> takes the depth over ten metres).</summary>
        public const float DeepLook = 10f;

        /// <summary>How far the sea's floor lies under the lowest point of what the sea holds.</summary>
        public const float Clear = 1f;

        public readonly SeaHeld Held;

        /// <summary>How far over its root the sea's surface lies, as if at size one.</summary>
        public readonly float SurfaceOverRoot;

        /// <summary>The shallowest sea the world places it in (<see cref="Shallowest"/>); null where no rule of the world's says.</summary>
        public readonly float? Placed;

        private SeaHold(SeaHeld held, float surfaceOverRoot, float? placed)
        {
            Held = held;
            SurfaceOverRoot = surfaceOverRoot;
            Placed = placed;
        }

        /// <summary>What the sea does not hold.</summary>
        public static SeaHold Not => default;

        /// <summary>A creature swimming, its root its swim depth under the surface (<c>m_swimDepth</c>).</summary>
        public static SeaHold Swims(float swimDepth, float? placed) => new SeaHold(SeaHeld.Swimming, swimDepth, placed);

        /// <summary>Something riding the surface, its root at the surface less its float offset (<c>m_floatOffset</c>).</summary>
        public static SeaHold AtSurface(float floatOffset, float? placed) => new SeaHold(SeaHeld.AtSurface, floatOffset, placed);

        /// <summary>
        /// The shallowest sea any of the world's rules for a thing places it in: each at least its
        /// least ocean depth (<c>m_minOceanDepth</c>) deep, and its ground at least as far under the
        /// water as its highest altitude, the ground's height against the water, has it
        /// (<c>m_maxAltitude</c>); null for no rules.
        /// </summary>
        public static float? Shallowest(IEnumerable<(float MinOceanDepth, float MaxAltitude)> rules)
        {
            float? shallowest = null;
            foreach (var (minOceanDepth, maxAltitude) in rules)
            {
                var depth = Math.Max(0f, Math.Max(minOceanDepth, -maxAltitude));
                if (shallowest == null || depth < shallowest) shallowest = depth;
            }
            return shallowest;
        }

        /// <summary>
        /// How deep the sea is under what is shown, as if at size one: for what the sea holds, as
        /// deep as the world places it, as <see cref="DeepLook"/> at the least, and clear under its
        /// lowest point (<paramref name="lowestUnderSurface"/>); for the rest, half its
        /// <paramref name="height"/> and no deeper than the shallows.
        /// </summary>
        public float Depth(float lowestUnderSurface, float height)
        {
            if (Held == SeaHeld.Not) return Math.Min(Shallows, height / 2f);
            return Math.Max(Math.Max(DeepLook, Placed ?? 0f), lowestUnderSurface + Clear);
        }

        /// <summary>
        /// Whether a creature's animator is told it is in water (<c>inWater</c>): one swimming in
        /// the sea shown, and one that lives only in water wherever it is shown, as the game never
        /// shows it out of water but stranded.
        /// </summary>
        public static bool InWater(SeaHeld held, bool seaShown, bool livesInWater) =>
            held == SeaHeld.Swimming && (seaShown || livesInWater);
    }
}
