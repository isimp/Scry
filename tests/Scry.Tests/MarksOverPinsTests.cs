using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MarksOverPinsTests
    {
        // Scry's marks stand under the game's pins and their names, so a base's pins hide them.
        // The settings choose what comes over them: never anything, what the pointer rests on, or
        // every mark always; the pips on their own; and how the pins near the pointer stand.

        private static SettingRow Row(string key) => SettingsCatalog.Sections.SelectMany(s => s.Rows).Single(r => r.Keys.FirstOrDefault() == key);

        [Fact]
        public void TheSettingsOfferWhatStandsOverThePinsBesideTheDotSize()
        {
            var marks = Row("MarksOverPins");
            Assert.Equal(SettingControl.Choices, marks.Control);
            Assert.Equal(new[] { ("Never", "Never"), ("Pointed", "When pointed at"), ("Always", "Always") }, marks.Choices.Select(c => (c.Value, c.Name)));
            var pips = Row("PipsOverPins");
            Assert.Equal(SettingControl.Switch, pips.Control);
            var near = Row("PinsNearPointer");
            Assert.Equal(new[] { ("Full", "Full"), ("Third", "A third") }, near.Choices.Select(c => (c.Value, c.Name)));
            var dots = Row("ShowOnMapDotSize");
            Assert.All(new[] { marks, pips, near }, r => Assert.Equal(dots.Group, r.Group));
        }

        [Fact]
        public void ANewInstallLiftsWhatThePointerRestsOnAndThePipsAndLeavesThePinsAtFull()
        {
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(ScrySource.Root(), "src", "Core", "Settings.cs"));
            Assert.Contains("config.Bind(\"1 - General\", \"MarksOverPins\", MarksOverPins.Pointed,", source);
            Assert.Contains("config.Bind(\"1 - General\", \"PipsOverPins\", true,", source);
            Assert.Contains("config.Bind(\"1 - General\", \"PinsNearPointer\", PinsNearPointer.Full,", source);
        }

        [Fact]
        public void NeverKeepsEveryMarkUnderThePins()
        {
            Assert.False(MapLook.DotsOver(MarksOverPins.Never));
            Assert.False(MapLook.PointedOver(MarksOverPins.Never));
            Assert.False(MapLook.PipsOver(MarksOverPins.Never, pipsOver: false));
        }

        [Fact]
        public void WhenPointedAtLiftsOnlyWhatThePointerRestsOn()
        {
            Assert.False(MapLook.DotsOver(MarksOverPins.Pointed));
            Assert.True(MapLook.PointedOver(MarksOverPins.Pointed));
            Assert.False(MapLook.PipsOver(MarksOverPins.Pointed, pipsOver: false));
        }

        [Fact]
        public void AlwaysPutsEveryMarkAndItsPipsOverThePins()
        {
            Assert.True(MapLook.DotsOver(MarksOverPins.Always));
            Assert.True(MapLook.PointedOver(MarksOverPins.Always));
            Assert.True(MapLook.PipsOver(MarksOverPins.Always, pipsOver: false));
        }

        [Fact]
        public void PipsOverPinsLiftsThePipsWhateverTheMarksDo()
        {
            Assert.All(new[] { MarksOverPins.Never, MarksOverPins.Pointed, MarksOverPins.Always }, m => Assert.True(MapLook.PipsOver(m, pipsOver: true)));
        }

        [Fact]
        public void PinsNearAPointedMarkFadeToAThirdWhereTheSettingsAsk()
        {
            Assert.Equal(1f, MapLook.NearShare(PinsNearPointer.Full));
            Assert.Equal(SeenOnMap.OthersDimmed, MapLook.NearShare(PinsNearPointer.Third), 3);
            const float size = 20f;
            // Near a spread stack: within its marks' reach and a mark past it.
            var spread = MarkStacks.FadeReach(3, size);
            Assert.True(spread >= MarkStacks.FanRadius(3, size) + size);
            // Near a single dot: a mark and a half round it, never as far as a spread.
            var single = MarkStacks.FadeReach(1, size);
            Assert.True(single >= size * 1.5f && single < spread);
            Assert.True(MapLook.Fades(single - 1f, 0f, single));
            Assert.False(MapLook.Fades(single, single, single));
        }
    }
}
