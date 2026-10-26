using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The rooms the player stands in, inside a dungeon, twice a second whether the panel is open
    /// or not: the dungeon kept on first being in it, each room stood in walked
    /// (<see cref="DungeonMaps"/>), the room stood in by the rooms' boxes as they stand in the world
    /// (<see cref="SavedRooms.Holding"/>). Outside an interior it does nothing more than ask. The
    /// maps are the character's whose book the compendium holds, kept in a file of the same name
    /// under maps, read and written on a worker thread as the book is, a file that could not be
    /// read never written over.
    /// </summary>
    internal static class DungeonWalks
    {
        /// <summary>Leaving a world lets go of what is held of it (<see cref="WorldCaches"/>).</summary>
        static DungeonWalks() => WorldCaches.Register(nameof(DungeonWalks), Forget);

        /// <summary>How often the room stood in is looked for, in seconds.</summary>
        private const float Every = 0.5f;

        /// <summary>How long a quitting game waits for the maps to be written.</summary>
        private const int QuitWaitMs = 2000;

        private static readonly FileQueue Files = new FileQueue();

        private static float _next;

        /// <summary>The character's maps, the file they are kept in, and the reading of another's.</summary>
        private static DungeonMaps _own = new DungeonMaps();
        private static string _file;
        private static Task<(DungeonMaps Maps, string Aside)> _reading;
        private static bool _unreadable;
        private static float _savedAt;

        /// <summary>Maps the self-test keeps in place of the character's, and where it stands the player.</summary>
        private static DungeonMaps _instead;
        private static (string In, int At, Vec3 Where, float Yaw)? _standIn;

        /// <summary>The dungeons this character has been in, and how far each was walked.</summary>
        public static DungeonMaps Maps => _instead ?? _own;

        /// <summary>Whether the character's maps are read, so what it walked is all here.</summary>
        public static bool Ready => _file != null && _reading == null && !_unreadable;

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

        /// <summary>Forgets one dungeon's map, saved at once.</summary>
        public static void ForgetMap(string id)
        {
            if (Maps.Forget(id)) Save(now: true);
        }

        /// <summary>Forgets the maps of the world played, saved at once; how many were forgotten.</summary>
        public static int ForgetWorld()
        {
            var world = CompendiumMap.World;
            var forgotten = world != null ? Maps.ForgetWorld(world) : 0;
            if (forgotten > 0) Save(now: true);
            return forgotten;
        }

        public static void Update()
        {
            Faults.TellEach(Feature.DungeonMaps, Files.Failures());
            var file = CompendiumKeeper.CharacterFile;
            if (file != _file) Begin(file);
            if (_reading != null && !Opened()) return;
            if (_file == null) return;
            Save();

            if (_standIn != null || Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + Every;
            var player = Player.m_localPlayer;
            if (player != null && !player.InInterior()) VisitInWorld(player);
            var world = player != null && player.InInterior() ? WorldDungeons.AtPlayer() : null;
            if (world == null || world.Id == null)
            {
                YouIn = null;
                YouAt = -1;
                return;
            }
            // Today, as the game counts its days: the dungeon's first day where it is kept anew, its last day while in it.
            var day = EnvMan.instance != null ? EnvMan.instance.GetDay() : -1;
            var entered = YouIn != world.Id;
            Maps.Keep(world.Id, world.Location, world.Saved, world.Entrance, world.Generator, world.Turn, day);
            var newDay = Maps.Visit(world.Id, day);
            var at = player.transform.position;
            YouIn = world.Id;
            YouAt = SavedRooms.Holding(world.Saved, world.SizeOf, new Vec3(at.x, at.y, at.z));
            // The server hears of each visit and each new day in it, so its last day moves as anyone walks it, as well as of each room new to this walk.
            var walked = YouAt >= 0 && Maps.Walk(world.Id, YouAt);
            if (walked || entered || newDay) GroupMapsLink.Walked(world.Id);
        }

        /// <summary>
        /// A place built as a dungeon whose rooms stand in the world (<see cref="DungeonKinds.InWorld"/>),
        /// as Hildir's Sealed Tower, stood in as the compendium counts it (<c>Location.GetLocation</c>):
        /// kept by where it stands, with the day, no rooms counted; never told to the server, as it
        /// holds no walk.
        /// </summary>
        private static void VisitInWorld(Player player)
        {
            var place = Location.GetLocation(player.transform.position);
            if (place == null) return;
            var name = Utils.GetPrefabName(place.gameObject);
            var world = CompendiumMap.World;
            if (world == null || !DungeonKinds.InWorld(name)) return;
            var at = place.transform.position;
            var day = EnvMan.instance != null ? EnvMan.instance.GetDay() : -1;
            Maps.Visited(DungeonMaps.EntranceIdOf(world, at.x, at.z), name, new Vec3(at.x, at.y, at.z), day);
        }

        /// <summary>Writes the maps once they changed, at most every ten seconds unless asked to now (<see cref="CompendiumBook.WriteEvery"/>), on a worker thread.</summary>
        public static void Save(bool now = false)
        {
            if (_file == null || _reading != null || _unreadable || !_own.Changed || (!now && Time.unscaledTime - _savedAt < CompendiumBook.WriteEvery)) return;
            _savedAt = Time.unscaledTime;
            var text = _own.ToSave();
            var path = PathOf(_file);
            Files.Then("keeping the maps of the dungeons walked", () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                FileSwap.Write(path, aside => File.WriteAllText(aside, text()), lasting: true);
            });
        }

        /// <summary>Writes the maps and waits a moment for them to be written, as the game quits.</summary>
        public static void Finish()
        {
            Save(now: true);
            Files.Then(() => true).Wait(QuitWaitMs);
        }

        /// <summary>Starts reading another character's maps, once those held are written.</summary>
        private static void Begin(string file)
        {
            Save(now: true);
            _file = file;
            _own = new DungeonMaps();
            _unreadable = false;
            _reading = null;
            if (file == null) return;
            var path = PathOf(file);
            _reading = Files.Then(() => Read(path));
        }

        /// <summary>Takes the maps read, once they are: those that could not be read set aside and new ones started, told once; none, the file never written over, where even that failed.</summary>
        private static bool Opened()
        {
            if (!_reading.IsCompleted) return false;
            if (_reading.Status == TaskStatus.RanToCompletion)
            {
                _own = _reading.Result.Maps;
                if (_reading.Result.Aside != null)
                {
                    Faults.Tell(Feature.DungeonMaps, "reading the maps of the dungeons walked", CompendiumWords.SetAside(Path.GetFileName(_reading.Result.Aside)));
                    SetAsideNotice.Files.Tell(SetAsideKind.Walks, _reading.Result.Aside);
                }
            }
            else
            {
                Faults.Tell(Feature.DungeonMaps, "reading the maps of the dungeons walked", _reading.Exception?.GetBaseException());
                _own = new DungeonMaps();
                _unreadable = true;
            }
            _reading = null;
            return true;
        }

        /// <summary>The maps kept for a character, those kept under a generator's number re-keyed by place and so written again (<see cref="DungeonMaps.ByPlace"/>); none for a character never kept. On a worker thread.</summary>
        private static (DungeonMaps Maps, string Aside) Read(string path)
        {
            var maps = KeptFile.Read(path, text => DungeonMaps.Load(text).ByPlace(), CompendiumKeeper.Stamp(), out var aside);
            return (maps, aside);
        }

        private static string PathOf(string file) => Path.Combine(Settings.CacheFolder, "maps", file);
    }
}
