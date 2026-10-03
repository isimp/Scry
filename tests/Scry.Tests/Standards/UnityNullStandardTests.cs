using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A Unity object is held to be none by Unity's own check, which <c>?.</c>, <c>??</c> and
    /// <c>??=</c> skip: a copy destroyed, the player gone, a component not there would pass them
    /// and fail on use. So none of them is used on a Unity object unless it went through
    /// <c>OrNull()</c> first, which makes Unity's none a real null.
    /// </summary>
    public class UnityNullStandardTests
    {
        [Fact]
        public void NullChecksOnUnityObjectsUseUnitysOwn()
        {
            var found = new List<string>();
            foreach (var (access, model) in ScrySource.All<ConditionalAccessExpressionSyntax>())
            {
                if (Unchecked(access.Expression, model)) found.Add($"{ScrySource.Where(access)} {access.Expression}?.");
            }
            foreach (var (binary, model) in ScrySource.All<BinaryExpressionSyntax>())
            {
                if (binary.IsKind(SyntaxKind.CoalesceExpression) && Unchecked(binary.Left, model)) found.Add($"{ScrySource.Where(binary)} {binary.Left} ??");
            }
            foreach (var (assign, model) in ScrySource.All<AssignmentExpressionSyntax>())
            {
                if (assign.IsKind(SyntaxKind.CoalesceAssignmentExpression) && Unchecked(assign.Left, model)) found.Add($"{ScrySource.Where(assign)} {assign.Left} ??=");
            }
            Violations.None("check Unity objects with Unity's own null, or OrNull() before ?. and ??", found);
        }

        /// <summary>A Unity object used with ?. or ?? that did not go through OrNull().</summary>
        private static bool Unchecked(ExpressionSyntax operand, SemanticModel model)
        {
            if (!IsUnity(model.GetTypeInfo(operand).Type)) return false;
            return !(operand is InvocationExpressionSyntax call && model.GetSymbolInfo(call).Symbol is IMethodSymbol method && method.Name == "OrNull" && method.ContainingType?.Name == "UnityNull");
        }

        private static bool IsUnity(ITypeSymbol type)
        {
            for (var each = type; each != null; each = each.BaseType)
            {
                if (each.ToDisplayString() == "UnityEngine.Object") return true;
            }
            return false;
        }
    }
}
