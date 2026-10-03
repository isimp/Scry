using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Scry.Tests
{
    /// <summary>The standards tests read Scry's source with the compiler's types, which they can trust only while it compiles here as it does in the build.</summary>
    public class SourceCompilesTests
    {
        [Fact]
        public void ScryAndItsSelfTestCompileHereAsInTheBuild()
        {
            var errors = ScrySource.Compilations
                .SelectMany(c => c.GetDiagnostics())
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => $"{d.Location.GetLineSpan().Path}:{d.Location.GetLineSpan().StartLinePosition.Line + 1} {d.Id} {d.GetMessage()}")
                .Take(20)
                .ToList();
            Assert.True(errors.Count == 0, "Scry's source does not compile for the standards tests:\n" + string.Join("\n", errors));
        }
    }
}
