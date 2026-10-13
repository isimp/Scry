using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Catches a character's book up from its explored map once per world (<see cref="MapCatchUp"/>):
    /// the kinds of place lying where it explored count as seen. Where this game is the world's
    /// server (single player, or hosting) it holds where every place is
    /// (<c>ZoneSystem.m_locationInstances</c>); a dedicated server's client asks the server's Scry
    /// (<see cref="CompendiumLink"/>), which never sends positions back. Only the character's own
    /// exploring counts (<c>Minimap.m_explored</c>), not a map shared at a cartography table. The
    /// cells are worked out on a worker thread from copies taken here; nothing is read before the
    /// character has stood in the world a while, so its map is in.
    /// </summary>
    internal static class CompendiumMap
    {
        static CompendiumMap() => WorldCaches.Register(nameof(CompendiumMap), Forget);

        /// <summary>How long the character stands in the world before its map is read.</summary>
        private const float SettleSeconds = 10f;

        private static AccessTools.FieldRef<Minimap, BitArray> _exploredBits;
        private static Task<List<string>> _reading;
        private static string _readingFor;
        private static string _failedFor;
        private static Player _player;
        private static float _playerSince;

        /// <summary>The world as the book knows it: its id, as the game keeps the character's map of it; none outside a world.</summary>
        public static string World => ZNet.instance != null ? Stored.Count(ZNet.instance.GetWorldUID()) : null;

        /// <summary>Whether the explored map is being read now.</summary>
        public static bool Reading => _reading != null;

        /// <summary>Once a second: catches the book up when its world has not been, or takes in what was worked out.</summary>
        public static void Update(CompendiumBook book)
        {
            if (_reading != null)
            {
                if (_reading.IsCompleted) Take(book);
                return;
            }

            var player = Player.m_localPlayer;
            if (player != _player)
            {
                _player = player;
                _playerSince = Time.unscaledTime;
            }
            var world = World;
            if (book == null || player == null || world == null || book.Mapped(world) || world == _failedFor) return;
            if (Time.unscaledTime - _playerSince < SettleSeconds) return;

            if (ZNet.instance.IsServer()) Start(world, Places());
            else CompendiumLink.Ask(world);
        }

        /// <summary>Reads the map again for the character played in this world: for <c>/scry map</c> and the button.</summary>
        public static string Again(CompendiumBook book)
        {
            var world = World;
            if (book == null || world == null) return CompendiumWords.MapNoWorld;
            book.Unmap(world);
            _failedFor = null;
            return CompendiumWords.MapAgain;
        }

        /// <summary>Takes in the kinds of place a server told lie in the explored map.</summary>
        public static void Told(CompendiumBook book, string world, List<string> kinds)
        {
            if (book == null || world == null || world != World || book.Mapped(world)) return;
            var raised = MapCatchUp.Apply(book, world, kinds);
            Log.Report(CompendiumWords.MapRead(kinds.Count, raised, fromServer: true));
        }

        /// <summary>Gives up on this world for the session, as when its server has no Scry to ask.</summary>
        public static void GiveUp(string world) => _failedFor = world;

        /// <summary>The explored map of the character played, packed on a worker thread from a copy taken now; none before it is in.</summary>
        public static Task<byte[]> PackOnWorker()
        {
            var (size, pixel, words) = Copy();
            return words == null ? null : Task.Run(() => Grid(size, pixel, words).Pack());
        }

        /// <summary>The explored map of the character played, packed; none before it is in.</summary>
        public static byte[] PackedMap()
        {
            var (size, pixel, words) = Copy();
            return words == null ? null : Grid(size, pixel, words).Pack();
        }

        /// <summary>Where every place this game holds is, by its kind: only a world's server holds them all.</summary>
        public static List<(string Kind, float X, float Z)> Places()
        {
            var places = new List<(string, float, float)>();
            var zones = ZoneSystem.instance;
            if (zones == null) return places;
            foreach (var instance in zones.m_locationInstances.Values)
            {
                var location = instance.m_location;
                if (location == null) continue;
                var name = string.IsNullOrEmpty(location.m_prefabName) ? location.m_prefab.Name : location.m_prefabName;
                places.Add((name, instance.m_position.x, instance.m_position.z));
            }
            return places;
        }

        /// <summary>Works out on a worker thread which kinds of place lie in the explored map.</summary>
        private static void Start(string world, List<(string Kind, float X, float Z)> places)
        {
            var (size, pixel, words) = Copy();
            if (words == null) return;
            _readingFor = world;
            _reading = Task.Run(() => MapCatchUp.Kinds(Grid(size, pixel, words), places));
        }

        private static void Take(CompendiumBook book)
        {
            var reading = _reading;
            var world = _readingFor;
            _reading = null;
            _readingFor = null;
            if (reading.Status != TaskStatus.RanToCompletion)
            {
                _failedFor = world;
                Faults.Tell(Feature.CompendiumMap, "reading the explored map", reading.Exception?.GetBaseException());
                return;
            }
            if (book == null || world != World || book.Mapped(world)) return;
            var raised = MapCatchUp.Apply(book, world, reading.Result);
            Log.Report(CompendiumWords.MapRead(reading.Result.Count, raised, fromServer: false));
        }

        /// <summary>The map's size, its pixels' width and a copy of its explored bits; no bits before the character's map is in (it always has the ground it stands on explored).</summary>
        private static (int Size, float Pixel, int[] Words) Copy()
        {
            var map = Minimap.instance;
            if (map == null) return (0, 0f, null);
            if (_exploredBits == null) _exploredBits = AccessTools.FieldRefAccess<Minimap, BitArray>("m_explored");
            var bits = _exploredBits(map);
            var size = map.m_textureSize;
            if (bits == null || size <= 0 || bits.Length < size * size) return (0, 0f, null);
            var words = new int[(bits.Length + 31) / 32];
            bits.CopyTo(words, 0);
            return Array.TrueForAll(words, w => w == 0) ? (0, 0f, null) : (size, map.m_pixelSize, words);
        }

        private static ExploredGrid Grid(int size, float pixel, int[] words) =>
            ExploredGrid.FromMap(size, pixel, i => (words[i >> 5] & (1 << (i & 31))) != 0);

        private static void Forget()
        {
            _reading = null;
            _readingFor = null;
            _failedFor = null;
            _player = null;
        }
    }
}
