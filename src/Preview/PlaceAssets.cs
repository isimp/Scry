using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The bundle of the location or dungeon room selected, loaded in the background and held
    /// while it is shown, one at a time: what it holds is read from it and its stage copy made
    /// from it. It is let go of when another entry is selected or the panel closes, and a release
    /// unloads the bundle once nothing else holds it, so nothing of it stays in memory.
    /// </summary>
    internal static class PlaceAssets
    {
        /// <summary>Leaving a world lets go of what is held of it (<see cref="WorldCaches"/>).</summary>
        static PlaceAssets() => WorldCaches.Register(nameof(PlaceAssets), Release);

        private static PlaceSource _source;
        private static bool _holding;
        private static bool _told;

        /// <summary>Whether the held place's bundle could not be loaded.</summary>
        public static bool Failed { get; private set; }

        /// <summary>The place whose bundle is held, or null, for the self-test to see nothing is left held.</summary>
        public static PlaceSource Held => _holding ? _source : null;

        /// <summary>How far a place's model has got; one not held yet is on its way, as the next frame asks for it.</summary>
        public static PlaceLoad State(PlaceSource source)
        {
            if (source == null) return PlaceLoad.None;
            if (!_holding || !ReferenceEquals(source, _source)) return PlaceLoad.Loading;
            if (Failed) return PlaceLoad.Failed;
            if (!_source.Reference.IsLoaded) return PlaceLoad.Loading;
            return _source.Reference.Asset != null ? PlaceLoad.Ready : PlaceLoad.Failed;
        }

        /// <summary>Asks for a place's bundle, letting go of any other; null lets go of all. True when it starts loading one.</summary>
        public static bool Hold(PlaceSource source)
        {
            if (ReferenceEquals(source, _source)) return false;
            Release();
            if (source == null) return false;
            _source = source;
            _holding = true;
            _told = false;
            Failed = false;

            // Holds a reference until released, as Load does; loads over the next frames.
            source.Reference.LoadAsync();
            return true;
        }

        public static void Release()
        {
            // Let go of once no copy is being made from it.
            if (_holding)
            {
                var reference = _source.Reference;
                Ghost.Building.WhenIdle(() => reference.Release());
            }
            _holding = false;
            _source = null;
            Failed = false;
        }

        /// <summary>The place's loaded prefab while it is held, or null while it loads, when it failed, or for another place.</summary>
        public static GameObject Asset(PlaceSource source)
        {
            if (!_holding || !ReferenceEquals(source, _source) || !_source.Reference.IsLoaded) return null;
            return _source.Reference.Asset;
        }

        /// <summary>Checks on the held place each frame: Ready or Failed once, on the frame it comes to that, else None.</summary>
        public static PlaceLoad Update()
        {
            if (!_holding || Failed || _told) return PlaceLoad.None;
            if (_source.Reference.IsLoaded)
            {
                _told = true;
                _source.LoadFailed = _source.Reference.Asset == null;
                if (!_source.LoadFailed) return PlaceLoad.Ready;
            }
            else if (_source.Reference.IsLoading)
            {
                return PlaceLoad.None;
            }
            Failed = true;
            _source.LoadFailed = true;
            Faults.Skip("previews of places", _source.Prefab, "it did not load");
            return PlaceLoad.Failed;
        }
    }

    /// <summary>
    /// A location or room as the game could have placed it: a copy of it, its parts that are there
    /// only by chance rolled as the game rolls them when it first builds a zone
    /// (<c>ZoneSystem.SpawnLocation</c>): each <c>RandomSpawn</c> at its chance, showing its object
    /// for when it is off otherwise, then each <c>RandomObject</c> picking one of its objects by
    /// weight. The rolls are made on the copy while it still sleeps, so the prefab itself is
    /// never touched.
    ///
    /// A dungeon's interior is left out: the game builds it thousands of metres above its
    /// entrance, and on the stage it would stand so far off that the camera could frame
    /// neither. So is a view of the outside that the game shows only to someone inside
    /// (<c>EnvZone.m_exteriorMesh</c>).
    /// </summary>
    internal static class PlaceCopy
    {
        private static readonly System.Random Dice = new System.Random();

        /// <summary>How far above its root a part must be to be an interior: the game's stand some 5,000 m up.</summary>
        private const float InteriorHeight = 1000f;

        /// <summary>
        /// Starts the copy, made over the next frames as <see cref="Ghost.Building"/> goes on; its
        /// pose in the world, or in its parent's space. Its creature spawn points that are there
        /// once rolled go into <paramref name="spawns"/>, by the rules of the location they are
        /// in (<paramref name="rules"/>, else the copy's own).
        /// </summary>
        public static Ghost.Building Begin(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, int layer, bool keepColliders = false, bool local = false,
            List<Stage.SpawnHere> spawns = null, Location rules = null, List<GroundPaintAt> paints = null)
        {
            return new Ghost.Building(prefab, parent, position, rotation, layer, copy => Prepare(copy, spawns, rules, paints), keepColliders, local);
        }

        private static void Prepare(GameObject copy, List<Stage.SpawnHere> spawns, Location rules, List<GroundPaintAt> paints = null)
        {
            Roll(copy);
            LeaveOutInterior(copy);
            if (spawns != null)
            {
                foreach (var spawn in PlaceSpawns.Of(copy, rules != null ? rules : copy.GetComponent<Location>()))
                {
                    spawns.Add(new Stage.SpawnHere { At = spawn.At, Creature = spawn.Creature, Lift = spawn.Lift, Point = spawn.Point });
                }
                if (spawns.Count > 0) FloorProbe.NoteNotSolid(copy);
            }
            if (paints != null) ReadPaints(copy, paints);
            foreach (var zone in copy.GetComponentsInChildren<EnvZone>(true))
            {
                if (zone != null && zone.m_exteriorMesh != null) zone.m_exteriorMesh.enabled = false;
            }
        }

        /// <summary>Switches off each part under the root that stands an interior's height above it, and the one holding a dungeon generator there.</summary>
        private static void LeaveOutInterior(GameObject copy)
        {
            var root = copy.transform;
            foreach (Transform part in root)
            {
                if (part.localPosition.y >= InteriorHeight) part.gameObject.SetActive(false);
            }
            foreach (var generator in copy.GetComponentsInChildren<DungeonGenerator>(true))
            {
                if (generator == null || generator.transform == root) continue;
                if (root.InverseTransformPoint(generator.transform.position).y < InteriorHeight) continue;
                var top = generator.transform;
                while (top.parent != null && top.parent != root) top = top.parent;
                top.gameObject.SetActive(false);
            }
        }

        private static void Roll(GameObject copy)
        {
            var root = copy.transform;
            foreach (var spawn in copy.GetComponentsInChildren<RandomSpawn>(true))
            {
                if (spawn == null || !spawn.enabled || !Animators.SwitchedOn(spawn.transform, root)) continue;
                var there = Dice.NextDouble() * 100.0 <= spawn.m_chanceToSpawn;
                if (!there) spawn.gameObject.SetActive(false);
                if (spawn.m_OffObject != null) spawn.m_OffObject.SetActive(!there);
            }
            foreach (var pick in copy.GetComponentsInChildren<RandomObject>(true))
            {
                if (pick.OrNull()?.m_objects == null || !pick.enabled || !Animators.SwitchedOn(pick.transform, root)) continue;
                // An entry without an object weighs in too, and picking it leaves the whole pick out.
                var at = PlaceParts.Pick(pick.m_objects.Select(e => e?.m_weight ?? 0f).ToList(), Dice.NextDouble());
                var chosen = at >= 0 ? pick.m_objects[at]?.m_object : null;
                foreach (var entry in pick.m_objects) if (entry?.m_object != null) entry.m_object.SetActive(entry.m_object == chosen);
                if (chosen == null) pick.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// What the place paints on the ground under it once rolled (<c>TerrainModifier</c>): its
        /// paths, dirt and paving, read while the copy still sleeps, as stripping takes them off.
        /// </summary>
        private static void ReadPaints(GameObject copy, List<GroundPaintAt> into)
        {
            var root = copy.transform;
            foreach (var modifier in copy.GetComponentsInChildren<TerrainModifier>(true))
            {
                if (modifier == null || !modifier.enabled || !modifier.m_paintCleared || modifier.m_paintRadius <= 0f || !Animators.SwitchedOn(modifier.transform, root)) continue;
                into.Add(new GroundPaintAt
                {
                    At = modifier.transform, Radius = modifier.m_paintRadius, Strength = modifier.m_paintStrength, Type = modifier.m_paintType, Order = modifier.m_sortOrder,
                });
            }
        }
    }
}
