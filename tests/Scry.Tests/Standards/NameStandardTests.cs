using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// An entry is shown by the name the game gives it, or its prefab's where the game gives
    /// none, and that choice is made in one place, <c>Entry.ShownName</c>. A choice written out
    /// again (a display name if it has one, else something) would drift from it, so it is found
    /// in every form: a conditional with an entry's display name on either side.
    /// </summary>
    public class NameStandardTests
    {
        [Fact]
        public void AnEntrysShownNameIsWorkedOutOnlyByEntry()
        {
            var found = new List<string>();
            foreach (var (choice, model) in ScrySource.All<ConditionalExpressionSyntax>())
            {
                if (ScrySource.TopType(choice) == "Entry") continue;
                if (IsDisplayName(choice.WhenTrue, model) || IsDisplayName(choice.WhenFalse, model))
                {
                    found.Add($"{ScrySource.Where(choice)} in {ScrySource.TopType(choice)}.{ScrySource.Member(choice)}: {choice}");
                }
            }
            Violations.None("take an entry's shown name from Entry.ShownName", found);
        }

        private static bool IsDisplayName(ExpressionSyntax branch, SemanticModel model) =>
            branch is MemberAccessExpressionSyntax access && access.Name.Identifier.Text == "DisplayName"
            && model.GetSymbolInfo(access).Symbol is IPropertySymbol property && property.ContainingType?.Name == "Entry";
    }
}
