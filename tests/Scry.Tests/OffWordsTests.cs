using Xunit;

namespace Scry.Tests
{
    public class OffWordsTests
    {
        // When part of Scry is off (a game update it does not know, or a part of it failing), a
        // strip under the header says what in one line, and its details say what to do. It speaks
        // of what is off, not of the game, since the cause is not always the game.

        [Fact]
        public void OnePartOffIsNamed()
        {
            Assert.Equal("Location music is off until Scry is updated. Everything else works.", OffWords.Line(new[] { Feature.LocationMusic }));
        }

        [Fact]
        public void SeveralPartsOffAreCountedAndNamed()
        {
            Assert.Equal("3 parts of Scry are off until it is updated: location music, mods' hooks, in the details and drops seen in play. Everything else works.",
                OffWords.Line(new[] { Feature.LocationMusic, Feature.ModHooks, Feature.DropsSeenInPlay }));
            Assert.Equal("2 parts of Scry are off until it is updated: location music and drops seen in play. Everything else works.",
                OffWords.Line(new[] { Feature.LocationMusic, Feature.DropsSeenInPlay }));
        }

        [Fact]
        public void NothingOffSaysNothing()
        {
            Assert.Null(OffWords.Line(new Feature[0]));
            Assert.Null(OffWords.Line(null));
        }

        [Fact]
        public void TheDetailsSayWhyAndWhatToDo()
        {
            var said = OffWords.Details();
            Assert.Contains("does not know", said);
            Assert.Contains("LogOutput.log", said);
        }
    }
}
