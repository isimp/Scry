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
    }
}
