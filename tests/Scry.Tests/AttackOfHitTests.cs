using Xunit;

namespace Scry.Tests
{
    public class AttackOfHitTests
    {
        // A hit taken names the attack it came from by its own marks, as the game fills them from
        // the weapon (Attack.FireProjectileBurst, Projectile.Setup): whether it flew (a projectile
        // or an area, HitData.m_ranged), its damage types and its status effect. The weapon in the
        // creature's hand when the hit lands may be another by then, as a projectile flies up to
        // three seconds and the creature picks its weapon again once its attack is over.

        private static readonly (string Item, AttackMarks[] Attacks)[] Gjall =
        {
            ("gjall_attack_spit", new[] { new AttackMarks(new[] { "Blunt", "Poison" }, ranged: true, status: 0) }),
            ("gjall_attack_shake", new[] { new AttackMarks(new[] { "Blunt" }, ranged: false, status: 0) }),
            ("gjall_attack_taunt", new[] { new AttackMarks(new string[0], ranged: false, status: 0) }),
        };

        [Fact]
        public void AFlyingHitNamesTheWeaponThatFiresWhateverTheHandHoldsByThen()
        {
            var hit = new AttackMarks(new[] { "Blunt", "Poison" }, ranged: true, status: 0);
            Assert.Equal("gjall_attack_spit", AttackOfHit.Pick(Gjall, hit, held: "gjall_attack_taunt"));
        }

        [Fact]
        public void ASwingNamesTheWeaponThatSwings()
        {
            var hit = new AttackMarks(new[] { "Blunt" }, ranged: false, status: 0);
            Assert.Equal("gjall_attack_shake", AttackOfHit.Pick(Gjall, hit, held: "gjall_attack_spit"));
        }

        [Fact]
        public void TwoWeaponsAlikeAreToldByTheHand()
        {
            // A Dvergr's two crossbow shots fire the same bolt with the same damage type.
            (string, AttackMarks[])[] dvergr =
            {
                ("DvergerArbalest", new[] { new AttackMarks(new[] { "Pierce" }, ranged: true, status: 0) }),
                ("DvergerArbalest_shoot", new[] { new AttackMarks(new[] { "Pierce" }, ranged: true, status: 0) }),
            };
            var hit = new AttackMarks(new[] { "Pierce" }, ranged: true, status: 0);
            Assert.Equal("DvergerArbalest_shoot", AttackOfHit.Pick(dvergr, hit, held: "DvergerArbalest_shoot"));
            // The hand holding neither of them tells nothing rather than a guess.
            Assert.Null(AttackOfHit.Pick(dvergr, hit, held: "Dverger_melee"));
        }

        [Fact]
        public void AStatusEffectTheHitCarriesNamesTheWeaponGivingIt()
        {
            (string, AttackMarks[])[] mage =
            {
                ("DvergerStaffIce_icebolt", new[] { new AttackMarks(new[] { "Frost" }, ranged: true, status: 11) }),
                ("DvergerStaffNova", new[] { new AttackMarks(new[] { "Frost" }, ranged: true, status: 22) }),
            };
            Assert.Equal("DvergerStaffNova", AttackOfHit.Pick(mage, new AttackMarks(new[] { "Frost" }, ranged: true, status: 22), held: null));
            // A hit carrying no status effect, as one given only by chance, does not rule either out.
            Assert.Null(AttackOfHit.Pick(mage, new AttackMarks(new[] { "Frost" }, ranged: true, status: 0), held: null));
        }

        [Fact]
        public void AHitNoWeaponFitsFallsBackToTheHand()
        {
            // An area the attack leaves behind deals damage of its own, which may fit no weapon.
            var hit = new AttackMarks(new[] { "Fire" }, ranged: true, status: 0);
            Assert.Equal("gjall_attack_spit", AttackOfHit.Pick(Gjall, hit, held: "gjall_attack_spit"));
        }

        [Fact]
        public void AHitWithNoDamageTypesIsToldByWhetherItFlew()
        {
            // A projectile that spares a friend deals nothing, but still flew.
            var hit = new AttackMarks(new string[0], ranged: true, status: 0);
            Assert.Equal("gjall_attack_spit", AttackOfHit.Pick(Gjall, hit, held: "gjall_attack_shake"));
        }
    }
}
