using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Scry is built in layers, each leaning only on those below it, so a part can be read and
    /// changed knowing what it may reach: <c>src/Model</c>, which needs no game; <c>src/Core</c>,
    /// what every layer leans on (failures, the log, settings, timing); <c>src/Catalog</c> and
    /// <c>src/Patches</c>, which read and watch the game; <c>src/Preview</c>, which makes and plays
    /// the copies; <c>src/UI</c>, the panel; and at the top of <c>src</c> the plugin, the session
    /// and the command, which put them together. A type named from a layer below its own is a
    /// layer reaching up, found by the compiler's own reading of every name.
    /// </summary>
    public class LayerStandardTests
    {
        private static readonly Dictionary<string, int> Layers = new Dictionary<string, int>
        {
            ["Model"] = 0,
            ["Core"] = 1,
            ["Catalog"] = 2,
            ["Patches"] = 2,
            ["Preview"] = 3,
            ["UI"] = 4,
        };

        /// <summary>The files at the top of src put the layers together.</summary>
        private const int Top = 5;

        private static int LayerOf(string path)
        {
            var parts = Path.GetRelativePath(ScrySource.Root(), path).Replace('\\', '/').Split('/');
            return parts.Length > 2 && Layers.TryGetValue(parts[1], out var layer) ? layer : Top;
        }

        [Fact]
        public void EachLayerLeansOnlyOnThoseBelowIt()
        {
            var compilation = ScrySource.Compilation;
            var layerOf = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var declared in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(declared) is INamedTypeSymbol type && type.ContainingType == null) layerOf[type] = LayerOf(tree.FilePath);
                }
            }

            var found = new SortedSet<string>();
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                var from = LayerOf(tree.FilePath);
                var model = compilation.GetSemanticModel(tree);
                foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    var symbol = model.GetSymbolInfo(name).Symbol;
                    var type = symbol as INamedTypeSymbol ?? symbol?.ContainingType;
                    while (type?.ContainingType != null) type = type.ContainingType;
                    if (type == null || !layerOf.TryGetValue(type.OriginalDefinition, out var to) || to <= from) continue;
                    found.Add($"{Path.GetRelativePath(ScrySource.Root(), tree.FilePath).Replace('\\', '/')} names {type.Name}");
                }
            }
            Violations.None("lean only on the layers below", found);
        }
    }
}
