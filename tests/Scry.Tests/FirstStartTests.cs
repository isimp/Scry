using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FirstStartTests
    {
        // A new player is told once how to open Scry, a few seconds into a world; the first time
        // Scry opens it offers a short setup, every row set already, which Later puts off until it
        // next opens and Done puts away for good.

        [Fact]
        public void TheKeyIsToldOnceAFewSecondsIntoAWorld()
        {
            Assert.False(FirstStart.HintDue(hinted: false, inWorld: true, playerFor: 1f));
            Assert.True(FirstStart.HintDue(hinted: false, inWorld: true, playerFor: FirstStart.HintAfter));
            Assert.False(FirstStart.HintDue(hinted: true, inWorld: true, playerFor: 60f));
            Assert.False(FirstStart.HintDue(hinted: false, inWorld: false, playerFor: 60f));
            Assert.Equal("Scry is ready: press Shift+F7 to open it, or type /scry in the chat", FirstStart.Hint("Shift+F7"));
        }

        [Fact]
        public void TheSetupIsOfferedUntilDoneAndLaterPutsItOffUntilScryNextOpens()
        {
            Assert.True(FirstStart.SetupDue(setUp: false, putOff: false, characterAsked: false));
            Assert.False(FirstStart.SetupDue(setUp: false, putOff: true, characterAsked: false));
            Assert.False(FirstStart.SetupDue(setUp: true, putOff: false, characterAsked: true));
            // A character never asked about its compendium, after the setup was done for another.
            Assert.True(FirstStart.SetupDue(setUp: true, putOff: false, characterAsked: false));
        }

        [Fact]
        public void TheSetupSetsOnlyWhatTheSettingsCardHolds()
        {
            var keys = SettingsCatalog.Sections.SelectMany(s => s.Rows).SelectMany(r => r.Keys).ToList();
            Assert.Equal(new[] { "OpenKey", "WalkWhileOpen", "LookWithRightMouse", "RecordSeenSpots" },
                SettingsCatalog.Setup.SelectMany(r => r.Keys));
            Assert.All(SettingsCatalog.Setup.SelectMany(r => r.Keys), key => Assert.Contains(key, keys));
        }

        [Fact]
        public void WhatWasSetUpIsKeptWithThePanel()
        {
            var place = new PanelPlace { Hinted = true, SetUp = false };
            var read = PanelPlace.Read(place.Lines().ToArray());
            Assert.True(read.Hinted);
            Assert.False(read.SetUp);
            Assert.Null(PanelPlace.Read(new string[0]).Hinted);
        }

        [Fact]
        public void WritingTheFlagsKeepsEveryOtherLineOfThePanelsFile()
        {
            var lines = new[] { "full 1 2 3 4", "spin 0", "hinted 0", "plannames 1" };
            var written = PanelPlace.WithFirstStart(lines, hinted: true, setUp: false).ToArray();
            Assert.Equal(new[] { "full 1 2 3 4", "spin 0", "plannames 1", "hinted 1", "setup 0" }, written);
            Assert.Equal(new[] { "setup 1" }, PanelPlace.WithFirstStart(new string[0], hinted: null, setUp: true));
        }
    }
}
