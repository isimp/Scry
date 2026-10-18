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
            Assert.Equal("Take it off the person", ActionWords.Wear(worn: true));
            Assert.Equal("Put it on a person on the stage", ActionWords.Wear(worn: false));
            Assert.Contains("while you look at other things", ActionWords.Keep(kept: false));
            Assert.Contains("when you look at something else", ActionWords.Keep(kept: true));
            Assert.Equal("Put it back", ActionWords.Ragdoll(falling: true));
            Assert.Equal("Put it back", ActionWords.LetFall(falling: true));
            Assert.Equal("Take the preview out of the world", ActionWords.InWorld(shown: true));
            Assert.Contains("only you see", ActionWords.InWorld(shown: false));
            Assert.Equal("Stop it", ActionWords.PlayThere(playing: true));
            Assert.Equal("Stop it", ActionWords.PlayOnYou(playing: true));
            Assert.Equal("Stop repeating clips, effects and sounds", ActionWords.Repeat(on: true));
            Assert.DoesNotContain("Stop", ActionWords.Repeat(on: false));
            Assert.Contains("clips, effects and sounds", ActionWords.Repeat(on: false));
            Assert.Contains("never applied", ActionWords.ShowStatus(showing: false));
            Assert.Equal("Take its look off you", ActionWords.ShowStatus(showing: true));
            Assert.Equal("Go on from where it was paused", ActionWords.PauseSound(paused: true));
        }

        [Fact]
        public void APairThatLooksAlikeSaysHowItDiffers()
        {
            Assert.Contains("leaves to chance", ActionWords.RollPlace);
            Assert.Contains("another example", ActionWords.AnotherLayout);
            Assert.Contains("picked at random", ActionWords.PlaySound(3));
            Assert.Equal("Play it", ActionWords.PlaySound(1));
        }

        [Fact]
        public void CopyingTheNameSaysWhichNameItCopies()
        {
            Assert.Equal("Copy Bjorn, the name the console and mods use", ActionWords.CopyName("Bjorn"));
        }
    }
}
