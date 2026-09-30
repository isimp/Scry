using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// A location's or dungeon room's facts: the rules the world generator places a location by,
    /// known from the start; and once its bundle has been read, what it holds and how likely, the
    /// creatures its spawn points place, how its dungeon is built, and what a room is.
    /// </summary>
    internal sealed partial class Facts
    {
        // ----- Locations and rooms -----

        private void Place(Entry entry, PlaceSource place)
        {
            if (place.IsRoom) Part("room", () => RoomFacts(place));
            else Part("placement", () => Placement(place));

            var contents = place.Contents;
            if (contents == null)
            {
                Add("What it holds", LocationWords.HoldsNote(PlaceAssets.State(place)));
                return;
            }
            Part("location", () => PlaceHolds(entry, contents));
            if (contents.Dungeon != null) Part("dungeon", () => Dungeon(contents.Dungeon));
        }

        /// <summary>The rules of each set the world's location list places it by: a row each for one set, a line each for several.</summary>
        private void Placement(PlaceSource place)
        {
            var sets = place.Rules;
            if (sets.Count == 1)
            {
                foreach (var row in LocationWords.Rows(RulesOf(sets[0]))) Add(row.Key, row.Value);
                if (!string.IsNullOrEmpty(sets[0].AltBiomeParent)) Add("Only in", $"the {Naming.FieldLabel(sets[0].AltBiomeParent).ToLowerInvariant()} part of its biome");
                return;
            }
            for (var i = 0; i < sets.Count; i++)
            {
                var line = LocationWords.Line(RulesOf(sets[i]));
                if (!string.IsNullOrEmpty(sets[i].AltBiomeParent)) line += $", only in the {Naming.FieldLabel(sets[i].AltBiomeParent).ToLowerInvariant()} part of it";
                Add(i == 0 ? "Placed" : "Also placed", line);
            }
        }

        private static LocationRules RulesOf(ZoneSystem.ZoneLocation location) => new LocationRules
        {
            Quantity = location.m_quantity,
            Biomes = PlaceEntries.BiomeWords(location.m_biome),
            BiomeArea = location.m_biomeArea.ToString(),
            MinDistance = location.m_minDistance,
            MaxDistance = location.m_maxDistance,
            MinDistanceFromCenter = location.m_minDistanceFromCenter,
            MaxDistanceFromCenter = location.m_maxDistanceFromCenter,
            MinAltitude = location.m_minAltitude,
            MaxAltitude = location.m_maxAltitude,
            MinSimilar = location.m_minDistanceFromSimilar,
            MaxSimilar = location.m_maxDistanceFromSimilar,
            Group = location.m_group ?? "",
            GroupMax = location.m_groupMax ?? "",
            MinTerrainDelta = location.m_minTerrainDelta,
            MaxTerrainDelta = location.m_maxTerrainDelta,
            ExteriorRadius = location.m_exteriorRadius,
            Prioritized = location.m_prioritized,
            CenterFirst = location.m_centerFirst,
            Unique = location.m_unique,
            BiomeKeys = Knowledge.BiomeKeys(location.m_biome),
            InForest = location.m_inForest,
            ForestMin = location.m_forestTresholdMin,
            ForestMax = location.m_forestTresholdMax,
            MinVegetation = location.m_minimumVegetation,
            MaxVegetation = location.m_maximumVegetation,
            SurroundCheck = location.m_surroundCheckVegetation,
            SurroundDistance = location.m_surroundCheckDistance,
            SurroundBetter = location.m_surroundBetterThanAverage,
        };

        /// <summary>What a room is, from its record before it is read, and its shape once it is.</summary>
        private void RoomFacts(PlaceSource place)
        {
            if (_entry != null && _entry.FoundIn.Length > 0) Add("Built into", string.Join(", ", _entry.FoundIn.Select(Places.NameOf)));
            var shape = place.Contents?.Room;
            if (shape == null) return;
            Add("Is", DungeonWords.Role(shape));
            Add("Size", DungeonWords.Size(shape));
            Add("Doorways", DungeonWords.Doorways(shape));
            if (shape.MinPlaceOrder > 0) Add("Not before", $"{shape.MinPlaceOrder} rooms from the entrance");
        }

        /// <summary>
        /// What it holds: the levels it gives its creatures, where nothing can be built, the
        /// creatures its spawn points place, every networked part with how many and how likely,
        /// and where its vegvisirs point.
        /// </summary>
        private void PlaceHolds(Entry entry, PlaceContents contents)
        {
            if (contents.EnemyMinLevel > 0 || contents.EnemyMaxLevel > 0)
            {
                var levels = SpawnWords.Stars(contents.EnemyMinLevel > 0 ? contents.EnemyMinLevel : 1, contents.EnemyMaxLevel > 0 ? contents.EnemyMaxLevel : contents.EnemyMinLevel);
                Add("Its creatures", levels + (contents.EnemyLevelUpChance >= 0f ? ", " + SpawnWords.StarChance(contents.EnemyLevelUpChance) : ""));
            }
            if (contents.NoBuild && contents.NoBuildRadius > 0f) Add("Building", $"not within {Naming.Number(contents.NoBuildRadius)} m");

            if (contents.Creatures.Count > 0)
            {
                var row = new Row { Title = "Its spawn points place" };
                foreach (var part in contents.Creatures) row.Items.Add(PartChip(part));
                Rows.Add(row);
            }
            if (contents.Parts.Count > 0)
            {
                var row = new Row { Title = "Holds" };
                foreach (var part in contents.Parts) row.Items.Add(PartChip(part));
                Rows.Add(row);
            }
            if (contents.Vegvisirs.Count > 0)
            {
                var row = new Row { Title = "Its vegvisir points to" };
                foreach (var to in contents.Vegvisirs) row.Items.Add(new Ingredient { Name = PlaceName(to), Amount = "", Prefab = EntryKeys.For(Kind.Location, to) });
                Rows.Add(row);
            }
        }

        private static Ingredient PartChip(PlacePart part)
        {
            var prefab = Looks.Prefab(part.Prefab);
            return new Ingredient { Icon = AnyIcon(prefab), Name = AnyName(prefab, part.Prefab), Amount = PlaceParts.Amount(part.Count, part.Chance), Prefab = part.Prefab };
        }

        /// <summary>A location's name as its entry shows it, by its prefab name.</summary>
        private static string PlaceName(string prefab)
        {
            var catalog = Session.Explorer?.Catalog;
            var entry = catalog?.FirstOrDefault(e => e.Kind == Kind.Location && e.Name == prefab);
            return entry != null && entry.DisplayName.Length > 0 ? entry.DisplayName : prefab;
        }

        /// <summary>How its dungeon or camp is built, and the rooms it is built of, each going to its entry.</summary>
        private void Dungeon(DungeonPlan plan)
        {
            Add("Laid out", DungeonWords.Layout(plan));
            Add("Picks rooms", DungeonWords.Picks(plan));
            Add("Doors", DungeonWords.Doors(plan));

            var rooms = RoomsOf(plan.Themes);
            if (rooms.Count == 0) return;
            var row = new Row { Title = $"Built of {rooms.Count} kinds of room" };
            foreach (var room in rooms) row.Items.Add(new Ingredient { Name = room.DisplayName, Amount = "", Prefab = room.Key });
            Rows.Add(row);
        }

        /// <summary>The room entries of the kinds a dungeon is built of, entrances first, then rooms, then end caps.</summary>
        public static List<Entry> RoomsOf(int themes)
        {
            var catalog = Session.Explorer?.Catalog;
            if (catalog == null || themes == 0) return new List<Entry>();
            int Order(PlaceSource p) => p.Contents?.Room == null ? 1 : p.Contents.Room.Entrance ? 0 : p.Contents.Room.EndCap ? 2 : 1;
            return catalog
                .Where(e => e.Source is PlaceSource p && p.IsRoom && ((int)p.Room.m_theme & themes) != 0)
                .OrderBy(e => Order((PlaceSource)e.Source)).ThenBy(e => e.DisplayName)
                .ToList();
        }
    }
}
