using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// What Scry follows of the game, the startup check watches, so an update that changes it is
    /// told (<c>Compatibility</c>). A game method a comment names is a rule Scry follows, so it
    /// is watched by the shape of its code, unless Scry patches it (the check tells whether each
    /// patch holds) or calls it (building against the new game fails where it changed). A game
    /// member Scry reaches by its name, out of the compiler's sight, is in the check too.
    /// </summary>
    public class GameRuleStandardTests
    {
        private static readonly string[] GameAssemblies = { "assembly_valheim", "assembly_utils", "assembly_guiutils" };

        /// <summary>Every method of the game's own assemblies, as "Type.Method" by the type's own name (a nested one's too).</summary>
        private static HashSet<string> GameMethods()
        {
            var methods = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in GameAssemblies)
            {
                using (var stream = File.OpenRead(Path.Combine(ScrySource.Root(), "lib", assembly + ".dll")))
                using (var pe = new PEReader(stream))
                {
                    var reader = pe.GetMetadataReader();
                    foreach (var handle in reader.TypeDefinitions)
                    {
                        var type = reader.GetTypeDefinition(handle);
                        var name = reader.GetString(type.Name);
                        foreach (var method in type.GetMethods()) methods.Add(name + "." + reader.GetString(reader.GetMethodDefinition(method).Name));
                    }
                }
            }
            return methods;
        }

        /// <summary>What the startup check watches or looks for, as "Type.Member" by the type's own name.</summary>
        private static HashSet<string> Checked()
        {
            var source = File.ReadAllText(Path.Combine(ScrySource.Root(), "src", "Patches", "Compatibility.cs"));
            var found = Regex.Matches(source, "\\(\"([\\w+]+)\", \"(\\w+)\", \\d+, 0x")
                .Cast<Match>().Concat(Regex.Matches(source, "Member\\(list, \"([\\w+]+)\", \"(\\w+)\"").Cast<Match>())
                .Select(m => m.Groups[1].Value.Split('+').Last() + "." + m.Groups[2].Value);
            return new HashSet<string>(found, StringComparer.Ordinal);
        }

        private static bool OfTheGame(ITypeSymbol type) => type != null && GameAssemblies.Contains(type.ContainingAssembly?.Name);

        [Fact]
        public void EveryGameMethodACommentNamesIsWatchedPatchedOrCalled()
        {
            var methods = GameMethods();
            var watched = Checked();
            var reached = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(call).Symbol is IMethodSymbol method && method.ContainingType != null) reached.Add(method.ContainingType.Name + "." + method.Name);
            }
            foreach (var (attribute, model) in ScrySource.All<AttributeSyntax>())
            {
                if (!attribute.Name.ToString().StartsWith("HarmonyPatch", StringComparison.Ordinal) || attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count < 2) continue;
                var type = (attribute.ArgumentList.Arguments[0].Expression as TypeOfExpressionSyntax)?.Type;
                var name = model.GetConstantValue(attribute.ArgumentList.Arguments[1].Expression).Value as string;
                if (type != null && name != null) reached.Add(model.GetTypeInfo(type).Type?.Name + "." + name);
            }

            var found = new List<string>();
            foreach (var tree in ScrySource.Compilation.SyntaxTrees)
            {
                if (string.IsNullOrEmpty(tree.FilePath)) continue;
                foreach (var trivia in tree.GetRoot().DescendantTrivia())
                {
                    if (!(trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                          || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))) continue;
                    foreach (Match named in Regex.Matches(trivia.ToFullString(), @"\b([A-Z]\w+)\.([A-Za-z_]\w+)\b"))
                    {
                        var key = named.Groups[1].Value + "." + named.Groups[2].Value;
                        if (methods.Contains(key) && !watched.Contains(key) && !reached.Contains(key))
                            found.Add($"{ScrySource.Relative(tree)}:{trivia.GetLocation().GetLineSpan().StartLinePosition.Line + 1} {key}");
                    }
                }
            }
            Violations.None("name a game rule the startup check does not watch", found);
        }

        [Fact]
        public void EveryGameMemberReachedByNameIsInTheStartupCheck()
        {
            var watched = Checked();
            var found = new List<string>();
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var owner = method.ContainingType?.ToDisplayString() ?? "";
                var byName = owner == "System.Type" && method.Name.StartsWith("Get", StringComparison.Ordinal) && !method.Name.EndsWith("s", StringComparison.Ordinal)
                             || owner.StartsWith("HarmonyLib.AccessTools", StringComparison.Ordinal);
                if (!byName) continue;
                var name = call.ArgumentList.Arguments.Select(a => model.GetConstantValue(a.Expression).Value).OfType<string>().FirstOrDefault();
                if (name == null) continue;
                // The type it is looked for on: a typeof given, the type it is asked of, or the type argument.
                var type = call.ArgumentList.Arguments.Select(a => a.Expression).OfType<TypeOfExpressionSyntax>().Select(t => model.GetTypeInfo(t.Type).Type).FirstOrDefault()
                           ?? ((call.Expression as MemberAccessExpressionSyntax)?.Expression is TypeOfExpressionSyntax asked ? model.GetTypeInfo(asked.Type).Type : null)
                           ?? method.TypeArguments.FirstOrDefault();
                if (!OfTheGame(type)) continue;
                var key = type.Name + "." + name;
                if (!watched.Contains(key)) found.Add($"{ScrySource.Where(call)} {key}");
            }
            Violations.None("reach a game member by name the startup check does not look for", found);
        }
    }
}
