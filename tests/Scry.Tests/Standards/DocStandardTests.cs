using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Each doc comment belongs to the member it stands over: one summary to a member, and none
    /// left behind where a member moved away, which would otherwise read as the next one's.
    /// </summary>
    public class DocStandardTests
    {
        [Fact]
        public void EachMemberHasOneSummaryOfItsOwn()
        {
            var found = new List<string>();
            var trees = ScrySource.Compilations.SelectMany(c => c.SyntaxTrees).GroupBy(t => t.FilePath).Select(g => g.First());
            foreach (var tree in trees)
            {
                foreach (var token in tree.GetRoot().DescendantTokens())
                {
                    var docs = token.LeadingTrivia.Where(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)).ToList();
                    if (docs.Count == 0) continue;
                    var where = $"{ScrySource.Relative(tree)}:{docs[0].GetLocation().GetLineSpan().StartLinePosition.Line + 1}";
                    var summaries = docs.Sum(d => d.GetStructure().DescendantNodes().OfType<XmlElementSyntax>().Count(e => e.StartTag.Name.LocalName.Text == "summary"));
                    if (summaries > 1) found.Add($"{where} {summaries} summaries over one member");
                    if (!StartsADeclaration(token)) found.Add($"{where} a doc comment over no member");
                }
            }
            Violations.None("leave a doc comment that is not one member's own", found);
        }

        /// <summary>Whether a token is the first of a member's, an enum value's or a local function's declaration.</summary>
        private static bool StartsADeclaration(SyntaxToken token) =>
            token.Parent.AncestorsAndSelf().Any(n => (n is MemberDeclarationSyntax || n is LocalFunctionStatementSyntax || n is AccessorDeclarationSyntax) && n.GetFirstToken() == token);
    }
}
