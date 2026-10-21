using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// What Scry knows of the world it is in lives in src/Catalog, and has to go when the world is
    /// left, or the next world starts with the last one's. Every class there keeping something in
    /// a static field it changes registers one reset with <c>WorldCaches</c>, and that reset, or
    /// what it calls of the class's own, sets or empties every such field. A field made once for
    /// the whole game, the same in every world, is listed with why.
    /// </summary>
    public class SessionStateStandardTests
    {
        /// <summary>Fields kept from world to world, by class and name, with why.</summary>
        private static readonly Dictionary<string, string> KeptAcrossWorlds = new Dictionary<string, string>
        {
            ["CatalogBuilder.OwnListsByType"] = "the effect lists each of the game's types holds, found once per type",
            ["CharacterRecords._materials"] = "bound once to the game's own code, the same in every world",
            ["CharacterRecords._recipes"] = "bound once to the game's own code, the same in every world",
            ["CharacterRecords._stations"] = "bound once to the game's own code, the same in every world",
            ["CharacterRecords._biomes"] = "bound once to the game's own code, the same in every world",
            ["CharacterRecords._trophies"] = "bound once to the game's own code, the same in every world",
            ["CharacterRecords._uniques"] = "bound once to the game's own code, the same in every world",
            ["CompendiumKeeper._file"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["CompendiumKeeper._fileOf"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["CompendiumKeeper._namedBy"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["CompendiumKeeper._sourceOf"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["CompendiumKeeper._idOf"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["CompendiumKeeper._reading"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["CompendiumKeeper._unreadable"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["CompendiumKeeper._caughtUp"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["CompendiumKeeper.BiomeKeys"] = "each biome's key, the same in every world",
            ["CompendiumLink._registeredOn"] = "registered on the game's message router; a new router, as in another world, registers anew and starts afresh",
            ["DungeonsInGround._registeredOn"] = "registered on the game's message router; a new router, as in another world, registers anew and starts afresh",
            ["EventLink._registeredOn"] = "registered on the game's message router; a new router, as in another world, registers anew and starts afresh",
            ["GroupMapsLink._registeredOn"] = "registered on the game's message router; a new router, as in another world, registers anew and starts afresh",
            ["CompendiumMap.ReadThisSession"] = "the worlds whose map was read again, kept until the game closes so each is read again once a game",
            ["CompendiumMap._exploredBits"] = "bound once to the game's own code, the same in every world",
            ["CompendiumMap._sharedBits"] = "bound once to the game's own code, the same in every world",
            ["CompendiumSight._places"] = "bound once to the game's own code, the same in every world",
            ["CompendiumSight._container"] = "bound once to the game's own code, the same in every world",
            ["CompendiumSight._solid"] = "the layers sight looks through, the same in every world",
            ["DungeonWalks._own"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["DungeonWalks._file"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["DungeonWalks._reading"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["DungeonWalks._unreadable"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["DungeonWalks._savedAt"] = "the character's own, kept from world to world while it plays, worked out again when what names it changes",
            ["Facts._materialProperties"] = "bound once to the game's own code, the same in every world",
            ["Knowledge.Properties"] = "the properties of the game's types a mod sets, found once per type",
            ["Knowledge._spawnSystems"] = "bound once to the game's own code, the same in every world",
            ["Knowledge._spawnSystemsTried"] = "bound once to the game's own code, the same in every world",
            ["ModConfigs._byName"] = "the mods loaded, the same for the whole game",
            ["ModFolders.Icons"] = "made from the mods' own files, of no world, so kept for the next",
            ["ModFolders._loadImage"] = "bound once to the game's own code, the same in every world",
            ["ModFolders._lookedForLoadImage"] = "bound once to the game's own code, the same in every world",
            ["ModHooks.Seen"] = "the mods loaded, the same for the whole game",
            ["ModHooks.ByRule"] = "the mods loaded, the same for the whole game",
            ["ModHooks.ByMod"] = "the mods loaded, the same for the whole game",
            ["PrefabGear.ToldSets"] = "the armour sets told in the log, each once a game",
            ["TypeFields.ByName"] = "the fields of the game's types, found once per type",
        };

        private static readonly HashSet<string> Collections = new HashSet<string>
        {
            "List", "Dictionary", "HashSet", "Queue", "Stack", "SortedSet", "SortedDictionary", "LinkedList", "ConcurrentDictionary", "ConcurrentQueue",
        };

        private static readonly HashSet<string> Filling = new HashSet<string>
        {
            "Add", "AddRange", "Insert", "Remove", "RemoveAt", "RemoveAll", "RemoveWhere", "Clear", "Enqueue", "Dequeue", "Push", "Pop", "TryAdd", "TryRemove", "UnionWith", "ExceptWith", "IntersectWith", "Sort", "Reverse",
        };

        /// <summary>The read-only collections something fills, empties or writes an element of after they are made.</summary>
        private static HashSet<IFieldSymbol> FilledCollections(Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation)
        {
            var filled = new HashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    if (node is InvocationExpressionSyntax call && call.Expression is MemberAccessExpressionSyntax member && Filling.Contains(member.Name.Identifier.Text)
                        && model.GetSymbolInfo(member.Expression).Symbol is IFieldSymbol on) filled.Add(on);
                    else if (node is AssignmentExpressionSyntax assignment && assignment.Left is ElementAccessExpressionSyntax element
                        && model.GetSymbolInfo(element.Expression).Symbol is IFieldSymbol written) filled.Add(written);
                }
            }
            return filled;
        }

        private static HashSet<IFieldSymbol> _filled;

        /// <summary>Whether a static field keeps something that changes: one written after it is made, or a collection something fills.</summary>
        private static bool Changes(IFieldSymbol field) =>
            field.IsStatic && !field.IsConst && !field.IsImplicitlyDeclared && (!field.IsReadOnly || Collections.Contains(field.Type.Name) && _filled.Contains(field));

        /// <summary>
        /// The fields a method sets, empties or forgets, and those of what it calls: a method of its
        /// own class, or another class's that forgets, resets or lets go; each gone through once.
        /// </summary>
        private static void Resets(SyntaxNode body, SemanticModel model, INamedTypeSymbol type, HashSet<IFieldSymbol> reset, HashSet<SyntaxNode> seen)
        {
            if (body == null || !seen.Add(body)) return;
            foreach (var node in body.DescendantNodesAndSelf())
            {
                switch (node)
                {
                    case AssignmentExpressionSyntax assignment when model.GetSymbolInfo(assignment.Left).Symbol is IFieldSymbol set:
                        reset.Add(set);
                        break;
                    // A count bumped (Version++): what was kept under it is worked out again.
                    case PostfixUnaryExpressionSyntax bumped when model.GetSymbolInfo(bumped.Operand).Symbol is IFieldSymbol counted:
                        reset.Add(counted);
                        break;
                    case PrefixUnaryExpressionSyntax bumped when model.GetSymbolInfo(bumped.Operand).Symbol is IFieldSymbol counted:
                        reset.Add(counted);
                        break;
                    case InvocationExpressionSyntax call:
                        // A field's own method called on it (Clear, Forget, Reset): emptied.
                        if (call.Expression is MemberAccessExpressionSyntax member && model.GetSymbolInfo(member.Expression).Symbol is IFieldSymbol on) reset.Add(on);
                        // A method of the class's own, or another's forgetting: what it resets counts.
                        if (model.GetSymbolInfo(call).Symbol is IMethodSymbol method
                            && (SymbolEqualityComparer.Default.Equals(method.ContainingType, type) || method.Name.StartsWith("Forget", System.StringComparison.Ordinal)
                                || method.Name.StartsWith("Reset", System.StringComparison.Ordinal) || method.Name.StartsWith("LetGo", System.StringComparison.Ordinal)))
                        {
                            foreach (var declared in method.DeclaringSyntaxReferences)
                            {
                                var syntax = declared.GetSyntax();
                                Resets(syntax, model.Compilation.GetSemanticModel(syntax.SyntaxTree), type, reset, seen);
                            }
                        }
                        break;
                    case ArgumentSyntax argument when !argument.RefKindKeyword.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.None) && model.GetSymbolInfo(argument.Expression).Symbol is IFieldSymbol byRef:
                        reset.Add(byRef);
                        break;
                }
            }
        }

        [Fact]
        public void WhatACatalogClassKeepsOfAWorldIsResetWhenItIsLeft()
        {
            var compilation = ScrySource.Compilation;
            _filled = FilledCollections(compilation);
            var types = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath) && ScrySource.Relative(t).StartsWith("src/Catalog/", System.StringComparison.Ordinal)))
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var declaration in tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    if (model.GetDeclaredSymbol(declaration) is INamedTypeSymbol type) types.Add(type);
                }
            }

            // Every reset registered anywhere, and what each sets, empties or forgets.
            var reset = new HashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
            var seen = new HashSet<SyntaxNode>();
            foreach (var tree in compilation.SyntaxTrees.Where(t => !string.IsNullOrEmpty(t.FilePath)))
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol register) || register.Name != "Register" || register.ContainingType?.Name != "WorldCaches") continue;
                    if (call.ArgumentList.Arguments.Count < 2 || !(model.GetEnclosingSymbol(call.SpanStart)?.ContainingType is INamedTypeSymbol owner)) continue;
                    var forget = call.ArgumentList.Arguments[1].Expression;
                    if (forget is AnonymousFunctionExpressionSyntax lambda) Resets(lambda.Body, model, owner, reset, seen);
                    else if (model.GetSymbolInfo(forget).Symbol is IMethodSymbol method)
                    {
                        foreach (var declared in method.DeclaringSyntaxReferences)
                        {
                            var body = declared.GetSyntax();
                            Resets(body, compilation.GetSemanticModel(body.SyntaxTree), owner, reset, seen);
                        }
                    }
                }
            }

            var found = new List<string>();
            foreach (var type in types)
            {
                foreach (var field in type.GetMembers().OfType<IFieldSymbol>().Where(Changes))
                {
                    if (reset.Contains(field) || KeptAcrossWorlds.ContainsKey($"{type.Name}.{field.Name}")) continue;
                    var where = field.Locations.FirstOrDefault(l => l.IsInSource);
                    var at = where != null ? $"{ScrySource.Relative(where.SourceTree)}:{where.GetLineSpan().StartLinePosition.Line + 1}" : type.Name;
                    found.Add($"{at} {type.Name}.{field.Name} is not reset when the world is left");
                }
            }
            foreach (var kept in KeptAcrossWorlds.Keys)
            {
                var named = types.FirstOrDefault(t => kept.StartsWith(t.Name + ".", System.StringComparison.Ordinal) && t.GetMembers(kept.Substring(t.Name.Length + 1)).OfType<IFieldSymbol>().Any(Changes));
                if (named == null) found.Add($"{kept} is listed as kept across worlds but is no such field: take it off the list");
            }
            Violations.None("reset what is kept of a world", found);
        }
    }
}
