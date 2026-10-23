using System;
using System.IO;

namespace Scry
{
    /// <summary>
    /// Reading a file Scry keeps of what was learned or walked: a character's book, its walks, a
    /// server's dungeon records. The one in place is read (<see cref="FileSwap.ToRead"/>), else the
    /// one kept from before it; where neither can be read, both are set aside beside it, dated,
    /// never written over nor lost (<see cref="FileSwap.SetAside"/>), and a new one starts, which
    /// saves, so a broken file stops nothing learned from then on. A file that cannot be set aside
    /// fails the reading, as one that cannot be read did before.
    /// </summary>
    internal static class KeptFile
    {
        /// <summary>The file read by <paramref name="load"/> (given null for none); where it was set aside, the path it went to, dated by <paramref name="stamp"/>.</summary>
        public static T Read<T>(string path, Func<string, T> load, string stamp, out string setAside)
        {
            setAside = null;
            var first = FileSwap.ToRead(path);
            if (first == null) return load(null);
            if (Steps.Run(() => load(File.ReadAllText(first)), out var value, null) == null) return value;
            var before = FileSwap.BeforeOf(path);
            if (before != first && File.Exists(before) && Steps.Run(() => load(File.ReadAllText(before)), out value, null) == null) return value;
            setAside = FileSwap.SetAside(path, stamp);
            return load(null);
        }
    }
}
