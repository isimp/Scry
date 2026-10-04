using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The panel, the stage, the previews and what is known of the world are each one static
    /// class in parts, a file for each part. State has one owner: a field of such a class is
    /// changed only in the file that declares it, by the part it belongs to, and another part,
    /// or another class, asks that part to change it by a method named for what it does, or
    /// through a property the owner offers for setting. A field set, cleared or filled from
    /// another file is state with two owners, which no one part can be read to understand.
    /// </summary>
    public class OwnerStandardTests
    {
        private static readonly HashSet<string> Changes = new HashSet<string>
        {
            "Clear", "Add", "AddRange", "Insert", "Remove", "RemoveAt", "RemoveAll", "Enqueue", "Dequeue", "Push", "Pop", "UnionWith", "ExceptWith", "Sort", "Reverse",
        };

        [Fact]
        public void APartsStateIsChangedOnlyByThatPart()
        {
            var found = new List<string>();
            foreach (var (name, model) in ScrySource.All<IdentifierNameSyntax>())
            {
                if (!Changed(name)) continue;
                var symbol = model.GetSymbolInfo(name).Symbol;
                if (ChangedThroughCall(name) && !IsCollection(symbol)) continue;
                if (!(symbol is IFieldSymbol || symbol is IPropertySymbol property && KeptPrivately(property)) || !symbol.IsStatic) continue;
                var owner = symbol.ContainingType;
                if (owner == null || !owner.IsStatic || owner.DeclaringSyntaxReferences.Length < 2) continue;
                var declared = symbol.Locations.FirstOrDefault(l => l.IsInSource)?.SourceTree?.FilePath;
                if (declared == null || declared == name.SyntaxTree.FilePath) continue;
                found.Add($"{ScrySource.Where(name)} {owner.Name}.{symbol.Name}, kept in {Path.GetFileName(declared)}");
            }
            Violations.None("change a part's state from another file", found.Distinct().OrderBy(f => f, System.StringComparer.Ordinal));
        }

        /// <summary>
        /// An auto-property whose setter only the class itself may use: state, like a field. One
        /// that offers its setter beyond the class is what the owner offers for setting.
        /// </summary>
        private static bool KeptPrivately(IPropertySymbol property) =>
            property.SetMethod != null && property.SetMethod.DeclaredAccessibility == Accessibility.Private && property.DeclaringSyntaxReferences.Any(r =>
                r.GetSyntax() is PropertyDeclarationSyntax declaration && declaration.AccessorList != null && declaration.AccessorList.Accessors.All(a => a.Body == null && a.ExpressionBody == null));

        /// <summary>Whether a name is changed by a call on it (<c>Add</c>, <c>Clear</c> and the like) rather than set.</summary>
        private static bool ChangedThroughCall(IdentifierNameSyntax name)
        {
            SyntaxNode node = name;
            if (node.Parent is MemberAccessExpressionSyntax access && access.Name == node) node = access;
            return node.Parent is MemberAccessExpressionSyntax call && call.Expression == node;
        }

        /// <summary>
        /// Whether a field holds one of .NET's collections, whose contents are the owner's state; a
        /// book of the model's own (<c>UseBook</c>, <c>MakerBook</c>) keeps its own state and is
        /// asked to change it.
        /// </summary>
        private static bool IsCollection(ISymbol symbol)
        {
            var type = (symbol as IFieldSymbol)?.Type ?? (symbol as IPropertySymbol)?.Type;
            return type is IArrayTypeSymbol || type?.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";
        }

        /// <summary>Whether a name is changed where it stands: assigned, stepped, passed by ref or out, its contents changed, or an item set.</summary>
        private static bool Changed(IdentifierNameSyntax name)
        {
            SyntaxNode node = name;
            if (node.Parent is MemberAccessExpressionSyntax access && access.Name == node) node = access;
            var parent = node.Parent;
            switch (parent)
            {
                case AssignmentExpressionSyntax assign: return assign.Left == node;
                case PrefixUnaryExpressionSyntax pre: return pre.IsKind(SyntaxKind.PreIncrementExpression) || pre.IsKind(SyntaxKind.PreDecrementExpression);
                case PostfixUnaryExpressionSyntax post: return post.IsKind(SyntaxKind.PostIncrementExpression) || post.IsKind(SyntaxKind.PostDecrementExpression);
                case ArgumentSyntax argument when argument.Parent is TupleExpressionSyntax tuple:
                    return tuple.Parent is AssignmentExpressionSyntax deconstruct && deconstruct.Left == tuple;
                case ArgumentSyntax argument: return !argument.RefKindKeyword.IsKind(SyntaxKind.None) && !argument.RefKindKeyword.IsKind(SyntaxKind.InKeyword);
                case MemberAccessExpressionSyntax call: return call.Expression == node && call.Parent is InvocationExpressionSyntax && Changes.Contains(call.Name.Identifier.Text);
                case ElementAccessExpressionSyntax item: return item.Expression == node && item.Parent is AssignmentExpressionSyntax set && set.Left == item;
                default: return false;
            }
        }
    }
}
