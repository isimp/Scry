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
    public sealed class Favourites
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

        private string Aside => _path + ".tmp";

        private void Load()
        {
            try
            {
                // Without a list in place, one written aside is a save that stopped before
                // moving it there, and is the newest list there is.
                var path = File.Exists(_path) ? _path : File.Exists(Aside) ? Aside : null;
                if (path == null) return;

                foreach (var line in File.ReadAllLines(path))
                {
                    var key = line.Trim();
                    if (key.Length > 0) _keys.Add(key);
                }
            }
            catch (Exception ex)
            {
                Problem = ex.Message;
            }
        }

        private void Save()
        {
            try
            {
                var folder = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

                // Written aside and swapped into place in one step, so a crash at any point leaves
                // either the old list or the new one, never half a list or none.
                var keys = new List<string>(_keys);
                keys.Sort(StringComparer.OrdinalIgnoreCase);
                File.WriteAllLines(Aside, keys);
                if (!File.Exists(_path)) File.Move(Aside, _path);
                else
                {
                    try
                    {
                        File.Replace(Aside, _path, null);
                    }
                    catch (Exception) when (File.Exists(Aside))
                    {
                        // Where the file system cannot swap files, copying over the old list
                        // still saves; the list aside is only removed once the copy is done.
                        File.Copy(Aside, _path, true);
                        File.Delete(Aside);
                    }
                }
                Problem = null;
            }
            catch (Exception ex)
            {
                Problem = ex.Message;
            }
        }
    }
}
