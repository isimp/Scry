namespace Scry
{
    /// <summary>
    /// Whether Scry's marks step aside on Valheim's large map for a portal mod's map of portals to
    /// choose from: while it is open and the mod hides the map's pins there, as TargetPortal does
    /// by its own *Hide map pins* (<see cref="TargetPortalMap"/>), so its portals alone stand on it.
    /// </summary>
    internal static class PortalMap
    {
        /// <summary>Whether the marks step aside: a portal's map is open for <paramref name="choosing"/> a target, and it <paramref name="hidesPins"/>.</summary>
        public static bool MarksAside(bool choosing, bool hidesPins) => choosing && hidesPins;

        /// <summary>Whether TargetPortal's switch, by the word it reads ("On" or "Off"), hides the pins; nothing read hides nothing.</summary>
        public static bool HidesPins(string toggle) => toggle == "On";
    }
}
