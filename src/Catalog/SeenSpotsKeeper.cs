using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Where the character played saw things worth finding again (<see cref="SeenSpots"/>), kept in
    /// a file of its own beside the dungeon maps, read and written on a worker thread as those are
    /// (<see cref="DungeonWalks"/>): read when the character changes, written once changed at most
    /// every ten seconds, a file that does not read set aside and a new one started. What the
    /// sight records goes in through <see cref="Saw"/> and <see cref="Gone"/>, for the world played.
    /// </summary>
    internal static class SeenSpotsKeeper
    {
        /// <summary>How long a quitting game waits for the spots to be written.</summary>
        private const int QuitWaitMs = 2000;

        private static readonly FileQueue Files = new FileQueue();

        /// <summary>The character's spots, the file they are kept in, and the reading of another's.</summary>
        private static SeenSpots _own = new SeenSpots();
        private static string _file;
        private static Task<(SeenSpots Spots, string Aside)> _reading;
        private static bool _unreadable;
        private static float _savedAt;

        /// <summary>The spots the character saw, in every world.</summary>
        public static SeenSpots Spots => _own;

        /// <summary>Whether the character's spots are read, so what it saw is all here and new ones may be kept.</summary>
        public static bool Ready => _file != null && _reading == null && !_unreadable;

        /// <summary>Whether the character's spots could not be read, so none are kept while it plays.</summary>
        public static bool Unreadable => _unreadable;

        /// <summary>How many of a kind the character saw in the world played; none before its spots are read. Counted once a frame for the kind last asked, as a page asks each time it is drawn.</summary>
        public static int CountOf(string kind)
        {
            if (!Ready) return 0;
            if (_countedIn == Time.frameCount && string.Equals(kind, _countedKind, System.StringComparison.Ordinal)) return _counted;
            _countedIn = Time.frameCount;
            _countedKind = kind;
            _counted = _own.CountOf(CompendiumMap.World, kind);
            return _counted;
        }

        private static int _countedIn = -1, _counted;
        private static string _countedKind;

        /// <summary>Changes whenever the spots held change, another character's read in among them, for what is drawn of them to be worked out again.</summary>
        public static (int Read, int Version) Stamp => (Reads, _own.Version);

        /// <summary>How many times another character's spots were read in, or begun anew.</summary>
        private static int Reads { get; set; }

        /// <summary>The spots of a kind the character saw in the world played; none before its spots are read.</summary>
        public static IReadOnlyList<(int X, int Z)> Of(string kind) => Ready ? _own.Of(CompendiumMap.World, kind) : System.Array.Empty<(int, int)>();

        /// <summary>A thing worth finding again seen at x and z in the world played: kept as a spot, where recording is on; whether it was new.</summary>
        public static bool Saw(string kind, float x, float z)
        {
            var world = CompendiumMap.World;
            return Ready && Settings.RecordSeenSpots && world != null && _own.Saw(world, kind, x, z);
        }

        /// <summary>A thing worth finding again gone from x and z in the world played: its spot taken away; whether one was kept.</summary>
        public static bool Gone(string kind, float x, float z)
        {
            var world = CompendiumMap.World;
            return Ready && world != null && _own.Gone(world, kind, x, z);
        }

        /// <summary>Forgets the spots kept in the world played, written at once; how many; none where no character's spots are read.</summary>
        public static int? ForgetWorld()
        {
            var world = CompendiumMap.World;
            if (!Ready || world == null) return null;
            var count = _own.CountIn(world);
            _own.Forget(world);
            Save(now: true);
            CompendiumSight.LookAgainForSpots();
            return count;
        }

        public static void Update()
        {
            Faults.TellEach(Feature.SeenSpots, Files.Failures());
            CompendiumSight.WatchGone();
            var file = CompendiumKeeper.CharacterFile;
            if (file != _file) Begin(file);
            if (_reading != null && !Opened()) return;
            Save();
        }

        /// <summary>Writes the spots once they changed, at most every ten seconds unless asked to now (<see cref="CompendiumBook.WriteEvery"/>), on a worker thread.</summary>
        public static void Save(bool now = false)
        {
            if (_file == null || _reading != null || _unreadable || !_own.Changed || (!now && Time.unscaledTime - _savedAt < CompendiumBook.WriteEvery)) return;
            _savedAt = Time.unscaledTime;
            var text = _own.ToSave();
            var path = PathOf(_file);
            Files.Then("keeping where things were seen", () =>
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                FileSwap.Write(path, aside => File.WriteAllText(aside, text()), lasting: true);
            });
        }

        /// <summary>Writes the spots and waits a moment for them to be written, as the game quits.</summary>
        public static void Finish()
        {
            Save(now: true);
            Files.Then(() => true).Wait(QuitWaitMs);
        }

        /// <summary>Starts reading another character's spots, once those held are written.</summary>
        private static void Begin(string file)
        {
            Save(now: true);
            _file = file;
            _own = new SeenSpots();
            Reads++;
            _unreadable = false;
            _reading = null;
            if (file == null) return;
            var path = PathOf(file);
            _reading = Files.Then(() => Read(path));
        }

        /// <summary>Takes the spots read, once they are: those that could not be read set aside and new ones started, told once; none, the file never written over, where even that failed.</summary>
        private static bool Opened()
        {
            if (!_reading.IsCompleted) return false;
            if (_reading.Status == TaskStatus.RanToCompletion)
            {
                _own = _reading.Result.Spots;
                Reads++;
                if (_reading.Result.Aside != null) Faults.Tell(Feature.SeenSpots, "reading where things were seen", CompendiumWords.SetAside(Path.GetFileName(_reading.Result.Aside)));
            }
            else
            {
                Faults.Tell(Feature.SeenSpots, "reading where things were seen", _reading.Exception?.GetBaseException());
                _own = new SeenSpots();
                Reads++;
                _unreadable = true;
            }
            _reading = null;
            return true;
        }

        /// <summary>The spots kept for a character; none for a character never kept. On a worker thread.</summary>
        private static (SeenSpots Spots, string Aside) Read(string path)
        {
            var spots = KeptFile.Read(path, SeenSpots.Load, CompendiumKeeper.Stamp(), out var aside);
            return (spots, aside);
        }

        public static string PathOf(string file) => Path.Combine(Settings.CacheFolder, "spots", file);
    }
}
