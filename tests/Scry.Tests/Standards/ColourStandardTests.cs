using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The panel's colours are the skin's: each named once in <c>Skin</c>, and any other made
    /// from them there (one at another strength, darkened under its own colour, lifted toward
    /// white). A part of the panel that wants a colour asks the skin for it, so the look is
    /// changed in one place and every part keeps to it. The stage's lights, grounds and pixels
    /// are the stage's own, not the panel's.
    /// </summary>
    public class ColourStandardTests
    {
        [Fact]
        public void ThePanelMakesNoColourOutsideTheSkin()
        {
            var found = new List<string>();
            foreach (var (made, model) in ScrySource.All<BaseObjectCreationExpressionSyntax>())
            {
                if (!InPanel(made) || !IsColour(model.GetTypeInfo(made).Type)) continue;
                found.Add($"{ScrySource.Where(made)} a colour made in {ScrySource.TopType(made)}.{ScrySource.Member(made)}");
            }
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!InPanel(call) || !(model.GetSymbolInfo(call).Symbol is IMethodSymbol method) || !method.IsStatic || !IsColour(method.ContainingType)) continue;
                found.Add($"{ScrySource.Where(call)} Color.{method.Name} in {ScrySource.TopType(call)}.{ScrySource.Member(call)}");
            }
            Violations.None("take the panel's colours from Skin", found);
        }

        private static bool InPanel(SyntaxNode node)
        {
            var path = ScrySource.Relative(node.SyntaxTree);
            return path.StartsWith("src/UI/", System.StringComparison.Ordinal) && path != "src/UI/Skin.cs";
        }

        private static bool IsColour(ITypeSymbol type)
        {
            var name = type?.ToDisplayString();
            return name == "UnityEngine.Color" || name == "UnityEngine.Color32";
        }
    }
}
