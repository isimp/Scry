namespace Scry
{
    /// <summary>How the player's other pins stand while a kind is shown on the map.</summary>
    internal enum OtherPinsShown
    {
        Full,
        Third,
        Hidden,
    }

    /// <summary>How dark the map's picture lies under a kind's marks.</summary>
    internal enum MapDarkening
    {
        Off,
        Half,
        Strong,
    }

    /// <summary>How big Show on map's dots are drawn, beside their normal size.</summary>
    internal enum DotSizeChoice
    {
        Small,
        Normal,
        Large,
    }

    /// <summary>
    /// What the settings' Map section chooses of Show on map's look: how bright the player's
    /// other pins stand (<see cref="SeenOnMap.Dimmed"/>), how dark a veil lies over the map
    /// (<see cref="SeenOnMap.MapDarkened"/>), and how big the dots are drawn
    /// (<see cref="SeenOnMap.DotSize"/>); and the dungeon marks' size, chosen as the dots' is.
    /// </summary>
    internal static class MapLook
    {
        /// <summary>The share of their brightness the player's other pins keep.</summary>
        public static float PinsShare(OtherPinsShown shown) =>
            shown == OtherPinsShown.Full ? 1f : shown == OtherPinsShown.Hidden ? 0f : SeenOnMap.OthersDimmed;

        /// <summary>Whether the pins are dimmed at all, so that at full their layers are left as the game has them.</summary>
        public static bool Dims(OtherPinsShown shown) => shown != OtherPinsShown.Full;

        /// <summary>
        /// How much of the veil covers the map: none, half of what the eye sees, or about a third
        /// left, as the game blends in linear colour (<see cref="SeenOnMap.MapDarkened"/>).
        /// </summary>
        public static float Veil(MapDarkening darkening) =>
            darkening == MapDarkening.Off ? 0f : darkening == MapDarkening.Strong ? 0.92f : SeenOnMap.MapDarkened;

        /// <summary>How much bigger or smaller than normal the dots are drawn.</summary>
        public static float DotScale(DotSizeChoice size) =>
            size == DotSizeChoice.Small ? 0.7f : size == DotSizeChoice.Large ? 1.4f : 1f;

        /// <summary>The dungeon marks' sizes, as a share of a map pin: small, normal and large as the dots'.</summary>
        public static readonly float[] MarkScales = { DotScale(DotSizeChoice.Small), DotScale(DotSizeChoice.Normal), DotScale(DotSizeChoice.Large) };

        /// <summary>The mark size nearest one kept from before the three, the smaller where two are as near.</summary>
        public static float NearestMarkScale(float kept) => MarkScales[SettingsWords.Nearest(MarkScales, kept)];
    }
}
