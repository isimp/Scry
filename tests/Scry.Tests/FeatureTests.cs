using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FeatureTests
    {
        // What the notice of what is off names: Scry's features, each made once in one list and
        // named one way, whether the startup check finds a part of the game missing or a part of
        // Scry fails later because the game changed.

        [Fact]
        public void EveryFeatureHasANameOfItsOwn()
        {
            var names = Feature.All.Select(f => f.Name).ToList();
            Assert.True(names.Count > 150);
            Assert.Equal(names.Count, names.Distinct().Count());
        }

        [Fact]
        public void AFeaturesNameReadsInTheMiddleOfASentence()
        {
            foreach (var feature in Feature.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(feature.Name));
                Assert.True(!char.IsUpper(feature.Name[0]) || feature.Name.StartsWith("Scry", System.StringComparison.Ordinal), feature.Name);
                Assert.False(feature.Name.EndsWith(".", System.StringComparison.Ordinal), feature.Name);
                Assert.Equal(feature.Name.Trim(), feature.Name);
            }
        }

        [Fact]
        public void AFeatureOffForTwoReasonsIsNamedOnce()
        {
            var off = Feature.Off(new[] { Feature.DropsSeenInPlay, Feature.LocationMusic }, new[] { Feature.DropsSeenInPlay, Feature.Panel });
            Assert.Equal(new[] { Feature.DropsSeenInPlay, Feature.LocationMusic, Feature.Panel }, off);
            Assert.Empty(Feature.Off(new Feature[0], new Feature[0]));
        }

        [Fact]
        public void AFeatureOfAPartIsTheSameWhereverItIsMade()
        {
            Assert.Equal(Feature.Details("resistances"), Feature.Details("resistances"));
            Assert.Equal("the resistances details", Feature.Details("resistances").Name);
            Assert.NotEqual(Feature.Details("resistances"), Feature.Details("weak spots"));
            Assert.Equal("falling copies landing on terrain", Feature.FallingCopies("terrain").Name);
            Assert.Equal("effects keeping their LightFlicker", Feature.KeptScript("LightFlicker").Name);
            Assert.Equal("linking damage to Burning", Feature.DamageLink("Burning").Name);
            Assert.Single(Feature.Off(new[] { Feature.FallingCopies("terrain") }, new[] { Feature.FallingCopies("terrain") }));
        }

        [Fact]
        public void TheSameFeatureIsNotListedUnderTwoNames()
        {
            var names = Feature.All.Select(f => f.Name).ToList();
            Assert.DoesNotContain("what beds need, in the details", names);
            Assert.DoesNotContain("what ballistas shoot, in the details", names);
            Assert.DoesNotContain("the game's keys while the panel is open", names);
            Assert.Contains("what a bed needs, in the details", names);
            Assert.Contains("what a ballista shoots, in the details", names);
        }
    }
}
