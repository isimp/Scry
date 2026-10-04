using Xunit;

namespace Scry.Tests
{
    public class PreviewWordingTests
    {
        // What the previews say: what each of a creature's clips is, and what playing an entry's
        // music did, with how to stop it where it was started.

        [Fact]
        public void AnAttacksClipSaysItsWeaponAndWhetherItIsTheSecond()
        {
            Assert.Equal("attack club", ClipWords.Attack("club", second: false));
            Assert.Equal("attack club, second", ClipWords.Attack("club", second: true));
            Assert.True(ClipWords.IsAttack(ClipWords.Attack("club", second: true)));
            Assert.False(ClipWords.IsAttack("jumps, by name"));
            Assert.False(ClipWords.IsAttack(null));
        }

        [Fact]
        public void TheGamesOwnActionsAreNamedByWhatTheCreatureDoes()
        {
            Assert.True(ClipWords.Action("jump", out var jump));
            Assert.Equal("jumps", jump);
            Assert.True(ClipWords.Action("dead", out var dead));
            Assert.Equal("dies", dead);
            Assert.False(ClipWords.Action("dance", out _));
            Assert.Equal("idles", ClipWords.Idles);
        }

        [Fact]
        public void AClipFoundByItsNameSaysSo()
        {
            Assert.Equal("jumps, by name", ClipWords.ByName("Jump_Start"));
            Assert.Equal("swims, by name", ClipWords.ByName("swim_idle"));
            Assert.Equal("in water, by name", ClipWords.ByName("Water_Idle"));
        }

        [Fact]
        public void APieceOfMusicIsNamedInWordsWhateverTheGameCallsIt()
        {
            Assert.Equal("Black forest location music", MusicWords.Name("BlackForestLocationMusic"));
            Assert.Equal("Music fuling camp", MusicWords.Name("Music_FulingCamp"));
            Assert.Equal("Boss eikthyr", MusicWords.Name("boss_eikthyr"));
            Assert.Equal("Crypt", MusicWords.Name("crypt"));
            Assert.Equal("", MusicWords.Name(null));
        }

        [Fact]
        public void PlayingMusicSaysHowToStopItWhereItWasStarted()
        {
            Assert.Equal("Playing Meadows; Enter again stops it.", MusicWords.Playing("Meadows", MusicStop.Enter));
            Assert.Equal("Playing Boss eikthyr; click it again to stop it.", MusicWords.Playing("Boss eikthyr", MusicStop.Click));
            Assert.Equal("Playing Boss eikthyr; click it or press Enter again to stop it.", MusicWords.Playing("Boss eikthyr", MusicStop.ClickOrEnter));
        }
    }
}
