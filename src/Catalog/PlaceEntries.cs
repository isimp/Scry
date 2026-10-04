using System;
using System.Collections.Generic;
using System.Linq;
using SoftReferenceableAssets;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What a location or dungeon room entry stands for: its prefab, kept by the game only as a
    /// soft reference to an asset bundle, the rules the world generator places a location by, or
    /// the room's own record, and what was read of it once its bundle was loaded.
    /// </summary>
    internal sealed class PlaceSource
    {
        public string Prefab = "";
        public SoftReference<GameObject> Reference;

        /// <summary>Every rule set the world's location list places this prefab by; none for a room.</summary>
        public readonly List<ZoneSystem.ZoneLocation> Rules = new List<ZoneSystem.ZoneLocation>();

        /// <summary>The room's record in the dungeon database; null for a location.</summary>
        public DungeonDB.RoomData Room;

        public bool IsRoom => Room != null;

        /// <summary>What was read of it once its bundle was loaded (on selecting it, or reading all the locations); null before.</summary>
        public PlaceContents Contents;

        /// <summary>Whether its bundle failed to load when it was last asked for, so what it holds cannot be read.</summary>
        public bool LoadFailed;

        /// <summary>The biomes its rule sets place it in, all together.</summary>
        public Heightmap.Biome Biomes
        {
            get
            {
                var biomes = Heightmap.Biome.None;
                foreach (var rules in Rules) biomes |= rules.m_biome;
                return biomes;
            }
        }
    }

    /// <summary>
    /// Makes an entry for every location the world can place (<c>ZoneSystem.m_locations</c>) and
    /// every dungeon room it can build with (<c>DungeonDB</c>), with no bundle loaded: what the
    /// game keeps of them in memory names them, groups them and tells their rules.
    /// </summary>
    internal static class PlaceEntries
    {
        /// <summary>Creatures' shown names by their prefab names, for the words of places named after them.</summary>
        public static Dictionary<string, string> CreatureNames(IEnumerable<Entry> entries)
        {
            var creatures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (entry.Kind == Kind.Creature && !string.IsNullOrEmpty(entry.DisplayName) && !creatures.ContainsKey(entry.Name)) creatures[entry.Name] = entry.DisplayName;
            }
            return creatures;
        }

        public static void Add(List<Entry> entries)
        {
            var creatures = CreatureNames(entries);

            var zones = ZoneSystem.instance;
            if (zones != null)
            {
                var sources = new Dictionary<string, PlaceSource>(StringComparer.Ordinal);
                var order = new List<PlaceSource>();
                foreach (var location in zones.m_locations)
                {
                    if (location == null || !location.m_enable || !location.m_prefab.IsValid) continue;
                    var name = location.m_prefab.Name;
                    if (!sources.TryGetValue(name, out var source))
                    {
                        sources[name] = source = new PlaceSource { Prefab = name, Reference = location.m_prefab };
                        order.Add(source);
                    }
                    source.Rules.Add(location);
                }
                foreach (var source in order)
                {
                    Guard.Each(Feature.Locations, "location entries", source.Prefab, () => entries.Add(LocationEntry(source, creatures)));
                }
            }

            var rooms = DungeonDB.instance != null ? DungeonDB.GetRooms() : null;
            if (rooms == null)
            {
                Arrange(entries);
                return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var room in rooms)
            {
                if (room == null || !room.m_enabled || !room.m_prefab.IsValid || !seen.Add(room.m_prefab.Name)) continue;
                Guard.Each(Feature.Locations, "dungeon room entries", room.m_prefab.Name, () => entries.Add(RoomEntry(new PlaceSource { Prefab = room.m_prefab.Name, Reference = room.m_prefab, Room = room }, creatures)));
            }
            Arrange(entries);
        }

        private static Entry LocationEntry(PlaceSource source, IReadOnlyDictionary<string, string> creatures)
        {
            var biomes = source.Biomes;
            var label = Places.LocationLabel(new PlaceFacts { Prefab = source.Prefab, Biome = BiomeWords(biomes) }, creatures);
            var keys = Knowledge.BiomeKeys(biomes);
            var group = LocationWords.Group(keys, Knowledge.BiomeName);
            return new Entry
            {
                Name = source.Prefab,
                DisplayName = Places.NameOf(label),
                Kind = Kind.Location,
                Group = group.Name,
                GroupOrder = group.Order,
                Origin = Origins.Locations.Of(source.Prefab),
                ModName = Knowledge.ModName(source.Prefab),
                ModClue = Knowledge.ModClue(source.Prefab),
                Source = source,
                Components = new[] { nameof(Location) },
                Biomes = keys,
                FoundIn = new[] { label },
            };
        }

        private static Entry RoomEntry(PlaceSource source, IReadOnlyDictionary<string, string> creatures)
        {
            var dungeons = ThemeNames(source.Room.m_theme, creatures);
            var group = LocationWords.RoomGroup(dungeons[0]);
            return new Entry
            {
                Name = source.Prefab,
                DisplayName = LocationWords.RoomName(source.Prefab),
                Kind = Kind.Location,
                Group = group.Name,
                GroupOrder = group.Order,
                Origin = Origins.Rooms.Of(source.Prefab),
                ModName = Knowledge.ModName(source.Prefab),
                ModClue = Knowledge.ModClue(source.Prefab),
                Source = source,
                Components = new[] { nameof(Room) },
                FoundIn = dungeons.ToArray(),
            };
        }

        /// <summary>
        /// Gives a location or room entry the names its places were read to have: a location its
        /// label (its game name, boss or trader, with its biome), a room the dungeons built with
        /// it. Where either is listed is left to <see cref="Arrange"/>.
        /// </summary>
        public static void Named(Entry entry, string[] labels)
        {
            if (labels == null || labels.Length == 0 || !(entry.Source is PlaceSource place)) return;
            entry.FoundIn = labels;
            if (!place.IsRoom) entry.DisplayName = Places.NameOf(labels[0]);
        }

        /// <summary>
        /// Puts every location and room where the Locations list shows it (<see cref="PlaceGrouping"/>),
        /// from what is known of them now: a location read tells the kinds of room its dungeon or
        /// camp is built of, and a room read tells whether it is an entrance, an end cap or a wall.
        /// Done when the catalog is made and again whenever places are read.
        /// </summary>
        public static void Arrange(IEnumerable<Entry> entries)
        {
            var places = new List<(Entry Entry, PlaceItem Item)>();
            Dictionary<string, string> creatures = null;
            var words = new Dictionary<int, string>();
            foreach (var entry in entries)
            {
                if (!(entry.Source is PlaceSource source)) continue;
                var item = new PlaceItem { Key = entry.Key, Prefab = source.Prefab, Shown = entry.ShownName, Room = source.IsRoom };
                if (source.IsRoom)
                {
                    item.Theme = (int)source.Room.m_theme;
                    item.Shape = source.Contents?.Room;
                    var home = PlaceGrouping.Home(item.Theme);
                    if (!words.TryGetValue(home, out var named))
                    {
                        creatures = creatures ?? CreatureNames(entries);
                        words[home] = named = Places.NameOf(ThemeNames((Room.Theme)home, creatures)[0]);
                    }
                    item.ThemeWords = named;
                }
                else
                {
                    item.Theme = source.Contents?.Dungeon?.Themes ?? 0;
                    item.Algorithm = source.Contents?.Dungeon?.Algorithm ?? "";
                    item.Biome = LocationWords.Group(Knowledge.BiomeKeys(source.Biomes), Knowledge.BiomeName);
                }
                places.Add((entry, item));
            }

            var placed = PlaceGrouping.Arrange(places.Select(p => p.Item).ToList());
            foreach (var (entry, item) in places)
            {
                if (!placed.TryGetValue(item.Key, out var placing)) continue;
                entry.Group = placing.Group.Name;
                entry.GroupOrder = placing.Group.Order;
                entry.GroupRank = placing.Rank;
                entry.Tag = placing.Tag;
                entry.Indent = placing.Indent;
            }
        }

        /// <summary>The biomes a place is in as the game shows them, or none when that is none or every one, as its label takes them.</summary>
        public static string BiomeWords(Heightmap.Biome biome)
        {
            if (biome == Heightmap.Biome.None) return "";
            var names = Knowledge.BiomeNames(biome);
            return names == "every biome" || names == "no biome" ? "" : names;
        }

        /// <summary>
        /// The kinds of dungeon a room is built into, by the game's names for its theme flags, in
        /// words as places are named ("GoblinCamp" is a Fuling camp), until reading the locations
        /// tells which dungeons use them.
        /// </summary>
        public static List<string> ThemeNames(Room.Theme theme, IReadOnlyDictionary<string, string> creatures)
        {
            var names = new List<string>();
            foreach (Room.Theme flag in Enum.GetValues(typeof(Room.Theme)))
            {
                var bits = (int)flag;
                if (bits == 0 || (bits & (bits - 1)) != 0 || (theme & flag) == 0) continue;
                var name = Places.LocationLabel(new PlaceFacts { Prefab = flag.ToString() }, creatures);
                if (!names.Contains(name)) names.Add(name);
            }
            if (names.Count == 0) names.Add(Places.AnyDungeon);
            return names;
        }
    }
}
