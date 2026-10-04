using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The fact readers (<c>src/Catalog/Facts</c>) read the game and hand what they read to the
    /// model's words: a reader writes no text of its own beyond a fixed label or value, so every
    /// phrase a page shows is worded once, in the model, and tested there. Text a reader puts
    /// together (an interpolated string, strings added or joined, a choice between two texts) is
    /// a phrase that belongs in the model.
    /// </summary>
    public class ReaderStandardTests
    {
        private const string Readers = "src/Catalog/Facts/";

        [Fact]
        public void FactReadersPutNoTextTogether()
        {
            var found = new List<string>();
            void Add(SyntaxNode node, string how)
            {
                if (ScrySource.Relative(node.SyntaxTree).StartsWith(Readers, System.StringComparison.Ordinal)) found.Add($"{ScrySource.Where(node)} {how}: {Short(node)}");
            }

            foreach (var (text, _) in ScrySource.All<InterpolatedStringExpressionSyntax>()) Add(text, "an interpolated string");
            foreach (var (plus, model) in ScrySource.All<BinaryExpressionSyntax>())
            {
                if (plus.IsKind(SyntaxKind.AddExpression) && IsString(model.GetTypeInfo(plus).Type)) Add(plus, "strings added");
            }
            foreach (var (add, model) in ScrySource.All<AssignmentExpressionSyntax>())
            {
                if (add.IsKind(SyntaxKind.AddAssignmentExpression) && IsString(model.GetTypeInfo(add.Left).Type)) Add(add, "added to a string");
            }
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(call).Symbol is IMethodSymbol method && method.ContainingType?.SpecialType == SpecialType.System_String
                    && (method.Name == "Join" || method.Name == "Concat" || method.Name == "Format"))
                    Add(call, $"string.{method.Name}");
            }
            foreach (var (choice, _) in ScrySource.All<ConditionalExpressionSyntax>())
            {
                if (IsText(choice.WhenTrue) && IsText(choice.WhenFalse)) Add(choice, "a choice between two texts");
            }
            Violations.None("put text together in a fact reader rather than in the model's words", found.Distinct());
        }

        private static bool IsString(ITypeSymbol type) => type?.SpecialType == SpecialType.System_String;

        private static bool IsText(ExpressionSyntax expression) => expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression);

        private static string Short(SyntaxNode node)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(node.ToString(), @"\s+", " ");
            return text.Length > 100 ? text.Substring(0, 100) + "…" : text;
        }
    }
}
