namespace Scry
{
    /// <summary>
    /// Where a dungeon been in is marked on Valheim's large map: its entrance placed as the game
    /// places its pins (<c>Minimap.WorldToMapPoint</c> turns a world point into one of the map's
    /// texture, <c>Minimap.MapPointToLocalGuiPos</c> into the window of it the large map shows),
    /// the mark as big as a pin and standing just above where a pin there stands, so it leaves the pin clear.
    /// </summary>
    internal static class MapMarks
    {
        /// <summary>
        /// Where an entrance is on the large map's pin layer, from its lower left corner; false where
        /// the window the map shows leaves it out.
        /// </summary>
        public static bool Place(Vec3 entrance, float pixelSize, int textureSize, (float XMin, float YMin, float Width, float Height) window, (float Width, float Height) layer, out (float X, float Y) at)
        {
            var half = textureSize / 2;
            var mx = (entrance.X / pixelSize + half) / textureSize;
            var my = (entrance.Z / pixelSize + half) / textureSize;
            at = ((mx - window.XMin) / window.Width * layer.Width, (my - window.YMin) / window.Height * layer.Height);
            return mx > window.XMin && mx < window.XMin + window.Width && my > window.YMin && my < window.YMin + window.Height;
        }

        /// <summary>How far above the entrance the mark stands, by the size of a pin on the large map: a pin's height, clear of one there.</summary>
        public static float Lift(float pinSize) => pinSize;

        /// <summary>How big the mark is: as big as a pin on the large map.</summary>
        public static float Size(float pinSize) => pinSize;
    }
}
