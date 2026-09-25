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

            Assert.Equal(Origin.Vanilla, provenance.OfEffect(new[] { "CoolMod_TrollStatue", "Troll" }));
        }

        [Fact]
        public void AnEffectOnlyAModsPrefabsUseIsTheMods()
        {
            var provenance = SceneStartedWith("Troll");

            Assert.Equal(Origin.Mod, provenance.OfEffect(new[] { "CoolMod_TrollStatue" }));
        }

        [Fact]
        public void AnInterfaceSoundIsTheGames()
        {
            var provenance = SceneStartedWith("Troll");

            Assert.Equal(Origin.Vanilla, provenance.OfEffect(new[] { Provenance.Interface }));
        }

        [Fact]
        public void AnEffectNobodyIsKnownToUseIsNotClaimed()
        {
            Assert.Equal(Origin.Unknown, SceneStartedWith("Troll").OfEffect(new string[0]));
        }
    }
}
