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

        /// <summary>How many kinds of place have their record held, those shown in this session, for the self-test.</summary>
        [ForTest]
        public static int Held => _kept?.Held ?? 0;

        /// <summary>Whether a file is still being listed, found or written.</summary>
        public static bool Busy => Files.Busy;

        /// <summary>Forgets what is held, on leaving a world; what is being written is written still.</summary>
        public static void Forget() => _kept = null;

        /// <summary>Starts listing what was kept, on entering a world; each kind of place is read once shown.</summary>
        public static void Begin() => _kept = new FloorFiles(Folder, Files);

        /// <summary>Starts finding what is kept of a kind of place on a worker thread, as a copy of it starts to be made.</summary>
        public static void Want(string prefab, bool room) => _kept?.Want(prefab, room);

        /// <summary>
        /// Whether what is kept of a kind of place is found, so finding it waits for nothing; asked
        /// each frame before its floors are read, the stage going on meanwhile, as the files are
        /// read one after another and one wanted after many others waits behind them all.
        /// </summary>
        public static bool Ready(string prefab, bool room) => _kept == null || _kept.Ready(prefab, room);

        /// <summary>What is kept of a kind of place with this stamp, waited for while still being found; null for none, or none known yet.</summary>
        public static FloorRecord Find(string prefab, bool room, string stamp)
        {
            if (_kept == null) return null;
            var timed = Timing.Start();
            var record = _kept.Find(prefab, room, stamp);
            Timing.Add("floors kept found", timed);
            return record;
        }

        /// <summary>
        /// For the self-test: holds the files' work behind a job that waits until
        /// <paramref name="until"/> is set, a minute at the most, as a long run of files wanted
        /// before them holds them.
        /// </summary>
        [Diagnostic, ForTest]
        public static void HoldFiles(System.Threading.WaitHandle until) => Files.Then("holding the floors kept", () => until.WaitOne(System.TimeSpan.FromMinutes(1)));

        /// <summary>Keeps a record, and writes it to its file on a worker thread.</summary>
        public static void Put(FloorRecord record) => _kept?.Put(record);

        /// <summary>Forgets every kind of place's floors, and removes their files, after anything still being written.</summary>
        public static void Clear() => (_kept ?? new FloorFiles(Folder, Files)).Clear();

        /// <summary>Tells any listing, finding or writing that failed; called each frame.</summary>
        public static void Update()
        {
            if (_kept != null) Faults.TellEach(Feature.FloorsKept, _kept.Failures());
            Faults.TellEach(Feature.FloorsKept, Files.Failures());
        }
    }
}
