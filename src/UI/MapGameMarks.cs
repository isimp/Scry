using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Where Valheim's map marks places itself (<see cref="GameMarks"/>), read from the game: the
    /// places it gives an icon (<c>ZoneSystem.GetLocationIcons</c>) and the character's pins a
    /// Vegvisir set (<c>Minimap.m_pins</c>), known by the pins the places read say their Vegvisirs
    /// set (<see cref="PlaceContents.VegvisirPins"/>). Worked out again as the pins or the places
    /// read change, at most every few seconds; a place's squares leave out where it is marked.
    /// </summary>
    internal static class MapGameMarks
    {
        static MapGameMarks() => WorldCaches.Register(nameof(MapGameMarks), Forget);

        /// <summary>How often, in seconds, the pins and icons are read again at most.</summary>
        private const float Again = 2f;

        private static readonly Dictionary<string, List<(int X, int Z)>> ByPlace = new Dictionary<string, List<(int X, int Z)>>(System.StringComparer.Ordinal);
        private static readonly Dictionary<Vector3, string> Icons = new Dictionary<Vector3, string>();
        private static readonly IReadOnlyList<(int X, int Z)> None = System.Array.Empty<(int, int)>();
        private static AccessTools.FieldRef<Minimap, List<Minimap.PinData>> _pins;
        private static (int Pins, int Records, int Icons) _readFor = (-1, -1, -1);
        private static float _readAt = float.MinValue;

        /// <summary>Counts up as where the game marks places changes, for what is shown of them to be worked out again.</summary>
        public static int Version { get; private set; }

        /// <summary>Where the game's map marks places of a kind, by the place's prefab name; none where it marks none.</summary>
        public static IReadOnlyList<(int X, int Z)> Of(string place)
        {
            Refresh();
            return place != null && ByPlace.TryGetValue(place, out var marks) ? marks : None;
        }

        /// <summary>The spots seen of a place with where the game marks one, and the entrances of its dungeons walked, besides them, each once: what its squares leave out, as each stands marked on the map.</summary>
        public static IReadOnlyList<(int X, int Z)> With(string place, IReadOnlyList<(int X, int Z)> seen)
        {
            seen = seen ?? None;
            var marks = Of(place);
            var walked = DungeonKinds.Is(place) ? GameMarks.WalkedNotSeen(CompendiumMap.World, place, DungeonWalks.Maps.OfWorld(CompendiumMap.World), seen) : null;
            if (marks.Count == 0 && (walked == null || walked.Count == 0)) return seen;
            if (walked != null) walked.AddRange(marks);
            return GameMarks.With(seen, walked ?? (IReadOnlyList<(int X, int Z)>)marks);
        }

        /// <summary>Reads the pins and icons again where a few seconds have gone by, working out where the game marks places where they changed.</summary>
        public static void Refresh()
        {
            var map = Minimap.instance;
            var zones = ZoneSystem.instance;
            if (map == null || zones == null || Time.unscaledTime - _readAt < Again) return;
            _readAt = Time.unscaledTime;
            if (_pins == null) _pins = AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");
            var pins = _pins(map);
            var records = PlaceRecords.Kept?.Records;
            Icons.Clear();
            zones.GetLocationIcons(Icons);
            var readFor = (pins?.Count ?? 0, records?.Count ?? 0, Icons.Count);
            if (readFor == _readFor) return;
            _readFor = readFor;
            var vegvisirPins = new List<(string, string, int)>();
            if (records != null) foreach (var record in records) vegvisirPins.AddRange(record.Contents.VegvisirPins);
            var placesOfPins = GameMarks.PlacesOfPins(vegvisirPins);
            var gamePins = new List<(float X, float Z, string Pin, int Type)>();
            if (pins != null) foreach (var pin in pins) if (pin != null) gamePins.Add((pin.m_pos.x, pin.m_pos.z, pin.m_name, (int)pin.m_type));
            var icons = new List<(float X, float Z, string Place)>();
            foreach (var icon in Icons) icons.Add((icon.Key.x, icon.Key.z, icon.Value));
            // Each place the game marks, worked out once.
            var places = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var icon in icons) places.Add(icon.Place);
            foreach (var pin in gamePins) if (pin.Pin != null && placesOfPins.TryGetValue((pin.Pin, pin.Type), out var of)) places.UnionWith(of);
            ByPlace.Clear();
            foreach (var place in places) ByPlace[place] = GameMarks.MarksOf(place, icons, gamePins, placesOfPins);
            Version++;
        }

        /// <summary>Which places the game's map marks itself and how many of each, for the self-test's note.</summary>
        [Diagnostic]
        public static string SaidForTest()
        {
            Refresh();
            var said = new System.Text.StringBuilder("the game's map marks itself: ");
            if (ByPlace.Count == 0) said.Append("nothing");
            foreach (var pair in ByPlace) said.Append(pair.Key).Append(' ').Append(Numbers.Count(pair.Value.Count)).Append("; ");
            return said.ToString();
        }

        public static void Forget()
        {
            ByPlace.Clear();
            Icons.Clear();
            _readFor = (-1, -1, -1);
            _readAt = float.MinValue;
            Version++;
        }
    }
}
