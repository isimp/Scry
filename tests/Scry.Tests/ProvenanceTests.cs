using Xunit;

namespace Scry.Tests
{
    public class ProvenanceTests
    {
        private static Provenance SceneStartedWith(params string[] names)
        {
            var provenance = new Provenance();
            provenance.RecordOriginal(names);
            return provenance;
        }

        [Fact]
        public void APrefabTheSceneStartedWithIsTheGames()
        {
            Assert.Equal(Origin.Vanilla, SceneStartedWith("Troll", "Bow").Of("Troll"));
        }

        [Fact]
        public void APrefabRegisteredAfterTheSceneStartedIsMarkedAsAddedByAMod()
        {
            Assert.Equal(Origin.Mod, SceneStartedWith("Troll", "Bow").Of("CoolMod_TrollStatue"));
        }

        [Fact]
        public void BeforeTheSceneIsSeenNothingIsClaimed()
        {
            Assert.Equal(Origin.Unknown, new Provenance().Of("Troll"));
        }

        [Fact]
        public void AnEffectAnyGamePrefabUsesIsTheGames()
        {
            var provenance = SceneStartedWith("Troll");

            Assert.Equal(Origin.Vanilla, Provenance.Combine(new[] { provenance.Of("CoolMod_TrollStatue"), provenance.Of("Troll") }));
        }

        [Fact]
        public void AnEffectOnlyAModsPrefabsUseIsTheMods()
        {
            var provenance = SceneStartedWith("Troll");

            Assert.Equal(Origin.Mod, Provenance.Combine(new[] { provenance.Of("CoolMod_TrollStatue") }));
        }

        [Fact]
        public void AnEffectAModUsesBesideSomethingOfUnknownOriginIsNotClaimed()
        {
            Assert.Equal(Origin.Unknown, Provenance.Combine(new[] { Origin.Mod, Origin.Unknown }));
        }

        [Fact]
        public void AnEffectNobodyIsKnownToUseIsNotClaimed()
        {
            Assert.Equal(Origin.Unknown, Provenance.Combine(new Origin[0]));
        }
    }
}
