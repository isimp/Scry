using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ProjectileDamageTests
    {
        // A projectile an attack fires takes the attack's damage (Projectile.Setup); one spawned
        // any other way, as Yagluth's meteors fall, deals its own.

        [Fact]
        public void AProjectileSpawnedWithoutAnAttackGivesTheEffectsOfItsOwnDamage()
        {
            var damage = new ProjectileDamage();
            damage.Own("projectile_meteor", new[] { ("fire", 120f), ("frost", 0f) });
            damage.Own("radiation", new[] { ("lightning", 30f), ("poison", 30f) });
            var given = damage.Gives().ToList();
            Assert.Contains(("projectile_meteor", "Burning", CombatWords.OfDamage("fire")), given);
            Assert.Contains(("radiation", "Lightning", CombatWords.OfDamage("lightning")), given);
            Assert.Contains(("radiation", "Poison", CombatWords.OfDamage("poison")), given);
            Assert.Equal(3, given.Count);
        }

        [Fact]
        public void AProjectileAnAttackFiresGivesNothingOfItsOwnDamage()
        {
            // Ahri's orb is the staff's shot: its hit is the staff's, which the staff's page tells.
            var damage = new ProjectileDamage();
            damage.Own("staff_OrbofAhri_projectile", new[] { ("fire", 50f), ("frost", 50f) });
            damage.Fired("staff_OrbofAhri_projectile");
            damage.Own("staff_OrbofAhri_projectile_return", new[] { ("fire", 50f) });
            Assert.Equal(new[] { ("staff_OrbofAhri_projectile_return", "Burning", CombatWords.OfDamage("fire")) }, damage.Gives());

            // Fired told before its damage is read, as the prefabs come in any order.
            var other = new ProjectileDamage();
            other.Fired("staff_clusterbombstaff_projectile");
            other.Own("staff_clusterbombstaff_projectile", new[] { ("fire", 60f) });
            Assert.Empty(other.Gives());

            other.Clear();
            other.Own("staff_clusterbombstaff_projectile", new[] { ("fire", 60f) });
            Assert.Single(other.Gives());
        }

        [Fact]
        public void AnAreaKeepingItsOwnSettingsGivesItsOwnDamageEvenFired()
        {
            // An area takes an attack's hit only where it uses the attack's settings (Aoe.Setup), as
            // the frost nova a Dverger's staff fires does not; one below the prefab is never given it.
            var damage = new ProjectileDamage();
            damage.Own("DvergerStaffNova_aoe", new[] { ("frost", 150f) }, keepsOwn: true);
            damage.Fired("DvergerStaffNova_aoe");
            Assert.Equal(new[] { ("DvergerStaffNova_aoe", "Frost", CombatWords.OfDamage("frost")) }, damage.Gives());
        }

        [Fact]
        public void AnAreaTakingTheAttacksSettingsAndFiredGivesNothingOfItsOwn()
        {
            var damage = new ProjectileDamage();
            damage.Own("ModStaff_aoe", new[] { ("fire", 40f) }, keepsOwn: false);
            Assert.Single(damage.Gives());
            damage.Fired("ModStaff_aoe");
            Assert.Empty(damage.Gives());

            // What keeps its own within the same prefab still gives it, as an area below the top one.
            damage.Own("ModStaff_aoe", new[] { ("lightning", 10f) }, keepsOwn: true);
            Assert.Equal(new[] { ("ModStaff_aoe", "Lightning", CombatWords.OfDamage("lightning")) }, damage.Gives());

            // An area keeping its own fire beside the top one taking the attack's: the fire stays, whichever is read first.
            foreach (var keptFirst in new[] { true, false })
            {
                var both = new ProjectileDamage();
                both.Fired("ModStaff_aoe");
                both.Own("ModStaff_aoe", new[] { ("fire", 40f) }, keepsOwn: keptFirst);
                both.Own("ModStaff_aoe", new[] { ("fire", 10f) }, keepsOwn: !keptFirst);
                Assert.Single(both.Gives());
            }
        }

        [Fact]
        public void APrefabsAreasOfOneDamageGiveItsEffectOnce()
        {
            // An upgrade's lightning: a rod and an area below it, both lightning.
            var damage = new ProjectileDamage();
            damage.Own("fx_UpgradeStation_Fail", new[] { ("lightning", 60f) }, keepsOwn: true);
            damage.Own("fx_UpgradeStation_Fail", new[] { ("lightning", 400f) }, keepsOwn: true);
            Assert.Single(damage.Gives());
        }
    }
}
