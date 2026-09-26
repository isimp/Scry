using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class PlaybackTests
    {
        /// <summary>Stands in for a copy: alive until finished.</summary>
        private sealed class Thing
        {
            public bool Done;
        }

        private static Playback<Thing> Tracker() => new Playback<Thing>(t => !t.Done);

        [Fact]
        public void AButtonStaysLitWhileAnythingItStartedIsStillPlaying()
        {
            var playback = Tracker();
            var sound = new Thing();
            var puff = new Thing();

            playback.Started("Death", new[] { ("sfx", sound), ("vfx", puff) });
            puff.Done = true;

            Assert.True(playback.IsPlaying("Death"));
        }

        [Fact]
        public void ItGoesOutWhenAllItStartedHasFinished()
        {
            var playback = Tracker();
            var sound = new Thing();
            playback.Started("Death", new[] { ("sfx", sound) });

            sound.Done = true;

            Assert.False(playback.IsPlaying("Death"));
        }

        [Fact]
        public void SomethingNeverPlayedIsNotLit()
        {
            Assert.False(Tracker().IsPlaying("Death"));
        }

        [Fact]
        public void AButtonThatStartedNothingIsNotLit()
        {
            var playback = Tracker();

            playback.Started("Death", new (string, Thing)[0]);

            Assert.False(playback.IsPlaying("Death"));
        }

        [Fact]
        public void PlayingAgainAddsToWhatIsStillPlaying()
        {
            var playback = Tracker();
            var first = new Thing();
            var second = new Thing();
            playback.Started("Hit", new[] { ("sfx", first) });
            playback.Started("Hit", new[] { ("sfx", second) });

            second.Done = true;

            Assert.True(playback.IsPlaying("Hit"));
        }

        [Fact]
        public void EachPartOfAListIsLitWhileItsOwnCopyPlays()
        {
            var playback = Tracker();
            var sound = new Thing();
            var puff = new Thing { Done = true };
            playback.Started("Death", new[] { ("sfx_troll_death", sound), ("vfx_troll_death", puff) });

            Assert.True(playback.IsPlaying("Death", "sfx_troll_death"));
            Assert.False(playback.IsPlaying("Death", "vfx_troll_death"));
            Assert.False(playback.IsPlaying("Hit", "sfx_troll_death"));
        }

        [Fact]
        public void TheListPlayedLastIsRememberedAfterItFinishes()
        {
            var playback = Tracker();
            var sound = new Thing();
            playback.Started("Hit", new[] { ("sfx", new Thing()) });
            playback.Started("Death", new[] { ("sfx", sound) });

            sound.Done = true;

            Assert.Equal("Death", playback.Last);
        }

        [Fact]
        public void StoppingAButtonHandsBackWhatItStartedAndPutsItOut()
        {
            var playback = Tracker();
            var sound = new Thing();
            var puff = new Thing();
            playback.Started("Death", new[] { ("sfx", sound), ("vfx", puff) });

            var taken = playback.Take("Death");

            Assert.Equal(new[] { sound, puff }, taken);
            Assert.False(playback.IsPlaying("Death"));
        }

        [Fact]
        public void WhatAnAnimationStartsOfItselfDoesNotChangeTheButtonPressedLast()
        {
            var playback = Tracker();
            playback.Started("Death", new[] { ("sfx", new Thing()) });

            playback.Started("walk", new[] { ("step", new Thing()) }, pressed: false);

            Assert.Equal("Death", playback.Last);
            Assert.True(playback.IsPlaying("walk", "step"));
        }

        [Fact]
        public void ForgettingEverythingPutsAllOut()
        {
            var playback = Tracker();
            playback.Started("Death", new[] { ("sfx", new Thing()) });

            playback.Forget();

            Assert.False(playback.IsPlaying("Death"));
            Assert.Null(playback.Last);
        }

        [Fact]
        public void FinishedThingsAreLetGo()
        {
            var playback = Tracker();
            var things = new List<(string, Thing)>();
            for (var i = 0; i < 100; i++) things.Add(("sfx", new Thing { Done = true }));
            playback.Started("Hit", things);

            playback.IsPlaying("Hit");

            Assert.Equal(0, playback.Tracked);
        }
    }
}
