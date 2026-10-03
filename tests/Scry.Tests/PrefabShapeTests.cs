using Xunit;

namespace Scry.Tests
{
    public class PrefabShapeTests
    {
        // Debris is loose parts that fly apart under physics (planks, splinters, stones): a body
        // physics moves, and no ragdoll, creature or item, which are whole things of their own.

        [Fact]
        public void LoosePartsUnderPhysicsAreDebris()
        {
            Assert.True(new PrefabShape { FreeBody = true }.IsDebris);
            Assert.True(new PrefabShape { FreeBody = true, Skinned = true }.IsDebris);
        }

        [Fact]
        public void WithoutABodyPhysicsMovesNothingIsDebris()
        {
            Assert.False(new PrefabShape().IsDebris);
            Assert.False(new PrefabShape { Skinned = true }.IsDebris);
        }

        [Fact]
        public void ARagdollACreatureOrAnItemIsNoDebrisThoughPhysicsMovesIt()
        {
            Assert.False(new PrefabShape { FreeBody = true, Ragdoll = true }.IsDebris);
            Assert.False(new PrefabShape { FreeBody = true, Character = true }.IsDebris);
            Assert.False(new PrefabShape { FreeBody = true, Item = true }.IsDebris);
        }

        // An effect list's prefab is a whole model rather than an effect when it is a ragdoll, a
        // creature, an item, a skinned body or parts physics moves; a copy keeps none of its
        // physics, so effects leave such a thing out.

        [Fact]
        public void EachOfThoseMakesAWholeModel()
        {
            Assert.True(new PrefabShape { Ragdoll = true }.IsWholeModel);
            Assert.True(new PrefabShape { Character = true }.IsWholeModel);
            Assert.True(new PrefabShape { Item = true }.IsWholeModel);
            Assert.True(new PrefabShape { Skinned = true }.IsWholeModel);
            Assert.True(new PrefabShape { FreeBody = true }.IsWholeModel);
        }

        [Fact]
        public void AFlashOrAPuffIsNoWholeModel()
        {
            Assert.False(new PrefabShape().IsWholeModel);
        }
    }
}
