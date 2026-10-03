using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Keys and search terms are made one way each: an entry's key by <c>EntryKeys</c> ("se:" and
    /// a status effect's name, "mod:" and a mod's), a term the search reads by
    /// <c>SearchHelp.Term</c> ("mod:coolstatues"), and the marks before an effect list's field name
    /// by <c>Groups</c>. A prefix written out anywhere else is a key made by hand, which drifts from
    /// the one the rest of Scry reads; a whole term written out, such as a search example, is text.
    /// </summary>
    public class KeyStandardTests
    {
        private static readonly HashSet<string> Prefixes = new HashSet<string>(System.StringComparer.Ordinal)
        {
            "se:", "raid:", "loc:", "mod:", "biome:", "ui:", "kind:", "has:", "playedby:", "station:", "in:",
        };

        private static readonly string[] Makers = { "EntryKeys.cs", "SearchHelp.cs", "Search.cs", "Groups.cs" };

        [Fact]
        public void KeysAndTermsAreMadeWhereTheyAreRead()
        {
            var found = new List<string>();
            foreach (var (literal, _) in ScrySource.All<LiteralExpressionSyntax>())
            {
                if (!literal.IsKind(SyntaxKind.StringLiteralExpression) || !Prefixes.Contains(literal.Token.ValueText) || Violations.In(literal, Makers)) continue;
                found.Add($"{ScrySource.Where(literal)} \"{literal.Token.ValueText}\" in {ScrySource.TopType(literal)}.{ScrySource.Member(literal)}");
            }
            Violations.None("make keys with EntryKeys and terms with SearchHelp.Term", found);
        }
    }
}
