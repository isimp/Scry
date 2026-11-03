using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Where Valheim's map marks places itself (<see cref="GameMarks"/>), read from the game: the
    /// places it gives an icon, as the compendium looks at them every half minute
    /// (<see cref="CompendiumMap.IconsSeen"/>, the game going through every place to find them),
    /// and the character's pins a
    /// Vegvisir set (<c>Minimap.m_pins</c>), known by the pins the places read say their Vegvisirs
    /// set (<see cref="PlaceContents.VegvisirPins"/>). Worked out again as the pins or the places
    /// read change, at most every few seconds, each frame's own update rather than the map's, so
    /// opening the map does no more for it; a place's squares leave out where it is marked.
    /// </summary>
    internal static class MapGameMarks
    {
        static MapGameMarks() => WorldCaches.Register(nameof(MapGameMarks), Forget);

        /// <summary>How often, in seconds, the pins and icons are read again at most.</summary>
        private const float Again = 2f;

        private static readonly Dictionary<string, List<(int X, int Z)>> ByPlace = new Dictionary<string, List<(int X, int Z)>>(System.StringComparer.Ordinal);
        private static readonly IReadOnlyList<(int X, int Z)> None = System.Array.Empty<(int, int)>();
        private static AccessTools.FieldRef<Minimap, List<Minimap.PinData>> _pins;
        private static (int Pins, int Records, int Icons, int PinsSaid, string Language) _readFor = (-1, -1, -1, 0, null);
        private static float _readAt = float.MinValue;

        /// <summary>Counts up as where the game marks places changes, for what is shown of them to be worked out again.</summary>
        public static int Version { get; private set; }

        /// <summary>Where the game's map marks places of a kind, by the place's prefab name; none where it marks none.</summary>
        public static IReadOnlyList<(int X, int Z)> Of(string place) => place != null && ByPlace.TryGetValue(place, out var marks) ? marks : None;

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

        /// <summary>
        /// Each frame, in its own update: the pins and icons read again where a few seconds have
        /// gone by, where the game marks places worked out again where they changed, and
        /// <see cref="Version"/> counted up only where that changed what stands.
        /// </summary>
        public static void Refresh()
        {
            var map = Minimap.instance;
            if (map == null || Time.unscaledTime - _readAt < Again) return;
            _readAt = Time.unscaledTime;
            if (_pins == null) _pins = AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");
            var pins = _pins(map);
            var records = PlaceRecords.Kept?.Records;
            // A pin swapped for another, or the game's language changed, is seen as well as one more or fewer.
            var pinsSaid = 17;
            if (pins != null) foreach (var pin in pins) if (pin != null) pinsSaid = pinsSaid * 31 + (int)pin.m_type * 7919 + Mathf.RoundToInt(pin.m_pos.x) * 131 + Mathf.RoundToInt(pin.m_pos.z);
            var readFor = (pins?.Count ?? 0, records?.Count ?? 0, CompendiumMap.IconsRead, pinsSaid, Localization.instance?.GetSelectedLanguage());
            if (readFor == _readFor) return;
            _readFor = readFor;
            var vegvisirPins = new List<(string, string, int)>();
            if (records != null) foreach (var record in records) vegvisirPins.AddRange(record.Contents.VegvisirPins);
            // The game keeps a pin a Vegvisir set by the name the Vegvisir gives it; some maps hold it by the name it shows, in the game's language.
            var language = Localization.instance;
            var placesOfPins = GameMarks.PlacesOfPins(vegvisirPins, language != null ? (System.Func<string, string>)language.Localize : null);
            var gamePins = new List<(float X, float Z, string Pin, int Type)>();
            if (pins != null) foreach (var pin in pins) if (pin != null) gamePins.Add((pin.m_pos.x, pin.m_pos.z, pin.m_name, (int)pin.m_type));
            var icons = new List<(float X, float Z, string Place)>();
            foreach (var icon in CompendiumMap.IconsSeen) icons.Add((icon.Key.x, icon.Key.z, icon.Value));
            // Each place the game marks, worked out once.
            var places = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var icon in icons) places.Add(icon.Place);
            foreach (var pin in gamePins) if (pin.Pin != null && placesOfPins.TryGetValue((pin.Pin, pin.Type), out var of)) places.UnionWith(of);
            var worked = new Dictionary<string, List<(int X, int Z)>>(System.StringComparer.Ordinal);
            foreach (var place in places) worked[place] = GameMarks.MarksOf(place, icons, gamePins, placesOfPins);
            // The places being read in the background change the count each few seconds; what stands changes far seldomer.
            if (Same(worked)) return;
            ByPlace.Clear();
            foreach (var pair in worked) ByPlace[pair.Key] = pair.Value;
            Version++;
        }

        /// <summary>Whether marks worked out are those standing, place by place.</summary>
        private static bool Same(Dictionary<string, List<(int X, int Z)>> worked)
        {
            if (worked.Count != ByPlace.Count) return false;
            foreach (var pair in worked)
            {
                if (!ByPlace.TryGetValue(pair.Key, out var standing) || standing.Count != pair.Value.Count) return false;
                for (var i = 0; i < standing.Count; i++) if (standing[i] != pair.Value[i]) return false;
            }
            return true;
        }

        /// <summary>Which places the game's map marks itself and how many of each, for the self-test's note.</summary>
        [Diagnostic]
        public static string SaidForTest()
        {
            Refresh();
            var said = new System.Text.StringBuilder("the game's map marks itself: ");
            if (ByPlace.Count == 0) said.Append("nothing");
            foreach (var pair in ByPlace) said.Append(pair.Key).Append(' ').Append(Numbers.Count(pair.Value.Count)).Append("; ");
            // The game's own pins as it keeps them, the bosses' and Hildir's, and how many pins the places read name, so a pin not matched shows why.
            said.Append(" the game's pins: ");
            var map = Minimap.instance;
            var pins = map != null && _pins != null ? _pins(map) : null;
            if (pins != null)
                foreach (var pin in pins)
                    if (pin != null && (pin.m_type == Minimap.PinType.Boss || pin.m_type >= Minimap.PinType.Hildir1 && pin.m_type <= Minimap.PinType.Hildir3))
                        said.Append(pin.m_name).Append(' ').Append(pin.m_type.ToString()).Append("; ");
            var named = 0;
            if (PlaceRecords.Kept?.Records is IReadOnlyList<PlaceRecord> records) foreach (var record in records) named += record.Contents.VegvisirPins.Count;
            said.Append(" pins the places read name: ").Append(Numbers.Count(named));
            return said.ToString();
        }

        public static void Forget()
        {
            ByPlace.Clear();
            _readFor = (-1, -1, -1, 0, null);
            _readAt = float.MinValue;
            Version++;
        }
    }
}
