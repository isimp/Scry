using System.Collections.Generic;

namespace Scry
{
    /// <summary>What the Locations list knows of a location or dungeon room when it groups it.</summary>
    internal sealed class PlaceItem
    {
        public string Key = "", Prefab = "", Shown = "";
        public bool Room;

        /// <summary>A room's theme flags (<c>Room.m_theme</c>); a location's dungeon's (<c>DungeonGenerator.m_themes</c>), 0 while it has none or is not read.</summary>
        public int Theme;

        /// <summary>A location's dungeon's way of laying out ("Dungeon", "CampGrid", "CampRadial").</summary>
        public string Algorithm = "";

        /// <summary>A location's biome group.</summary>
        public Group Biome;

        /// <summary>A room's home theme in words, for the group its rooms wait in until their dungeon is read.</summary>
        public string ThemeWords = "";

        /// <summary>A room's shape once read, which tells an entrance, an end cap, a divider or a wall.</summary>
        public RoomShape Shape;
    }

    /// <summary>Where the Locations list puts a place: its group, its rank there, the tag beside its name, and whether it is indented under its dungeon.</summary>
    internal sealed class PlacePlacing
    {
        public Group Group;
        public int Rank;
        public string Tag;
        public bool Indent;

        /// <summary>What it is, as the search's is: takes it: "dungeon", "camp" or "room"; null for none.</summary>
        public string Is;
    }

    /// <summary>
    /// How the Locations list arranges places: a dungeon or camp and its rooms are one group
    /// inside its biome, right after the biome's other places, the location first and tagged,
    /// its rooms indented and tagged, entrances first, then rooms, then end caps, dividers and
    /// walls. A room's home is the dungeon of its lowest theme flag, so a room two dungeons
    /// share is under one of them; until a location building its theme is read, a theme's
    /// rooms wait together after every biome.
    /// </summary>
    internal static class PlaceGrouping
    {
        /// <summary>A room's home theme: its lowest flag, 0 for none.</summary>
        public static int Home(int theme) => theme & -theme;

        /// <summary>
        /// What a place is, as its tag says and the search's is: takes it: a room of a dungeon or
        /// camp; a location building one with its rooms, once it is read, a dungeon or a camp by
        /// its way of laying out; null for any other.
        /// </summary>
        public static string Is(bool room, int theme, string algorithm)
        {
            if (room) return "room";
            if (theme == 0) return null;
            return algorithm == "Dungeon" ? "dungeon" : "camp";
        }

        public static Dictionary<string, PlacePlacing> Arrange(IReadOnlyList<PlaceItem> items)
        {
            // Each theme a read location builds with, the group of the first such location.
            var dungeons = new Dictionary<int, Group>();
            foreach (var item in items)
            {
                if (item.Room) continue;
                for (var bits = item.Theme; bits != 0; bits &= bits - 1)
                {
                    var flag = Home(bits);
                    if (!dungeons.ContainsKey(flag)) dungeons[flag] = LocationWords.DungeonGroup(item.Biome, item.Shown);
                }
            }

            var placed = new Dictionary<string, PlacePlacing>();
            foreach (var item in items)
            {
                if (!item.Room)
                {
                    var builds = Is(false, item.Theme, item.Algorithm);
                    placed[item.Key] = builds != null
                        ? new PlacePlacing { Group = dungeons[Home(item.Theme)], Tag = builds + " \u00b7 " + item.Prefab, Is = builds }
                        : new PlacePlacing { Group = item.Biome };
                    continue;
                }
                var home = dungeons.TryGetValue(Home(item.Theme), out var group);
                placed[item.Key] = new PlacePlacing
                {
                    Group = home ? group : LocationWords.RoomGroup(item.ThemeWords),
                    Rank = Rank(item.Shape),
                    Tag = Role(item.Shape) + " \u00b7 " + item.Prefab,
                    Indent = home,
                    Is = Is(true, item.Theme, item.Algorithm),
                };
            }
            return placed;
        }

        /// <summary>A room's place among its dungeon's: entrances first, then rooms, then what closes it off.</summary>
        private static int Rank(RoomShape shape)
        {
            if (shape == null) return 2;
            if (shape.Entrance) return 1;
            return shape.EndCap || shape.Divider || shape.Perimeter ? 3 : 2;
        }

        /// <summary>What a room is, in words, as its tag says; a room not read yet is a room.</summary>
        private static string Role(RoomShape shape)
        {
            if (shape == null) return "room";
            if (shape.Entrance) return "entrance room";
            if (shape.EndCap) return "end cap";
            if (shape.Divider) return "divider";
            return shape.Perimeter ? "wall" : "room";
        }
    }
}
