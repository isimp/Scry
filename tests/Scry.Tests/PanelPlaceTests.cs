using System.Globalization;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PanelPlaceTests
    {
        // The panel opens where it was left, in both views, with the stage seen as it was.

        private static PanelPlace Everything() => new PanelPlace
        {
            Full = new PanelPlace.Area(10f, 20f, 900f, 600f),
            Compact = new PanelPlace.Area(30f, 40f, 500f, 700f),
            CompactView = true,
            Lighting = 2,
            Backdrop = 1,
            Ground = "Swamp",
            Person = true,
            Worn = false,
            Spin = true,
            Creatures = false,
            Folded = new[] { "layout", "plan" },
            StageScale = 1.25f,
            ListShareFull = 0.4f,
            ListHiddenFull = false,
            ListShareCompact = 0.46f,
            ListHiddenCompact = true,
        };

        [Fact]
        public void WhatIsWrittenReadsBackTheSame()
        {
            var read = PanelPlace.Read(Everything().Lines());
            var all = Everything();
            Assert.Equal(all.Full, read.Full);
            Assert.Equal(all.Compact, read.Compact);
            Assert.Equal(all.CompactView, read.CompactView);
            Assert.Equal(all.Lighting, read.Lighting);
            Assert.Equal(all.Backdrop, read.Backdrop);
            Assert.Equal(all.Ground, read.Ground);
            Assert.Equal(all.Person, read.Person);
            Assert.Equal(all.Worn, read.Worn);
            Assert.Equal(all.Spin, read.Spin);
            Assert.Equal(all.Creatures, read.Creatures);
            Assert.Equal(all.Folded, read.Folded);
            Assert.Equal(all.StageScale, read.StageScale);
            Assert.Equal(all.ListShareFull, read.ListShareFull);
            Assert.Equal(all.ListHiddenFull, read.ListHiddenFull);
            Assert.Equal(all.ListShareCompact, read.ListShareCompact);
            Assert.Equal(all.ListHiddenCompact, read.ListHiddenCompact);
        }

        [Fact]
        public void TheFileReadsAsItAlwaysHas()
        {
            // As 0.2.0 writes it, one setting a line.
            var read = PanelPlace.Read(new[]
            {
                "full 10 20 900 600", "compact 30 40 500 700", "view compact", "light 2", "backdrop 1", "ground Swamp",
                "person 1", "worn 0", "spin 1", "creatures 0", "folded layout plan", "stage 1.25", "list 0.40", "listhidden 0",
                "clist 0.46", "clisthidden 1",
            });
            Assert.Equal(new PanelPlace.Area(10f, 20f, 900f, 600f), read.Full);
            Assert.True(read.CompactView);
            Assert.Equal(2, read.Lighting);
            Assert.Equal(new[] { "layout", "plan" }, read.Folded);
            Assert.Equal(0.46f, read.ListShareCompact);
            Assert.True(read.ListHiddenCompact);
        }

        [Fact]
        public void WhatTheFileDoesNotSayIsLeftAsItIs()
        {
            var read = PanelPlace.Read(new[] { "view full" });
            Assert.False(read.CompactView);
            Assert.Null(read.Full);
            Assert.Null(read.Lighting);
            Assert.Null(read.Ground);
            Assert.Null(read.Spin);
            Assert.Null(read.Folded);
            Assert.Null(read.StageScale);
        }

        [Fact]
        public void ABadLineIsSkippedAndTheRestStillRead()
        {
            var read = PanelPlace.Read(new[] { "full 10 x 900 600", "light two", "stage 1,5", "compact 30 40 500 700", "spin 1", "", "nonsense here" });
            Assert.Null(read.Full);
            Assert.Null(read.Lighting);
            Assert.Null(read.StageScale);
            Assert.Equal(new PanelPlace.Area(30f, 40f, 500f, 700f), read.Compact);
            Assert.True(read.Spin);
        }

        [Fact]
        public void ALineWithMoreThanItsSettingNeedsIsSkipped()
        {
            var read = PanelPlace.Read(new[] { "full 10 20 900 600 7", "light 2 3", "spin 0", "folded layout  plan" });
            Assert.Null(read.Full);
            Assert.Null(read.Lighting);
            Assert.False(read.Spin);
            Assert.Equal(new[] { "layout", "plan" }, read.Folded);
            // Only a 1 switches a setting on.
            Assert.False(PanelPlace.Read(new[] { "spin on" }).Spin);
        }

        [Fact]
        public void TheWindowsPlaceIsKeptToTheWholePixel()
        {
            var place = new PanelPlace { Full = new PanelPlace.Area(10.6f, 20.4f, 900.5f, 600f) };
            Assert.Equal(new[] { "full 11 20 901 600" }, place.Lines());
        }

        [Fact]
        public void APanelPlacedOnAGermanPcOpensThereAnywhere()
        {
            var was = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var lines = Everything().Lines().ToList();
                Assert.Contains("stage 1.25", lines);
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.Equal(1.25f, PanelPlace.Read(lines).StageScale);
            }
            finally
            {
                CultureInfo.CurrentCulture = was;
            }
        }
    }
}
