namespace Scry
{
    /// <summary>What a mark's label on the large map shows.</summary>
    internal enum MarkWords
    {
        None,

        /// <summary>Its name alone, as the game names a pin.</summary>
        Name,

        /// <summary>Its name and how far it was walked, on a dark plate.</summary>
        All,
    }

    /// <summary>
    /// Where a dungeon been in is marked on Valheim's map: its entrance placed as the game
    /// places its pins (<c>Minimap.WorldToMapPoint</c> turns a world point into one of the map's
    /// texture, <c>Minimap.MapPointToLocalGuiPos</c> into the window of it the map shows), the
    /// mark on the entrance, as big as a pin scaled as the settings say; which marks show, how an
    /// entrance alone stands faint, and which the small map shows.
    /// </summary>
    internal static class MapMarks
    {
        /// <summary>What a mark's words show: all of them while the pointer rests on it, its name alone where the settings show names always, else nothing.</summary>
        public static MarkWords WordsOf(bool pointedAt, bool namesAlways) => pointedAt ? MarkWords.All : namesAlways ? MarkWords.Name : MarkWords.None;

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

        /// <summary>How big the mark is: a pin's size on the map shown, scaled as the settings say.</summary>
        public static float Size(float pinSize, float scale) => pinSize * scale;

        /// <summary>How much bigger a mark's name is drawn than the game's pin names: as the mark is.</summary>
        public static float NameScale(float scale) => scale;

        /// <summary>Whether a mark shows, by its look, as the settings' three switches say: walked by you, walked on this server, entrances alone.</summary>
        public static bool Shown(MarkLook look, bool walkedByYou, bool walkedOnServer, bool entrances)
        {
            switch (look)
            {
                case MarkLook.Yours:
                case MarkLook.Whole:
                case MarkLook.Visited: return walkedByYou;
                case MarkLook.Server:
                case MarkLook.ServerWhole: return walkedOnServer;
                default: return entrances;
            }
        }

        /// <summary>How faint an entrance alone stands until the pointer rests on it.</summary>
        public const float FaintAlpha = 0.45f;

        /// <summary>A mark's opacity: an entrance alone faint until pointed at, every other whole.</summary>
        public static float Alpha(MarkLook look, bool pointedAt) => look == MarkLook.Entrance && !pointedAt ? FaintAlpha : 1f;

        /// <summary>Whether the small map shows a mark, by its look: those of dungeons you walked and places you stood in alone.</summary>
        public static bool OnSmallMap(MarkLook look) => look == MarkLook.Yours || look == MarkLook.Whole || look == MarkLook.Visited;

        /// <summary>
        /// Whether a press let go is a click of a mark: the pointer moved no more than half a mark
        /// since it was pressed. The game calls any press under a quarter second a click, however
        /// far it moved (<c>Minimap.OnMapLeftUp</c>), so a quick drag of the map let go over a mark
        /// would open it otherwise.
        /// </summary>
        public static bool Clicked((float X, float Y) pressed, (float X, float Y) let, float size)
        {
            var dx = let.X - pressed.X;
            var dy = let.Y - pressed.Y;
            return dx * dx + dy * dy <= size * size / 4f;
        }
    }
}
