using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Reads Scry's own source: each layer leans only on those before it. What reads the game
    /// (src/Catalog) and what makes the previews (src/Preview) never name the panel's classes
    /// (src/UI), which listen instead (Learned); what reads the game names only the few preview
    /// helpers it shares, each with why.
    /// </summary>
    public class LayerSourceTests
    {
        private static string Root()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Scry.csproj"))) return dir.FullName;
            }
            throw new InvalidOperationException("Scry.csproj not found above the test run.");
        }

        /// <summary>A type declared in a namespace, not nested in another: nested ones are a type's own words for its own parts.</summary>
        private static readonly Regex Declared = new Regex(@"^    (?:public |internal )?(?:static |sealed |abstract |partial )*(?:class|struct|enum|interface)\s+(\w+)", RegexOptions.Multiline);

        /// <summary>The types a layer declares, by the folder of its source.</summary>
        private static HashSet<string> TypesOf(string folder)
        {
            var types = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(Path.Combine(Root(), "src", folder), "*.cs"))
            {
                foreach (Match m in Declared.Matches(File.ReadAllText(file))) types.Add(m.Groups[1].Value);
            }
            return types;
        }

        /// <summary>Each use of another layer's type in a layer's source, as "File: Type".</summary>
        private static List<string> Uses(string folder, IEnumerable<string> types, ISet<string> allowed)
        {
            var found = new List<string>();
            var names = types.Where(t => !allowed.Contains(t)).ToList();
            if (names.Count == 0) return found;
            // A type named: its members, its type arguments, made new or asked its type; a call of
            // a layer's own method by the same name is no use of it.
            var alternatives = string.Join("|", names.Select(Regex.Escape));
            var use = new Regex(@"\b(" + alternatives + @")\s*[.<]|\bnew\s+(" + alternatives + @")\s*\(|typeof\((" + alternatives + @")\)");
            foreach (var file in Directory.GetFiles(Path.Combine(Root(), "src", folder), "*.cs"))
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    var code = line.Trim();
                    if (code.StartsWith("//", StringComparison.Ordinal)) continue;
                    foreach (Match m in use.Matches(code))
                    {
                        var name = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value;
                        found.Add(Path.GetFileName(file) + ": " + name);
                    }
                }
            }
            return found.Distinct().ToList();
        }

        /// <summary>Types the panel declares that the layers below may name: the facts' rows are the panel's, but none are needed.</summary>
        private static readonly HashSet<string> SharedFromPanel = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Preview helpers the reading of the game uses, with why.</summary>
        private static readonly HashSet<string> SharedFromPreview = new HashSet<string>(StringComparer.Ordinal)
        {
            "Variants",      // a prefab's looks are read with the catalog and shown by the previews
            "ClipPlayer",    // finding a prefab's animator, which the catalog reads the events of
            "AnimationEars", // the animation events the previews answer, which the catalog notes
            "PlaceCopy",     // a place's spawn points are read the same way for the stage and for whether it leaves anything to chance
        };

        [Fact]
        public void WhatReadsTheGameNeverNamesThePanel()
        {
            var uses = Uses("Catalog", TypesOf("UI"), SharedFromPanel);
            Assert.True(uses.Count == 0, "Names the panel: " + string.Join("; ", uses));
        }

        [Fact]
        public void ThePreviewsNeverNameThePanel()
        {
            var uses = Uses("Preview", TypesOf("UI"), SharedFromPanel);
            Assert.True(uses.Count == 0, "Names the panel: " + string.Join("; ", uses));
        }

        [Fact]
        public void WhatReadsTheGameNamesOnlyThePreviewHelpersItShares()
        {
            var uses = Uses("Catalog", TypesOf("Preview"), SharedFromPreview);
            Assert.True(uses.Count == 0, "Names the previews: " + string.Join("; ", uses));
        }
    }
}
