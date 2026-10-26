using Xunit;

namespace Scry.Tests
{
    public class DeathAttackTests
    {
        // A creature whose death hurts what stands near: Character.OnDeath makes its death effects
        // (m_deathEffects), and an Aoe among them hits all around with its own damage, unrolled and
        // the same at any star, since nothing sets it up with a level or an owner (Aoe.Setup is
        // never called for it). The Writhan's writhan_explosion, the lava blob's, the aspects'.

        private static readonly (string, float)[] WrithanBlast = { ("blunt", 60f), ("chop", 50f), ("pickaxe", 50f), ("poison", 40f) };

        private static DeathPart Part(string prefab, (string, float)[] damage = null, float nonPlayer = 0f, string effect = null, float radius = 6f, bool creature = false) =>
            new DeathPart { Prefab = prefab, Damage = damage ?? new (string, float)[0], NonPlayer = nonPlayer, Effect = effect, Radius = radius, IsCreature = creature };

        [Fact]
        public void ADeathWhoseEffectsHurtAroundItIsOneOfItsAttacks()
        {
            var picked = DeathAttack.Pick(new[] { Part("sfx_writhan_death"), Part("writhan_explosion", WrithanBlast), Part("Writhan_ragdoll") });
            Assert.Equal("writhan_explosion", picked?.Prefab);
        }

        [Fact]
        public void ADeathThatOnlyPutsAnEffectOnIsAnAttackToo()
        {
            Assert.Equal("cloud", DeathAttack.Pick(new[] { Part("cloud", effect: "Poison") })?.Prefab);
        }

        [Fact]
        public void ADeathThatHurtsOnlyCreaturesIsAnAttack()
        {
            // The aspects' explosion: 1,000 to everything but players, 130 m around.
            Assert.Equal("aspect_aoe_explosion", DeathAttack.Pick(new[] { Part("aspect_aoe_explosion", nonPlayer: 1000f, radius: 130f) })?.Prefab);
        }

        [Fact]
        public void ADeathThatOnlyLooksAndSoundsIsNoAttack()
        {
            Assert.Null(DeathAttack.Pick(new[] { Part("vfx_death"), Part("sfx_death"), Part("ragdoll") }));
            Assert.Null(DeathAttack.Pick(new DeathPart[0]));
        }

        [Fact]
        public void ADeathThatBringsTheNextPhaseOfABossIsNoAttack()
        {
            // The Frozen King's death makes its next phase, a creature whose own parts hurt as it
            // runs (RunHitDamager): that is the next creature's, not a blast of this one's death.
            Assert.Null(DeathAttack.Pick(new[] { Part("FrozenKing_p2", new[] { ("chop", 300f), ("pickaxe", 300f) }, radius: 4f, creature: true) }));
        }

        [Fact]
        public void TheDeathAttackIsALineOfTheAttacksTableWithItsDamageAsItLands()
        {
            // Unrolled, so no 87–100% range as a swing has; all around it as it dies; no AI picks it, so no interval.
            var cells = CombatWords.DeathAttackCells("Writhan explosion", WrithanBlast, 0f, 6f);
            Assert.Equal(new[] { "Writhan explosion", "60 blunt, 50 chop, 50 pickaxe, 40 poison", "all around, as it dies", "6 m", "–" }, cells);
        }

        [Fact]
        public void DamageOnlyToCreaturesSaysSo()
        {
            var cells = CombatWords.DeathAttackCells("Aspect explosion", new (string, float)[0], 1000f, 130f);
            Assert.Equal("1,000 to creatures, none to players", cells[1]);
            Assert.Equal("130 m", cells[3]);
        }

        [Fact]
        public void TheDeathAttackLinePlaysTheDeathAnimation()
        {
            // Its link plays an attack, by a name no item has, so it is told apart from every carried item.
            Assert.True(EntryKeys.PlaysAttack(EntryKeys.PlayAttack(EntryKeys.DeathAttack), out var item, out var second, out var step));
            Assert.Equal(EntryKeys.DeathAttack, item);
            Assert.False(second);
            Assert.Equal(0, step);
            Assert.True(EntryKeys.IsDeathAttack(item));
            Assert.False(EntryKeys.IsDeathAttack("writhan_bite"));
        }

        [Fact]
        public void TheDeathAnimationOfACreatureThatHurtsAsItDiesIsListedAsAnAttack()
        {
            var tag = ClipWords.DeathAttack("writhan explosion");
            Assert.True(ClipWords.IsAttack(tag));
            Assert.Contains("dies", tag);
        }

        // Under the strict rules the death attack is found out as an attack is: seen up close.

        private const long Me = 101;

        private static CompendiumBook Told(float away, System.Func<string, bool> hurtsAsItDies, long[] doers = null)
        {
            var book = new CompendiumBook();
            var death = new WorldEvent(Me, 1, Happened.Died, "Writhan", away, 0f, 0f, new[] { ("WrithanRoots", 1) }, 1, doers);
            EventRule.Tell(death, Me, 0f, 0f, 0f, new EventLedger(), book, null, deathAttack: hurtsAsItDies);
            return book;
        }

        [Fact]
        public void SeeingItDieUpCloseFindsOutWhatItsDeathDoes()
        {
            Assert.True(Told(5f, c => c == "Writhan").Proved("Writhan", EntryKeys.Attack(EntryKeys.DeathAttack)));
        }

        [Fact]
        public void ADeathSeenFromFurtherThanAnAttackCanBeTellsNothingOfIt()
        {
            // Within 30 m it counts as seen, but an attack is found out only within the seen reach.
            var book = Told(25f, c => c == "Writhan");
            Assert.Equal(1, book.Tallied(Tally.Fell).Kills("Writhan"));
            Assert.False(book.Proved("Writhan", EntryKeys.Attack(EntryKeys.DeathAttack)));
        }

        [Fact]
        public void TheCharactersOwnKillFromAfarTellsNothingOfItEither()
        {
            Assert.False(Told(25f, c => c == "Writhan", new[] { Me }).Proved("Writhan", EntryKeys.Attack(EntryKeys.DeathAttack)));
        }

        [Fact]
        public void ItsDeathIsNothingItWasSeenCarrying()
        {
            var book = Told(5f, c => c == "Writhan");
            book.FoundOut("Writhan", EntryKeys.Attack("writhan_bite"));
            Assert.Equal(new[] { "writhan_bite" }, book.AttackedWith("Writhan"));
        }

        [Fact]
        public void ACreatureWhoseDeathHurtsNothingGetsNoDeathAttackFoundOut()
        {
            Assert.False(Told(5f, c => false).Proved("Writhan", EntryKeys.Attack(EntryKeys.DeathAttack)));
            Assert.False(Told(5f, null).Proved("Writhan", EntryKeys.Attack(EntryKeys.DeathAttack)));
        }
    }
}
