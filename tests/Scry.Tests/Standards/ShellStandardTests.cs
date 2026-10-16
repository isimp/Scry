using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Scry touches the player's computer only through its own text files: it opens nothing
    /// outside the game and starts no program. A website is copied for the player to open, not
    /// opened for them; a browser the game started would also keep Steam showing Valheim as
    /// running after it quits, as Steam counts every process the game starts as part of it.
    /// </summary>
    public class ShellStandardTests
    {
        [Fact]
        public void NothingOpensOutsideTheGame()
        {
            var found = new List<string>();
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var owner = method.ContainingType?.ToDisplayString();
                if ((owner == "UnityEngine.Application" && method.Name == "OpenURL") || (owner == "System.Diagnostics.Process" && method.Name == "Start"))
                    found.Add($"{ScrySource.Where(call)} {method.Name} in {ScrySource.TopType(call)}.{ScrySource.Member(call)}");
            }
            foreach (var (made, model) in ScrySource.All<BaseObjectCreationExpressionSyntax>())
            {
                if (model.GetTypeInfo(made).Type?.ToDisplayString() != "System.Diagnostics.ProcessStartInfo") continue;
                found.Add($"{ScrySource.Where(made)} ProcessStartInfo in {ScrySource.TopType(made)}.{ScrySource.Member(made)}");
            }
            Violations.None("open something outside the game or start a program", found);
        }
    }
}
