using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Reads the notes under docs, the player's guide and the release workflow's notes left out, against the source: every type of Scry's it names in code
    /// quotes is there, with the member it names, and every file it names is there. A name
    /// starting with one of the game's, Unity's or BepInEx's types (read from the reference
    /// copies in lib/) is theirs and is left to the startup check; a name that is no one's
    /// type was Scry's, renamed or gone. A name quoted bare is a word of Scry's code or a
    /// member of theirs. A file it names by itself (README.md, icon.png) is in
    /// the repo, or is a library the repo builds or keeps in lib/.
    /// </summary>
    public class DocSourceTests
    {
        private static string Root()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Scry.csproj"))) return dir.FullName;
            }
            throw new InvalidOperationException("Scry.csproj not found above the test run.");
        }

        private static List<string> SourceFiles()
        {
            var root = Root();
            return new[] { "src", "selftest", "tools", "tests" }
                .Where(d => Directory.Exists(Path.Combine(root, d)))
                .SelectMany(d => Directory.GetFiles(Path.Combine(root, d), "*.cs", SearchOption.AllDirectories))
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .ToList();
        }

        /// <summary>
        /// Every type the game, Unity and BepInEx declare, by name, and every name of a method,
        /// field, property or event they declare, read from the reference copies' metadata.
        /// </summary>
        private static (HashSet<string> Types, HashSet<string> Members) Theirs()
        {
            var types = new HashSet<string>(StringComparer.Ordinal);
            var members = new HashSet<string>(StringComparer.Ordinal);
            foreach (var dll in Directory.GetFiles(Path.Combine(Root(), "lib"), "*.dll"))
            {
                using (var stream = File.OpenRead(dll))
                using (var pe = new PEReader(stream))
                {
                    if (!pe.HasMetadata) continue;
                    var reader = pe.GetMetadataReader();
                    foreach (var handle in reader.TypeDefinitions) types.Add(reader.GetString(reader.GetTypeDefinition(handle).Name));
                    foreach (var handle in reader.MethodDefinitions) members.Add(reader.GetString(reader.GetMethodDefinition(handle).Name));
                    foreach (var handle in reader.FieldDefinitions) members.Add(reader.GetString(reader.GetFieldDefinition(handle).Name));
                    foreach (var handle in reader.PropertyDefinitions) members.Add(reader.GetString(reader.GetPropertyDefinition(handle).Name));
                    foreach (var handle in reader.EventDefinitions) members.Add(reader.GetString(reader.GetEventDefinition(handle).Name));
                }
            }
            return (types, members);
        }

        /// <summary>The name of every file in the repo outside build output, and of every library a project of it builds.</summary>
        private static HashSet<string> RepoFiles()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var left = new Stack<string>();
            left.Push(Root());
            while (left.Count > 0)
            {
                var dir = left.Pop();
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    var name = Path.GetFileName(sub);
                    if (name != "bin" && name != "obj" && !name.StartsWith(".", StringComparison.Ordinal)) left.Push(sub);
                }
                foreach (var file in Directory.GetFiles(dir))
                {
                    names.Add(Path.GetFileName(file));
                    if (file.EndsWith(".csproj", StringComparison.Ordinal)) names.Add(Path.GetFileNameWithoutExtension(file) + ".dll");
                }
            }
            return names;
        }

        [Fact]
        public void EveryNameTheNotesQuoteIsThere()
        {
            var files = SourceFiles();
            var code = string.Join("\n", files.Select(File.ReadAllText));
            var names = new HashSet<string>(files.Select(Path.GetFileNameWithoutExtension), StringComparer.Ordinal);
            var types = new HashSet<string>(Regex.Matches(code, @"\b(?:class|struct|enum|interface)\s+(\w+)").Cast<Match>().Select(m => m.Groups[1].Value), StringComparer.Ordinal);
            var (theirTypes, theirMembers) = Theirs();
            var repo = RepoFiles();
            // Every word of Scry's code and project files, for a name the notes quote bare: a method, a setting, a log line's word.
            var projects = Directory.GetFiles(Root(), "*.*proj", SearchOption.AllDirectories).Concat(Directory.GetFiles(Root(), "*.props", SearchOption.TopDirectoryOnly));
            var words = new HashSet<string>(Regex.Matches(code + "\n" + string.Join("\n", projects.Select(File.ReadAllText)), @"\w+").Cast<Match>().Select(m => m.Value), StringComparer.Ordinal);
            var guide = Path.DirectorySeparatorChar + "guide" + Path.DirectorySeparatorChar;
            var notes = string.Join("\n", Directory.GetFiles(Path.Combine(Root(), "docs"), "*.md", SearchOption.AllDirectories).Where(f => !f.Contains(guide) && Path.GetFileName(f) != "releasing.md").Select(File.ReadAllText));

            var missing = new List<string>();
            foreach (var quoted in Regex.Matches(notes, "`([^`]+)`").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
            {
                if (quoted.EndsWith(".cs", StringComparison.Ordinal))
                {
                    if (!names.Contains(Path.GetFileNameWithoutExtension(quoted.Replace('/', Path.DirectorySeparatorChar)))) missing.Add(quoted);
                    continue;
                }
                // A file named by itself; a path with folders is the game folder's, not the repo's.
                if (Regex.IsMatch(quoted, @"^[\w.\-]+\.[a-z]+$"))
                {
                    if (!repo.Contains(quoted)) missing.Add(quoted);
                    continue;
                }
                var parts = Regex.Match(quoted, @"^([A-Z]\w*)(?:\.(\w+))?(?:\.(\w+))?(?:\(\))?$");
                if (!parts.Success) continue;
                var first = parts.Groups[1].Value;
                if (!types.Contains(first))
                {
                    if (names.Contains(quoted) || names.Contains(first)) continue;
                    if (theirTypes.Contains(first))
                    {
                        // Their type's member is one of theirs (by name, on any of their types).
                        var theirMember = parts.Groups[3].Success ? parts.Groups[3].Value : parts.Groups[2].Success ? parts.Groups[2].Value : null;
                        if (theirMember != null && !theirMembers.Contains(theirMember) && !theirTypes.Contains(theirMember)) missing.Add(quoted);
                        continue;
                    }
                    // A bare name is a word of Scry's or a member of theirs; a dotted one starting with no one's type was a type of Scry's that went.
                    if (parts.Groups[2].Success || !(words.Contains(first) || theirMembers.Contains(first))) missing.Add(quoted);
                    continue;
                }
                var member = parts.Groups[3].Success ? parts.Groups[3].Value : parts.Groups[2].Success ? parts.Groups[2].Value : null;
                // A file named without its ending (ScryPanel.ViewMenu) is there as a file.
                if (member != null && !names.Contains(quoted) && !Regex.IsMatch(code, $@"\b{Regex.Escape(member)}\b")) missing.Add(quoted);
            }
            Assert.True(missing.Count == 0, "Named in the notes but not in the source: " + string.Join(", ", missing));
        }

        /// <summary>
        /// Every label the docs set in italics is one Scry shows, by the text of a string in its
        /// source or the self-test's, case aside as a heading is drawn in capitals: a button, a tab
        /// or a heading renamed is renamed in the README, the newest changelog entry and every note
        /// under docs too. Older changelog entries keep the labels of their own release.
        /// </summary>
        [Fact]
        public void EveryLabelTheDocsSetInItalicsIsOneScryShows()
        {
            var root = Root();
            var strings = new List<string>();
            foreach (var file in new[] { "src", "selftest" }.SelectMany(d => Directory.GetFiles(Path.Combine(root, d), "*.cs", SearchOption.AllDirectories)))
            {
                // Quotes in a comment are no label shown.
                var code = Regex.Replace(File.ReadAllText(file), @"(?m)^\s*//.*$", "");
                strings.AddRange(Regex.Matches(code, @"""((?:\\.|[^""\\])*)""").Cast<Match>().Select(m => m.Groups[1].Value.ToLowerInvariant()));
            }
            // A label is a whole string, or starts or ends one with a count or a mark beside it ("Learned (3)").
            bool Shown(string label) => strings.Any(s => s == label || s.StartsWith(label, StringComparison.Ordinal) && !char.IsLetterOrDigit(s[label.Length])
                                                       || s.EndsWith(label, StringComparison.Ordinal) && !char.IsLetterOrDigit(s[s.Length - label.Length - 1]));
            var changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));
            var newest = Regex.Split(changelog, @"(?m)^## ").Skip(1).FirstOrDefault() ?? "";
            var docs = new List<(string Name, string Text)> { ("README.md", File.ReadAllText(Path.Combine(root, "README.md"))), ("CHANGELOG.md", newest) };
            docs.AddRange(Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories).Select(f => (Path.GetFileName(f), File.ReadAllText(f))));

            var missing = new List<string>();
            foreach (var (name, text) in docs)
            {
                foreach (var label in Regex.Matches(text, @"(?<![\*\w])\*([^\*\n]+?)\*(?![\*\w])").Cast<Match>().Select(m => m.Groups[1].Value).Distinct())
                {
                    if (!Shown(label.ToLowerInvariant().TrimEnd('.'))) missing.Add($"{name}: {label}");
                }
            }
            Assert.True(missing.Count == 0, "Set in italics as a label but shown nowhere: " + string.Join(", ", missing));
        }

        [Fact]
        public void TheReadmesSearchExamplesAreTermsTheSearchKnows()
        {
            var readme = File.ReadAllText(Path.Combine(Root(), "README.md"));
            var examples = Regex.Matches(readme, @"`(\w+:\w+)`").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.NotEmpty(examples);
            foreach (var example in examples)
            {
                var terms = Search.Parse(example).Terms;
                Assert.True(terms.Count == 1 && Search.Keys.Contains(terms[0].Key), example + " is not a search term");
                if (terms[0].Key == "kind")
                    Assert.True(((Kind[])Enum.GetValues(typeof(Kind))).Any(kind => Search.KindMatches(kind, terms[0].Value)), example + " names no kind");
            }
        }
    }
}
