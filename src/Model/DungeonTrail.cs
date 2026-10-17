using System.Collections.Generic;

namespace Scry
{
    /// <summary>What a part of the breadcrumb over a dungeon's inside stands for.</summary>
    internal enum TrailPart
    {
        Dungeon,
        Level,
        Room,
    }

    /// <summary>
    /// The breadcrumb in the stage's top left inside a dungeon's example: the dungeon, the level
    /// open or the one the room gone into is on, and the room gone into, the last being where the
    /// view is.
    /// </summary>
    internal static class DungeonTrail
    {
        /// <summary>The parts, from the dungeon in: a level and a room only where there is one.</summary>
        public static List<(TrailPart Part, string Text, bool Current)> Of(string dungeon, string level, string room)
        {
            var parts = new List<(TrailPart, string, bool)> { (TrailPart.Dungeon, dungeon, false) };
            if (level != null) parts.Add((TrailPart.Level, level, false));
            if (room != null) parts.Add((TrailPart.Room, room, false));
            var last = parts[parts.Count - 1];
            parts[parts.Count - 1] = (last.Item1, last.Item2, true);
            return parts;
        }
    }
}
