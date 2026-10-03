using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Numbers read one way everywhere: a number becomes shown text only through <c>Numbers</c>
    /// (thousands by commas, English whatever the PC's language), text in Scry's own files and
    /// keys only through <c>Stored</c>, and a number is read from text only through <c>Stored</c>. Every other way a
    /// number becomes text, written or implicit (a <c>{count}</c> in an interpolated string, a
    /// <c>"x" + 2</c>), takes the PC's language or misses the thousands, so this holds the whole
    /// source and the self-test to it.
    /// </summary>
    public class NumberStandardTests
    {
        private static readonly string[] Formatters = { "Numbers.cs", "Stored.cs" };

        [Fact]
        public void NumbersBecomeTextOnlyThroughNumbersOrStored()
        {
            var found = new List<string>();
            void Add(SyntaxNode node, string how)
            {
                if (!Violations.In(node, Formatters)) found.Add($"{ScrySource.Where(node)} {how}: {Short(node)}");
            }

            foreach (var (hole, model) in ScrySource.All<InterpolationSyntax>())
                if (Violations.Numeric(model.GetTypeInfo(hole.Expression).Type)) Add(hole, "a number in an interpolated string");

            foreach (var (plus, model) in ScrySource.All<BinaryExpressionSyntax>())
            {
                if (!plus.IsKind(SyntaxKind.AddExpression)) continue;
                var left = model.GetTypeInfo(plus.Left).Type;
                var right = model.GetTypeInfo(plus.Right).Type;
                if ((left?.SpecialType == SpecialType.System_String && Violations.Numeric(right)) || (right?.SpecialType == SpecialType.System_String && Violations.Numeric(left)))
                    Add(plus, "a number added to a string");
            }

            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var owner = method.ContainingType?.ToDisplayString();
                if (method.Name == "ToString" && Violations.Numeric(method.ContainingType)) Add(call, "a number's ToString");
                else if (owner == "System.Convert" && method.Name == "ToString" && method.Parameters.Length > 0 && Violations.Numeric(method.Parameters[0].Type)) Add(call, "Convert.ToString of a number");
                else if ((owner == "string" || owner == "System.Text.StringBuilder" || owner == "System.IO.TextWriter") && Formats(call, method, model))
                    Add(call, $"a number given to {method.ContainingType.Name}.{method.Name}");
            }
            Violations.None("turn a number into text outside Numbers and Stored", found);
        }

        [Fact]
        public void NumbersAreReadFromTextOnlyThroughStored()
        {
            var found = new List<string>();
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var parses = (method.Name == "Parse" || method.Name == "TryParse") && Violations.Numeric(method.ContainingType)
                             || method.ContainingType?.ToDisplayString() == "System.Convert" && method.Name.StartsWith("To") && Violations.Numeric(method.ReturnType)
                                && method.Parameters.Length > 0 && method.Parameters[0].Type.SpecialType == SpecialType.System_String;
                if (parses && !Violations.In(call, "Stored.cs")) found.Add($"{ScrySource.Where(call)} {Short(call)}");
            }
            Violations.None("read a number from text outside Stored", found);
        }

        /// <summary>
        /// Whether a string or writer method writes a number as text: one joined or concatenated
        /// as such (<c>string.Join</c> of numbers), one boxed for formatting, or the value an
        /// <c>Append</c>, <c>Insert</c> or <c>Write</c> writes. A position or a length is no text.
        /// </summary>
        private static bool Formats(InvocationExpressionSyntax call, IMethodSymbol method, SemanticModel model)
        {
            if (method.TypeArguments.Any(Violations.Numeric)) return true;
            if (!(model.GetOperation(call) is Microsoft.CodeAnalysis.Operations.IInvocationOperation invocation)) return false;
            foreach (var argument in invocation.Arguments)
            {
                // A number boxed for formatting is converted implicitly; one cast to a character on purpose is a character.
                var value = argument.Value is Microsoft.CodeAnalysis.Operations.IConversionOperation conversion && conversion.IsImplicit ? conversion.Operand : argument.Value;
                if (argument.Value is Microsoft.CodeAnalysis.Operations.IArrayCreationOperation array && array.Initializer != null)
                {
                    if (array.Initializer.ElementValues.Any(e => Violations.Numeric((e as Microsoft.CodeAnalysis.Operations.IConversionOperation)?.Operand.Type ?? e.Type))) return true;
                    continue;
                }
                if (!Violations.Numeric(value.Type)) continue;
                var parameter = argument.Parameter;
                if (parameter == null) continue;
                if (parameter.Type.SpecialType == SpecialType.System_Object || parameter.Name == "value") return true;
            }
            return false;
        }

        private static string Short(SyntaxNode node)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(node.ToString(), @"\s+", " ");
            return text.Length > 100 ? text.Substring(0, 100) + "…" : text;
        }
    }
}
