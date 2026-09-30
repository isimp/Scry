using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// How a dungeon or camp is built and what a room is, in words, as <c>DungeonGenerator</c> does
    /// it: a dungeon grows room by room from its entrance (<c>GenerateDungeon</c>), a camp is laid
    /// on a grid (<c>GenerateCampGrid</c>) or scattered in a ring and walled (<c>GenerateCampRadial</c>).
    /// </summary>
    public static class DungeonWords
    {
        private static string Number(float value) => value.ToString("#,0.##", CultureInfo.InvariantCulture);

        public static string Layout(DungeonPlan plan)
        {
            switch (plan.Algorithm)
            {
                case "CampGrid":
                    var squares = plan.SpawnChance >= 1f ? "every square built on" : $"each built on {DropWords.Share(plan.SpawnChance)} of the time";
                    return $"on a {plan.GridSize} × {plan.GridSize} grid of {Number(plan.TileWidth)} m squares, {squares}";
                case "CampRadial":
                    // Random.Range(int, int) leaves the top out, as a creature's drops do.
                    var rooms = DropWords.CreatureAmount(plan.MinRooms, plan.MaxRooms, false);
                    var ring = plan.CampRadiusMax > plan.CampRadiusMin ? $"{Number(plan.CampRadiusMin)}–{Number(plan.CampRadiusMax)} m" : $"{Number(plan.CampRadiusMin)} m";
                    var words = $"{rooms} rooms scattered within a ring of {ring}";
                    return plan.PerimeterSections > 0 ? words + $", then a wall of up to {plan.PerimeterSections} sections round it" : words;
                default:
                    // PlaceRooms stops early only when it has required rooms and enough are in.
                    var required = plan.MinRequiredRooms > 0 && plan.RequiredRooms.Count > 0;
                    var tries = required
                        ? $"up to {plan.MaxRooms} tries at a room, stopping once {plan.MinRequiredRooms} of its required rooms and over {plan.MinRooms} rooms are in"
                        : $"{plan.MaxRooms} tries at a room";
                    return $"room by room from its entrance, {tries}, then end caps on every open doorway";
            }
        }

        /// <summary>
        /// How it picks each room: a dungeon any room that fits the doorway, all alike
        /// (<c>GetRandomRoom</c>), or by weight when it is set to (<c>GetRandomWeightedRoom</c>); a
        /// camp always by weight.
        /// </summary>
        public static string Picks(DungeonPlan plan)
        {
            if (plan.Algorithm != "Dungeon") return "by their weights";
            return plan.Weighted ? "by their weights, among those that fit the doorway" : "any room that fits the doorway, all alike";
        }

        /// <summary>
        /// How often a doorway between rooms gets a door (<c>PlaceDoors</c>): a door type's own
        /// chance where it has one, else the dungeon's. Null for a dungeon without doors.
        /// </summary>
        public static string Doors(DungeonPlan plan)
        {
            if (plan.Doors.Count == 0) return null;
            var own = plan.Doors.Where(d => d.Chance > 0f).ToList();
            if (own.Count == 0) return $"{DropWords.Share(plan.DoorChance)} of doorways";
            var parts = own.Select(d => $"{DropWords.Share(d.Chance)} of {d.Type} doorways").ToList();
            if (own.Count < plan.Doors.Count) parts.Add($"{DropWords.Share(plan.DoorChance)} of the others");
            return string.Join(", ", parts);
        }

        public static string Size(RoomShape room) => $"{Number(room.Size.X)} × {Number(room.Size.Z)} m, {Number(room.Size.Y)} m high";

        public static string Role(RoomShape room)
        {
            if (room.Entrance) return "an entrance, where the dungeon starts";
            if (room.EndCap) return "an end cap, closing a doorway nothing else took";
            if (room.Divider) return "a divider, between doorways of two types that meet";
            if (room.Perimeter) return "a piece of a camp's wall";
            return "a room";
        }

        /// <summary>What a dungeon's plan says while the rooms it is built of are read.</summary>
        public static string Reading(int read, int total) => $"Reading the {total} kinds of room it is built of, {read} so far";

        /// <summary>
        /// What an example holds: its rooms (the entrance among them), end caps, dividers, pieces
        /// of a camp's wall and doors, each only when there are any, and the kinds of room left
        /// out because they could not be loaded.
        /// </summary>
        public static string Example(DungeonExample example, int failed)
        {
            string Count(int n, string one, string many) => n == 1 ? "1 " + one : $"{n} {many}";
            var rooms = example.Rooms.Count(r => !r.Room.EndCap && !r.Room.Divider && !r.Room.Perimeter);
            var parts = new List<string>();
            if (rooms > 0) parts.Add(Count(rooms, "room", "rooms"));
            var caps = example.Rooms.Count(r => r.Room.EndCap);
            if (caps > 0) parts.Add(Count(caps, "end cap", "end caps"));
            var dividers = example.Rooms.Count(r => r.Room.Divider);
            if (dividers > 0) parts.Add(Count(dividers, "divider", "dividers"));
            var wall = example.Rooms.Count(r => r.Room.Perimeter);
            if (wall > 0) parts.Add(Count(wall, "piece of wall", "pieces of wall"));
            if (example.Doors.Count > 0) parts.Add(Count(example.Doors.Count, "door", "doors"));

            var text = parts.Count > 0 ? string.Join(", ", parts) : "Nothing could be laid out";
            if (failed > 0) text += failed == 1 ? "; 1 kind of room could not be loaded and is left out" : $"; {failed} kinds of room could not be loaded and are left out";
            return text;
        }

        /// <summary>How many doorways and of which types, the commonest first.</summary>
        public static string Doorways(RoomShape room)
        {
            if (room.Doorways.Count == 0) return "none";
            var types = room.Doorways.GroupBy(d => d.Type).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}");
            return $"{room.Doorways.Count}: {string.Join(", ", types)}";
        }
    }
}
