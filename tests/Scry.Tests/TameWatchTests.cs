using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class TameWatchTests
    {
        // Whether a creature is tame is kept on it for every player to see, so a taming is seen as
        // it happens wherever the game tames it: a creature seen wild near the character, tame
        // when next looked at.

        private sealed class Creature
        {
        }

        [Fact]
        public void ACreatureSeenWildThenTameWasTamedOnce()
        {
            var watch = new TameWatch<Creature>();
            var wolf = new Creature();

            Assert.Empty(watch.Look(new[] { (wolf, false) }));
            Assert.Equal(new[] { wolf }, watch.Look(new[] { (wolf, true) }));
            Assert.Empty(watch.Look(new[] { (wolf, true) }));
        }

        [Fact]
        public void OneTameWhenFirstSeenWasNotTamedInSight()
        {
            // Tamed before the character came near, or long ago: no taming seen.
            var watch = new TameWatch<Creature>();
            var boar = new Creature();

            Assert.Empty(watch.Look(new[] { (boar, true) }));
            Assert.Empty(watch.Look(new[] { (boar, true) }));
        }

        [Fact]
        public void OneSeenWildThatCameBackTameWasTamed()
        {
            // Out of sight a while and back tame: tamed, as the character saw it wild.
            var watch = new TameWatch<Creature>();
            var lox = new Creature();
            var other = new Creature();

            watch.Look(new[] { (lox, false) });
            watch.Look(new[] { (other, false) });
            Assert.Equal(new[] { lox }, watch.Look(new[] { (lox, true), (other, false) }));
        }

        [Fact]
        public void TheWildRememberedAreFewAndForgottenWithTheWorld()
        {
            var watch = new TameWatch<Creature>();
            var many = Enumerable.Range(0, TameWatch<Creature>.Kept + 10).Select(_ => new Creature()).ToList();
            watch.Look(many.Select(c => (c, false)));
            Assert.Equal(TameWatch<Creature>.Kept, watch.Count);

            watch.Forget();
            Assert.Equal(0, watch.Count);
            Assert.Empty(watch.Look(new[] { (many[0], true) }));
        }

        [Fact]
        public void AWildCreatureNotNearForHalfAMinuteIsForgottenSoItsTamingElsewhereIsNotSeen()
        {
            // Seen wild, then away for longer than the watch keeps a creature out of sight: tamed meanwhile by someone else, it comes back as one tame when first seen.
            var watch = new TameWatch<Creature>();
            var wolf = new Creature();
            watch.Look(new[] { (wolf, false) }, now: 0.0);
            watch.Look(new (Creature, bool)[0], now: TameWatch<Creature>.OutOfSight + 1.0);
            Assert.Equal(0, watch.Count);
            Assert.Empty(watch.Look(new[] { (wolf, true) }, now: TameWatch<Creature>.OutOfSight + 2.0));
            // Within that time it is still the one seen wild.
            watch.Look(new[] { (wolf, false) }, now: 100.0);
            watch.Look(new (Creature, bool)[0], now: 100.0 + TameWatch<Creature>.OutOfSight - 1.0);
            Assert.Equal(new[] { wolf }, watch.Look(new[] { (wolf, true) }, now: 100.0 + TameWatch<Creature>.OutOfSight));
        }

        [Fact]
        public void TheOneLongestUnseenIsForgottenFirst()
        {
            var watch = new TameWatch<Creature>();
            var many = Enumerable.Range(0, TameWatch<Creature>.Kept).Select(_ => new Creature()).ToList();
            watch.Look(many.Select(c => (c, false)));
            watch.Look(new[] { (many[0], false) });
            watch.Look(new[] { (new Creature(), false) });

            Assert.Equal(new[] { many[0] }, watch.Look(new[] { (many[0], true), (many[1], true) }));
            Assert.Equal(new[] { many[2] }, watch.Look(new[] { (many[2], true) }));
        }
    }
}
