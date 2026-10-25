using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How a room of the world's own dungeon shows under the compendium.</summary>
    internal enum RoomFog
    {
        Shown,

        /// <summary>Shown only while peeking, marked as not walked.</summary>
        Peeked,

        /// <summary>Not drawn, named or counted.</summary>
        Dark,
    }

    /// <summary>A doorway of a room walked leading on to a room not walked.</summary>
    internal sealed class OpenWay
    {
        public PlacedRoom From;
        public PlacedRoom To;
        public Vec3 At;
    }

    /// <summary>
    /// What of the world's own dungeon shows (<see cref="RealDungeon"/>): every room with the
    /// compendium off; with it on, the rooms the player has stood in, each doorway of theirs
    /// leading on to a room not walked marked as an open way, and while peeking the rest too,
    /// marked as peeked. An end cap or divider shows as the room it is joined to does.
    /// </summary>
    internal static class DungeonFog
    {
        public static RoomFog Of(bool fogged, bool peeking, bool walked) =>
            !fogged || walked ? RoomFog.Shown : peeking ? RoomFog.Peeked : RoomFog.Dark;

        /// <summary>The dungeon as it shows, its rooms the same as the whole one's and in its order, and the rooms shown only as peeked.</summary>
        public static DungeonExample Shown(DungeonExample whole, Func<PlacedRoom, bool> walked, bool fogged, bool peeking, out HashSet<PlacedRoom> peeked)
        {
            var shown = new DungeonExample { Site = whole.Site };
            shown.Rooms.AddRange(Lit(whole, walked, fogged, peeking, out peeked));
            shown.Doors.AddRange(whole.Doors);
            return shown;
        }

        /// <summary>
        /// Lays the fog again on the dungeon as shown (<see cref="Shown"/>), in place: the rooms
        /// newly shown join it after those standing, in the whole's order, and the rooms hidden
        /// again leave it, so the stage keeps the rooms standing; true when any joined or left,
        /// those rooms given, with the rooms shown only as peeked.
        /// </summary>
        public static bool Relay(DungeonExample shown, DungeonExample whole, Func<PlacedRoom, bool> walked, bool fogged, bool peeking,
            out HashSet<PlacedRoom> peeked, out List<PlacedRoom> added, out List<PlacedRoom> removed)
        {
            var lit = new HashSet<PlacedRoom>(Lit(whole, walked, fogged, peeking, out peeked));
            var standing = new HashSet<PlacedRoom>(shown.Rooms);
            removed = shown.Rooms.Where(r => !lit.Contains(r)).ToList();
            added = whole.Rooms.Where(r => lit.Contains(r) && !standing.Contains(r)).ToList();
            if (removed.Count == 0 && added.Count == 0) return false;
            shown.Rooms.RemoveAll(r => !lit.Contains(r));
            shown.Rooms.AddRange(added);
            return true;
        }

        /// <summary>The rooms the fog shows, in the whole's order, and among them those shown only as peeked; an end cap or divider shows as the room it is joined to does.</summary>
        private static List<PlacedRoom> Lit(DungeonExample whole, Func<PlacedRoom, bool> walked, bool fogged, bool peeking, out HashSet<PlacedRoom> peeked)
        {
            peeked = new HashSet<PlacedRoom>();
            var lit = new List<PlacedRoom>();
            foreach (var room in whole.Rooms)
            {
                var own = room.Room.EndCap || room.Room.Divider ? room.JoinedTo : room;
                var fog = Of(fogged, peeking, own != null && walked(own));
                if (fog == RoomFog.Dark) continue;
                if (fog == RoomFog.Peeked) peeked.Add(room);
                lit.Add(room);
            }
            return lit;
        }

        /// <summary>Each way from a room walked to one not, at the doorway they share, in the order the rooms were placed.</summary>
        public static List<OpenWay> OpenWays(DungeonWays ways, Func<PlacedRoom, bool> walked)
        {
            var open = new List<OpenWay>();
            foreach (var room in ways.Rooms)
            {
                if (!walked(room)) continue;
                foreach (var other in ways.WaysOf(room))
                {
                    if (walked(other)) continue;
                    var at = ways.DoorBetween(room, other);
                    if (at.HasValue) open.Add(new OpenWay { From = room, To = other, At = at.Value });
                }
            }
            return open;
        }

        /// <summary>How many of the dungeon's rooms were walked, and how many it has, end caps and dividers left out.</summary>
        public static (int Walked, int Rooms) Walked(DungeonExample whole, Func<PlacedRoom, bool> walked)
        {
            var rooms = whole.Rooms.Where(r => !r.Room.EndCap && !r.Room.Divider).ToList();
            return (rooms.Count(walked), rooms.Count);
        }
    }
}
