using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A type's fields are looked through and read one way, by <c>TypeFields</c>: found once per
    /// type and kept, read without making anything per read, and a field that cannot be read (a
    /// mod's whose type will not load) left out and counted rather than stopping the reading.
    /// The startup check, which looks for the game's members themselves, and the table of .NET's
    /// own instructions the shapes are read with are the two other places that look at fields.
    /// </summary>
    public class FieldStandardTests
    {
        [Fact]
        public void FieldsAreLookedThroughAndReadOnlyByTypeFields()
        {
            var found = new List<string>();
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!(model.GetSymbolInfo(call).Symbol is IMethodSymbol method)) continue;
                var owner = method.ContainingType?.ToDisplayString();
                var fields = owner == "System.Type" && (method.Name == "GetField" || method.Name == "GetFields")
                             || owner == "System.Reflection.FieldInfo" && (method.Name == "GetValue" || method.Name == "SetValue");
                if (!fields || Violations.In(call, "TypeFields.cs", "Compatibility.cs", "IlShape.cs")) continue;
                found.Add($"{ScrySource.Where(call)} {method.Name} in {ScrySource.TopType(call)}.{ScrySource.Member(call)}");
            }
            Violations.None("look through or read fields outside TypeFields", found);
        }
    }
}
