using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The self-test reaches into Scry by hooks of its own: a member only it uses is marked
    /// <c>[ForTest]</c> or named ending in ForTest, and Scry's own code calls no such method but
    /// from another hook, so what a player runs never takes a path only the self-test should. A
    /// property named so may still be written by Scry for the self-test to read, or read as what
    /// the self-test stands in with; one marked is not touched by Scry at all.
    /// </summary>
    public class SelfTestHookStandardTests
    {
        private static bool Named(ISymbol symbol) => symbol.Name.EndsWith("ForTest", System.StringComparison.Ordinal);

        private static bool Marked(ISymbol symbol) => symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "ForTestAttribute");

        private static bool Hook(ISymbol symbol) => Named(symbol) || Marked(symbol);

        /// <summary>The member a node stands in: a method, property, constructor or field.</summary>
        private static ISymbol Within(SyntaxNode node, SemanticModel model)
        {
            foreach (var up in node.Ancestors())
            {
                switch (up)
                {
                    case MethodDeclarationSyntax _:
                    case PropertyDeclarationSyntax _:
                    case ConstructorDeclarationSyntax _:
                    case IndexerDeclarationSyntax _:
                        return model.GetDeclaredSymbol(up);
                    case FieldDeclarationSyntax field:
                        return model.GetDeclaredSymbol(field.Declaration.Variables.First());
                }
            }
            return null;
        }

        /// <summary>Every member Scry's own code uses, by id, and where each use stands with what it names.</summary>
        private static (HashSet<string> Ids, List<(SimpleNameSyntax Name, ISymbol Symbol, SemanticModel Model)> Uses) ScrysUses()
        {
            var ids = new HashSet<string>(System.StringComparer.Ordinal);
            var uses = new List<(SimpleNameSyntax, ISymbol, SemanticModel)>();
            var compilation = ScrySource.Compilation;
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    var symbol = model.GetSymbolInfo(name).Symbol?.OriginalDefinition;
                    if (symbol == null) continue;
                    // A member's use of itself (a counter bumped, a property kept) does not count.
                    var within = Within(name, model);
                    if (within != null && SymbolEqualityComparer.Default.Equals(within.OriginalDefinition, symbol)) continue;
                    var id = symbol.GetDocumentationCommentId();
                    if (id != null) ids.Add(id);
                    uses.Add((name, symbol, model));
                }
            }
            return (ids, uses);
        }

        [Fact]
        public void ScrysOwnCodeCallsNoHookOfTheSelfTest()
        {
            var found = new List<string>();
            foreach (var (name, symbol, model) in ScrysUses().Uses)
            {
                if (!Hook(symbol)) continue;
                // A property named a hook may be written for the self-test to read, or read for what it stands in with.
                if (!(symbol is IMethodSymbol) && !Marked(symbol)) continue;
                var within = Within(name, model);
                if (within != null && Hook(within)) continue;
                found.Add($"{ScrySource.Where(name)} {within?.Name ?? "?"} uses {symbol.ContainingType?.Name}.{symbol.Name}");
            }
            Violations.None("leave the self-test's hooks to it", found);
        }

        [Fact]
        public void AMemberOnlyTheSelfTestUsesIsAHook()
        {
            var used = ScrysUses().Ids;
            var found = new List<string>();
            var selfTest = ScrySource.SelfTest;
            foreach (var tree in selfTest.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                var model = selfTest.GetSemanticModel(tree);
                foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    var symbol = model.GetSymbolInfo(name).Symbol?.OriginalDefinition;
                    if (!(symbol is IMethodSymbol || symbol is IPropertySymbol) || symbol.IsImplicitlyDeclared || symbol.IsOverride || Hook(symbol)) continue;
                    if (symbol is IMethodSymbol method && method.MethodKind != MethodKind.Ordinary) continue;
                    if (!symbol.Locations.Any(l => l.IsInSource && ScrySource.Relative(l.SourceTree).StartsWith("src/", System.StringComparison.Ordinal))) continue;
                    if (used.Contains(symbol.GetDocumentationCommentId() ?? "")) continue;
                    var where = symbol.Locations.First(l => l.IsInSource);
                    found.Add($"{ScrySource.Relative(where.SourceTree)}:{where.GetLineSpan().StartLinePosition.Line + 1} {symbol.ContainingType?.Name}.{symbol.Name} is used only by the self-test: mark it [ForTest]");
                }
            }
            Violations.None("mark what only the self-test uses", found);
        }
    }
}
