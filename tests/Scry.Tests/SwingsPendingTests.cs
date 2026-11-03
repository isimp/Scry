using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class SwingsPendingTests
    {
        // On a friend's game a creature's attack trigger can arrive before the weapon it equipped
        // for it, as the game sends the trigger at once and changed data every 0.05 s; a re-pick
        // never falls inside an attack (MonsterAI only re-picks while not attacking). So a trigger
        // two weapons share waits: the hand a short while later tells it, else what the attack
        // put into the world beside its caster, else nothing.

        private static readonly string[] Spells = { "DvergerStaffSupport_buff", "DvergerMistile" };

        private static readonly (string Item, string Made, float Near)[] Makes =
        {
            ("DvergerStaffSupport_buff", "DvergerStaffSupport_aoe", 3f),
            ("DvergerMistile", "Mistile", 4f),
        };

        private static readonly Vec3 Caster = new Vec3(10f, 0f, 10f);

        [Fact]
        public void TheWeaponInHandOnceTheDataCaughtUpTellsTheAttack()
        {
            var pending = new SwingsPending();
            pending.Begin(1, "DvergerMage", Spells, Makes, Caster, away: 12f, now: 100f);

            // Still the old weapon in hand, and the data not caught up yet: nothing told.
            Assert.Empty(pending.Tick(_ => "DvergerStaffSupport_buff", 100.2f));
            // The swap arrived: the weapon now in hand tells it.
            var told = pending.Tick(_ => "DvergerMistile", 100f + SwingsPending.Settle);
            Assert.Equal(new[] { ("DvergerMage", "DvergerMistile", 12f) }, told.ToArray());
            Assert.Equal(0, pending.Count);
        }

        [Fact]
        public void WhatTheAttackPutBesideItsCasterTellsItBeforeTheHand()
        {
            var pending = new SwingsPending();
            pending.Begin(1, "DvergerMage", Spells, Makes, Caster, away: 12f, now: 100f);

            // A Mistile woke 2 m from the caster: the summon, whatever the hand says later.
            Assert.Equal(("DvergerMage", "DvergerMistile", 12f), pending.Woke("Mistile", new Vec3(12f, 3f, 10f), 100.6f));
            Assert.Empty(pending.Tick(_ => "DvergerStaffSupport_buff", 101f));
        }

        [Fact]
        public void SomethingWakingFarFromTheCasterOrOfNoCandidateTellsNothing()
        {
            var pending = new SwingsPending();
            pending.Begin(1, "DvergerMage", Spells, Makes, Caster, away: 12f, now: 100f);

            Assert.Null(pending.Woke("Mistile", new Vec3(30f, 0f, 10f), 100.5f));
            Assert.Null(pending.Woke("Greydwarf", new Vec3(10f, 0f, 10f), 100.5f));
            Assert.Equal(1, pending.Count);
        }

        [Fact]
        public void AHandHoldingNoneOfThemWaitsForWhatTheAttackMakes()
        {
            var pending = new SwingsPending();
            pending.Begin(1, "DvergerMage", Spells, Makes, Caster, away: 12f, now: 100f);

            Assert.Empty(pending.Tick(_ => null, 100f + SwingsPending.Settle));
            Assert.Equal(1, pending.Count);
            Assert.Equal(("DvergerMage", "DvergerStaffSupport_buff", 12f), pending.Woke("DvergerStaffSupport_aoe", new Vec3(11f, 1f, 10f), 101f));
        }

        [Fact]
        public void NothingToldWithinTheWindowIsLetGo()
        {
            var pending = new SwingsPending();
            pending.Begin(1, "DvergerMage", Spells, Makes, Caster, away: 12f, now: 100f);

            Assert.Empty(pending.Tick(_ => null, 100.5f));
            Assert.Empty(pending.Tick(_ => null, 101.9f));
            Assert.Equal(1, pending.Count);
            // Two seconds on, past a summon's spacing and a hand read late, it is let go.
            Assert.Empty(pending.Tick(_ => null, 102.1f));
            Assert.Equal(0, pending.Count);
            Assert.Null(pending.Woke("Mistile", new Vec3(11f, 0f, 10f), 102.2f));
        }

        [Fact]
        public void AThingWakingAfterTheWindowTellsNothingEvenBeforeATick()
        {
            var pending = new SwingsPending();
            pending.Begin(1, "DvergerMage", Spells, Makes, Caster, away: 12f, now: 100f);
            Assert.Null(pending.Woke("Mistile", new Vec3(11f, 0f, 10f), 102.1f));
        }

        [Fact]
        public void TheHandIsReadOnceAsALaterChangeIsTheNextPick()
        {
            var pending = new SwingsPending();
            pending.Begin(1, "DvergerMage", Spells, Makes, Caster, away: 12f, now: 100f);
            Assert.Empty(pending.Tick(_ => null, 100.5f));
            // A weapon taken up later is for the creature's next attack, not this one.
            Assert.Empty(pending.Tick(_ => "DvergerMistile", 101f));
            Assert.Equal(1, pending.Count);
        }

        [Fact]
        public void WhatTwoOfTheWeaponsBothMakeTellsNeither()
        {
            // The Fader's roar and its intense twin fire the same projectile.
            var pending = new SwingsPending();
            var twins = new[] { "Fader_Roar", "Fader_Roar_Intense" };
            pending.Begin(1, "Fader", twins, new[] { ("Fader_Roar", "Fader_Roar_Projectile", 3f), ("Fader_Roar_Intense", "Fader_Roar_Projectile", 3f) }, Caster, away: 30f, now: 100f);
            Assert.Null(pending.Woke("Fader_Roar_Projectile", Caster, 100.3f));
            Assert.Equal(1, pending.Count);
        }

        [Fact]
        public void ATriggerOneWeaponPlaysNeverWaits()
        {
            var pending = new SwingsPending();
            pending.Begin(1, "DvergerMage", new[] { "DvergerStaffHeal_heal" }, Makes, Caster, away: 12f, now: 100f);
            Assert.Equal(0, pending.Count);
        }

        [Fact]
        public void EachCreaturesAttackGoesByItsOwnHand()
        {
            var pending = new SwingsPending();
            pending.Begin(1, "DvergerMage", Spells, Makes, Caster, away: 12f, now: 100f);
            pending.Begin(2, "DvergerMageSupport", Spells, Makes, new Vec3(40f, 0f, 40f), away: 20f, now: 100.1f);

            var told = pending.Tick(id => id == 1 ? "DvergerMistile" : "DvergerStaffSupport_buff", 100.2f + SwingsPending.Settle);
            Assert.Equal(new[] { ("DvergerMage", "DvergerMistile", 12f), ("DvergerMageSupport", "DvergerStaffSupport_buff", 20f) }, told.ToArray());
        }
    }
}
