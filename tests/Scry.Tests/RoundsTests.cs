using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class RoundsTests
    {
        // What the character sees is looked for among everything loaded, thousands of things: a
        // share of them each frame, so that each is looked at once a second without any one frame
        // paying for all.

        private sealed class Thing
        {
            public int Looks;
        }

        private static List<Thing> Fill(Rounds<Thing> rounds, int count)
        {
            var things = Enumerable.Range(0, count).Select(_ => new Thing()).ToList();
            foreach (var thing in things) rounds.Add(thing);
            return things;
        }

        private static Looked Keep(Thing thing)
        {
            thing.Looks++;
            return Looked.Kept;
        }

        [Fact]
        public void EveryThingIsLookedAtOnceARoundEachFrameTakingItsShare()
        {
            var rounds = new Rounds<Thing>(1f);
            var things = Fill(rounds, 120);

            var shares = new List<int>();
            for (var frame = 0; frame < 60; frame++)
            {
                var before = things.Sum(t => t.Looks);
                rounds.Look(1f / 60, Keep);
                shares.Add(things.Sum(t => t.Looks) - before);
            }

            Assert.All(things, t => Assert.Equal(1, t.Looks));
            Assert.All(shares, share => Assert.Equal(2, share));
        }

        [Fact]
        public void FewerThingsThanFramesAreStillEachLookedAtOnceARound()
        {
            var rounds = new Rounds<Thing>(1f);
            var things = Fill(rounds, 10);

            for (var frame = 0; frame < 60; frame++) rounds.Look(1f / 60, Keep);

            Assert.All(things, t => Assert.Equal(1, t.Looks));
        }

        [Fact]
        public void AThingLetGoIsNotLookedAtAgainAndNoOtherIsPassedOver()
        {
            var rounds = new Rounds<Thing>(1f);
            var things = Fill(rounds, 10);
            var letGo = things.Where((_, i) => i % 2 == 0).ToHashSet();

            for (var round = 0; round < 3; round++)
            {
                rounds.Look(1f, thing =>
                {
                    thing.Looks++;
                    return letGo.Contains(thing) ? Looked.LetGo : Looked.Kept;
                });
            }

            Assert.All(letGo, t => Assert.Equal(1, t.Looks));
            Assert.All(things.Except(letGo), t => Assert.Equal(3, t.Looks));
            Assert.Equal(5, rounds.Count);
            Assert.Equal(5, rounds.LetGo);
            Assert.Equal(0, rounds.Dropped);
            Assert.All(letGo, t => Assert.False(rounds.Contains(t)));
            Assert.All(things.Except(letGo), t => Assert.True(rounds.Contains(t)));
        }

        [Fact]
        public void AThingGoneIsDroppedAndNotCountedAsLetGo()
        {
            var rounds = new Rounds<Thing>(1f);
            var things = Fill(rounds, 4);

            rounds.Look(1f, thing =>
            {
                thing.Looks++;
                return thing == things[1] ? Looked.Dropped : Looked.Kept;
            });
            rounds.Look(1f, Keep);

            Assert.Equal(1, things[1].Looks);
            Assert.Equal(3, rounds.Count);
            Assert.Equal(1, rounds.Dropped);
            Assert.Equal(0, rounds.LetGo);
        }

        [Fact]
        public void ALongFrameLooksAtEachThingOnceAtTheMost()
        {
            // A hitch of seconds is owed no more than a round; nothing is looked at twice in it,
            // not even when most are let go on the way.
            var rounds = new Rounds<Thing>(1f);
            var things = Fill(rounds, 10);

            rounds.Look(5f, thing =>
            {
                thing.Looks++;
                return thing == things[0] ? Looked.Kept : Looked.LetGo;
            });

            Assert.All(things, t => Assert.Equal(1, t.Looks));
            Assert.Equal(1, rounds.Count);
        }

        [Fact]
        public void AFrameWhoseTimeIsSpentLeavesTheRestForTheFramesAfter()
        {
            // However costly the things due, one frame never pays for more than its time; the
            // rest are owed and looked at in the frames after, each still once.
            var rounds = new Rounds<Thing>(1f);
            var things = Fill(rounds, 10);
            var shares = new List<int>();

            for (var frame = 0; frame < 5; frame++)
            {
                var looks = 0;
                rounds.Look(frame == 0 ? 1f : 0f, thing =>
                {
                    looks++;
                    return Keep(thing);
                }, () => looks >= 3);
                shares.Add(looks);
            }

            Assert.Equal(new[] { 3, 3, 3, 1, 0 }, shares);
            Assert.All(things, t => Assert.Equal(1, t.Looks));
        }

        [Fact]
        public void AThingAddedMidRoundIsLookedAtWithinTheRound()
        {
            var rounds = new Rounds<Thing>(1f);
            var things = Fill(rounds, 6);

            rounds.Look(0.5f, Keep);
            var late = new Thing();
            rounds.Add(late);
            rounds.Look(0.5f, Keep);
            rounds.Look(0.5f, Keep);

            Assert.All(things, t => Assert.InRange(t.Looks, 1, 2));
            Assert.Equal(1, late.Looks);
        }

        [Fact]
        public void EverythingCanBeLookedAtAtOnceLettingNoneGo()
        {
            // How the self-test times a whole round: every thing once, kept whatever it says.
            var rounds = new Rounds<Thing>(1f);
            var things = Fill(rounds, 5);

            rounds.LookAtAll(thing =>
            {
                thing.Looks++;
                return Looked.LetGo;
            });

            Assert.All(things, t => Assert.Equal(1, t.Looks));
            Assert.Equal(5, rounds.Count);
            Assert.Equal(0, rounds.LetGo);
        }

        [Fact]
        public void ClearedRoundsStartOverWithNothingCounted()
        {
            var rounds = new Rounds<Thing>(1f);
            var gone = Fill(rounds, 3)[0];
            rounds.Look(1f, thing => thing == gone ? Looked.Dropped : Looked.LetGo);
            Fill(rounds, 1);
            rounds.Look(0.9f, Keep);

            rounds.Clear();

            Assert.Equal(0, rounds.Count);
            Assert.Equal(0, rounds.LetGo);
            Assert.Equal(0, rounds.Dropped);
            // Nothing owed from before: half a round in, the one fresh thing is not yet due.
            var fresh = Fill(rounds, 1);
            rounds.Look(0.5f, Keep);
            Assert.Equal(0, fresh[0].Looks);
            rounds.Look(0.5f, Keep);
            Assert.Equal(1, fresh[0].Looks);
        }

        [Fact]
        public void NothingIsKeptWhereThereIsNoThing()
        {
            var rounds = new Rounds<Thing>(1f);
            rounds.Add(null);
            Assert.Equal(0, rounds.Count);
        }
    }
}
