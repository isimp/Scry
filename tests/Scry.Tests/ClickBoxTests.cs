using Xunit;

namespace Scry.Tests
{
    public class ClickBoxTests
    {
        // The compendium's box opens on a click on its button, not as the pointer passes over it;
        // it stays open while the pointer goes elsewhere, and a click anywhere outside it, or on
        // its button again, puts it away.

        [Fact]
        public void AClickOnTheButtonOpensTheBoxAndAnotherPutsItAway()
        {
            var box = new ClickBox();
            Assert.False(box.Open);
            box.Pressed();
            Assert.True(box.Open);
            box.Pressed();
            Assert.False(box.Open);
        }

        [Fact]
        public void AClickInsideKeepsItOpenAndOneOutsidePutsItAway()
        {
            var box = new ClickBox();
            box.Pressed();
            box.DownAt(inside: true);
            Assert.True(box.Open);
            box.DownAt(inside: false);
            Assert.False(box.Open);
            // Closed, a click outside opens nothing.
            box.DownAt(inside: false);
            Assert.False(box.Open);
        }

        [Fact]
        public void AButtonOutsideTheBoxTogglesItAsItStoodBeforeItsPress()
        {
            // The notice's chip stands outside the box: its press puts the box away before its click is read,
            // so the click toggles from where the box stood as the press began, closing an open box for good.
            var box = new ClickBox();
            box.Show();
            box.DownAt(inside: false);
            box.ToggledFromOutside();
            Assert.False(box.Open);
            box.DownAt(inside: false);
            box.ToggledFromOutside();
            Assert.True(box.Open);
        }

        [Fact]
        public void WhatOpensItForAPlayerOpensItTheSameWayAndClosingPutsItAway()
        {
            // The notice's chip opens it as a click would; the gear and the guide's ? put it away as they open their cards.
            var box = new ClickBox();
            box.Show();
            Assert.True(box.Open);
            box.Show();
            Assert.True(box.Open);
            box.Close();
            Assert.False(box.Open);
        }
    }
}
