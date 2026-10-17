using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Scry
{
    /// <summary>
    /// The places' floor records kept in a folder, a file for each kind of place
    /// (<see cref="FloorRecord.FileName"/>), each read only once its kind of place is wanted, so
    /// only the kinds of place shown are held however many are kept. The folder's files are
    /// listed on a worker thread; a kind wanted ahead (<see cref="Want"/>), as its copy starts to
    /// be made, is found on one too, and asked for (<see cref="Find"/>) is taken once found, waited
    /// for while still being found. A record put while its file is being found is kept over the
    /// file's; each record put is written aside and swapped into place (<see cref="FileSwap"/>).
    /// Asked for on the main thread; its files are read and written one job after another on the
    /// queue given (<see cref="FileQueue"/>), whose writing failures are for its owner to tell.
    /// Whether a kind is found yet is asked without waiting (<see cref="Ready"/>).
    /// </summary>
    internal sealed class FloorFiles
    {
        private const string Kind = ".dat";
        private const string Aside = ".tmp";

        private readonly string _folder;
        private readonly FileQueue _queue;
        private readonly Dictionary<(string Prefab, bool Room), FloorRecord> _held = new Dictionary<(string, bool), FloorRecord>();
        private readonly Dictionary<(string Prefab, bool Room), Task<FloorRecord>> _finding = new Dictionary<(string, bool), Task<FloorRecord>>();

        /// <summary>The files of the kinds of place kept, by name: those listed in the folder and those written since.</summary>
        private readonly HashSet<string> _files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private Task<string[]> _listing;
        private bool _listed;
        private readonly List<(string Doing, Exception Error)> _failed = new List<(string, Exception)>();

        public FloorFiles(string folder, FileQueue queue)
        {
            _folder = folder;
            _queue = queue;
            _listing = queue.Then(() => Directory.Exists(folder) ? Directory.GetFiles(folder).Select(Path.GetFileName).Select(KeptName).Where(n => n != null).ToArray() : Array.Empty<string>());
        }

        /// <summary>The name of the kind's file a file in the folder is, itself or written aside; null for any other file.</summary>
        private static string KeptName(string file)
        {
            if (file.EndsWith(Kind, StringComparison.OrdinalIgnoreCase)) return file;
            if (file.EndsWith(Kind + Aside, StringComparison.OrdinalIgnoreCase)) return file.Substring(0, file.Length - Aside.Length);
            return null;
        }

        /// <summary>How many kinds of place have their floors kept, in the folder or written since.</summary>
        public int Count
        {
            get
            {
                Listed();
                return _files.Count;
            }
        }

        /// <summary>How many kinds of place have their record held, found or made.</summary>
        public int Held => _held.Values.Count(r => r != null);

        /// <summary>Whether the folder's files are known: false while still being listed, or where they could not be.</summary>
        private bool Listed()
        {
            if (_listing == null || !_listing.IsCompleted) return _listed;
            if (_listing.Status == TaskStatus.RanToCompletion)
            {
                _files.UnionWith(_listing.Result);
                _listed = true;
            }
            else _failed.Add(("listing the floors kept", _listing.Exception?.GetBaseException()));
            _listing = null;
            return _listed;
        }

        /// <summary>Starts finding a kind of place's file on the worker thread, unless it is held, being found, or known to have none.</summary>
        public void Want(string prefab, bool room)
        {
            var key = (prefab, room);
            if (_held.ContainsKey(key) || _finding.ContainsKey(key)) return;
            var name = FloorRecord.FileName(prefab, room);
            if (Listed() && !_files.Contains(name))
            {
                _held[key] = null;
                return;
            }
            var path = Path.Combine(_folder, name);
            _finding[key] = _queue.Then(() =>
            {
                var file = FileSwap.ToRead(path);
                return file != null ? FloorRecord.Load(File.ReadAllText(file)) : null;
            });
        }

        /// <summary>
        /// Whether what is kept of a kind of place is found, so asking for it (<see cref="Find"/>)
        /// waits for nothing: held, known to have no file, or its file found or failed to be; its
        /// file starts being found where it was not wanted ahead. The files are read one job after
        /// another, so a kind wanted after many others waits behind every one of them: whatever
        /// needs it asks this each frame and goes on with other work until it is.
        /// </summary>
        public bool Ready(string prefab, bool room)
        {
            var key = (prefab, room);
            Want(prefab, room);
            if (!_finding.TryGetValue(key, out var finding)) return true;
            if (!finding.IsCompleted) return false;
            Take(key, finding);
            return true;
        }

        /// <summary>
        /// The record kept of a kind of place with this stamp, its file found first where it was not
        /// wanted ahead, and waited for while still being found; null for none, one kept with
        /// another stamp, or a stamp not known.
        /// </summary>
        public FloorRecord Find(string prefab, bool room, string stamp)
        {
            if (stamp.Length == 0) return null;
            var key = (prefab, room);
            Want(prefab, room);
            if (_finding.TryGetValue(key, out var finding))
            {
                // A finding that failed is told from the job's own state.
                Steps.Run(finding.Wait, null);
                Take(key, finding);
            }
            return _held.TryGetValue(key, out var record) && record != null && record.Stamp == stamp ? record : null;
        }

        /// <summary>Holds what a finding that is done found, or nothing where it failed, its failure told once asked (<see cref="Failures"/>).</summary>
        private void Take((string Prefab, bool Room) key, Task<FloorRecord> finding)
        {
            _finding.Remove(key);
            if (finding.Status == TaskStatus.RanToCompletion) _held[key] = finding.Result;
            else
            {
                _held[key] = null;
                _failed.Add(("finding the floors kept of " + key.Prefab, finding.Exception?.GetBaseException()));
            }
        }

        /// <summary>Holds a record, over any being found of its kind, and writes it, unless its stamp is not known.</summary>
        public void Put(FloorRecord record)
        {
            var key = (record.Prefab, record.Room);
            _finding.Remove(key);
            _held[key] = record;
            if (record.Stamp.Length == 0) return;
            var name = FloorRecord.FileName(record.Prefab, record.Room);
            _files.Add(name);
            var path = Path.Combine(_folder, name);
            _queue.Then("writing the floors kept", () => FileSwap.Write(path, aside => File.WriteAllText(aside, record.Save())));
        }

        /// <summary>Forgets every kind of place held, and removes every file kept of them once what is written before is.</summary>
        public void Clear()
        {
            _held.Clear();
            _finding.Clear();
            _files.Clear();
            _listing = null;
            _listed = true;
            var folder = _folder;
            _queue.Then("forgetting the floors kept", () =>
            {
                if (!Directory.Exists(folder)) return;
                foreach (var file in Directory.GetFiles(folder))
                {
                    if (KeptName(Path.GetFileName(file)) != null) File.Delete(file);
                }
            });
        }

        /// <summary>What could not be listed or found since last asked, each told once.</summary>
        public List<(string Doing, Exception Error)> Failures()
        {
            Listed();
            var failed = new List<(string, Exception)>(_failed);
            _failed.Clear();
            return failed;
        }
    }
}
