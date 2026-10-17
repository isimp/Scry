using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// The first time a session tells a place's page, the game's runtime compiles the code the
    /// page runs, in the frame that picked the place. Generic code is compiled once for every
    /// class together, but anew for each value type it is given: a LINQ call over a tuple brings
    /// its own iterators, sorters and comparers to compile, a key and value pair or a nullable the
    /// same. So the code that tells a place's page and orders its lists calls no generic method
    /// with such a value type: what it orders or counts are classes, numbers or names. Numbers,
    /// true or false and enums are compiled once for the whole game, and are left.
    /// </summary>
    public class FirstUseStandardTests
    {
        /// <summary>The files that tell a location's, a dungeon's or a room's page and order its lists.</summary>
        private static readonly string[] PlacePage =
        {
            "Facts.Places.cs", "Facts.Order.cs", "ContentOrder.cs", "PlaceContents.cs", "DungeonWords.cs",
        };

        [Fact]
        public void APlacesPageCompilesLittleTheFirstTimeItIsTold()
        {
            var found = new List<string>();
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!Violations.In(call, PlacePage)) continue;
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method) || !method.IsGenericMethod) continue;
                var made = method.TypeArguments.FirstOrDefault(MadeForTheMoment);
                if (made != null) found.Add($"{ScrySource.Where(call)} {method.Name}<{made.ToDisplayString()}>");
            }
            Violations.None("give a generic method on a place's page a value type compiled for it alone", found);
        }

        /// <summary>A value type the runtime compiles generic code for anew: anything but a number, true or false, a character or an enum.</summary>
        private static bool MadeForTheMoment(ITypeSymbol type)
        {
            if (type == null || !type.IsValueType || type.TypeKind == TypeKind.Enum || type.TypeKind == TypeKind.TypeParameter) return false;
            return type.SpecialType != SpecialType.System_Boolean && type.SpecialType != SpecialType.System_Char && !Violations.Numeric(type) || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        }
    }
}
