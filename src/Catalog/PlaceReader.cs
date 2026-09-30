using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Reads a loaded location or dungeon room into what Scry keeps of it (<see cref="PlaceContents"/>),
    /// names and figures only, so its bundle can be let go of afterwards. A part's chance follows
    /// how the game places it (<c>ZoneSystem.SpawnLocation</c>, <c>DungeonGenerator.PlaceRoom</c>):
    /// every <c>RandomSpawn</c> above it rolls its chance, every <c>RandomObject</c> above it picks
    /// one of its objects by weight, and a <c>RandomSpawn</c>'s object for when it is off shows
    /// the rest of the time.
    /// </summary>
    internal static class PlaceReader
    {
        public static PlaceContents Read(GameObject prefab, bool room)
        {
            var contents = new PlaceContents();
            var root = prefab.transform;
            var factors = Factors(prefab);

            try { NameFacts(prefab, contents); }
            catch (Exception ex) { Faults.Skip("names of locations", prefab.name, ex); }

            var parts = new List<(string, float)>();
            foreach (var view in prefab.GetComponentsInChildren<ZNetView>(false))
            {
                if (view == null || view.transform == root) continue;
                parts.Add((PrefabName(view.gameObject.name), ChanceOf(view.transform, root, factors)));
            }
            contents.Parts = PlaceParts.Group(parts);
            contents.LeftToChance = LeftToChance(prefab);

            try { Creatures(prefab, root, factors, contents); }
            catch (Exception ex) { Faults.Skip("creatures of locations", prefab.name, ex); }

            try { Dungeon(prefab, contents); }
            catch (Exception ex) { Faults.Skip("dungeons of locations", prefab.name, ex); }

            if (room)
            {
                try { contents.Room = Shape(prefab); }
                catch (Exception ex) { Faults.Skip("dungeon rooms", prefab.name, ex); }
            }

            try { Location(prefab, contents); }
            catch (Exception ex) { Faults.Skip("locations", prefab.name, ex); }
            return contents;
        }

        /// <summary>
        /// What each object's being there depends on, besides a <c>RandomSpawn</c> on it: the share
        /// a <c>RandomObject</c> picks it by, and for a <c>RandomSpawn</c>'s object shown when it is off,
        /// the chance that it is off.
        /// </summary>
        private static Dictionary<Transform, float> Factors(GameObject prefab)
        {
            var factors = new Dictionary<Transform, float>();
            void Times(Transform t, float factor)
            {
                if (t == null) return;
                factors[t] = (factors.TryGetValue(t, out var was) ? was : 1f) * factor;
            }
            foreach (var pick in prefab.GetComponentsInChildren<RandomObject>(false))
            {
                if (pick?.m_objects == null) continue;
                // An entry without an object weighs in too; picking it leaves the whole pick out.
                var shares = PlaceParts.Shares(pick.m_objects.Select(e => e?.m_weight ?? 0f).ToList());
                var nothing = 0f;
                for (var i = 0; i < shares.Length; i++)
                {
                    var entry = pick.m_objects[i];
                    if (entry?.m_object != null) Times(entry.m_object.transform, shares[i]);
                    else nothing += shares[i];
                }
                if (nothing > 0f) Times(pick.transform, 1f - nothing);
            }
            foreach (var spawn in prefab.GetComponentsInChildren<RandomSpawn>(false))
            {
                if (spawn != null && spawn.m_OffObject != null) Times(spawn.m_OffObject.transform, 1f - Mathf.Clamp01(spawn.m_chanceToSpawn / 100f));
            }
            return factors;
        }

        /// <summary>Whether a copy of it can come out otherwise, by the rolls its copy is made with (<see cref="PlaceCopy"/>).</summary>
        private static bool LeftToChance(GameObject prefab)
        {
            var spawns = prefab.GetComponentsInChildren<RandomSpawn>(false).Where(s => s != null && s.enabled).Select(s => s.m_chanceToSpawn);
            var picks = prefab.GetComponentsInChildren<RandomObject>(false)
                .Where(p => p?.m_objects != null && p.enabled)
                .Select(p => (IReadOnlyList<float>)p.m_objects.Select(e => e?.m_weight ?? 0f).ToList());
            return PlaceParts.LeftToChance(spawns.ToList(), picks.ToList());
        }

        /// <summary>The chance a part is there: every roll on it and above it, up to the prefab's root, multiplied.</summary>
        private static float ChanceOf(Transform part, Transform root, Dictionary<Transform, float> factors)
        {
            var chance = 1f;
            for (var t = part; t != null; t = t.parent)
            {
                var spawn = t.GetComponent<RandomSpawn>();
                if (spawn != null && spawn.enabled) chance *= Mathf.Clamp01(spawn.m_chanceToSpawn / 100f);
                if (factors.TryGetValue(t, out var factor)) chance *= factor;
                if (t == root) break;
            }
            return chance;
        }

        /// <summary>What names a place, each as the game shows it (see <see cref="Places.LocationLabel"/>).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void NameFacts(GameObject prefab, PlaceContents contents)
        {
            foreach (var door in prefab.GetComponentsInChildren<Teleport>(true))
            {
                if (contents.GameName.Length == 0 && door != null) contents.GameName = CatalogBuilder.Localize(door.m_enterText);
            }
            foreach (var location in prefab.GetComponentsInChildren<Location>(true))
            {
                if (contents.GameName.Length == 0 && location != null) contents.GameName = CatalogBuilder.Localize(location.m_discoverLabel);
            }
            foreach (var bowl in prefab.GetComponentsInChildren<OfferingBowl>(true))
            {
                var boss = bowl != null && bowl.m_bossPrefab != null ? bowl.m_bossPrefab.GetComponent<Character>() : null;
                if (contents.Boss.Length == 0 && boss != null) contents.Boss = CatalogBuilder.Localize(boss.m_name);
            }
            foreach (var trader in prefab.GetComponentsInChildren<Trader>(true))
            {
                if (contents.Trader.Length == 0 && trader != null) contents.Trader = CatalogBuilder.Localize(trader.m_name);
            }
        }

        /// <summary>The creatures its spawn points place, by how many points and how likely each point is there.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Creatures(GameObject prefab, Transform root, Dictionary<Transform, float> factors, PlaceContents contents)
        {
            var found = new List<(string, float)>();
            foreach (var point in prefab.GetComponentsInChildren<CreatureSpawner>(false))
            {
                if (point?.m_creaturePrefab == null) continue;
                found.Add((point.m_creaturePrefab.name, ChanceOf(point.transform, root, factors)));
            }
            contents.Creatures = PlaceParts.Group(found);

            foreach (var vegvisir in prefab.GetComponentsInChildren<Vegvisir>(false))
            {
                if (vegvisir?.m_locations == null) continue;
                foreach (var to in vegvisir.m_locations)
                {
                    if (to != null && !string.IsNullOrEmpty(to.m_locationName) && !contents.Vegvisirs.Contains(to.m_locationName)) contents.Vegvisirs.Add(to.m_locationName);
                }
            }
        }

        /// <summary>How the dungeon or camp in it is built, when there is one (<c>DungeonGenerator</c>).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Dungeon(GameObject prefab, PlaceContents contents)
        {
            var generator = prefab.GetComponentInChildren<DungeonGenerator>(true);
            if (generator == null) return;
            var plan = new DungeonPlan
            {
                Algorithm = generator.m_algorithm.ToString(),
                Themes = (int)generator.m_themes,
                MinRooms = generator.m_minRooms,
                MaxRooms = generator.m_maxRooms,
                MinRequiredRooms = generator.m_minRequiredRooms,
                RequiredRooms = new List<string>(generator.m_requiredRooms ?? new List<string>()),
                Weighted = generator.m_alternativeFunctionality,
                DoorChance = generator.m_doorChance,
                GridSize = generator.m_gridSize,
                TileWidth = generator.m_tileWidth,
                SpawnChance = generator.m_spawnChance,
                CampRadiusMin = generator.m_campRadiusMin,
                CampRadiusMax = generator.m_campRadiusMax,
                PerimeterSections = generator.m_perimeterSections,
                PerimeterBuffer = generator.m_perimeterBuffer,
                ZoneSize = new Vec3(generator.m_zoneSize.x, generator.m_zoneSize.y, generator.m_zoneSize.z),
            };
            if (generator.m_doorTypes != null)
            {
                foreach (var door in generator.m_doorTypes) if (door != null) plan.Doors.Add((door.m_connectionType ?? "", door.m_chance));
            }
            Site(prefab, generator, plan);
            contents.Dungeon = plan;
        }

        /// <summary>
        /// Where the generator stands, for an example's zone (<see cref="DungeonLayout.Site"/>), as
        /// <c>ZoneSystem.SpawnLocation</c> puts it: with an interior of its own, at the zone's
        /// centre moved on by the interior's and the generator's own offsets and turned only by
        /// the generator's own turn, its zone as high as the generator less its offset; otherwise
        /// where it is in its location, turned with it.
        /// </summary>
        private static void Site(GameObject prefab, DungeonGenerator generator, DungeonPlan plan)
        {
            var location = prefab.GetComponent<Location>();
            var interior = location != null ? location.m_interiorTransform : null;
            var own = location != null ? location.m_generator : null;
            if (location != null && location.m_useCustomInteriorTransform && interior != null && own != null)
            {
                var at = interior.localPosition;
                var offset = own.transform.localPosition;
                var turn = Quaternion.Inverse(interior.rotation) * own.transform.rotation;
                plan.CustomInterior = true;
                plan.ZoneFromGenerator = new Vec3(-(at.x + offset.x), -offset.y, -(at.z + offset.z));
                plan.GeneratorTurn = new Quat(turn.x, turn.y, turn.z, turn.w);
                return;
            }
            var root = prefab.transform;
            var inverse = Quaternion.Inverse(root.rotation);
            var from = inverse * (generator.transform.position - root.position);
            var turned = inverse * generator.transform.rotation;
            plan.GeneratorAt = new Vec3(from.x, from.y, from.z);
            plan.GeneratorTurn = new Quat(turned.x, turned.y, turned.z, turned.w);
        }

        /// <summary>A room's box, kind and doorways, each doorway where it sits relative to the room.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static RoomShape Shape(GameObject prefab)
        {
            var room = prefab.GetComponent<Room>();
            if (room == null) return null;
            var shape = new RoomShape
            {
                Name = prefab.name,
                Theme = (int)room.m_theme,
                Size = new Vec3(room.m_size.x, room.m_size.y, room.m_size.z),
                Entrance = room.m_entrance,
                EndCap = room.m_endCap,
                Divider = room.m_divider,
                Perimeter = room.m_perimeter,
                FaceCenter = room.m_faceCenter,
                EndCapPrio = room.m_endCapPrio,
                MinPlaceOrder = room.m_minPlaceOrder,
                Weight = room.m_weight,
            };

            // Room.GetConnections: the active doorways under the room.
            var root = prefab.transform;
            var inverse = Quaternion.Inverse(root.rotation);
            foreach (var connection in prefab.GetComponentsInChildren<RoomConnection>(false))
            {
                if (connection == null) continue;
                var at = inverse * (connection.transform.position - root.position);
                var turn = inverse * connection.transform.rotation;
                shape.Doorways.Add(new Doorway
                {
                    Type = connection.m_type ?? "",
                    Entrance = connection.m_entrance,
                    AllowDoor = connection.m_allowDoor,
                    DoorOnlyIfOtherAllows = connection.m_doorOnlyIfOtherAlsoAllowsDoor,
                    Position = new Vec3(at.x, at.y, at.z),
                    Rotation = new Quat(turn.x, turn.y, turn.z, turn.w),
                });
            }
            return shape;
        }

        /// <summary>What the place's own <c>Location</c> says: the levels it gives its creatures, and how near it nothing can be built (<c>Location.IsInside</c> with its build check).</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Location(GameObject prefab, PlaceContents contents)
        {
            var location = prefab.GetComponent<Location>();
            if (location == null) return;
            contents.EnemyMinLevel = location.m_enemyMinLevelOverride;
            contents.EnemyMaxLevel = location.m_enemyMaxLevelOverride;
            contents.EnemyLevelUpChance = location.m_enemyLevelUpOverride;
            contents.NoBuild = location.m_noBuild;
            contents.NoBuildRadius = location.m_noBuildRadiusOverride > 0f ? location.m_noBuildRadiusOverride : location.GetMaxRadius();
        }

        /// <summary>A placed copy's prefab name: without Unity's " (1)" and "(Clone)".</summary>
        public static string PrefabName(string name)
        {
            var cut = name.IndexOf(" (", StringComparison.Ordinal);
            if (cut < 0) cut = name.IndexOf("(Clone)", StringComparison.Ordinal);
            return cut > 0 ? name.Substring(0, cut) : name;
        }
    }
}
