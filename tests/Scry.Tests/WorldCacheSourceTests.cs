using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Reads Scry's own source: every static collection that keeps something of a world (a
    /// prefab, an entry, an effect list, an attack, a sprite and the like) has to be let go of
    /// when the world is left, and its class has to be on the list of those forgotten then.
    /// </summary>
    public class WorldCacheSourceTests
    {
        /// <summary>Types that belong to a world or its catalog.</summary>
        private static readonly Regex WorldType = new Regex(
            @"\b(GameObject|Entry|EffectList|Attack|Sprite|Material|StatusEffect|RuntimeAnimatorController|Probed|ClipPlays|Job|Loadout|Found|Facts|Recipe|SharedData|AudioClip|Component|Humanoid|Mesh|PlaysInRow|Source|WholeAttack|ClipRow|ListRow|Timed|Watch)\b");

        private static readonly Regex Declaration = new Regex(@"static\s+(?:readonly\s+)?((?:Dictionary|HashSet|List|SortedSet)<.*?>)\s+(\w+)\s*(?:=|;)");

        private static readonly Regex ClassName = new Regex(@"\bclass\s+(\w+)");

        /// <summary>What keeps world things but lets go of them itself, with why.</summary>
        private static readonly HashSet<string> LetGoOtherwise = new HashSet<string>
        {
            "Falling.All",   // stand-ins take themselves off in OnDestroy
            "Falling.Down",  // likewise
        };

        private static string Root()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Scry.csproj"))) return dir.FullName;
            }
            throw new InvalidOperationException("Scry.csproj not found above the test run.");
        }

        private static Dictionary<string, List<string>> SourceByClass()
        {
            var byClass = new Dictionary<string, List<string>>();
            foreach (var file in Directory.GetFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/src/Model/")) continue;
                var text = File.ReadAllText(file);
                var match = ClassName.Match(text);
                if (!match.Success) continue;
                var name = match.Groups[1].Value;
                if (!byClass.TryGetValue(name, out var parts)) byClass[name] = parts = new List<string>();
                parts.Add(text);
            }
            return byClass;
        }

        private static IEnumerable<(string Class, string Field, string Source)> WorldCollections()
        {
            foreach (var pair in SourceByClass())
            {
                var all = string.Join("\n", pair.Value);
                foreach (Match declaration in Declaration.Matches(all))
                {
                    if (!WorldType.IsMatch(declaration.Groups[1].Value)) continue;
                    yield return (pair.Key, declaration.Groups[2].Value, all.Replace(declaration.Value, ""));
                }
            }
        }

        [Fact]
        public void WhatIsKeptOfAWorldIsLetGoOfWhenItIsLeft()
        {
            var kept = WorldCollections().Where(c => !LetGoOtherwise.Contains(c.Class + "." + c.Field)).ToList();
            Assert.NotEmpty(kept);

            var never = kept.Where(c => !Regex.IsMatch(c.Source, $@"\b{c.Field}\s*(\.Clear\(\)|\.Remove\(|=\s*(null|new)\b)"))
                .Select(c => c.Class + "." + c.Field).ToList();
            Assert.True(never.Count == 0, "Never let go of: " + string.Join(", ", never));
        }

        [Fact]
        public void EveryClassKeepingSomethingOfAWorldIsForgottenWhenItIsLeft()
        {
            var classes = WorldCollections().Where(c => !LetGoOtherwise.Contains(c.Class + "." + c.Field)).Select(c => c.Class).Distinct();
            var sources = SourceByClass();

            var missing = classes.Where(c => !string.Join("\n", sources[c]).Contains("WorldCaches.Register(")).ToList();
            Assert.True(missing.Count == 0, "Not forgotten when a world is left: " + string.Join(", ", missing));
        }
    }
}
