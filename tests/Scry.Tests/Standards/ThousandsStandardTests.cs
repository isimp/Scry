using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A number of a thousand or more is written with its thousands by commas wherever words tell
    /// it, not only in the panel: in comments, in text written into the code, and in the notes,
    /// README and changelog. What is not prose keeps its own form: code (in backticks or
    /// <c>&lt;c&gt;</c>), quoted text, addresses, dates and hexadecimal. A run of digits starting
    /// with 0 is no amount (a sample of the digits), so it is left as it is.
    /// </summary>
    public class ThousandsStandardTests
    {
        /// <summary>An amount of four digits or more with no commas, not part of a longer word, version or decimal.</summary>
        private static readonly Regex Bare = new Regex(@"(?<![\w.,])[1-9]\d{3,}(?!\w|\.\d)", RegexOptions.CultureInvariant);

        /// <summary>What in prose is not prose: code spans, quoted text, addresses, dates, a character's number. Hexadecimal needs none, as its digits follow a letter.</summary>
        private static readonly Regex NotProse = new Regex(
            @"`[^`]*`|<c>.*?</c>|""[^""]*""|https?://\S+|\b\d{4}-\d{2}(-\d{2})?\b|U\+[0-9A-Fa-f]+",
            RegexOptions.CultureInvariant);

        [Fact]
        public void CommentsWriteThousandsWithCommas()
        {
            var found = new List<string>();
            foreach (var tree in ScrySource.WrittenTrees())
            {
                foreach (var trivia in tree.GetRoot().DescendantTrivia(descendIntoTrivia: true))
                {
                    if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                        && !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) && !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)) continue;
                    foreach (var bare in Bares(trivia.ToFullString())) found.Add($"{Where(tree, trivia.FullSpan.Start + bare.Index)} {bare.Value}");
                }
            }
            Violations.None("write thousands with commas in comments", found);
        }

        [Fact]
        public void TextInTheCodeWritesThousandsWithCommas()
        {
            var found = new List<string>();
            foreach (var (literal, _) in ScrySource.All<LiteralExpressionSyntax>())
            {
                if (!literal.IsKind(SyntaxKind.StringLiteralExpression)) continue;
                foreach (var number in BareIn(literal.Token.ValueText)) found.Add($"{ScrySource.Where(literal)} {number}");
            }
            foreach (var (text, _) in ScrySource.All<InterpolatedStringTextSyntax>())
            {
                foreach (var number in BareIn(text.TextToken.ValueText)) found.Add($"{ScrySource.Where(text)} {number}");
            }
            Violations.None("write thousands with commas in text", found);
        }

        [Fact]
        public void TheNotesWriteThousandsWithCommas()
        {
            var root = ScrySource.Root();
            var files = Directory.EnumerateFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
                .Concat(new[] { "README.md", "CHANGELOG.md" }.Select(name => Path.Combine(root, name)).Where(File.Exists))
                .ToList();
            Assert.NotEmpty(files);

            var found = new List<string>();
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                var fenced = false;
                for (var i = 0; i < lines.Length; i++)
                {
                    if (lines[i].TrimStart().StartsWith("```", System.StringComparison.Ordinal))
                    {
                        fenced = !fenced;
                        continue;
                    }
                    if (fenced) continue;
                    foreach (var number in BareIn(lines[i])) found.Add($"{Path.GetRelativePath(root, file).Replace('\\', '/')}:{i + 1} {number}");
                }
            }
            Violations.None("write thousands with commas in the notes", found);
        }

        [Theory]
        [InlineData("some 5000 m up", "5000")]
        [InlineData("a default of 9999, which", "9999")]
        [InlineData("from 500 to 20000.", "20000")]
        public void ABareAmountIsFound(string prose, string number) => Assert.Equal(new[] { number }, BareIn(prose));

        [Theory]
        [InlineData("some 5,000 m up")]
        [InlineData("under 999 or 1,200 s")]
        [InlineData("`denikson-BepInExPack_Valheim-5.4.2350`")]
        [InlineData("<c>m_range</c> of <c>9999</c>")]
        [InlineData("A whole number: \"12345\".")]
        [InlineData("released 2026-09-28, at https://example.com/1234")]
        [InlineData("the digits 0123456789, layer 0x7FFF, U+FFFD")]
        [InlineData("version 1.0.1600 and 3.14159")]
        [InlineData("Unity 2022.3 and a 1080p screen")]
        [InlineData("`limit 1000` in code")]
        [InlineData("U+2014, an em dash")]
        public void WhatIsNotABareAmountIsLeft(string prose) => Assert.Empty(BareIn(prose));

        private static List<string> BareIn(string text) => Bares(text).Select(m => m.Value).ToList();

        /// <summary>Each bare amount in a text, where it stands in it; what is not prose is blanked, keeping every place.</summary>
        private static IEnumerable<Match> Bares(string text) =>
            Bare.Matches(NotProse.Replace(text, m => new string(' ', m.Length))).Cast<Match>();

        private static string Where(SyntaxTree tree, int position) =>
            $"{Path.GetRelativePath(ScrySource.Root(), tree.FilePath).Replace('\\', '/')}:{tree.GetLineSpan(new Microsoft.CodeAnalysis.Text.TextSpan(position, 0)).StartLinePosition.Line + 1}";
    }
}
