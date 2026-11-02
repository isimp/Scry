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

        // Gjall's tentacles, a Wolf's body, the Ulv's and Neck's tails, a Seeker's jiggling bones,
        // Hildir's skirt and a ship's sail move by the cloth physics the game ships (MagicaCloth 2),
        // which lives on the prefab as scripts; stripped, they hang stiff in their resting shape.
        [Theory]
        [InlineData("MagicaCloth2.MagicaCloth")]
        [InlineData("MagicaCloth2.MagicaCapsuleCollider")]
        [InlineData("MagicaCloth2.MagicaSphereCollider")]
        [InlineData("MagicaCloth2.MagicaPlaneCollider")]
        public void ACopyKeepsTheClothPhysicsItsPrefabMovesWith(string script)
        {
            Assert.Equal(StripPolicy.Keep, StripPolicy.PassFor(Script(script)));
            Assert.Equal(StripPolicy.Keep, StripPolicy.PassFor(Script(script), falling: true));
        }

        // Cloth worn on the body (a cape, a dress, a Charred's hood) the game keeps and sets up as
        // it puts it on: bound to the wearer's bones and the shapes it rests against
        // (VisEquipment.SetupCloth). A copy skinned onto a body keeps it too, held from building
        // itself on the part's own bones, which no longer move, until the wearer sets it up.
        [Fact]
        public void ClothOnAPartSkinnedOntoABodyWaitsForItsWearer()
        {
            var cloth = Script("MagicaCloth2.MagicaCloth");
            Assert.True(StripPolicy.WaitsForWearer(cloth, skinned: true));
            // A hat hung on the head or a staff in hand builds its own cloth, as the game's do.
            Assert.False(StripPolicy.WaitsForWearer(cloth, skinned: false));
        }

        [Theory]
        // The shapes cloth rests against build nothing of their own.
        [InlineData("MagicaCloth2.MagicaCapsuleCollider")]
        [InlineData("MagicaCloth2.MagicaSphereCollider")]
        [InlineData("MagicaCloth2.MagicaPlaneCollider")]
        // A mod's script named as the cloth, in its own namespace or none.
        [InlineData("SomeMod.MagicaCloth")]
        [InlineData("MagicaCloth")]
        public void OnlyTheClothItselfWaitsForItsWearer(string script)
        {
            Assert.False(StripPolicy.WaitsForWearer(Script(script), skinned: true));
        }

        [Fact]
        public void TheEnginesOwnClothBuildsNothingToWaitFor()
        {
            Assert.False(StripPolicy.WaitsForWearer(Engine("Cloth"), skinned: true));
            Assert.False(StripPolicy.WaitsForWearer(new ComponentFacts("MagicaCloth2.MagicaCloth"), skinned: true));
        }

        [Theory]
        // A wind zone blows every cloth around it, the settings set the cloth of the whole world.
        [InlineData("MagicaCloth2.MagicaWindZone")]
        [InlineData("MagicaCloth2.MagicaSettings")]
        // A mod's script named as the cloth, in its own namespace.
        [InlineData("SomeMod.MagicaCloth")]
        [InlineData("MagicaCloth")]
        public void WhatActsOnTheWholeWorldsClothOrOnlySharesItsNameGoes(string script)
        {
            Assert.Equal(StripPolicy.ScriptsPass, StripPolicy.PassFor(Script(script)));
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

        [Theory]
        [InlineData("ItemDrop", 1f)]
        [InlineData("TreeLog", 1f)]
        [InlineData("TombStone", 1f)]
        [InlineData("Smoke", 1f)]
        [InlineData("Character", 2f)]
        [InlineData("Humanoid", 2f)]
        [InlineData("Ship", 2f)]
        [InlineData("Vagon", 2f)]
        public void AFallingCopyIsPushedOutOfWhatItOverlapsNoHarderThanTheGameLetsIt(string script, float limit)
        {
            Assert.Equal(limit, StripPolicy.PushApartLimit(new[] { "ZNetView", script }));
        }

        [Fact]
        public void AFallingCopyWhoseScriptsSetNoLimitKeepsTheEngines()
        {
            Assert.Null(StripPolicy.PushApartLimit(new[] { "ZNetView", "Destructible", "SomeMod.Turret" }));
            Assert.Null(StripPolicy.PushApartLimit(new string[0]));
        }

        [Fact]
        public void AFallingCopyWithTwoLimitingScriptsTakesTheGentler()
        {
            Assert.Equal(1f, StripPolicy.PushApartLimit(new[] { "Vagon", "ItemDrop" }));
        }
    }
}
