using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The log is written one way, so what it holds follows what the technical notes promise:
    /// a failure through <c>Faults</c>, told once and counted for the self-test; a line Scry
    /// always writes (the startup check, how long reading took) through <c>Log.Report</c>;
    /// something about the game or the PC a player should know through <c>Log.Warn</c>; and
    /// what a preview did through <c>Log.Note</c>, written only with <c>LogPreviews</c> on.
    /// Only those two classes hold the game's logger.
    /// </summary>
    public class LogStandardTests
    {
        [Fact]
        public void OnlyLogAndFaultsWriteToTheLog()
        {
            var found = new List<string>();
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var owner = method.ContainingType?.ToDisplayString() ?? "";
                var writes = owner.StartsWith("BepInEx.Logging", System.StringComparison.Ordinal) || owner == "UnityEngine.Debug" || owner == "System.Console"
                             || owner == "System.Diagnostics.Debug" || owner == "System.Diagnostics.Trace";
                if (!writes || Violations.In(call, "Log.cs", "Faults.cs")) continue;
                found.Add($"{ScrySource.Where(call)} {method.ContainingType.Name}.{method.Name} in {ScrySource.TopType(call)}.{ScrySource.Member(call)}");
            }
            Violations.None("write to the log outside Log and Faults", found);
        }
    }
}
