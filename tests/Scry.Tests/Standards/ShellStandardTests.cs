using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Steam counts every process the game starts as part of the game: a browser the game started
    /// itself keeps Steam showing Valheim as running after it quits, and refusing to start it
    /// again, until that browser is closed. So Scry starts no program itself. What it opens outside
    /// the game, a mod's website, goes through <c>Shell</c>, which on Windows hands it to the
    /// running desktop shell by a short-lived explorer.exe, leaving the program it starts to the
    /// shell.
    /// </summary>
    public class ShellStandardTests
    {
        [Fact]
        public void WhatOpensOutsideTheGameOpensThroughShell()
        {
            var found = new List<string>();
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method) || Violations.In(call, "Shell.cs")) continue;
                var owner = method.ContainingType?.ToDisplayString();
                if ((owner == "UnityEngine.Application" && method.Name == "OpenURL") || (owner == "System.Diagnostics.Process" && method.Name == "Start"))
                    found.Add($"{ScrySource.Where(call)} {method.Name} in {ScrySource.TopType(call)}.{ScrySource.Member(call)}");
            }
            foreach (var (made, model) in ScrySource.All<BaseObjectCreationExpressionSyntax>())
            {
                if (Violations.In(made, "Shell.cs") || model.GetTypeInfo(made).Type?.ToDisplayString() != "System.Diagnostics.ProcessStartInfo") continue;
                found.Add($"{ScrySource.Where(made)} ProcessStartInfo in {ScrySource.TopType(made)}.{ScrySource.Member(made)}");
            }
            Violations.None("open something outside the game other than through Shell", found);
        }

        [Fact]
        public void ShellStartsOnlyExplorerAndLetsTheShellStartTheRest()
        {
            var starts = new List<string>();
            var found = new List<string>();
            foreach (var (made, model) in ScrySource.All<BaseObjectCreationExpressionSyntax>())
            {
                if (!Violations.In(made, "Shell.cs") || model.GetTypeInfo(made).Type?.ToDisplayString() != "System.Diagnostics.ProcessStartInfo") continue;
                var where = ScrySource.Where(made);
                starts.Add(where);
                // Started by the operating system itself, not by the shell from the game's process.
                var direct = made.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>().Any(a =>
                    a.Left is IdentifierNameSyntax name && name.Identifier.Text == "UseShellExecute" && a.Right.IsKind(SyntaxKind.FalseLiteralExpression)) == true;
                if (!direct) found.Add($"{where} starts through the shell from the game's process (UseShellExecute is not false)");
                var body = made.Ancestors().OfType<MemberDeclarationSyntax>().First();
                var explorer = body.DescendantNodes().OfType<LiteralExpressionSyntax>().Any(l =>
                    l.IsKind(SyntaxKind.StringLiteralExpression) && l.Token.ValueText.EndsWith("explorer.exe", System.StringComparison.OrdinalIgnoreCase));
                if (!explorer) found.Add($"{where} starts something other than explorer.exe");
            }
            Assert.True(starts.Count > 0, "Shell starts nothing, so nothing opens outside the game");
            Violations.None("start a program other than explorer.exe from Shell", found);
        }
    }
}
