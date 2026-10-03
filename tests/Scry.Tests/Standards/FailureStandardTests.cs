using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A failure goes one way. <c>Steps.Run</c> is the one place any failure is caught; Scry's
    /// work runs through it by <c>Guard.Run</c> (a part, told once), <c>Guard.Each</c> (one
    /// item of many, left out and summed up) or <c>Guard.Read</c> (a step of reading the
    /// catalog), the model's own work by <c>Steps.Run</c>, and what fails is told through
    /// <c>Faults</c>, which the self-test counts. Only a failure of a named kind a parser or the
    /// runtime gives on purpose may be caught where it comes, and handled there.
    /// </summary>
    public class FailureStandardTests
    {
        /// <summary>The failures caught where they come, each for what it means there.</summary>
        private static readonly Dictionary<string, string> Expected = new Dictionary<string, string>
        {
            // A manifest that is no JSON is no manifest; any other failure is a fault.
            ["src/Model/ModManifest.cs"] = "System.FormatException",
            // Scry's own types that cannot load on this game's version are left out, the rest kept.
            ["src/Core/About.cs"] = "System.Reflection.ReflectionTypeLoadException",
        };

        [Fact]
        public void OnlyStepsCatchEveryFailure()
        {
            var found = new List<string>();
            foreach (var (clause, model) in ScrySource.All<CatchClauseSyntax>())
            {
                if (Violations.In(clause, "Steps.cs")) continue;
                var type = clause.Declaration == null ? null : model.GetTypeInfo(clause.Declaration.Type).Type?.ToDisplayString();
                var file = ScrySource.Relative(clause.SyntaxTree);
                if (type != null && Expected.TryGetValue(file, out var allowed) && allowed == type && clause.Block.Statements.Count > 0) continue;
                found.Add($"{ScrySource.Where(clause)} catch ({type ?? "everything"}) in {ScrySource.TopType(clause)}.{ScrySource.Member(clause)}");
            }
            Violations.None("catch a failure outside Steps.Run", found);
        }

        [Fact]
        public void FailuresAreToldThroughFaults()
        {
            // A failure written to the log by hand is neither told once nor counted for the self-test.
            var found = new List<string>();
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var owner = method.ContainingType?.ToDisplayString();
                var logs = owner != null && (owner.StartsWith("BepInEx.Logging", System.StringComparison.Ordinal) || owner == "UnityEngine.Debug");
                if (!logs || Violations.In(call, "Faults.cs")) continue;
                var tellsFailure = call.ArgumentList.DescendantNodes().OfType<ExpressionSyntax>()
                    .Any(e => IsException(model.GetTypeInfo(e).Type));
                if (tellsFailure) found.Add($"{ScrySource.Where(call)} {method.Name} of a failure");
            }
            Violations.None("tell a failure in the log outside Faults", found);
        }

        private static bool IsException(ITypeSymbol type)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                if (t.ToDisplayString() == "System.Exception") return true;
            }
            return false;
        }
    }
}
