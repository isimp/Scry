using System;
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

        /// <summary>Whether the held place's bundle is still loading.</summary>
        public static bool Loading => _holding && !Failed && !_source.Reference.IsLoaded;

        /// <summary>Asks for a place's bundle, letting go of any other; null lets go of all.</summary>
        public static void Hold(PlaceSource source)
        {
            if (ReferenceEquals(source, _source)) return;
            Release();
            if (source == null) return;
            _source = source;
            _holding = true;
            _told = false;
            Failed = false;

            // Holds a reference until released, as Load does; loads over the next frames.
            source.Reference.LoadAsync();
        }

        public static void Release()
        {
            if (_holding) _source.Reference.Release();
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

        /// <summary>Checks on the held place each frame: true once, on the frame it is ready.</summary>
        public static bool Update()
        {
            if (!_holding || Failed || _told) return false;
            if (_source.Reference.IsLoaded)
            {
                _told = true;
                return _source.Reference.Asset != null;
            }
            if (!_source.Reference.IsLoading)
            {
                Failed = true;
                Plugin.Log.LogWarning($"Scry could not load the location or dungeon room {_source.Prefab} to show it.");
            }
            return false;
        }
    }

    /// <summary>
    /// A location or room as the game could have placed it: a copy of it, its parts that are there
    /// only by chance rolled as the game rolls them when it first builds a zone
    /// (<c>ZoneSystem.SpawnLocation</c>): each <c>RandomSpawn</c> at its chance, showing its object
    /// for when it is off otherwise, then each <c>RandomObject</c> picking one of its objects by
    /// weight. The rolls are made on the copy while it still sleeps, so the prefab itself is
    /// never touched.
    /// </summary>
    internal static class PlaceCopy
    {
        private static readonly System.Random Dice = new System.Random();

        public static GameObject Make(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, int layer)
        {
            return Ghost.Make(prefab, parent, position, rotation, layer, prepare: Roll);
        }

        private static void Roll(GameObject copy)
        {
            var root = copy.transform;
            foreach (var spawn in copy.GetComponentsInChildren<RandomSpawn>(true))
            {
                if (spawn == null || !spawn.enabled || !ActiveUnder(spawn.transform, root)) continue;
                var there = Dice.NextDouble() * 100.0 <= spawn.m_chanceToSpawn;
                if (!there) spawn.gameObject.SetActive(false);
                if (spawn.m_OffObject != null) spawn.m_OffObject.SetActive(!there);
            }
            foreach (var pick in copy.GetComponentsInChildren<RandomObject>(true))
            {
                if (pick?.m_objects == null || !pick.enabled || !ActiveUnder(pick.transform, root)) continue;
                var total = 0f;
                foreach (var entry in pick.m_objects) if (entry?.m_object != null) total += entry.m_weight;
                var roll = (float)Dice.NextDouble() * total;
                GameObject chosen = null;
                foreach (var entry in pick.m_objects)
                {
                    if (entry?.m_object == null) continue;
                    roll -= entry.m_weight;
                    if (roll <= 0f && chosen == null) chosen = entry.m_object;
                }
                foreach (var entry in pick.m_objects) if (entry?.m_object != null) entry.m_object.SetActive(entry.m_object == chosen);
            }
        }

        /// <summary>Whether a part and everything above it up to the copy's root is switched on, as the prefab has it.</summary>
        private static bool ActiveUnder(Transform part, Transform root)
        {
            for (var t = part; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t == root) return true;
            }
            return true;
        }
    }
}
