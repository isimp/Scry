using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Every part of Scry that can fail names the feature it belongs to, from the one list in
    /// <c>Feature</c>, so a game change that breaks it is named on the notice of what is off as
    /// the startup check names it. The compiler holds the guards to that, as each takes a feature;
    /// a patch, applied by its class, names its feature in the plugin's map, which this test holds.
    /// </summary>
    public class FeatureStandardTests
    {
        [Fact]
        public void EveryPatchNamesTheFeatureItServes()
        {
            var patches = new List<(string Name, string Where)>();
            var mapped = new HashSet<string>();
            foreach (var (type, _) in ScrySource.All<ClassDeclarationSyntax>())
            {
                if (type.AttributeLists.SelectMany(l => l.Attributes).Any(a => a.Name.ToString() == "HarmonyPatch"))
                    patches.Add((type.Identifier.Text, ScrySource.Where(type)));
            }
            foreach (var (field, _) in ScrySource.All<VariableDeclaratorSyntax>())
            {
                if (field.Identifier.Text != "PatchFeatures" || !ScrySource.Relative(field.SyntaxTree).EndsWith("src/Plugin.cs", System.StringComparison.Ordinal)) continue;
                foreach (var named in field.DescendantNodes().OfType<TypeOfExpressionSyntax>()) mapped.Add(named.Type.ToString());
            }
            Assert.NotEmpty(patches);
            Violations.None("apply a patch without naming the feature it serves", patches.Where(p => !mapped.Contains(p.Name)).Select(p => $"{p.Where} {p.Name}"));
        }
    }
}
