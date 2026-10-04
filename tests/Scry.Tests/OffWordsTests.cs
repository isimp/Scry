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
            Assert.Equal("Location music is off until Scry is updated. Everything else works.", OffWords.Line(new[] { "location music" }));
        }

        [Fact]
        public void SeveralPartsOffAreCountedAndNamed()
        {
            Assert.Equal("3 parts of Scry are off until it is updated: location music, mods' hooks and drops seen in play. Everything else works.",
                OffWords.Line(new[] { "location music", "mods' hooks", "drops seen in play" }));
            Assert.Equal("2 parts of Scry are off until it is updated: location music and mods' hooks. Everything else works.",
                OffWords.Line(new[] { "location music", "mods' hooks" }));
        }

        [Fact]
        public void WhatTheStartupCheckFindsMissingIsNamedByWhatItTurnsOff()
        {
            Assert.Equal("falling copies landing on terrain", OffWords.FallingCopies("terrain"));
            Assert.Equal("effects keeping their LightFlicker", OffWords.KeptScript("LightFlicker"));
            Assert.Equal("linking damage to Burning", OffWords.DamageLink("Burning"));
        }

        [Fact]
        public void NothingOffSaysNothing()
        {
            Assert.Null(OffWords.Line(new string[0]));
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
