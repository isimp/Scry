using Xunit;

namespace Scry.Tests
{
    public class RoleTests
    {
        // ----- Effects and sounds by what they are for -----

        [Theory]
        [InlineData("m_triggerEffect", "Attacks and swings")]
        [InlineData("m_trailStartEffect", "Attacks and swings")]
        [InlineData("m_shootReleaseEffect", "Attacks and swings")]
        [InlineData("m_hitEffects", "Hits and blocks")]
        [InlineData("m_blockEffect", "Hits and blocks")]
        [InlineData("m_critHitEffects", "Hits and blocks")]
        [InlineData("m_deathEffects", "Deaths and destruction")]
        [InlineData("m_destroyedEffect", "Deaths and destruction")]
        [InlineData("m_unSummonEffect", "Deaths and destruction")]
        [InlineData("m_spawnEffects", "Spawning and summoning")]
        [InlineData("m_hatchEffect", "Spawning and summoning")]
        [InlineData("m_alertedEffects", "Creature calls")]
        [InlineData("m_idleSound", "Creature calls")]
        [InlineData("m_randomGreetFX", "Creature calls")]
        [InlineData("m_jumpEffects", "Footsteps and movement")]
        [InlineData("m_waterEffects", "Footsteps and movement")]
        [InlineData("m_placeEffect", "Building, crafting and using")]
        [InlineData("m_fuelAddedEffects", "Building, crafting and using")]
        [InlineData("m_eatEffect", "Building, crafting and using")]
        [InlineData("m_thunderEffect", "Weather")]
        [InlineData("m_tarEffects", "Footsteps and movement")]
        [InlineData("m_setTargetEffect", "Other")]
        [InlineData("m_lostTargetEffect", "Other")]
        [InlineData("m_effects", "Other")]
        public void AnEffectListSaysWhatItIsForByItsName(string field, string purpose)
        {
            Assert.Equal(purpose, Groups.Purpose(new[] { field }, false, false).Name);
        }

        [Fact]
        public void AStatusEffectsStartAndStopAreStatusEffectsNotAttacks()
        {
            Assert.Equal("Status effects", Groups.Purpose(new[] { "se:m_startEffects" }, false, false).Name);
        }

        [Fact]
        public void TheInterfacesSoundsAreTheInterface()
        {
            Assert.Equal("Interface", Groups.Purpose(new[] { "ui:m_buttonEffects" }, false, false).Name);
        }

        [Fact]
        public void FootstepsAndAnimationEventsCountTooWhereNoListSaysMore()
        {
            Assert.Equal("Footsteps and movement", Groups.Purpose(new string[0], true, false).Name);
            Assert.Equal("Animation sounds", Groups.Purpose(new string[0], false, true).Name);
            Assert.Equal("Hits and blocks", Groups.Purpose(new[] { "m_hitEffects" }, true, true).Name);
        }

        [Fact]
        public void WhatIsUsedForSeveralThingsGoesUnderTheFirstOfThem()
        {
            Assert.Equal("Attacks and swings", Groups.Purpose(new[] { "m_hitEffects", "m_triggerEffect" }, false, false).Name);
            Assert.Equal("Hits and blocks", Groups.Purpose(new[] { "m_deathEffects", "m_hitEffects" }, false, false).Name);
            Assert.Equal("Attacks and swings", Groups.Purpose(new[] { "m_triggerEffect", "m_hitEffects" }, false, false).Name);
            Assert.Equal("Hits and blocks", Groups.Purpose(new[] { "m_hitEffects", "m_deathEffects" }, false, false).Name);
        }

        [Fact]
        public void WhatNothingWasFoundToPlayComesLast()
        {
            var none = Groups.Purpose(new string[0], false, false);
            Assert.Equal("Played by nothing found", none.Name);
            Assert.True(Groups.Purpose(new[] { "m_effects" }, false, false).Order < none.Order);
        }

        // ----- Projectiles by who fires them -----

        [Fact]
        public void AProjectileGoesByWhoFiresIt()
        {
            Assert.Equal("Creatures", Groups.Projectile(new[] { new Shooter(Kind.Item, "Bows", true) }).Name);
            Assert.Equal("Creatures", Groups.Projectile(new[] { new Shooter(Kind.Creature, "", false) }).Name);
            Assert.Equal("Bows and crossbows", Groups.Projectile(new[] { new Shooter(Kind.Item, "Crossbows", false) }).Name);
            Assert.Equal("Staffs", Groups.Projectile(new[] { new Shooter(Kind.Item, "ElementalMagic", false) }).Name);
            Assert.Equal("Staffs", Groups.Projectile(new[] { new Shooter(Kind.Item, "BloodMagic", false) }).Name);
            Assert.Equal("Thrown", Groups.Projectile(new[] { new Shooter(Kind.Item, "Spears", false) }).Name);
            Assert.Equal("Traps and turrets", Groups.Projectile(new[] { new Shooter(Kind.Piece, "", false) }).Name);
            Assert.Equal("Other", Groups.Projectile(new Shooter[0]).Name);
        }

