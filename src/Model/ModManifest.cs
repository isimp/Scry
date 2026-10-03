using System;
using System.Collections.Generic;
using System.Text;

namespace Scry
{
    /// <summary>
    /// A mod package's manifest.json, as a mod manager installs it beside the mod: its name,
    /// version, website, description and the packages it depends on ("Author-Name-1.2.3"). Read
    /// with a small reader of its own, as the game ships no JSON reader Scry could use outside it.
    /// </summary>
    public sealed class ModManifest
    {
        public string Name = "", Version = "", Website = "", Description = "";
        public List<string> Dependencies = new List<string>();

        /// <summary>The manifest in a text, or null when the text is no JSON object.</summary>
        public static ModManifest Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var reader = new Reader(json);
            try
            {
                if (!(reader.Value() is Dictionary<string, object> fields)) return null;
                var manifest = new ModManifest
                {
                    Name = Text(fields, "name"),
                    Version = Text(fields, "version_number"),
                    Website = Text(fields, "website_url"),
                    Description = Text(fields, "description"),
                };
                if (fields.TryGetValue("dependencies", out var list) && list is List<object> items)
                {
                    foreach (var item in items) if (item is string dependency && dependency.Length > 0) manifest.Dependencies.Add(dependency);
                }
                return manifest;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// Who made a package, from the folder a mod manager installs it in, named for the
        /// package ("Author-Name"); null when the folder is named otherwise.
        /// </summary>
        public static string Author(string folder, string name)
        {
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(name)) return null;
            var suffix = "-" + name;
            if (folder.Length <= suffix.Length || !folder.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return null;
            return folder.Substring(0, folder.Length - suffix.Length);
        }

        /// <summary>The package a dependency names, without its version ("Author-Name").</summary>
        public static string Package(string dependency)
        {
            if (string.IsNullOrEmpty(dependency)) return "";
            var parts = dependency.Split('-');
            return parts.Length >= 3 ? parts[0] + "-" + parts[1] : dependency;
        }

        private static string Text(Dictionary<string, object> fields, string key) =>
            fields.TryGetValue(key, out var value) && value is string text ? text : "";

        /// <summary>Reads JSON values: objects, arrays and strings as they are, anything else as null.</summary>
        private sealed class Reader
        {
            private readonly string _text;
            private int _at;

            public Reader(string text)
            {
                _text = text;
                if (_text.Length > 0 && _text[0] == '\uFEFF') _at = 1;
            }

            public object Value()
            {
                Skip();
                if (_at >= _text.Length) throw new FormatException();
                switch (_text[_at])
                {
                    case '{': return Object();
                    case '[': return Array();
                    case '"': return String();
                    default: return Plain();
                }
            }

            private Dictionary<string, object> Object()
            {
                var fields = new Dictionary<string, object>(StringComparer.Ordinal);
                _at++;
                Skip();
                if (Next('}')) return fields;
                while (true)
                {
                    Skip();
                    if (_at >= _text.Length || _text[_at] != '"') throw new FormatException();
                    var key = String();
                    Skip();
                    if (!Next(':')) throw new FormatException();
                    fields[key] = Value();
                    Skip();
                    if (Next('}')) return fields;
                    if (!Next(',')) throw new FormatException();
                }
            }

            private List<object> Array()
            {
                var items = new List<object>();
                _at++;
                Skip();
                if (Next(']')) return items;
                while (true)
                {
                    items.Add(Value());
                    Skip();
                    if (Next(']')) return items;
                    if (!Next(',')) throw new FormatException();
                }
            }

            private string String()
            {
                var text = new StringBuilder();
                _at++;
                while (_at < _text.Length)
                {
                    var c = _text[_at++];
                    if (c == '"') return text.ToString();
                    if (c != '\\')
                    {
                        text.Append(c);
                        continue;
                    }
                    if (_at >= _text.Length) break;
                    var escaped = _text[_at++];
                    switch (escaped)
                    {
                        case 'n': text.Append('\n'); break;
                        case 't': text.Append('\t'); break;
                        case 'r': text.Append('\r'); break;
                        case 'b': text.Append('\b'); break;
                        case 'f': text.Append('\f'); break;
                        case 'u':
                            if (_at + 4 > _text.Length || !Stored.TryHex(_text.Substring(_at, 4), out var code)) throw new FormatException();
                            text.Append((char)code);
                            _at += 4;
                            break;
                        default: text.Append(escaped); break;
                    }
                }
                throw new FormatException();
            }

            /// <summary>A number, true, false or null: read past, as the manifest's fields Scry tells are none of these.</summary>
            private object Plain()
            {
                var start = _at;
                while (_at < _text.Length && (char.IsLetterOrDigit(_text[_at]) || _text[_at] == '.' || _text[_at] == '-' || _text[_at] == '+')) _at++;
                if (_at == start) throw new FormatException();
                return null;
            }

            private void Skip()
            {
                while (_at < _text.Length && char.IsWhiteSpace(_text[_at])) _at++;
            }

            private bool Next(char c)
            {
                if (_at < _text.Length && _text[_at] == c)
                {
                    _at++;
                    return true;
                }
                return false;
            }
        }
    }
}
