using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Scry.Tests
{
    /// <summary>What the standards tests share: the types they ask about and how they say what breaks a standard.</summary>
    internal static class Violations
    {
        /// <summary>Whether a type is a number (or a nullable one): what becomes text through <c>Numbers</c>.</summary>
        public static bool Numeric(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol named && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T) type = named.TypeArguments[0];
            switch (type?.SpecialType)
            {
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_Decimal:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Whether a node stands in one of these files, by its name: "Numbers.cs".</summary>
        public static bool In(SyntaxNode node, params string[] files) =>
            files.Contains(System.IO.Path.GetFileName(node.SyntaxTree.FilePath));

        /// <summary>
        /// Fails with every place that breaks the standard, one a line, sorted; the message shows
        /// the first 400, and with SCRY_VIOLATIONS naming a folder every one is written to a file
        /// there named for the standard.
        /// </summary>
        public static void None(string standard, IEnumerable<string> found)
        {
            var list = found.Distinct().OrderBy(s => s, System.StringComparer.Ordinal).ToList();
            var folder = System.Environment.GetEnvironmentVariable("SCRY_VIOLATIONS");
            if (list.Count > 0 && !string.IsNullOrEmpty(folder))
                System.IO.File.WriteAllLines(System.IO.Path.Combine(folder, System.Text.RegularExpressions.Regex.Replace(standard, @"\W+", "-") + ".txt"), list);
            Assert.True(list.Count == 0, $"{list.Count} place(s) {standard}:\n" + string.Join("\n", list.Take(400)));
        }
    }
}
