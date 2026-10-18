using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The rooms the player stands in, inside a dungeon, twice a second whether the panel is open
    /// or not: the dungeon kept on first being in it, each room stood in walked
    /// (<see cref="DungeonMaps"/>), the room stood in by the rooms' boxes as they stand in the world
    /// (<see cref="SavedRooms.Holding"/>). Outside an interior it does nothing more than ask.
    /// </summary>
    internal static class DungeonWalks
    {
        /// <summary>Leaving a world lets go of what is held of it (<see cref="WorldCaches"/>).</summary>
        static DungeonWalks() => WorldCaches.Register(nameof(DungeonWalks), Forget);

        /// <summary>How often the room stood in is looked for, in seconds.</summary>
        private const float Every = 0.5f;

        private static float _next;

        private static readonly DungeonMaps Own = new DungeonMaps();

        /// <summary>Maps the self-test keeps in place of the character's, and where it stands the player.</summary>
        private static DungeonMaps _instead;
        private static (string In, int At, Vec3 Where, float Yaw)? _standIn;

        /// <summary>The dungeons this character has been in, and how far each was walked.</summary>
        public static DungeonMaps Maps => _instead ?? Own;

        /// <summary>The map id of the dungeon the player stands in, none outside one.</summary>
        public static string YouIn { get; private set; }

        /// <summary>The room the player stands in, by its place in the dungeon's saved rooms; -1 in none.</summary>
        public static int YouAt { get; private set; } = -1;

        public static void Forget()
        {
            _next = 0f;
            YouIn = null;
            YouAt = -1;
            _instead = null;
            _standIn = null;
        }

        /// <summary>Keeps made-up maps in place of the character's, for the self-test; none puts them back.</summary>
        [Diagnostic]
        public static void MapsInsteadForTest(DungeonMaps maps) => _instead = maps;

        /// <summary>Stands the player in a room of a dungeon, where and facing as given, for the self-test; none ends it.</summary>
        [Diagnostic]
        public static void StandInForTest((string In, int At, Vec3 Where, float Yaw)? stand)
        {
            _standIn = stand;
            YouIn = stand?.In;
            YouAt = stand?.At ?? -1;
        }

        /// <summary>Where the player stands in the world and which way they face, in degrees about the upright; false without a player.</summary>
        public static bool YouStand(out Vec3 where, out float yaw)
        {
            if (_standIn.HasValue)
            {
                where = _standIn.Value.Where;
                yaw = _standIn.Value.Yaw;
                return true;
            }
            var player = Player.m_localPlayer;
            where = default;
            yaw = 0f;
            if (player == null) return false;
            var p = player.transform.position;
            where = new Vec3(p.x, p.y, p.z);
            yaw = player.transform.eulerAngles.y;
            return true;
        }

        public static void Update()
        {
            if (_standIn != null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Every;
            var player = Player.m_localPlayer;
            var world = player != null && player.InInterior() ? WorldDungeons.AtPlayer() : null;
            if (world == null || world.Id == null)
            {
                YouIn = null;
                YouAt = -1;
                return;
            }
            Maps.Keep(world.Id, world.Location, world.Saved, world.Entrance);
            var at = player.transform.position;
            YouIn = world.Id;
            YouAt = SavedRooms.Holding(world.Saved, world.SizeOf, new Vec3(at.x, at.y, at.z));
            if (YouAt >= 0) Maps.Walk(world.Id, YouAt);
        }
    }
}
