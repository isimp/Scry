using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A release packages Scry.dll alone, so a player installing Scry gets nothing of the
    /// self-test: Scry holds only the hook the self-test plugs into (<c>SelfTestHost</c>) and
    /// the read-outs it looks at. The runner, its words, its command, its files and how it is
    /// drawn in the panel come with <c>Scry.SelfTest.dll</c>, which only a development build
    /// installs, and installing it is what turns the self-test on, so Scry has no setting for it.
    /// </summary>
    public class SelfTestContainmentTests
    {
        /// <summary>The one part of the self-test Scry holds: where it plugs in, with what it offers.</summary>
        private static readonly string[] Hook = { "SelfTestHost", "IRunner" };

        [Fact]
        public void ScryAloneHoldsNothingOfTheSelfTestButTheHookItPlugsInto()
        {
            var found = Declared(ScrySource.Compilation)
                .Where(t => Names(t.Name) && !Hook.Contains(t.Name))
                .Select(t => $"{t.Where} {t.Name}");
            Violations.None("hold only the hook of the self-test in Scry", found);
        }

        [Fact]
        public void ScryAloneNamesNoSelfTestSettingCommandOrFile()
        {
            var found = Literals(ScrySource.Compilation)
                .Where(l => l.Text.IndexOf("selftest", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(l => $"{l.Where} \"{l.Text}\"");
            Violations.None("name no self-test setting, command or file in Scry", found);
        }

        [Fact]
        public void TheSelfTestPluginBringsItsRunnerItsWordsAndItsCommand()
        {
            var types = Declared(ScrySource.SelfTest).Select(t => t.Name).ToList();
            Assert.Contains("ScenarioRunner", types);
            Assert.Contains("SelfTestWords", types);
            Assert.Contains("SelfTestCommand", types);
            Assert.Contains(Literals(ScrySource.SelfTest), l => l.Text.Equals("selftest", StringComparison.OrdinalIgnoreCase));
        }

        [Theory]
        [InlineData("ScenarioRunner")]
        [InlineData("SelfTestWords")]
        [InlineData("SkipScenario")]
        public void APartOfTheSelfTestIsKnownByItsName(string name) => Assert.True(Names(name));

        [Theory]
        [InlineData("Scry")]
        [InlineData("SceneReader")]
        [InlineData("Testing")]
        public void WhatIsNoPartOfTheSelfTestIsLeftAlone(string name) => Assert.False(Names(name));

        /// <summary>Whether a type's name says it is part of the self-test or its runs.</summary>
        private static bool Names(string name) =>
            name.IndexOf("SelfTest", StringComparison.Ordinal) >= 0 || name.IndexOf("Scenario", StringComparison.Ordinal) >= 0;

        /// <summary>Every type a compilation declares, nested ones too, with where.</summary>
        private static IEnumerable<(string Name, string Where)> Declared(CSharpCompilation compilation)
        {
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                foreach (var type in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()) yield return (type.Identifier.Text, ScrySource.Where(type));
            }
        }

        /// <summary>Every string a compilation writes, plain or as the text of an interpolated one, with where.</summary>
        private static IEnumerable<(string Text, string Where)> Literals(CSharpCompilation compilation)
        {
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    if (node is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)) yield return (literal.Token.ValueText, ScrySource.Where(node));
                    else if (node is InterpolatedStringTextSyntax text) yield return (text.TextToken.ValueText, ScrySource.Where(node));
                }
            }
        }
    }
}
