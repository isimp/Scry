using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class KeptOnMapTests
    {
        // Any kind Show on map can show can be kept on Valheim's map, standing every time it opens,
        // kept with the character; every kind of dungeon is kept from the start, as the dungeon
        // marks stood before. One list in the panel's title row tells what is kept and takes each
        // off; pointing at a mark on the map says what it is.

        [Fact]
        public void ANewCharacterKeepsTheDungeonsAndNothingElse()
        {
            var book = new CompendiumBook();
            Assert.True(book.Keeps(CompendiumBook.Dungeons));
            Assert.False(book.Keeps("rock4_copper"));
            Assert.Equal(new[] { CompendiumBook.Dungeons }, book.Kept);
            Assert.DoesNotContain("kept", book.Save());
        }

        [Fact]
        public void AKindKeptOrTakenOffIsKeptWithTheCharacter()
        {
            var book = new CompendiumBook();
            book.Save();
            var version = book.KeptVersion;
            book.Keep("rock4_copper", true);
            book.Keep("loc:Crypt2+loc:Crypt3", true);
            Assert.True(book.KeptVersion > version);
            Assert.True(book.Changed);
            Assert.Equal(new[] { CompendiumBook.Dungeons, "loc:Crypt2+loc:Crypt3", "rock4_copper" }, book.Kept);

            var back = CompendiumBook.Load(book.Save());
            Assert.Equal(book.Kept, back.Kept);

            // Taking the dungeons off is kept too; putting them back writes nothing again.
            back.Keep(CompendiumBook.Dungeons, false);
            Assert.Contains("unkept\tdungeons", back.Save());
            var off = CompendiumBook.Load(back.Save());
            Assert.False(off.Keeps(CompendiumBook.Dungeons));
            Assert.Equal(new[] { "loc:Crypt2+loc:Crypt3", "rock4_copper" }, off.Kept);
            off.Keep(CompendiumBook.Dungeons, true);
            Assert.DoesNotContain("unkept", off.Save());

            // Keeping what is kept, or taking off what is not, changes nothing.
            var same = off.KeptVersion;
            off.Keep("rock4_copper", true);
            off.Keep("nothing", false);
            Assert.Equal(same, off.KeptVersion);
        }

        [Fact]
        public void KeepingAllKindsTakesTheirOwnLinesOffSoNothingStandsTwice()
        {
            // The skulls of Petrified Bone kept alone, then the ribs and the skulls kept as one line.
            var book = new CompendiumBook();
            book.Keep("rock4_copper", true);
            book.Keep("giant_skull", true);
            var skullMark = book.MarkOf("giant_skull");
            var both = MapShowing.AllKindsKey(new[] { "giant_ribs", "giant_skull" });
            book.Keep(both, true);
            Assert.True(book.Keeps(both));
            Assert.False(book.Keeps("giant_skull"));
            // The line keeps the mark the skulls stood in, so they keep their look.
            Assert.Equal(skullMark, book.MarkOf(both));
            // A kind of no other line stays kept.
            Assert.True(book.Keeps("rock4_copper"));
            // Kept again alone, the skulls stand on their own beside the line, as the player chose.
            book.Keep("giant_skull", true);
            Assert.True(book.Keeps("giant_skull") && book.Keeps(both));
            // Taking the line off brings no kind back.
            book.Keep("giant_skull", false);
            book.Keep(both, false);
            Assert.Equal(new[] { CompendiumBook.Dungeons, "rock4_copper" }, book.Kept);

            // A lower mark freed meanwhile goes to no one: the line keeps the skulls' own.
            var marked = new CompendiumBook();
            marked.Keep("rock4_copper", true);
            marked.Keep("giant_skull", true);
            var own = marked.MarkOf("giant_skull");
            marked.Keep("rock4_copper", false);
            marked.Keep(both, true);
            Assert.Equal(own, marked.MarkOf(both));
            Assert.NotEqual(marked.FreeMark(), own);
        }

        [Fact]
        public void TakingAllOffLeavesNothingKept()
        {
            var book = new CompendiumBook();
            book.Keep("rock4_copper", true);
            book.TakeAllOff();
            Assert.Empty(book.Kept);
            Assert.False(book.Keeps(CompendiumBook.Dungeons));
        }

        [Fact]
        public void ALineThatCannotBeReadIsPassedOver()
        {
            var odd = CompendiumBook.Load("kept\nunkept\tsomething\nkept\t\n");
            Assert.Equal(new[] { CompendiumBook.Dungeons }, odd.Kept);
        }

        [Fact]
        public void TheDungeonMarksSwitchLeftOffTakesTheDungeonsOffTheMapForTheFirstBookRead()
        {
            // An older config's Dungeon marks switched off: the first character's book read keeps them off, the switch then gone from the file;
            // a book that could not be read takes nothing, so the switch waits for the next.
            Assert.True(KeptMigration.TakeDungeonsOff(wasOff: true, bookRead: true));
            Assert.False(KeptMigration.TakeDungeonsOff(wasOff: true, bookRead: false));
            Assert.False(KeptMigration.TakeDungeonsOff(wasOff: false, bookRead: true));
            var settings = File.ReadAllText(Path.Combine(ScrySource.Root(), "src", "Core", "Settings.cs"));
            Assert.Contains("DungeonMarksTaken", settings);
        }

        [Fact]
        public void TheMarksStandingAreTheNearestWhereThereAreTooMany()
        {
            var marks = new List<(int X, int Z)> { (100, 0), (10, 0), (50, 0), (-20, 0), (0, 300) };
            Assert.Equal(new[] { (10, 0), (-20, 0), (50, 0) }, SeenOnMap.NearestOf(marks, 0f, 0f, 3));
            Assert.Equal(marks.Count, SeenOnMap.NearestOf(marks, 0f, 0f, 10).Count);
            Assert.True(SeenOnMap.MostKept >= 1000);
        }

        [Fact]
        public void TheListAndTheStripSayWhatIsKept()
        {
            Assert.Equal("On the map", SpotWords.KeptButton(0));
            Assert.Equal("On the map  4", SpotWords.KeptButton(4));
            Assert.Equal("Keep on the map", SpotWords.Keep);
            // The page's own button keeps its kind without showing it; its tip says what each way does.
            Assert.Contains("every time", SpotWords.KeepTip(kept: false));
            Assert.Contains("takes it off", SpotWords.KeepTip(kept: true));
            // A kind kept with others says by which line, as its own button cannot take it off alone.
            Assert.Equal("Kept on the map with Dungeons, every kind; the title row's On the map list takes it off", SpotWords.KeptWith(SpotWords.DungeonsRow));
            Assert.Equal("and 3 more kept; take some off to see them", SpotWords.KeptMore(3));
            // Both buttons that keep a kind are named where the list is empty and under the Map section.
            Assert.Contains("on a page or on the map's strip", SpotWords.NothingKept);
            Assert.Contains("on a page or on the map's strip", SettingsWords.ShowOnMapIntro);
            Assert.Equal("Dungeons, every kind", SpotWords.DungeonsRow);
            Assert.Equal("6 you saw", SpotWords.KeptCount(6, 0));
            Assert.Equal("2 you saw · 3 squares", SpotWords.KeptCount(2, 3));
            Assert.Equal("1 square", SpotWords.KeptCount(0, 1));
            Assert.Equal("none seen yet", SpotWords.KeptCount(0, 0));
            Assert.Equal("11 marked · 4 squares", SpotWords.DungeonsCount(11, 4));
            Assert.Equal("1 marked", SpotWords.DungeonsCount(1, 0));
            Assert.Equal("The nearest 2,000 of 3,150 stand; the farthest are left out", SpotWords.Capped(2000, 3150));
        }

        [Fact]
        public void PointingAtAMarkSaysWhatItIs()
        {
            Assert.Equal("Copper deposit\nyou saw it here · 120 m north-east", SpotWords.DotHover("Copper deposit", 120f, "north-east"));
            Assert.Equal("Copper deposit\nyou saw it here, beside you", SpotWords.DotHover("Copper deposit", 2f, "north"));
            Assert.Equal("Somewhere in this square\n2 Burial Chambers you never came near\nin ground you explored",
                SpotWords.SquareHover(new[] { ("Burial Chambers", 2) }, explored: true, shared: false));
            Assert.Equal("Somewhere in this square\n1 Burial Chambers, 1 Troll Cave you never came near\nin ground you explored or read on a map",
                SpotWords.SquareHover(new[] { ("Burial Chambers", 1), ("Troll Cave", 1) }, explored: true, shared: true));
            Assert.EndsWith("on a map you read", SpotWords.SquareHover(new[] { ("Troll Cave", 1) }, explored: false, shared: true));
        }
    }
}
