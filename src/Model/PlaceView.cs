using System.Linq;

namespace Scry
{
    /// <summary>How a location or dungeon room stands on the stage.</summary>
    public static class PlaceView
    {
        /// <summary>
        /// Where a place's ground is, above its root: a location's is its root, where the game
        /// stands it on the terrain, which hides whatever of it reaches below; a room's is its
        /// lowest doorway, the floor it is walked into on (<c>RoomConnection</c>), as rooms are
        /// joined level at their doorways.
        /// </summary>
        public static float Ground(PlaceContents contents, bool room)
        {
            if (!room) return 0f;
            var doorways = contents?.Room?.Doorways;
            return doorways == null || doorways.Count == 0 ? 0f : doorways.Min(d => d.Position.Y);
        }
    }
}
