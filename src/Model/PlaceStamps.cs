using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// A place's stamp (<see cref="PlaceRecord.Stamp"/>): the build of Scry that reads it, the mods
    /// loaded, any of which may change a place in memory, so a change of mods reads every place
    /// again, and what it is read from, its bundle's file by its name, size and when it was
    /// written, which a game update that rewrites the bundle changes. No stamp for a place whose
    /// bundle is not known, so it is always read.
    /// </summary>
    internal static class PlaceStamps
    {
        /// <summary>The stamp of a place read by this build of Scry from this source, with these mods loaded, each by its id and version.</summary>
        public static string Of(string build, IEnumerable<(string Id, string Version)> mods, string source)
        {
            if (string.IsNullOrEmpty(source)) return "";
            var list = new StringBuilder();
            foreach (var mod in mods.Select(m => m.Id + "@" + m.Version).OrderBy(m => m, StringComparer.Ordinal)) list.Append(mod).Append('\n');
            return build + " " + Hash(list.ToString()) + " " + source;
        }

        /// <summary>A bundle's file as a stamp's source: its name, its size and when it was written, in ticks.</summary>
        public static string Bundle(string name, long length, long writtenTicks) => name + " " + Stored.Count(length) + " " + Stored.Count(writtenTicks);

        /// <summary>A hash of the text that reads the same on every machine and run (64-bit FNV-1a over its characters), in hexadecimal.</summary>
        private static string Hash(string text)
        {
            var hash = 14695981039346656037UL;
            foreach (var c in text)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }
            return Stored.Hex(hash);
        }
    }
}
