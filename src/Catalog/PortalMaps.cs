namespace Scry
{
    /// <summary>
    /// The portal mods' maps of portals Scry's marks step aside on (<see cref="PortalMap"/>):
    /// TargetPortal's (<see cref="TargetPortalMap"/>) and AdjustablePortals' (<see cref="AdjustablePortalsMap"/>).
    /// </summary>
    internal static class PortalMaps
    {
        /// <summary>Whether Scry's marks, dots and strips step aside on the large map now.</summary>
        public static bool MarksAside => PortalMap.MarksAsideOnAny(TargetPortalMap.MarksAside, AdjustablePortalsMap.Picking);
    }
}
