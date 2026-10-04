using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The game's data is read through one reader for each shape it comes in, so a rule about it
    /// (which slots of an effect list the game plays; where a prefab is found by name) is kept in
    /// one place: an effect list's slots through <c>EffectSlots</c>, a prefab or item by name
    /// through <c>GamePrefabs</c>. Making a new effect list, by setting its slots, is no reading.
    /// </summary>
    public class GameReadingStandardTests
    {
        [Fact]
        public void AnEffectListsSlotsAreReadOnlyByEffectSlots()
        {
            var found = new List<string>();
            foreach (var (name, model) in ScrySource.All<IdentifierNameSyntax>())
            {
                if (name.Identifier.Text != "m_effectPrefabs" || Violations.In(name, "EffectSlots.cs")) continue;
                if (!(model.GetSymbolInfo(name).Symbol is IFieldSymbol field) || field.ContainingType?.Name != "EffectList") continue;
                if (IsSet(name)) continue;
                found.Add($"{ScrySource.Where(name)} in {ScrySource.TopType(name)}.{ScrySource.Member(name)}");
            }
            Violations.None("read an effect list's slots through EffectSlots", found);
        }

        /// <summary>
        /// What Scry tells and plays of an effect list is what the game plays of it, its switched-on
        /// slots (<c>EffectList.Create</c>): who plays a prefab, what a thing leaves behind, what a
        /// place holds. Only telling why a list showed nothing names the slots it leaves out.
        /// </summary>
        [Fact]
        public void OnlyTheSlotsTheGamePlaysAreTold()
        {
            var found = new List<string>();
            foreach (var (name, model) in ScrySource.All<IdentifierNameSyntax>())
            {
                if (name.Identifier.Text != "Names" && name.Identifier.Text != "NamesListed") continue;
                if (!(model.GetSymbolInfo(name).Symbol is IMethodSymbol method) || method.ContainingType?.Name != "EffectSlots") continue;
                if (Violations.In(name, "EffectSlots.cs") || ScrySource.Member(name) == "TellEmpty") continue;
                found.Add($"{ScrySource.Where(name)} EffectSlots.{method.Name} in {ScrySource.TopType(name)}.{ScrySource.Member(name)}");
            }
            Violations.None("tell an effect list's switched-off slots other than where a list that showed nothing is explained", found);
        }

        [Fact]
        public void PrefabsAreLookedUpByNameOnlyByGamePrefabs()
        {
            var found = new List<string>();
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (Violations.In(call, "GamePrefabs.cs") || !(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var owner = method.ContainingType?.Name;
                if ((owner == "ZNetScene" && method.Name == "GetPrefab") || (owner == "ObjectDB" && method.Name == "GetItemPrefab"))
                {
                    found.Add($"{ScrySource.Where(call)} {owner}.{method.Name} in {ScrySource.TopType(call)}.{ScrySource.Member(call)}");
                }
            }
            Violations.None("look prefabs up through GamePrefabs", found);
        }

        /// <summary>Whether a field is being set, as a new list's slots are, rather than read.</summary>
        private static bool IsSet(IdentifierNameSyntax name)
        {
            SyntaxNode target = name;
            if (target.Parent is MemberAccessExpressionSyntax access && access.Name == name) target = access;
            return target.Parent is AssignmentExpressionSyntax assignment && assignment.Left == target && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression);
        }
    }
}
