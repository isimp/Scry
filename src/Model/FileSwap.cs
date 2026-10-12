using System;
using System.IO;

namespace Scry
{
    /// <summary>
    /// How Scry writes a file of its own, so a crash at any point leaves either the old file or
    /// the new one, never half of one or none: written aside, then swapped into place in one step.
    /// Where the file system cannot swap files, it is copied over the old one, and the file aside
    /// removed only once the copy is done. A file aside with none in place is a save that stopped
    /// before moving it there, and is the newest there is.
    /// </summary>
    internal static class FileSwap
    {
        private static string Aside(string path) => path + ".tmp";

        /// <summary>The file to read: the one in place, else one written aside and never moved there; null for none.</summary>
        public static string ToRead(string path) => File.Exists(path) ? path : File.Exists(Aside(path)) ? Aside(path) : null;

        /// <summary>Removes the file, and any copy of it written aside, which would otherwise be read in its place.</summary>
        public static void Remove(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(Aside(path))) File.Delete(Aside(path));
        }

        /// <summary>Writes the file by <paramref name="writeAside"/>, given where to write it aside, then swaps it into place.</summary>
        public static void Write(string path, Action<string> writeAside)
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            var aside = Aside(path);
            writeAside(aside);
            if (!File.Exists(path))
            {
                File.Move(aside, path);
                return;
            }
            var swapped = Steps.Run(() => File.Replace(aside, path, null), null);
            if (swapped == null) return;
            // With no file aside, nothing was saved.
            if (!File.Exists(aside)) throw swapped;
            File.Copy(aside, path, true);
            File.Delete(aside);
        }
    }
}
