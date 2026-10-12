using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scry
{
    /// <summary>
    /// Where the places' floors are, kept from one session to the next (<see cref="FloorRecord"/>):
    /// a file for each kind of place in <c>floors</c> under Scry's cache folder, found on a
    /// worker thread on entering a world and written on one as a record grows (<see cref="FileQueue"/>),
    /// each written aside and swapped into place (<see cref="FileSwap"/>). A record whose stamp is not the
    /// place's now is passed over, and its file written anew once the place is probed again.
    /// </summary>
    internal static class FloorRecords
    {
        static FloorRecords() => WorldCaches.Register(nameof(FloorRecords), Forget);

        private static readonly Dictionary<(string Prefab, bool Room), FloorRecord> Kept = new Dictionary<(string, bool), FloorRecord>();

        private static readonly FileQueue Files = new FileQueue();
        private static Task<List<FloorRecord>> _loading;

        private static string Folder => Path.Combine(Settings.CacheFolder, "floors");

        /// <summary>A file of its own for each kind of place (<see cref="FloorRecord.FileName"/>).</summary>
        private static string FileOf(string prefab, bool room) => Path.Combine(Folder, FloorRecord.FileName(prefab, room));

        /// <summary>How many kinds of place have their floors kept, for the self-test.</summary>
        public static int Count => Kept.Count;

        /// <summary>Whether what was kept is still being found or written.</summary>
        public static bool Busy => _loading != null || Files.Busy;

        /// <summary>Forgets what is held, on leaving a world; what is being written is written still.</summary>
        public static void Forget()
        {
            Kept.Clear();
            _loading = null;
        }

        /// <summary>Starts finding what was kept, on entering a world.</summary>
        public static void Begin()
        {
            Forget();
            var folder = Folder;
            _loading = Files.Then(() =>
            {
                var found = new List<FloorRecord>();
                if (!Directory.Exists(folder)) return found;
                foreach (var file in Directory.GetFiles(folder, "*.dat"))
                {
                    var record = FloorRecord.Load(File.ReadAllText(file));
                    if (record != null) found.Add(record);
                }
                return found;
            });
        }

        /// <summary>Takes what was found once it is, keeping any record made meanwhile over the one found.</summary>
        private static void Take()
        {
            if (_loading == null || !_loading.IsCompleted) return;
            var loading = _loading;
            _loading = null;
            if (loading.Status != TaskStatus.RanToCompletion)
            {
                Faults.Tell(Feature.FloorsKept, "finding the floors kept", loading.Exception?.GetBaseException());
                return;
            }
            foreach (var record in loading.Result)
            {
                var key = (record.Prefab, record.Room);
                if (!Kept.ContainsKey(key)) Kept[key] = record;
            }
        }

        /// <summary>What is kept of a kind of place with this stamp; null for none, or none known yet.</summary>
        public static FloorRecord Find(string prefab, bool room, string stamp)
        {
            Take();
            if (stamp.Length == 0 || !Kept.TryGetValue((prefab, room), out var record)) return null;
            return record.Stamp == stamp ? record : null;
        }

        /// <summary>Keeps a record, and writes it to its file on a worker thread; held only, while what was kept is still being found, so a fuller record found is not written over.</summary>
        public static void Put(FloorRecord record)
        {
            Take();
            Kept[(record.Prefab, record.Room)] = record;
            if (_loading != null || record.Stamp.Length == 0) return;
            var path = FileOf(record.Prefab, record.Room);
            Files.Then("writing the floors kept", () => FileSwap.Write(path, aside => File.WriteAllText(aside, record.Save())));
        }

        /// <summary>Takes what was found, and tells any writing that failed; called each frame.</summary>
        public static void Update()
        {
            Take();
            foreach (var (doing, error) in Files.Failures()) Faults.Tell(Feature.FloorsKept, doing, error);
        }

        /// <summary>The kinds of place whose floors are kept, for the self-test.</summary>
        [Diagnostic]
        public static IEnumerable<(string Prefab, bool Room, int Parts)> Held => Kept.Values.Select(r => (r.Prefab, r.Room, r.Parts.Count));
    }
}
