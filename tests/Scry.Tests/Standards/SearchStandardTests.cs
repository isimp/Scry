using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The panel changes the search's text one way (<c>ScryPanel.Searched</c>), so typing,
    /// clearing it with its cross and completing it with Tab do alike: the list filtered anew
    /// from its top, the search's help and the mod report giving way, the selection shown.
    /// </summary>
    public class SearchStandardTests
    {
        [Fact]
        public void ThePanelChangesTheSearchOnlyThroughSearched()
        {
            var found = new List<string>();
            foreach (var (assign, model) in ScrySource.All<AssignmentExpressionSyntax>())
            {
                if (!ScrySource.Relative(assign.SyntaxTree).StartsWith("src/UI/", System.StringComparison.Ordinal)) continue;
                if (!(model.GetSymbolInfo(assign.Left).Symbol is IPropertySymbol property) || property.Name != "Text" || property.ContainingType?.Name != "Explorer") continue;
                if (ScrySource.Member(assign) == "Searched") continue;
                found.Add($"{ScrySource.Where(assign)} in {ScrySource.TopType(assign)}.{ScrySource.Member(assign)}");
            }
            Violations.None("change the search's text in the panel other than through Searched", found);
        }
    }
}
