using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// Scry's grounds for footsteps (<c>StepGround</c>) are the game's (<c>FootStep.GroundMaterial</c>)
    /// by value, so the model can choose a ground and the previews cast it: every one of the
    /// game's is there under its name with its value, and none besides.
    /// </summary>
    public class StepGroundStandardTests
    {
        [Fact]
        public void ScrysFootstepGroundsAreTheGamesByNameAndValue()
        {
            var compilation = ScrySource.Compilation;
            var game = compilation.GetTypeByMetadataName("FootStep+GroundMaterial");
            var ours = compilation.GetTypeByMetadataName("Scry.StepGround");
            Assert.NotNull(game);
            Assert.NotNull(ours);
            var theirs = game.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue && f.Name != "Everything").ToDictionary(f => f.Name, f => System.Convert.ToInt64(f.ConstantValue));
            var mine = ours.GetMembers().OfType<IFieldSymbol>().Where(f => f.HasConstantValue).ToDictionary(f => f.Name, f => System.Convert.ToInt64(f.ConstantValue));
            Assert.Equal(theirs.OrderBy(p => p.Key), mine.OrderBy(p => p.Key));
        }
    }
}
