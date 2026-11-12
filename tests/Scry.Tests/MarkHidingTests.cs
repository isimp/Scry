using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class MarkHidingTests
    {
        // A crowded map is thinned from the kinds list on Valheim's map: an eye on each kind,
        // each category and the list's title hides those marks without taking them off the map's
        // kept kinds, a mark's own menu hides its kind or every mark, and the spots crossed off and
        // those the server told of go at a click. What is hidden is kept with the character, the
        // list says how much, and one button shows everything again. The game's own pins are
        // never touched.

        [Fact]
        public void AKindHiddenStaysKeptWithItsMarkAndIsKeptWithTheCharacter()
        {
            var book = new CompendiumBook();
            book.Keep("rock4_copper", true);
            book.Pick("rock4_copper", KeptMarks.Most);
            book.Save();
            var version = book.HiddenVersion;
            book.Hide(new[] { "rock4_copper" }, true);
            Assert.True(book.Hides("rock4_copper"));
            Assert.False(book.Hides("MineRock_Tin"));
            Assert.True(book.Keeps("rock4_copper"));
            Assert.Equal(KeptMarks.Most, book.PickableMark("rock4_copper"));
            Assert.True(book.HiddenVersion > version);
            Assert.True(book.Changed);

            var back = CompendiumBook.Load(book.Save());
            Assert.True(back.Hides("rock4_copper"));
            Assert.True(back.Keeps("rock4_copper"));
        }

        [Fact]
        public void AKindShownAgainStandsAgainAndLeavesNothingInTheBook()
        {
            var book = new CompendiumBook();
            book.Hide(new[] { "rock4_copper", "MineRock_Tin" }, true);
            book.Hide(new[] { "rock4_copper" }, false);
            Assert.False(book.Hides("rock4_copper"));
            Assert.True(book.Hides("MineRock_Tin"));
            book.Hide(new[] { "MineRock_Tin" }, false);
            Assert.False(book.HidesAny);
            Assert.DoesNotContain("hid", book.Save());
        }

        [Fact]
        public void HidingWhatIsHiddenOrShowingWhatStandsChangesNothing()
        {
            var book = new CompendiumBook();
            book.Hide(new[] { "rock4_copper" }, true);
            var version = book.HiddenVersion;
            book.Hide(new[] { "rock4_copper" }, true);
            book.Hide(new[] { "MineRock_Tin" }, false);
            book.HideAll(false);
            Assert.Equal(version, book.HiddenVersion);
        }

        [Fact]
        public void HidingAllHidesEveryKindAndIsKeptWithTheCharacter()
        {
            var book = new CompendiumBook();
            book.HideAll(true);
            Assert.True(book.HidesAll);
            Assert.True(book.Hides("rock4_copper"));
            Assert.True(book.Hides("loc:Crypt2"));
            var back = CompendiumBook.Load(book.Save());
            Assert.True(back.HidesAll);
            back.HideAll(false);
            Assert.False(back.Hides("rock4_copper"));
        }

        [Fact]
        public void ShowingOneKindWhileAllAreHiddenShowsItAloneTheOthersStayingHidden()
        {
            var book = new CompendiumBook();
            book.HideAll(true);
            book.Hide(new[] { "rock4_copper" }, false, standing: new[] { "rock4_copper", "MineRock_Tin", "loc:Crypt2" });
            Assert.False(book.HidesAll);
            Assert.False(book.Hides("rock4_copper"));
            Assert.True(book.Hides("MineRock_Tin"));
            Assert.True(book.Hides("loc:Crypt2"));
        }

        [Fact]
        public void ShowEverythingBringsBackAllThatWasHidden()
        {
            var book = new CompendiumBook();
            book.Hide(new[] { "rock4_copper" }, true);
            book.HideAll(true);
            book.ShowAll();
            Assert.False(book.HidesAny);
            Assert.False(book.Hides("rock4_copper"));
            Assert.DoesNotContain("hid", book.Save());
        }

        [Fact]
        public void KeepingAKindAgainShowsIt()
        {
            // Keeping something on the map is asking to see it, so a kind hidden then taken off
            // and kept again stands, and so do the kinds of a place kept as one line.
            var book = new CompendiumBook();
            book.Keep("rock4_copper", true);
            book.Hide(new[] { "rock4_copper", "giant_ribs", "giant_skull", "MineRock_Tin" }, true);
            book.Keep("rock4_copper", false);
            book.Keep("rock4_copper", true);
            Assert.False(book.Hides("rock4_copper"));
            book.Keep(MapShowing.AllKindsKey(new[] { "giant_ribs", "giant_skull" }), true);
            Assert.False(book.Hides("giant_ribs"));
            Assert.False(book.Hides("giant_skull"));
            Assert.True(book.Hides("MineRock_Tin"));
        }

        [Fact]
        public void TheListsTitleSaysHowManyAreHidden()
        {
            Assert.Equal("Kinds on the map · 5", SpotWords.KindsTitle(5, 0, 0));
            Assert.Equal("Kinds on the map · 5 · 2 hidden", SpotWords.KindsTitle(5, 0, 2));
            Assert.Equal("Kinds on the map · 5 · 1 picked · 2 hidden", SpotWords.KindsTitle(5, 1, 2));
        }

        [Fact]
        public void ANarrowListsTitleSaysTheSameInFewerWords()
        {
            // Its eye and its arrow leave a narrow list's title little room, so the counts keep it.
            Assert.Equal("Kinds · 5", SpotWords.KindsTitleShort(5, 0, 0));
            Assert.Equal("Kinds · 5 · 1 picked · 2 hidden", SpotWords.KindsTitleShort(5, 1, 2));
        }

        [Fact]
        public void HiddenCountsEachKindListedWhollyHiddenAndEachSwitchOff()
        {
            var chips = new[] { new[] { "rock4_copper" }, new[] { "giant_ribs", "giant_skull" }, new[] { "MineRock_Tin" } };
            var hidden = new[] { "rock4_copper", "giant_ribs" };
            // A family half hidden still stands, so it is not counted.
            Assert.Equal(1, KindsList.HiddenCount(chips, hidden.Contains, all: false, crossedHidden: false, toldHidden: false));
            Assert.Equal(3, KindsList.HiddenCount(chips, hidden.Contains, all: false, crossedHidden: true, toldHidden: true));
            Assert.Equal(3, KindsList.HiddenCount(chips, _ => false, all: true, crossedHidden: false, toldHidden: false));
            Assert.Equal(0, KindsList.HiddenCount(chips, _ => false, all: false, crossedHidden: false, toldHidden: false));
        }

        [Fact]
        public void TheListStandsWhileAnythingIsHiddenSoItCanBeShownAgain()
        {
            // The list usually stands only for two kinds or more; one kind hidden keeps it there.
            Assert.False(KindsList.Stands(listOn: true, chips: 1, anyHidden: false));
            Assert.True(KindsList.Stands(listOn: true, chips: 2, anyHidden: false));
            Assert.True(KindsList.Stands(listOn: true, chips: 1, anyHidden: true));
            Assert.False(KindsList.Stands(listOn: true, chips: 0, anyHidden: true));
            Assert.False(KindsList.Stands(listOn: false, chips: 3, anyHidden: true));
        }

        [Fact]
        public void WithTheListTurnedOffNothingIsHidden()
        {
            // Hiding is set and undone on the list, so with the list off every kind stands.
            Assert.True(KindsList.Hides(listOn: true, hidden: true));
            Assert.False(KindsList.Hides(listOn: false, hidden: true));
            Assert.False(KindsList.Hides(listOn: true, hidden: false));
        }

        [Fact]
        public void AMarksMenuHidesItsKindOrEveryMarkBeforeItsPage()
        {
            Assert.Equal(new[] { MapMenuChoice.Cross, MapMenuChoice.Highlight, MapMenuChoice.HideKind, MapMenuChoice.HideAll, MapMenuChoice.OpenPage }, MapMenu.Of(SpotMode.Plain, 1, hideable: true));
            Assert.Equal(new[] { MapMenuChoice.Cross, MapMenuChoice.Highlight, MapMenuChoice.ZoomIn, MapMenuChoice.HideKind, MapMenuChoice.HideAll, MapMenuChoice.OpenPage }, MapMenu.Of(SpotMode.Plain, 6, hideable: true));
            // The kind a page shows on demand is put away by its strip's Hide, so its menu offers no hiding.
            Assert.Equal(new[] { MapMenuChoice.Cross, MapMenuChoice.Highlight, MapMenuChoice.OpenPage }, MapMenu.Of(SpotMode.Plain, 1, hideable: false));
            Assert.Null(MapMenu.ModeOf(MapMenuChoice.HideKind));
            Assert.Null(MapMenu.ModeOf(MapMenuChoice.HideAll));
        }

        [Fact]
        public void TheMenuNamesTheKindItHides()
        {
            Assert.Equal("Hide Rock copper", SpotWords.MenuWord(MapMenuChoice.HideKind, 6, "Rock copper"));
            Assert.Equal("Hide all marks", SpotWords.MenuWord(MapMenuChoice.HideAll, 1, "Rock copper"));
        }

        [Fact]
        public void EachEyeSaysWhatItDoesAndThatTheKindsStayKept()
        {
            Assert.Equal("Hide this kind on the map; it stays kept", SpotWords.EyeTip(EyeOn.Kind, hidden: false));
            Assert.Equal("Show this kind on the map again", SpotWords.EyeTip(EyeOn.Kind, hidden: true));
            Assert.Equal("Hide every kind under it; they stay kept", SpotWords.EyeTip(EyeOn.Category, hidden: false));
            Assert.Equal("Show every kind under it again", SpotWords.EyeTip(EyeOn.Category, hidden: true));
            Assert.Equal("Hide all of Scry's marks on the map; they stay kept", SpotWords.EyeTip(EyeOn.All, hidden: false));
            Assert.Equal("Show Scry's marks on the map again", SpotWords.EyeTip(EyeOn.All, hidden: true));
        }

        [Fact]
        public void TheListsFootNamesTheSpotsCrossedOffAndThoseTheServerToldOf()
        {
            Assert.Equal("Crossed off 3", SpotWords.CrossedChip(3));
            Assert.Equal("Told by the server 12", SpotWords.ToldChip(12));
            // Hidden, they are not gathered, so the chip names them without a count.
            Assert.Equal("Told by the server", SpotWords.ToldChip(null));
            Assert.Equal("Show everything", SpotWords.ShowEverything);
            Assert.Equal("Hide the spots you crossed off", SpotWords.CrossedChipTip(hidden: false));
            Assert.Equal("Show the spots you crossed off again, faded", SpotWords.CrossedChipTip(hidden: true));
            Assert.Equal("Hide what the server told of: its squares and hollow dots", SpotWords.ToldChipTip(hidden: false));
            Assert.Equal("Show what the server told of again", SpotWords.ToldChipTip(hidden: true));
        }
    }
}
