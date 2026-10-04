using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// What a player reads is worded in the model (<c>src/Model</c>), once, and tested there; the
    /// rest of Scry hands the model plain values and shows what comes back, beyond a fixed label
    /// or value of its own. Outside the model, text is put together (an interpolated string,
    /// strings added, joined or formatted, a choice between two texts) only where no player reads
    /// it as Scry's wording: a line for the log or a failure report, the name of a guarded or
    /// timed part, a key, an id or a set's member, text only compared, a member marked
    /// <c>[Diagnostic]</c> (the self-test's strip and reports, the console's dump), a member named
    /// for the key it makes (<c>...Key</c>), or a Unity object's name. Text held in a local first
    /// counts by where the local goes. The fact readers (<c>src/Catalog/Facts</c>) and the panel
    /// are held to it like the rest.
    /// </summary>
    public class WordingStandardTests
    {
        /// <summary>The classes all of whose text goes to the log or a failure report.</summary>
        private static readonly HashSet<string> Telling = new HashSet<string>
        {
            "Log", "Faults", "Listen",
        };

        /// <summary>Parameters that take a key, an id or a part's name, never wording.</summary>
        private static readonly HashSet<string> KeyParameters = new HashSet<string>
        {
            "key", "tipKey", "token", "part",
        };

        /// <summary>The string methods that compare or search text rather than show it.</summary>
        private static readonly HashSet<string> Comparing = new HashSet<string>
        {
            "StartsWith", "EndsWith", "Contains", "IndexOf", "LastIndexOf", "Equals", "Replace", "Split", "TrimEnd", "TrimStart",
        };

        [Fact]
        public void TextAPlayerReadsIsWordedInTheModel()
        {
            var found = new List<string>();
            foreach (var (node, model, how) in Composed())
            {
                if (Allowed(node, model)) continue;
                found.Add($"{ScrySource.Where(node)} {how} in {ScrySource.TopType(node)}.{ScrySource.Member(node)}: {Short(node)}");
            }
            Violations.None("put a player's text together outside the model", found);
        }

        /// <summary>Every outermost piece of text put together outside the model and the self-test.</summary>
        private static IEnumerable<(SyntaxNode Node, SemanticModel Model, string How)> Composed()
        {
            bool Outside(SyntaxNode node)
            {
                var file = ScrySource.Relative(node.SyntaxTree);
                return file.StartsWith("src/", System.StringComparison.Ordinal) && !file.StartsWith("src/Model/", System.StringComparison.Ordinal);
            }
            bool Inner(SyntaxNode node, SemanticModel model) => node.Ancestors().Any(a => a is InterpolatedStringExpressionSyntax
                || (a is BinaryExpressionSyntax b && b.IsKind(SyntaxKind.AddExpression) && model.GetTypeInfo(b).Type?.SpecialType == SpecialType.System_String));

            var seen = new HashSet<SyntaxNode>();
            foreach (var (text, model) in ScrySource.All<InterpolatedStringExpressionSyntax>())
                if (Outside(text) && !Inner(text, model) && seen.Add(text)) yield return (text, model, "an interpolated string");
            foreach (var (plus, model) in ScrySource.All<BinaryExpressionSyntax>())
                if (plus.IsKind(SyntaxKind.AddExpression) && model.GetTypeInfo(plus).Type?.SpecialType == SpecialType.System_String && Outside(plus) && !Inner(plus, model) && seen.Add(plus))
                    yield return (plus, model, "strings added");
            foreach (var (add, model) in ScrySource.All<AssignmentExpressionSyntax>())
                if (add.IsKind(SyntaxKind.AddAssignmentExpression) && model.GetTypeInfo(add.Left).Type?.SpecialType == SpecialType.System_String && Outside(add) && seen.Add(add))
                    yield return (add.Right, model, "added to a string");
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
                if (model.GetSymbolInfo(call).Symbol is IMethodSymbol method && method.ContainingType?.SpecialType == SpecialType.System_String
                    && (method.Name == "Join" || method.Name == "Concat" || method.Name == "Format") && Outside(call) && !Inner(call, model) && seen.Add(call))
                    yield return (call, model, $"string.{method.Name}");
            foreach (var (choice, model) in ScrySource.All<ConditionalExpressionSyntax>())
                if (IsText(choice.WhenTrue) && IsText(choice.WhenFalse) && Outside(choice) && !Inner(choice, model) && seen.Add(choice))
                    yield return (choice, model, "a choice between two texts");
        }

        private static bool IsText(ExpressionSyntax expression) => expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression);

        /// <summary>Whether text put together here is no player's wording, by the member it is in or where it goes.</summary>
        private static bool Allowed(SyntaxNode node, SemanticModel model)
        {
            var member = node.Ancestors().OfType<MemberDeclarationSyntax>().FirstOrDefault();
            if (member != null && IsDiagnostic(member, model)) return true;
            var name = ScrySource.Member(node);
            if (name != null && (name.EndsWith("Key", System.StringComparison.Ordinal) || name.StartsWith("Key", System.StringComparison.Ordinal))) return true;
            return GoesWhereAllowed(node, model, 0);
        }

        private static bool IsDiagnostic(MemberDeclarationSyntax member, SemanticModel model)
        {
            var symbol = model.GetDeclaredSymbol(member) ?? (member is FieldDeclarationSyntax field ? model.GetDeclaredSymbol(field.Declaration.Variables[0]) : null);
            for (var s = symbol; s != null; s = s.ContainingType)
                if (s.GetAttributes().Any(a => a.AttributeClass?.Name == "DiagnosticAttribute")) return true;
            return false;
        }

        /// <summary>Where an expression's value goes: allowed into a telling method, a key's parameter, a comparison, a set, an index, a Unity object's name, or a local that goes only there.</summary>
        private static bool GoesWhereAllowed(SyntaxNode node, SemanticModel model, int depth)
        {
            var value = node;
            while (value.Parent is ParenthesizedExpressionSyntax || value.Parent is ConditionalExpressionSyntax || value.Parent is CastExpressionSyntax)
                value = value.Parent;
            switch (value.Parent)
            {
                case ArgumentSyntax argument when argument.Parent?.Parent is InvocationExpressionSyntax call:
                {
                    if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) return false;
                    if (Telling.Contains(method.ContainingType?.Name ?? "")) return true;
                    if (method.ContainingType?.SpecialType == SpecialType.System_String && Comparing.Contains(method.Name)) return true;
                    if (method.ContainingType?.Name == "HashSet") return true;
                    var index = call.ArgumentList.Arguments.IndexOf(argument);
                    var parameter = argument.NameColon != null ? argument.NameColon.Name.Identifier.Text
                        : index >= 0 && index < method.Parameters.Length ? method.Parameters[index].Name : null;
                    return parameter != null && KeyParameters.Contains(parameter);
                }
                case ArgumentSyntax argument when argument.Parent is BracketedArgumentListSyntax:
                    return true;
                case EqualsValueClauseSyntax equals when equals.Parent is VariableDeclaratorSyntax declarator && declarator.Parent?.Parent is LocalDeclarationStatementSyntax:
                    return depth < 3 && LocalGoesWhereAllowed(model.GetDeclaredSymbol(declarator), declarator, model, depth);
                case AssignmentExpressionSyntax named when named.Right == value && model.GetSymbolInfo(named.Left).Symbol is IPropertySymbol property
                                                           && property.Name == "name" && property.ContainingType?.Name == "Object":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Whether every use of a local is somewhere allowed, or part of more text put together, which is judged on its own.</summary>
        private static bool LocalGoesWhereAllowed(ISymbol local, SyntaxNode from, SemanticModel model, int depth)
        {
            if (local == null) return false;
            var body = from.Ancestors().FirstOrDefault(a => a is BaseMethodDeclarationSyntax || a is AccessorDeclarationSyntax || a is LocalFunctionStatementSyntax
                                                             || a is AnonymousFunctionExpressionSyntax);
            if (body == null) return false;
            foreach (var use in body.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (use.Identifier.Text != local.Name || !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(use).Symbol, local)) continue;
                if (use.Parent is AssignmentExpressionSyntax set && set.Left == use) continue;
                if (use.Ancestors().Any(a => a is InterpolatedStringExpressionSyntax
                        || (a is BinaryExpressionSyntax b && b.IsKind(SyntaxKind.AddExpression) && model.GetTypeInfo(b).Type?.SpecialType == SpecialType.System_String)
                        || (a is AssignmentExpressionSyntax s && s.IsKind(SyntaxKind.AddAssignmentExpression) && s.Left == use))) continue;
                if (!GoesWhereAllowed(use, model, depth + 1)) return false;
            }
            return true;
        }

        private static string Short(SyntaxNode node)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(node.ToString(), @"\s+", " ");
            return text.Length > 90 ? text.Substring(0, 90) + "…" : text;
        }
    }
}
