using Xunit;

namespace Scry.Tests
{
    public class ActionWordsTests
    {
        // Every button of the selection says on hover what it does, and a button lit while what it
        // started goes on says that a click undoes it.

        [Fact]
        public void ASwitchSaysWhatAClickDoesNow()
        {
            Assert.Equal("Takes it off the person", ActionWords.Wear(worn: true));
            Assert.Equal("Puts it on a person on the stage", ActionWords.Wear(worn: false));
            Assert.Contains("while you look at other things", ActionWords.Keep(kept: false));
            Assert.Contains("when you look at something else", ActionWords.Keep(kept: true));
            Assert.Equal("Puts it back", ActionWords.Ragdoll(falling: true));
            Assert.Equal("Puts it back", ActionWords.LetFall(falling: true));
            Assert.Equal("Takes the copy out of the world", ActionWords.InWorld(shown: true));
            Assert.Contains("only you see", ActionWords.InWorld(shown: false));
            Assert.Equal("Stops it", ActionWords.PlayThere(playing: true));
            Assert.Equal("Stops it", ActionWords.PlayOnYou(playing: true));
            Assert.Contains("stop repeating", ActionWords.Repeat(on: true));
            Assert.DoesNotContain("stop", ActionWords.Repeat(on: false));
            Assert.Contains("never applied", ActionWords.ShowStatus(showing: false));
            Assert.Equal("Takes its look off you", ActionWords.ShowStatus(showing: true));
            Assert.Equal("Goes on from where it was paused", ActionWords.PauseSound(paused: true));
        }

        [Fact]
        public void APairThatLooksAlikeSaysHowItDiffers()
        {
            Assert.Contains("leaves to chance", ActionWords.RollPlace);
            Assert.Contains("anew", ActionWords.AnotherLayout);
            Assert.Contains("picked at random", ActionWords.PlaySound(3));
            Assert.Equal("Plays it", ActionWords.PlaySound(1));
        }

        [Fact]
        public void CopyingTheNameSaysWhichNameItCopies()
        {
            Assert.Equal("Copies Bjorn, the name the console and mods use", ActionWords.CopyName("Bjorn"));
        }
    }
}
