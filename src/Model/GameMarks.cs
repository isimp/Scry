using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The places Valheim's map marks itself, so Show on map shades no square saying one lies
    /// somewhere inside where the map shows exactly where: those the game gives an icon on every
    /// map (<c>ZoneSystem.GetLocationIcons</c>), and those a Vegvisir pinned for the character
    /// (<c>Game.DiscoverClosestLocation</c>), each pin known for the place it marks by the name and kind
    /// of pin the Vegvisirs read set for it (<c>Vegvisir.m_locations</c>).
    /// </summary>
    internal static class GameMarks
    {
        /// <summary>How near, in metres, a place's spot seen stands to where the game marks one for them to be the same place.</summary>
        public const float SamePlace = 8f;

        /// <summary>The places each pin a Vegvisir sets marks, by the pin's name and kind.</summary>
        public static Dictionary<(string Pin, int Type), HashSet<string>> PlacesOfPins(IEnumerable<(string Location, string Pin, int Type)> vegvisirPins)
        {
            var places = new Dictionary<(string, int), HashSet<string>>();
            foreach (var (location, pin, type) in vegvisirPins)
            {
                if (string.IsNullOrEmpty(location) || string.IsNullOrEmpty(pin)) continue;
                if (!places.TryGetValue((pin, type), out var set)) places[(pin, type)] = set = new HashSet<string>(StringComparer.Ordinal);
                set.Add(location);
            }
            return places;
        }

        /// <summary>Where the game's map marks a place of a kind: its icons, and the pins whose name and kind a Vegvisir sets for it.</summary>
        public static List<(int X, int Z)> MarksOf(string place, IEnumerable<(float X, float Z, string Place)> icons, IEnumerable<(float X, float Z, string Pin, int Type)> pins,
            IReadOnlyDictionary<(string Pin, int Type), HashSet<string>> placesOfPins)
        {
            var marks = new List<(int X, int Z)>();
            if (place == null) return marks;
            foreach (var (x, z, of) in icons)
                if (string.Equals(of, place, StringComparison.Ordinal)) marks.Add(At(x, z));
            foreach (var (x, z, pin, type) in pins)
                if (pin != null && placesOfPins.TryGetValue((pin, type), out var places) && places.Contains(place)) marks.Add(At(x, z));
            return marks;
        }

        /// <summary>
        /// The entrances of a place's dungeons walked in a world whose entrance was not seen, each
        /// marked on the map as a dungeon walked (<see cref="DungeonMarks"/>): one seen too is the
        /// same dungeon by its entrance's id (<see cref="DungeonMaps.EntranceIdOf"/>), counted with the spot seen.
        /// </summary>
        public static List<(int X, int Z)> WalkedNotSeen(string world, string place, IEnumerable<(string Id, DungeonMap Map)> walked, IReadOnlyList<(int X, int Z)> seen)
        {
            var entrances = new List<(int X, int Z)>();
            if (place == null) return entrances;
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (x, z) in seen) seenIds.Add(DungeonMaps.EntranceIdOf(world, x, z));
            foreach (var (id, map) in walked)
                if (map != null && string.Equals(map.Location, place, StringComparison.Ordinal) && !seenIds.Contains(id)) entrances.Add(At(map.Entrance.X, map.Entrance.Z));
            return entrances;
        }

        /// <summary>The spots seen with the places the game marks besides them, each once: one marked where a spot was seen is that spot.</summary>
        public static List<(int X, int Z)> With(IReadOnlyList<(int X, int Z)> seen, IReadOnlyList<(int X, int Z)> marks)
        {
            var with = new List<(int X, int Z)>(seen);
            foreach (var (x, z) in marks)
            {
                var already = false;
                foreach (var (sx, sz) in with)
                {
                    if ((sx - x) * (sx - x) + (sz - z) * (sz - z) > SamePlace * SamePlace) continue;
                    already = true;
                    break;
                }
                if (!already) with.Add((x, z));
            }
            return with;
        }

        private static (int X, int Z) At(float x, float z) => ((int)Math.Round(x), (int)Math.Round(z));
    }
}
