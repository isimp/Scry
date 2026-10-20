using System;
using System.IO;

namespace Scry
{
    /// <summary>
    /// How Scry writes a file of its own, so a crash at any point leaves either the old file or
    /// the new one, never half of one or none: written aside, then swapped into place in one step.
    /// Where the file system cannot swap files, it is copied over the old one, and the file aside
    /// removed only once the copy is done. A file aside with none in place is a save that stopped
    /// before moving it there, and is the newest there is. A file that must outlast a power cut
    /// (<c>lasting</c>: what a character knows, its walks, a server's records) is written through
    /// to the disk before it is swapped in, and the one before it is kept beside it, read where a
    /// power cut left the new one's length on the disk with none of its bytes.
    /// </summary>
    internal static class FileSwap
    {
        private static string Aside(string path) => path + ".tmp";

        private static string Before(string path) => path + ".bak";

        /// <summary>The file to read: the one in place, else one written aside and never moved there, else the one before it where the one in place was lost; null for none.</summary>
        public static string ToRead(string path)
        {
            if (File.Exists(path) && !LostToZeros(path)) return path;
            if (!File.Exists(path) && File.Exists(Aside(path))) return Aside(path);
            if (File.Exists(Before(path))) return Before(path);
            return File.Exists(path) ? path : null;
        }

        /// <summary>Whether a file holds bytes that are all zeros where its text starts, as a power cut leaves one whose length reached the disk before its bytes did.</summary>
        private static bool LostToZeros(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var start = new byte[16];
                var read = stream.Read(start, 0, start.Length);
                if (read == 0) return false;
                for (var i = 0; i < read; i++) if (start[i] != 0) return false;
                return true;
            }
        }

        /// <summary>Removes the file, and any copy of it written aside or kept from before, which would otherwise be read in its place.</summary>
        public static void Remove(string path)
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(Aside(path))) File.Delete(Aside(path));
            if (File.Exists(Before(path))) File.Delete(Before(path));
        }

        /// <summary>Writes the file by <paramref name="writeAside"/>, given where to write it aside, then swaps it into place; a <paramref name="lasting"/> one written through to the disk first, the one before it kept beside it.</summary>
        public static void Write(string path, Action<string> writeAside, bool lasting = false)
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            var aside = Aside(path);
            writeAside(aside);
            if (lasting) OnDisk(aside);
            if (!File.Exists(path))
            {
                File.Move(aside, path);
                return;
            }
            var before = lasting ? Before(path) : null;
            var swapped = Steps.Run(() => File.Replace(aside, path, before), null);
            if (swapped == null) return;
            // With no file aside, nothing was saved.
            if (!File.Exists(aside)) throw swapped;
            if (lasting) File.Copy(path, before, true);
            File.Copy(aside, path, true);
            File.Delete(aside);
        }

        /// <summary>Has the system write what it holds of a file to the disk.</summary>
        private static void OnDisk(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) stream.Flush(true);
        }
    }
}
