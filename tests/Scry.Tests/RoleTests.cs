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
        [InlineData("m_thunderEffect", "Weather and ambience")]
        [InlineData("weather", "Weather and ambience")]
        [InlineData("ambience", "Weather and ambience")]
        [InlineData("m_projectileHitEffects.m_effect", "Hits and blocks")]
        [InlineData("m_fireworkItemList.m_fireworksEffects", "Building, crafting and using")]
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
        public void ArrowsAndBoltsGoWithBowsAndATurretsAmmoWithTraps()
        {
            // Ammo carries the projectile, and its own skill says nothing of the bow that fires it.
            Assert.Equal("Bows and crossbows", Groups.Projectile(new[] { new Shooter(Kind.Item, "Swords", false, "$ammo_arrows") }).Name);
            Assert.Equal("Bows and crossbows", Groups.Projectile(new[] { new Shooter(Kind.Item, "", false, "$ammo_bolts") }).Name);
            Assert.Equal("Traps and turrets", Groups.Projectile(new[] { new Shooter(Kind.Item, "", false, "$ammo_turretbolt") }).Name);
            Assert.Equal("Thrown", Groups.Projectile(new[] { new Shooter(Kind.Item, "", false, "$ammo_bombs") }).Name);
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
            Assert.Equal("Given by nothing found", Groups.StatusEffect(new Giver[0]).Name);
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

        [Fact]
        public void AProjectileAnotherSpawnsGoesWithIt()
        {
            // A cluster bomb's splinters fly with the staff that fires the bomb, a meteor's rocks with the meteor.
            Entry P(string name, string group, int order) => new Entry { Name = name, Kind = Kind.Projectile, Group = group, GroupOrder = order };
            var bomb = P("clusterbomb", "Staffs", 2);
            var splinter = P("splinter", "Other", 6);
            splinter.Links.Add(new Link { Group = "Spawned by", Target = "clusterbomb" });
            var shard = P("shard", "Other", 6);
            shard.Links.Add(new Link { Group = "Spawned by", Target = "splinter" });
            var catalog = new System.Collections.Generic.List<Entry> { shard, splinter, bomb };

            Groups.FollowSpawners(catalog, "Spawned by", "Other");

            Assert.Equal("Staffs", splinter.Group);
            Assert.Equal(2, splinter.GroupOrder);
            Assert.Equal("Staffs", shard.Group);
        }

        [Fact]
        public void AProjectileTwoOthersSpawnGoesWithTheOneThatHasAGroup()
        {
            Entry P(string name, string group, int order) => new Entry { Name = name, Kind = Kind.Projectile, Group = group, GroupOrder = order };
            var stray = P("stray", "Other", 6);
            var bomb = P("clusterbomb", "Staffs", 2);
            var splinter = P("splinter", "Other", 6);
            splinter.Links.Add(new Link { Group = "Spawned by", Target = "stray" });
            splinter.Links.Add(new Link { Group = "Spawned by", Target = "clusterbomb" });

            Groups.FollowSpawners(new System.Collections.Generic.List<Entry> { stray, bomb, splinter }, "Spawned by", "Other");

            Assert.Equal("Staffs", splinter.Group);
        }

        [Fact]
        public void AProjectileWithAGroupOfItsOwnKeepsIt()
        {
            Entry P(string name, string group, int order) => new Entry { Name = name, Kind = Kind.Projectile, Group = group, GroupOrder = order };
            var bomb = P("clusterbomb", "Staffs", 2);
            var arrow = P("arrow", "Bows and crossbows", 1);
            arrow.Links.Add(new Link { Group = "Spawned by", Target = "clusterbomb" });

            Groups.FollowSpawners(new System.Collections.Generic.List<Entry> { bomb, arrow }, "Spawned by", "Other");

            Assert.Equal("Bows and crossbows", arrow.Group);
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

        [Theory]
        [InlineData("equip", "when worn")]
        [InlineData("consume", "when used")]
        [InlineData("set", "set bonus")]
        [InlineData("attack", "on hit")]
        [InlineData("fire damage", "fire damage")]
        [InlineData("guardian power", "guardian power")]
        [InlineData("", "")]
        public void HowAnItemGivesAnEffectIsToldInTheWordsOfTheItemsOwnFacts(string how, string shown)
        {
            // An item says "When worn: Troll armour set"; the effect says it back in the same words.
            Assert.Equal(shown, Groups.GiverWords(how));
        }
    }
}
