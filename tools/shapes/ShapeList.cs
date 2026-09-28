using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Scry
{
    /// <summary>One game method the startup check knows the shape of, as written in <c>Compatibility.Copied</c>.</summary>
    public sealed class ShapeEntry
    {
        /// <summary>Its line in the file, from 1.</summary>
        public int Line;

        /// <summary>The game type by name, a nested one as <c>Outer+Inner</c>.</summary>
        public string Type = "";
        public string Method = "";
        public int Params;
        public uint Shape;
        public string Feature = "";
    }

    /// <summary>
    /// The list of game methods whose shapes the startup check compares, read from the source of
    /// <c>Compatibility.cs</c>, and written back with the shapes a new game version has: each
    /// changed shape replaced where it stands, and every change and every method the game no
    /// longer has told, so a person reads what the changed methods do before trusting them.
    /// </summary>
    public static class ShapeList
    {
        private static readonly Regex Entry = new Regex(
            @"\(\s*""(?<type>[^""]+)""\s*,\s*""(?<method>[^""]+)""\s*,\s*(?<params>\d+)\s*,\s*0x(?<shape>[0-9A-Fa-f]{8})\s*,\s*""(?<feature>[^""]*)""\s*\)",
            RegexOptions.Compiled);

        public static List<ShapeEntry> Parse(string source)
        {
            var entries = new List<ShapeEntry>();
            var lines = source.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var match = Entry.Match(lines[i]);
                if (!match.Success) continue;
                entries.Add(new ShapeEntry
                {
                    Line = i + 1,
                    Type = match.Groups["type"].Value,
                    Method = match.Groups["method"].Value,
                    Params = int.Parse(match.Groups["params"].Value, CultureInfo.InvariantCulture),
                    Shape = uint.Parse(match.Groups["shape"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    Feature = match.Groups["feature"].Value,
                });
            }
            return entries;
        }

        /// <summary>
        /// The source with each entry's shape as the game has it now (<paramref name="now"/> gives
        /// null for a method the game no longer has, which is left as it is), and a line for each
        /// change and each missing method.
        /// </summary>
        public static string Update(string source, Func<ShapeEntry, uint?> now, out List<string> report)
        {
            report = new List<string>();
            var lines = source.Split('\n');
            foreach (var entry in Parse(source))
            {
                var shape = now(entry);
                var name = $"{entry.Type}.{entry.Method} ({entry.Params} parameters)";
                if (shape == null)
                {
                    report.Add($"missing  {name}, line {entry.Line}: {entry.Feature} is off until Scry is changed");
                    continue;
                }
                if (shape.Value == entry.Shape) continue;
                var old = $"0x{entry.Shape:X8}";
                var fresh = $"0x{shape.Value:X8}";
                lines[entry.Line - 1] = lines[entry.Line - 1].Replace(old, fresh);
                report.Add($"changed  {name}, line {entry.Line}: {old} -> {fresh}; read it against what Scry does for {entry.Feature}");
            }
            return string.Join("\n", lines);
        }
    }
}
