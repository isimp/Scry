using Xunit;

namespace Scry.Tests
{
    public class StripPolicyTests
    {
        private static ComponentFacts Engine(string name) => new ComponentFacts("UnityEngine." + name);
        private static ComponentFacts Script(string name) => new ComponentFacts(name, isScript: true);

        [Theory]
        [InlineData("BoxCollider")]
        [InlineData("MeshCollider")]
        [InlineData("CapsuleCollider")]
        [InlineData("Rigidbody")]
        [InlineData("CharacterController")]
        [InlineData("AI.NavMeshAgent")]
        public void APreviewCopyHasNothingSolid(string engineComponent)
        {
            Assert.NotEqual(StripPolicy.Keep, StripPolicy.PassFor(Engine(engineComponent)));
        }

        [Theory]
        [InlineData("ZNetView")]
        [InlineData("ZSyncTransform")]
        [InlineData("Character")]
        [InlineData("Humanoid")]
        [InlineData("MonsterAI")]
        [InlineData("CharacterAnimEvent")]
        [InlineData("Projectile")]
        [InlineData("Aoe")]
        [InlineData("ItemDrop")]
        [InlineData("Piece")]
        [InlineData("WearNTear")]
        [InlineData("Container")]
        [InlineData("Pickable")]
        [InlineData("SomeMod.Turret")]
        public void APreviewCopyCannotBeHitPickedUpFoughtOrSaved(string script)
        {
            Assert.Equal(StripPolicy.ScriptsPass, StripPolicy.PassFor(Script(script)));
        }

        [Theory]
        [InlineData("Transform")]
        [InlineData("MeshFilter")]
        [InlineData("MeshRenderer")]
        [InlineData("SkinnedMeshRenderer")]
        [InlineData("LODGroup")]
        [InlineData("Animator")]
        [InlineData("ParticleSystem")]
        [InlineData("ParticleSystemRenderer")]
        [InlineData("TrailRenderer")]
        [InlineData("LineRenderer")]
        [InlineData("Light")]
        [InlineData("AudioSource")]
        public void APreviewCopyKeepsWhatDrawsAnimatesLightsAndSounds(string engineComponent)
        {
            Assert.Equal(StripPolicy.Keep, StripPolicy.PassFor(Engine(engineComponent)));
        }

        [Theory]
        [InlineData("LightFlicker")]
        [InlineData("ZSFX")]
        [InlineData("TimedDestruction")]
        [InlineData("EffectFade")]
        public void AnEffectKeepsTheScriptsThatMakeItPlayAndEnd(string script)
        {
            Assert.Equal(StripPolicy.Keep, StripPolicy.PassFor(Script(script)));
        }

        [Fact]
        public void ALightStaysOnFarFromThePlayer()
        {
            // LightLod switches a light off by its distance from the player, and the preview
            // stage is far from where the player stands.
            Assert.Equal(StripPolicy.ScriptsPass, StripPolicy.PassFor(Script("LightLod")));
        }

        [Fact]
        public void ScriptsGoBeforeJointsAndJointsBeforeTheBodiesTheyHold()
        {
            var joint = new ComponentFacts("UnityEngine.HingeJoint", isJoint: true);

            Assert.True(StripPolicy.PassFor(Script("Character")) < StripPolicy.PassFor(joint));
            Assert.True(StripPolicy.PassFor(joint) < StripPolicy.PassFor(Engine("Rigidbody")));
        }

        [Fact]
        public void AnEngineComponentNotKnownToBeHarmlessGoes()
        {
            Assert.NotEqual(StripPolicy.Keep, StripPolicy.PassFor(Engine("WindZone")));
        }

        [Fact]
        public void AModsScriptWithTheSameNameAsAKeptOneInAnotherNamespaceGoes()
        {
            Assert.NotEqual(StripPolicy.Keep, StripPolicy.PassFor(Script("SomeMod.LightFlicker")));
        }

        // ----- A copy that falls: a ragdoll, or a piece breaking apart -----

        [Theory]
        [InlineData("Rigidbody")]
        [InlineData("BoxCollider")]
        [InlineData("SphereCollider")]
        [InlineData("CapsuleCollider")]
        [InlineData("MeshCollider")]
        public void AFallingCopyKeepsWhatMakesItFall(string engineComponent)
        {
            Assert.Equal(StripPolicy.Keep, StripPolicy.PassFor(Engine(engineComponent), falling: true));
        }

        [Fact]
        public void AFallingCopyKeepsTheJointsThatHoldARagdollTogether()
        {
            Assert.Equal(StripPolicy.Keep, StripPolicy.PassFor(new ComponentFacts("UnityEngine.CharacterJoint", isJoint: true), falling: true));
        }

        [Theory]
        [InlineData("CharacterController")]
        [InlineData("AI.NavMeshAgent")]
        [InlineData("WheelCollider")]
        public void AFallingCopyStillCannotWalkOrDrive(string engineComponent)
        {
            Assert.NotEqual(StripPolicy.Keep, StripPolicy.PassFor(Engine(engineComponent), falling: true));
        }

        [Theory]
        [InlineData("Ragdoll")]
        [InlineData("ZNetView")]
        [InlineData("Character")]
        [InlineData("ItemDrop")]
        public void AFallingCopyStillCannotBeFoughtLootedOrSaved(string script)
        {
            Assert.Equal(StripPolicy.ScriptsPass, StripPolicy.PassFor(Script(script), falling: true));
        }
    }
}
