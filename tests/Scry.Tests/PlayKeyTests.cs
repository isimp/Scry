using Xunit;

namespace Scry.Tests
{
    public class PlayKeyTests
    {
        // A play button outside an effect list or a clip (an effect played on you or in front of
        // you, a log let fall, a ragdoll) is lit while what it started plays: the previews start
        // it and the panel asks after it by one key, made one way.

        [Fact]
        public void TheSameButtonForTheSameThingIsOneKey()
        {
            Assert.Equal(PlayKey.OnYou("vfx_fire"), PlayKey.OnYou("vfx_fire"));
            Assert.Equal(PlayKey.OnYou("vfx_fire").GetHashCode(), PlayKey.OnYou("vfx_fire").GetHashCode());
            Assert.Equal(PlayKey.LetFall, PlayKey.LetFall);
        }

        [Fact]
        public void AnotherButtonOrAnotherThingIsAnotherKey()
        {
            Assert.NotEqual(PlayKey.OnYou("vfx_fire"), PlayKey.There("vfx_fire"));
            Assert.NotEqual(PlayKey.OnYou("vfx_fire"), PlayKey.OnYou("vfx_frost"));
            Assert.NotEqual(PlayKey.LetFall, PlayKey.Ragdoll);
            Assert.False(PlayKey.LetFall.Equals(null));
        }

        [Fact]
        public void APlayButtonStaysLitByItsKey()
        {
            var thing = new object();
            var playback = new Playback<object>(_ => true);
            playback.Started(PlayKey.OnYou("vfx_fire"), new[] { ("", thing) });

            Assert.True(playback.IsPlaying(PlayKey.OnYou("vfx_fire")));
            Assert.False(playback.IsPlaying(PlayKey.There("vfx_fire")));
        }
    }
}
