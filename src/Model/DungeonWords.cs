using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// How a dungeon or camp is built and what a room is, in words, as <c>DungeonGenerator</c> does
    /// it: a dungeon grows room by room from its entrance (<c>GenerateDungeon</c>), a camp is laid
    /// on a grid (<c>GenerateCampGrid</c>) or scattered in a ring and walled (<c>GenerateCampRadial</c>).
    /// </summary>
    internal static class DungeonWords
    {
        public static string Layout(DungeonPlan plan)
        {
            switch (plan.Algorithm)
            {
                case "CampGrid":
                    var squares = plan.SpawnChance >= 1f ? "every square built on" : $"each built on {DropWords.Share(plan.SpawnChance)} of the time";
                    return $"on a {Numbers.Count(plan.GridSize)} × {Numbers.Count(plan.GridSize)} grid of {Numbers.Amount(plan.TileWidth)} m squares, {squares}";
                case "CampRadial":
                    // Random.Range(int, int) leaves the top out, as a creature's drops do.
                    var rooms = DropWords.CreatureAmount(plan.MinRooms, plan.MaxRooms, false);
                    var ring = plan.CampRadiusMax > plan.CampRadiusMin ? $"{Numbers.Amount(plan.CampRadiusMin)}–{Numbers.Amount(plan.CampRadiusMax)} m" : $"{Numbers.Amount(plan.CampRadiusMin)} m";
                    var words = $"{rooms} rooms scattered within a ring of {ring}";
                    return plan.PerimeterSections > 0 ? words + $", then a wall of up to {Numbers.Count(plan.PerimeterSections)} sections round it" : words;
                default:
                    // PlaceRooms stops early only when it has required rooms and enough are in.
                    var required = plan.MinRequiredRooms > 0 && plan.RequiredRooms.Count > 0;
                    var tries = required
                        ? $"up to {Numbers.Count(plan.MaxRooms)} tries at a room, stopping once {Numbers.Count(plan.MinRequiredRooms)} of its required rooms and over {Numbers.Count(plan.MinRooms)} rooms are in"
                        : $"{Numbers.Count(plan.MaxRooms)} tries at a room";
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
        /// The doors a dungeon puts between rooms, a row for each type of doorway (<c>PlaceDoors</c>):
        /// a doorway that allows a door gets one of those listed for its type, each listing alike
        /// likely (<c>FindDoorType</c>), at that door's own chance or, where it has none, the
        /// dungeon's. The title tells how many such doorways get a door; each door is named, with
        /// its own chance where the doors' differ. None for a dungeon without doors.
        /// </summary>
        public static List<(string Title, List<(string Prefab, string Chance)> Doors)> DoorRows(DungeonPlan plan)
        {
            var rows = new List<(string, List<(string, string)>)>();
            foreach (var type in plan.Doors.Select(d => d.Type ?? "").Distinct())
            {
                var listed = plan.Doors.Where(d => (d.Type ?? "") == type).ToList();
                float Chance((string Type, float Chance, string Prefab) d) => d.Chance > 0f ? d.Chance : plan.DoorChance;
                var alike = listed.All(d => Chance(d) == Chance(listed[0]));
                var doors = listed.GroupBy(d => d.Prefab).Select(g => (g.Key, alike ? "" : DropWords.Share(Chance(g.First())))).ToList();
                var share = DropWords.Share(listed.Average(d => Chance(d)));
                var typed = type.Length > 0 ? type + " " : "";
                var title = doors.Count == 1
                    ? $"Door in {share} of {typed}doorways that allow one"
                    : $"Doors in {share} of {typed}doorways that allow one, one of these alike";
                rows.Add((title, doors));
            }
            return rows;
        }

        public static string Size(RoomShape room) => $"{Numbers.Amount(room.Size.X)} × {Numbers.Amount(room.Size.Z)} m, {Numbers.Amount(room.Size.Y)} m high";

        public static string Role(RoomShape room)
        {
            if (room.Entrance) return "an entrance, where the dungeon starts";
            if (room.EndCap) return "an end cap, closing a doorway nothing else took";
            if (room.Divider) return "a divider, between doorways of two types that meet";
            if (room.Perimeter) return "a piece of a camp's wall";
            return "a room";
        }

        /// <summary>What a dungeon's plan says while the rooms it is built of are read.</summary>
        public static string Reading(int read, int total) => $"Reading the {Numbers.Count(total)} kinds of room it is built of, {Numbers.Count(read)} so far";

        /// <summary>How far the example has been built on the stage, room by room.</summary>
        public static string Building(int shown, int total) => $"Building the example on the stage, {Numbers.Count(shown)} of its {Numbers.Count(total)} {(total == 1 ? "room" : "rooms")} so far";

        /// <summary>
        /// What an example holds: its rooms (the entrance among them), end caps, dividers, pieces
        /// of a camp's wall and doors, each only when there are any, and the kinds of room left
        /// out because they could not be loaded.
        /// </summary>
        public static string Example(DungeonExample example, int failed)
        {
            string Count(int n, string one, string many) => n == 1 ? "1 " + one : $"{Numbers.Count(n)} {many}";
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
            if (failed > 0) text += failed == 1 ? "; 1 kind of room could not be loaded and is left out" : $"; {Numbers.Count(failed)} kinds of room could not be loaded and are left out";
            return text;
        }

        /// <summary>How many doorways and of which types, the commonest first.</summary>
        public static string Doorways(RoomShape room)
        {
            if (room.Doorways.Count == 0) return "none";
            var types = room.Doorways.GroupBy(d => d.Type).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase).Select(g => $"{Numbers.Count(g.Count())} {g.Key}");
            return $"{Numbers.Count(room.Doorways.Count)}: {string.Join(", ", types)}";
        }
    }
}
