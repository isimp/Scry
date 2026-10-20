using System;
using System.Collections.Generic;
using System.IO;

namespace Scry
{
    /// <summary>
    /// The entries marked as favourites, one key per line in a plain text file. Keys of prefabs
    /// that are not in the game right now are kept, so a favourite from a mod that is switched off
    /// is still there when it comes back.
    ///
    /// A file that cannot be read starts an empty list and one that cannot be written keeps the
    /// list for this session only; neither stops the panel from working.
    /// </summary>
    internal sealed class Favourites
    {
        private readonly string _path;
        private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The last reason the file could not be read or written, for the log.</summary>
        public string Problem { get; private set; }

        public Favourites(string path)
        {
            _path = path;
            Load();
        }

        public ICollection<string> Keys => _keys;

        public bool Contains(Entry entry)
        {
            return entry != null && _keys.Contains(entry.Key);
        }

        /// <summary>Adds or removes one, and saves at once.</summary>
        public void Toggle(Entry entry)
        {
            if (entry == null) return;
            if (!_keys.Remove(entry.Key)) _keys.Add(entry.Key);
            Save();
        }

        private void Load() => Problem = Steps.Run(ReadList, null)?.Message;

        private void ReadList()
        {
            var path = FileSwap.ToRead(_path);
            if (path == null) return;

            foreach (var line in File.ReadAllLines(path))
            {
                var key = line.Trim();
                if (key.Length > 0) _keys.Add(key);
            }
        }

        private void Save() => Problem = Steps.Run(WriteList, null)?.Message;

        private void WriteList()
        {
            var keys = new List<string>(_keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            FileSwap.Write(_path, aside => File.WriteAllLines(aside, keys), lasting: true);
        }
    }
}