        [Fact]
        public void AProjectileBothPlayersAndCreaturesFireGoesWithTheWeapon()
        {
            var both = new[] { new Shooter(Kind.Item, "Bows", true), new Shooter(Kind.Item, "Bows", false) };
            Assert.Equal("Bows and crossbows", Groups.Projectile(both).Name);
            var reversed = new[] { new Shooter(Kind.Item, "Bows", false), new Shooter(Kind.Item, "Bows", true) };
            Assert.Equal("Bows and crossbows", Groups.Projectile(reversed).Name);
        }

        // ----- Status effects by where they come from -----

        [Fact]
        public void AStatusEffectGoesByWhatGivesIt()
        {
            Assert.Equal("Guardian powers", Groups.StatusEffect(new[] { new Giver(Kind.Piece, "guardian power") }).Name);
            Assert.Equal("Food and meads", Groups.StatusEffect(new[] { new Giver(Kind.Item, "consume") }).Name);
            Assert.Equal("Food and meads", Groups.StatusEffect(new[] { new Giver(Kind.Piece, "consume") }).Name);
            Assert.Equal("Set bonuses", Groups.StatusEffect(new[] { new Giver(Kind.Item, "set") }).Name);
            Assert.Equal("Worn equipment", Groups.StatusEffect(new[] { new Giver(Kind.Item, "equip") }).Name);
            Assert.Equal("From damage", Groups.StatusEffect(new[] { new Giver(Kind.Item, "fire damage") }).Name);
            Assert.Equal("From attacks and creatures", Groups.StatusEffect(new[] { new Giver(Kind.Creature, "") }).Name);
            Assert.Equal("From attacks and creatures", Groups.StatusEffect(new[] { new Giver(Kind.Item, "attack") }).Name);
            Assert.Equal("From pieces", Groups.StatusEffect(new[] { new Giver(Kind.Piece, "") }).Name);
            Assert.Equal("Other", Groups.StatusEffect(new[] { new Giver(Kind.Other, "") }).Name);
            Assert.Equal("Given by the game itself", Groups.StatusEffect(new Giver[0]).Name);
        }

        [Fact]
        public void AStatusEffectGivenSeveralWaysGoesUnderTheFirst()
        {
            var eatenAndBurnt = new[] { new Giver(Kind.Item, "fire damage"), new Giver(Kind.Item, "consume") };
            Assert.Equal("Food and meads", Groups.StatusEffect(eatenAndBurnt).Name);
            Assert.Equal("Food and meads", Groups.StatusEffect(new[] { eatenAndBurnt[1], eatenAndBurnt[0] }).Name);
        }

        [Fact]
        public void WhatTheGameGivesItselfComesLast()
        {
            Assert.True(Groups.StatusEffect(new[] { new Giver(Kind.Other, "") }).Order < Groups.StatusEffect(new Giver[0]).Order);
        }

        // ----- Other by role -----

        [Fact]
        public void OtherThingsGoByWhatTheyAreThere()
        {
            Assert.Equal("Spawners and altars", Groups.Role(new PrefabTraits { HasSpawner = true, HasRenderer = true }).Name);
            Assert.Equal("Spawners and altars", Groups.Role(new PrefabTraits { IsAltar = true, HasRenderer = true }).Name);
            Assert.Equal("Remains", Groups.Role(new PrefabTraits { HasRagdoll = true, HasRenderer = true }).Name);
            Assert.Equal("Chests and containers", Groups.Role(new PrefabTraits { HasContainer = true, IsUsable = true, HasRenderer = true }).Name);
            Assert.Equal("Things you can use", Groups.Role(new PrefabTraits { IsUsable = true, HasRenderer = true, HasSolidCollider = true }).Name);
            Assert.Equal("Scenery", Groups.Role(new PrefabTraits { HasRenderer = true, HasSolidCollider = true }).Name);
            Assert.Equal("Decoration", Groups.Role(new PrefabTraits { HasRenderer = true }).Name);
            Assert.Equal("Nothing to show", Groups.Role(new PrefabTraits()).Name);
        }

        [Fact]
        public void RolesComeSpawnersFirstAndNothingToShowLast()
        {
            var spawner = Groups.Role(new PrefabTraits { HasSpawner = true, HasRenderer = true });
            var scenery = Groups.Role(new PrefabTraits { HasRenderer = true, HasSolidCollider = true });
            var nothing = Groups.Role(new PrefabTraits());
            Assert.True(spawner.Order < scenery.Order);
            Assert.True(scenery.Order < nothing.Order);
        }
    }
}
