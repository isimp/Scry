using System;
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
                Add("What it holds", LocationWords.HoldsNote(place.LoadFailed ? PlaceLoad.Failed : PlaceLoad.Loading));
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
                foreach (var row in LocationWords.Rows(RulesOf(sets[0])))
                {
                    if (row.Key == "Biome") BiomeLine(row.Value, sets[0].m_biome);
                    else Add(row.Key, row.Value);
                }
                if (!string.IsNullOrEmpty(sets[0].AltBiomeParent)) Add("Only in", LocationWords.OnlyInPart(sets[0].AltBiomeParent));
                return;
            }
            for (var i = 0; i < sets.Count; i++)
            {
                Add(LocationWords.Placed(i == 0), LocationWords.WithPart(LocationWords.Line(RulesOf(sets[i])), sets[i].AltBiomeParent));
            }
        }

        /// <summary>
        /// The biome it is placed in, going to the biome's page: one biome is the line itself,
        /// several a row of chips under it, each going to its page.
        /// </summary>
        private void BiomeLine(string words, Heightmap.Biome biome)
        {
            var keys = Knowledge.BiomeKeys(biome);
            var page = keys.Length == 1 ? EntryKeys.For(Kind.Biome, keys[0]) : null;
            if (page != null && EntryOf(page) != null)
            {
                Add("Biome", words, page);
                return;
            }
            Add("Biome", words);
            if (keys.Length > 1) BiomeRow("Its biomes", biome);
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
            // Every dungeon built with it, each going to its location; a room two share is listed
            // under the first, and named here for both.
            if (_entry != null && _entry.FoundIn.Length > 0)
            {
                var catalog = WorldCatalog.Current?.All;
                var row = new Row { Title = "Built into" };
                foreach (var label in _entry.FoundIn)
                {
                    var location = catalog != null ? Places.LocationNamed(catalog, label) : null;
                    if (location != null && row.Items.All(i => i.Prefab != location.Key)) row.Items.Add(EntryChip(location));
                }
                if (row.Items.Count > 0) Rows.Add(row);
                else Add("Built into", Naming.Commas(_entry.FoundIn.Select(Places.NameOf)));
            }
            var shape = place.Contents?.Room;
            if (shape == null) return;
            Add("Is", DungeonWords.Role(shape));
            Add("Size", DungeonWords.Size(shape));
            Add("Doorways", DungeonWords.Doorways(shape));
            if (shape.MinPlaceOrder > 0) Add("Not before", DungeonWords.NotBefore(shape.MinPlaceOrder));
        }

        /// <summary>
        /// What it holds: the levels it sets for its spawn points, where nothing can be built, every
        /// networked part with how many and how likely, a row for each kind of part
        /// (<see cref="PlaceParts.RoleOf"/>), the creatures its spawn points place, toughest first,
        /// before its building pieces, and where its vegvisirs point.
        /// </summary>
        private void PlaceHolds(Entry entry, PlaceContents contents)
        {
            Add("Levels at its spawn points", SpawnWords.LocationLevels(contents.EnemyMinLevel, contents.EnemyMaxLevel, contents.EnemyLevelUpChance, contents.LevelOverrideExceptions));
            if (contents.NoBuild && contents.NoBuildRadius > 0f) Add("Building", LocationWords.NoBuild(contents.NoBuildRadius));
            Add("Music", LocationWords.Music(contents.Music), contents.Music.Count > 0 ? PlayMusic : null);

            foreach (var role in PlaceParts.Roles)
            {
                if (role == PartRole.Built && contents.Creatures.Count > 0)
                {
                    var creatures = new Row { Title = "Its spawn points place" };
                    foreach (var part in ContentOrder.ToughestFirst(contents.Creatures, c => FoeOf(c.Prefab))) creatures.Items.Add(PartChip(part));
                    Rows.Add(creatures);
                }
                var parts = InRow(contents.Parts, p => p.Prefab, p => p.Chance, role);
                if (parts.Count == 0) continue;
                var row = new Row { Title = PlaceParts.Title(role, false) };
                foreach (var part in parts) row.Items.Add(PartChip(part));
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
            var prefab = GamePrefabs.Item(part.Prefab);
            return new Ingredient { Icon = AnyIcon(prefab), Name = AnyName(prefab, part.Prefab), Amount = PlaceParts.Amount(part.Count, part.Chance), Prefab = part.Prefab };
        }

        /// <summary>A location's name as its entry shows it, by its prefab name.</summary>
        private static string PlaceName(string prefab)
        {
            var entry = WorldCatalog.Find(EntryKeys.For(Kind.Location, prefab));
            return entry != null ? entry.ShownName : prefab;
        }

        /// <summary>How its dungeon or camp is built, and the rooms it is built of, each going to its entry.</summary>
        private void Dungeon(DungeonPlan plan)
        {
            Add("Laid out", DungeonWords.Layout(plan));
            Add("Picks rooms", DungeonWords.Picks(plan));
            foreach (var (title, doors) in DungeonWords.DoorRows(plan))
            {
                var doorRow = new Row { Title = title };
                foreach (var (prefab, chance) in doors) if (prefab.Length > 0) doorRow.Items.Add(Chip(prefab, chance));
                if (doorRow.Items.Count > 0) Rows.Add(doorRow);
            }

            var rooms = RoomsOf(plan.Themes);
            if (rooms.Count == 0) return;
            var row = new Row { Title = DungeonWords.BuiltOf(rooms.Count) };
            foreach (var room in rooms) row.Items.Add(new Ingredient { Name = room.DisplayName, Amount = "", Prefab = room.Key });
            Rows.Add(row);
            RoomsHold(rooms);
        }

        /// <summary>
        /// What a dungeon's or camp's rooms hold, as its own prefab holds little: every part, a row
        /// for each kind of part as on a location's page, the loot those give (a chest's filling,
        /// what a pickable or a pile of remains yields) right after its chests, rarest first, and
        /// the creatures their spawn points place, toughest first, before its building pieces; each
        /// with in how many of its kinds of room it is. Its rooms are read as its example is laid
        /// out, or with every location.
        /// </summary>
        private void RoomsHold(List<Entry> rooms)
        {
            var read = rooms.Select(r => ((PlaceSource)r.Source).Contents).Where(c => c != null).ToList();
            if (read.Count == 0)
            {
                Add("What its rooms hold", "read once its rooms have loaded for its example layout");
                return;
            }
            string Read(string title) => DungeonWords.RoomsRead(title, read.Count, rooms.Count);

            var parts = PlaceParts.Across(read.Select(c => (IReadOnlyList<PlacePart>)c.Parts));
            foreach (var role in PlaceParts.Roles)
            {
                if (role == PartRole.Built)
                {
                    var creatures = PlaceParts.Across(read.Select(c => (IReadOnlyList<PlacePart>)c.Creatures));
                    if (creatures.Count > 0)
                    {
                        var spawned = new Row { Title = Read("Its rooms' spawn points place") };
                        foreach (var (creature, count) in ContentOrder.ToughestFirst(creatures, c => FoeOf(c.Prefab))) spawned.Items.Add(Chip(creature, PlaceParts.InRooms(count)));
                        Rows.Add(spawned);
                    }
                }
                var inRow = InRow(parts, p => p.Prefab, p => p.Rooms, role);
                if (inRow.Count > 0)
                {
                    var row = new Row { Title = Read(PlaceParts.Title(role, true)) };
                    foreach (var (prefab, count) in inRow) row.Items.Add(Chip(prefab, PlaceParts.InRooms(count)));
                    Rows.Add(row);
                }
                if (role != PartRole.Loot) continue;

                // The loot in them, in the fewest kinds of room first.
                var loot = PlaceParts.Across(read.Select(c => (IReadOnlyList<PlacePart>)c.Parts
                    .SelectMany(p => Knowledge.LootOf(GamePrefabs.Named(p.Prefab)))
                    .Distinct()
                    .Select(item => new PlacePart { Prefab = item, Count = 1, Chance = 1f })
                    .ToList()));
                if (loot.Count > 0)
                {
                    var row = new Row { Title = Read("Loot in its rooms") };
                    foreach (var (item, count) in ContentOrder.RarestFirst(loot, l => l.Rooms)) row.Items.Add(Chip(item, PlaceParts.InRooms(count)));
                    Rows.Add(row);
                }
            }
        }

        /// <summary>The room entries of the kinds a dungeon is built of, entrances first, then rooms, then end caps.</summary>
        public static List<Entry> RoomsOf(int themes)
        {
            var catalog = WorldCatalog.Current?.All;
            if (catalog == null || themes == 0) return new List<Entry>();
            int Order(PlaceSource p) => p.Contents?.Room == null ? 1 : p.Contents.Room.Entrance ? 0 : p.Contents.Room.EndCap ? 2 : 1;
            return catalog
                .Where(e => e.Source is PlaceSource p && p.IsRoom && ((int)p.Room.m_theme & themes) != 0)
                .OrderBy(e => Order((PlaceSource)e.Source)).ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
