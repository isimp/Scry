using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FarMarksTests
    {
        // A crowded patch of kept marks far out: a kind's dots that would cover each other merge
        // into one mark of that kind with how many, every mark drops its ring for a plain shape far
        // out, and a strip of the kinds kept shows one of them alone.

        private static SettingRow Row(string key) => SettingsCatalog.Sections.SelectMany(s => s.Rows).Single(r => r.Keys.FirstOrDefault() == key);

        [Fact]
        public void TheSettingsChooseHowMarksStandFarOutAndWhetherTheKindsStripShows()
        {
            var far = Row("FarMarks");
            Assert.Equal(new[] { ("Full", "Full marks"), ("FullMerged", "Full, merged by kind"), ("Plain", "Plain"), ("PlainMerged", "Plain, merged by kind") }, far.Choices.Select(c => (c.Value, c.Name)));
            Assert.Equal(SettingControl.Switch, Row("KindsStrip").Control);
            Assert.Equal(Row("ShowOnMapDotSize").Group, far.Group);
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(ScrySource.Root(), "src", "Core", "Settings.cs"));
            Assert.Contains("config.Bind(\"1 - General\", \"FarMarks\", FarMarks.PlainMerged,", source);
            Assert.Contains("config.Bind(\"1 - General\", \"KindsStrip\", true,", source);
        }

        [Fact]
        public void OnlyTheMergingChoicesMergeAKindsDots()
        {
            Assert.False(MapLook.Merges(FarMarks.Full));
            Assert.True(MapLook.Merges(FarMarks.FullMerged));
            Assert.False(MapLook.Merges(FarMarks.Plain));
            Assert.True(MapLook.Merges(FarMarks.PlainMerged));
        }

        [Fact]
        public void MarksTurnPlainOnlyFarOutAndOnlyWhereTheSettingsAsk()
        {
            var far = SeenOnMap.PlainFrom * 2f;
            var near = SeenOnMap.PlainFrom / 2f;
            Assert.True(MapLook.PlainAt(FarMarks.Plain, far));
            Assert.True(MapLook.PlainAt(FarMarks.PlainMerged, far));
            Assert.False(MapLook.PlainAt(FarMarks.Plain, near));
            Assert.False(MapLook.PlainAt(FarMarks.Full, far));
            Assert.False(MapLook.PlainAt(FarMarks.FullMerged, far));
            // The plain shape is the mark's middle without its ring, smaller than the whole.
            Assert.InRange(SeenOnMap.PlainShare, 0.5f, 0.7f);
        }

        [Fact]
        public void AKindsDotsMergeWithEachOtherAndNeverWithAnotherKinds()
        {
            // Two copper deposits and a tin between them, all within a touch.
            var spots = new[] { (0f, 0f), (6f, 0f), (3f, 1f) };
            var kinds = new[] { 0, 0, 1 };
            var merged = new MarkStacks();
            merged.Gather(spots.Length, i => spots[i], 10f, i => kinds[i]);
            Assert.Equal(0, merged.AnchorOf(1));
            Assert.Equal(2, merged.CountAt(0));
            Assert.Equal(2, merged.AnchorOf(2));
            Assert.Equal(1, merged.CountAt(2));
        }

        [Fact]
        public void StacksGatherOnlyTheMarksStillStanding()
        {
            // A merged copper stands for both; the tin beside it stacks with it alone.
            var spots = new[] { (0f, 0f), (6f, 0f), (3f, 1f) };
            var stacks = new MarkStacks();
            stacks.Gather(spots.Length, i => spots[i], 10f, include: i => i != 1);
            Assert.Equal(-1, stacks.AnchorOf(1));
            Assert.Equal(new[] { 0, 2 }, stacks.MembersOf(0));
        }

        [Fact]
        public void AMergedMarkPointedAtNamesItsKindHowManyAndTheNearest()
        {
            Assert.Equal("6 Ancient Armour here\nnearest 410 m north-east · click to zoom in on them", SpotWords.MergedHover("Ancient Armour", 6, false, 410f, "north-east"));
            Assert.Equal("2 Infested Mine the server told of here\nnearest beside you · click to zoom in on them", SpotWords.MergedHover("Infested Mine", 2, true, 2f, "north"));
        }

        [Fact]
        public void AClickOnAMergedMarkZoomsInUntilItsDotsComeApart()
        {
            Assert.True(MapLook.ZoomInOn(0.2f) < 0.2f * 0.5f);
            Assert.Equal(SeenOnMap.NearestZoom, MapLook.ZoomInOn(SeenOnMap.NearestZoom * 1.1f));
        }

        [Fact]
        public void TheKindsStripNamesEachKindWithHowManyStand()
        {
            // How many you saw, and those the server told of apart, as their hollow dots are.
            Assert.Equal("Ancient Armour 14", SpotWords.KindChip("Ancient Armour", 14, 0));
            Assert.Equal("Infested Mine 2 · 4 told", SpotWords.KindChip("Infested Mine", 2, 4));
            Assert.Equal("Infested Mine · 4 told", SpotWords.KindChip("Infested Mine", 0, 4));
            // Several kinds may stand out at once: the first picked alone, then each added, and taken back.
            Assert.Contains("alone", SpotWords.KindChipTip(picked: false, anyPicked: false));
            Assert.Contains("too", SpotWords.KindChipTip(picked: false, anyPicked: true));
            Assert.Contains("fade", SpotWords.KindChipTip(picked: true, anyPicked: true));
            // Past its rows, the kinds left out are counted and found in the kept list.
            Assert.Equal("and 4 more, in the title row's On the map list", SpotWords.KindsMore(4));
            // The kinds not shown alone fade to a fifth.
            Assert.Equal(0.2f, SeenOnMap.SoloOthers, 3);
        }
    }
}
