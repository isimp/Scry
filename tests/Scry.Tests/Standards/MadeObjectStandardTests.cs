using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A texture, render texture or mesh Scry makes is noted with <c>Kept</c> as it is made, so the
    /// resource monitor counts what Scry holds without searching all of Unity's objects; one made
    /// only to work with is destroyed in the method that made it. Either way none is made and
    /// forgotten, which Unity would keep until the game unloads it.
    /// </summary>
    public class MadeObjectStandardTests
    {
        private static readonly HashSet<string> Counted = new HashSet<string> { "Texture2D", "RenderTexture", "Mesh", "Cubemap", "Texture3D", "Texture2DArray" };

        [Fact]
        public void EveryTextureAndMeshScryMakesIsKeptOrDestroyed()
        {
            // Methods whose result is handed to Kept.Add where they are called (a disc made and kept).
            var keptMakers = new HashSet<string>();
            foreach (var (call, model) in ScrySource.All<InvocationExpressionSyntax>())
            {
                if (!call.Expression.ToString().EndsWith("Kept.Add", System.StringComparison.Ordinal)) continue;
                foreach (var made in call.ArgumentList.DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    if (model.GetSymbolInfo(made).Symbol is IMethodSymbol maker) keptMakers.Add(maker.ContainingType?.Name + "." + maker.Name);
                }
            }

            var found = new List<string>();
            foreach (var (creation, model) in ScrySource.All<ObjectCreationExpressionSyntax>())
            {
                var type = model.GetTypeInfo(creation).Type;
                if (type == null || !Counted.Contains(type.Name) || type.ContainingNamespace?.ToDisplayString() != "UnityEngine") continue;
                var method = creation.Ancestors().FirstOrDefault(a => a is MethodDeclarationSyntax || a is LocalFunctionStatementSyntax || a is PropertyDeclarationSyntax);
                if (method == null) continue;
                var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(i => i.Expression.ToString()).ToList();
                var keeps = calls.Any(c => c.EndsWith("Kept.Add", System.StringComparison.Ordinal));
                var destroys = calls.Any(c => c.EndsWith("Destroy", System.StringComparison.Ordinal) || c.EndsWith("DestroyImmediate", System.StringComparison.Ordinal));
                var kept = model.GetDeclaredSymbol(method) is IMethodSymbol self && keptMakers.Contains(self.ContainingType?.Name + "." + self.Name);
                if (!keeps && !destroys && !kept) found.Add($"{ScrySource.Where(creation)} a {type.Name} in {ScrySource.TopType(creation)}.{ScrySource.Member(creation)}");
            }
            Violations.None("make a texture or mesh neither kept nor destroyed", found);
        }
    }
}
