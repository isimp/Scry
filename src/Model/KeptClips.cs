using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Scry
{
    /// <summary>
    /// What hidden copies of animators were seen to play (<see cref="Probed"/>), kept from one
    /// session to the next as text (<see cref="Save"/>, <see cref="Load"/>), each probe by its key
    /// with the stamp of what it was seen with: one whose stamp differs now is seen again. Each probe
    /// is a run of lines closed by an end line, so one cut short is left out and the rest kept; a
    /// file of another kind gives nothing. Text is written with its tabs, line breaks and
    /// backslashes escaped.
    /// </summary>
    internal sealed class KeptClips
    {
        /// <summary>The first line, which a file of these begins with; another format is another first line.</summary>
        private const string Header = "Scry clips 1";

        private readonly Dictionary<string, (string Stamp, Probed Seen)> _probes = new Dictionary<string, (string, Probed)>(StringComparer.Ordinal);

        public int Count => _probes.Count;

        /// <summary>A probe's stamp: what everything was seen with, and the animator it was seen on.</summary>
        public static string StampWith(string seenWith, string animator) => (seenWith ?? "") + " " + (animator ?? "");

        public IEnumerable<string> Keys => _probes.Keys;

        /// <summary>Keeps what a probe saw, a copy of it as it is now, in the place of what was kept of it before.</summary>
        public void Put(string key, string stamp, Probed seen)
        {
            if (string.IsNullOrEmpty(key) || seen == null) return;
            _probes[key] = (stamp ?? "", Copy(seen));
        }

        /// <summary>A copy of what a probe saw, where it was kept with this stamp; false otherwise.</summary>
        public bool TryGet(string key, string stamp, out Probed seen)
        {
            seen = null;
            if (key == null || !_probes.TryGetValue(key, out var kept) || kept.Stamp != (stamp ?? "")) return false;
            seen = Copy(kept.Seen);
            return true;
        }

        public void Clear() => _probes.Clear();

        /// <summary>A copy of all that is kept, for writing it on another thread.</summary>
        public KeptClips CopyAll()
        {
            var copy = new KeptClips();
            foreach (var pair in _probes) copy._probes[pair.Key] = (pair.Value.Stamp, Copy(pair.Value.Seen));
            return copy;
        }

        private static Probed Copy(Probed from)
        {
            var to = new Probed();
            foreach (var pair in from.Triggers) to.Triggers[pair.Key] = pair.Value.ToList();
            foreach (var pair in from.Actions) to.Actions[pair.Key] = pair.Value.ToList();
            to.Idle.UnionWith(from.Idle);
            foreach (var pair in from.Stances) to.Stances[pair.Key] = new HashSet<string>(pair.Value);
            foreach (var pair in from.Holds) to.Holds[pair.Key] = new HashSet<string>(pair.Value);
            foreach (var pair in from.Visits) to.Visits[pair.Key] = new HashSet<string>(pair.Value);
            foreach (var pair in from.Switched) to.Switched[pair.Key] = new HashSet<string>(pair.Value);
            return to;
        }

        public string Save()
        {
            var text = new StringBuilder();
            text.Append(Header).Append('\n');
            foreach (var pair in _probes.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                var seen = pair.Value.Seen;
                Line(text, "probe", new[] { pair.Key, pair.Value.Stamp });
                Line(text, "idle", seen.Idle.OrderBy(c => c, StringComparer.Ordinal));
                foreach (var t in seen.Triggers) Line(text, "trigger", new[] { t.Key }.Concat(t.Value));
                foreach (var a in seen.Actions) Line(text, "action", new[] { a.Key }.Concat(a.Value));
                foreach (var s in seen.Stances) Line(text, "stance", new[] { Stored.Count(s.Key) }.Concat(Sorted(s.Value)));
                foreach (var h in seen.Holds) Line(text, "hold", new[] { h.Key }.Concat(Sorted(h.Value)));
                foreach (var v in seen.Visits) Line(text, "visit", new[] { v.Key }.Concat(Sorted(v.Value)));
                foreach (var s in seen.Switched) Line(text, "switched", new[] { s.Key }.Concat(Sorted(s.Value)));
                Line(text, "end", Enumerable.Empty<string>());
            }
            return text.ToString();
        }

        private static IEnumerable<string> Sorted(IEnumerable<string> clips) => clips.OrderBy(c => c, StringComparer.Ordinal);

        private static void Line(StringBuilder text, string kind, IEnumerable<string> fields)
        {
            text.Append(kind);
            foreach (var field in fields) text.Append('\t').Append(Stored.Escaped(field ?? ""));
            text.Append('\n');
        }

        /// <summary>What was kept in a file's text; none for a file of another kind or none.</summary>
        public static KeptClips Load(string text)
        {
            var kept = new KeptClips();
            if (string.IsNullOrEmpty(text)) return kept;
            var lines = text.Split('\n');
            if (lines[0].TrimEnd('\r') != Header) return kept;

            string key = null, stamp = null;
            Probed seen = null;
            var whole = true;
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (line.Length == 0) continue;
                var raw = line.Split('\t');
                var fields = new string[raw.Length - 1];
                var read = true;
                for (var f = 1; f < raw.Length; f++) read &= Stored.TryUnescaped(raw[f], out fields[f - 1]);
                var kind = raw[0];
                if (kind == "probe")
                {
                    // A probe begun before this one and never ended is cut short: left out.
                    seen = null;
                    if (!read || fields.Length != 2) continue;
                    key = fields[0];
                    stamp = fields[1];
                    seen = new Probed();
                    whole = true;
                    continue;
                }
                if (seen == null) continue;
                if (kind == "end")
                {
                    if (whole) kept._probes[key] = (stamp, seen);
                    seen = null;
                    continue;
                }
                whole &= read && ReadLine(seen, kind, fields);
            }
            return kept;
        }

        /// <summary>Takes one line of a probe by its first word; false for a line that cannot be read, or of a word none reads, which leaves the probe out.</summary>
        private static bool ReadLine(Probed seen, string kind, string[] f)
        {
            var rest = f.Skip(1).ToList();
            switch (kind)
            {
                case "idle":
                    seen.Idle.UnionWith(f);
                    return true;
                case "trigger" when f.Length >= 1:
                    seen.Triggers[f[0]] = rest;
                    return true;
                case "action" when f.Length >= 1:
                    seen.Actions[f[0]] = rest;
                    return true;
                case "stance" when f.Length >= 1 && Stored.TryCount(f[0], out var stance):
                    seen.Stances[stance] = new HashSet<string>(rest);
                    return true;
                case "hold" when f.Length >= 1:
                    seen.Holds[f[0]] = new HashSet<string>(rest);
                    return true;
                case "visit" when f.Length >= 1:
                    seen.Visits[f[0]] = new HashSet<string>(rest);
                    return true;
                case "switched" when f.Length >= 1:
                    seen.Switched[f[0]] = new HashSet<string>(rest);
                    return true;
                default:
                    return false;
            }
        }
    }
}
