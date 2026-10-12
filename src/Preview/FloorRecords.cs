using System.IO;

namespace Scry
{
    /// <summary>
    /// Where the places' floors are, kept from one session to the next (<see cref="FloorRecord"/>):
    /// a file for each kind of place in <c>floors</c> under Scry's cache folder, each read only
    /// once its kind of place is shown (<see cref="FloorFiles"/>): wanted as its copy starts to be
    /// made, found on a worker thread meanwhile, and written on one as a record grows
    /// (<see cref="FileQueue"/>), aside and swapped into place (<see cref="FileSwap"/>). A record
    /// whose stamp is not the place's now is passed over, and its file written anew once the place
    /// is probed again.
    /// </summary>
    internal static class FloorRecords
    {
        static FloorRecords() => WorldCaches.Register(nameof(FloorRecords), Forget);

        private static readonly FileQueue Files = new FileQueue();
        private static FloorFiles _kept;

        private static string Folder => Path.Combine(Settings.CacheFolder, "floors");

        /// <summary>How many kinds of place have their floors kept, for the resource monitor and the self-test.</summary>
        public static int Count => _kept?.Count ?? 0;

        /// <summary>Whether a file is still being listed, found or written.</summary>
        public static bool Busy => Files.Busy;

        /// <summary>Forgets what is held, on leaving a world; what is being written is written still.</summary>
        public static void Forget() => _kept = null;

        /// <summary>Starts listing what was kept, on entering a world; each kind of place is read once shown.</summary>
        public static void Begin() => _kept = new FloorFiles(Folder, Files);

        /// <summary>Starts finding what is kept of a kind of place on a worker thread, as a copy of it starts to be made.</summary>
        public static void Want(string prefab, bool room) => _kept?.Want(prefab, room);

        /// <summary>What is kept of a kind of place with this stamp, waited for while still being found; null for none, or none known yet.</summary>
        public static FloorRecord Find(string prefab, bool room, string stamp)
        {
            if (_kept == null) return null;
            var timed = Timing.Start();
            var record = _kept.Find(prefab, room, stamp);
            Timing.Add("floors kept found", timed);
            return record;
        }

        /// <summary>Keeps a record, and writes it to its file on a worker thread.</summary>
        public static void Put(FloorRecord record) => _kept?.Put(record);

        /// <summary>Forgets every kind of place's floors, and removes their files, after anything still being written.</summary>
        public static void Clear() => (_kept ?? new FloorFiles(Folder, Files)).Clear();

        /// <summary>Tells any listing, finding or writing that failed; called each frame.</summary>
        public static void Update()
        {
            if (_kept != null) foreach (var (doing, error) in _kept.Failures()) Faults.Tell(Feature.FloorsKept, doing, error);
            foreach (var (doing, error) in Files.Failures()) Faults.Tell(Feature.FloorsKept, doing, error);
        }
    }
}
