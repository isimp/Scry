using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Under the strict rules a page keeps numbers back until a deed shows them (<see cref="Deed"/>)
    /// and names back until they were seen go together one way (<see cref="Tie"/>). A deed a page
    /// waits on that nothing in play or in the catch-up ever writes keeps that page from being found
    /// out whole whatever the player does, and so does a way of going together nothing writes. So
    /// every deed the facts wait on is written by something in Scry outside the facts, as a deed
    /// found out (<c>FoundOut</c>, <c>Prove</c>, or the deed an event tells, <c>EventRule.DeedOf</c>),
    /// and every way of going together is written as seen (<c>SawWith</c>, <c>Saw</c>).
    /// </summary>
    public class ProducerStandardTests
    {
        private const string Facts = "src/Catalog/Facts/";

        /// <summary>The members of a Scry enum named in Scry's own source, each with where it stands.</summary>
        private static IEnumerable<(IFieldSymbol Member, MemberAccessExpressionSyntax Node)> Named(string type)
        {
            foreach (var (access, model) in ScrySource.All<MemberAccessExpressionSyntax>())
            {
                if (!ScrySource.Relative(access.SyntaxTree).StartsWith("src/")) continue;
                if (model.GetSymbolInfo(access).Symbol is IFieldSymbol field && field.ContainingType?.TypeKind == TypeKind.Enum && field.ContainingType.Name == type)
                    yield return (field, access);
            }
        }

        /// <summary>Whether a node stands inside a call to a method of one of these names, or inside a member of a type, as "EventRule.DeedOf".</summary>
        private static bool Inside(SyntaxNode node, string[] calls, string member = null) =>
            node.Ancestors().OfType<InvocationExpressionSyntax>().Any(call => calls.Contains(Called(call)))
            || (member != null && ScrySource.TopType(node) + "." + ScrySource.Member(node) == member);

        private static string Called(InvocationExpressionSyntax call)
        {
            switch (call.Expression)
            {
                case MemberAccessExpressionSyntax access: return access.Name.Identifier.Text;
                case MemberBindingExpressionSyntax binding: return binding.Name.Identifier.Text;
                case IdentifierNameSyntax name: return name.Identifier.Text;
                default: return null;
            }
        }

        [Fact]
        public void EveryDeedAPageWaitsOnIsWrittenBySomething()
        {
            var deeds = Named(nameof(Deed)).ToList();
            var waited = deeds.Where(d => ScrySource.Relative(d.Node.SyntaxTree).StartsWith(Facts)).Select(d => d.Member.Name).Distinct();
            var written = new HashSet<string>(deeds
                .Where(d => !ScrySource.Relative(d.Node.SyntaxTree).StartsWith(Facts) && Inside(d.Node, new[] { "FoundOut", "Prove" }, "EventRule.DeedOf"))
                .Select(d => d.Member.Name));
            Violations.None("write every deed a page waits on, so the page can be found out whole", waited.Where(d => !written.Contains(d)).Select(d => $"Deed.{d} is waited on in the facts and written by nothing"));
        }

        [Fact]
        public void EveryWayOfGoingTogetherIsWrittenBySomething()
        {
            var ties = Named(nameof(Tie)).ToList();
            var written = new HashSet<string>(ties.Where(t => Inside(t.Node, new[] { "SawWith", "Saw" })).Select(t => t.Member.Name));
            var all = typeof(Tie).GetEnumNames();
            Violations.None("write every way of going together a page can ask for", all.Where(t => !written.Contains(t)).Select(t => $"Tie.{t} is written by nothing"));
        }
    }
}
