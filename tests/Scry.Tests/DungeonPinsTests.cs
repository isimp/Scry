using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DungeonPinsTests
    {
        // A source of ground, the ground you explored or ground from shared maps, has a pin at the
        // end of its row: on, and while the row shows With insides, each dungeon lying in that
        // ground whose area has loaded gets a mark of its own on Valheim's big map, hollow where it
        // was not walked, naming the ground it shows from. A walked dungeon is marked as before,
        // whatever the pins. The pins are kept with the character, off until turned on.

        private static DungeonMap Map(string location) =>
            new DungeonMap { Location = location, Rooms = new List<(int, Vec3, Vec3)>(), Entrance = new Vec3(1f, 2f, 3f) };

        [Fact]
        public void OnlyASourceOfGroundHasAPin()
        {
            Assert.True(WorldSources.HasGround(WorldSource.Explored));
            Assert.True(WorldSources.HasGround(WorldSource.SharedMaps));
            Assert.False(WorldSources.HasGround(WorldSource.Marked));
            Assert.False(WorldSources.HasGround(WorldSource.OnEveryMap));
        }

        [Fact]
        public void APinIsOffUntilTurnedOnAndIsKeptWithTheCharacter()
        {
            var book = new CompendiumBook();
            Assert.False(book.MarksDungeons(WorldSource.Explored));
            var version = book.SourcesVersion;
            book.Save();
            book.MarkDungeons(WorldSource.Explored, true);
            Assert.True(book.MarksDungeons(WorldSource.Explored));
            Assert.True(book.SourcesVersion > version);
            Assert.True(book.Changed);
            Assert.Contains("marks\tExplored", book.Save());

            var back = CompendiumBook.Load(book.Save());
            Assert.True(back.MarksDungeons(WorldSource.Explored));
            Assert.False(back.MarksDungeons(WorldSource.SharedMaps));

            // A source without ground keeps no pin; one turned off writes nothing.
            book.MarkDungeons(WorldSource.Marked, true);
            Assert.False(book.MarksDungeons(WorldSource.Marked));
            book.MarkDungeons(WorldSource.Explored, false);
            Assert.DoesNotContain("marks", book.Save());

            // A line that cannot be read is passed over.
            var odd = CompendiumBook.Load("marks\tNowhere\nmarks\tMarked\nmarks");
            Assert.True(WorldSources.All.All(s => !odd.MarksDungeons(s)));
        }

        [Fact]
        public void APinMarksOnlyWhileItsRowShowsWithInsides()
        {
            var book = new CompendiumBook();
            book.MarkDungeons(WorldSource.Explored, true);
            Assert.False(book.MarksDungeonsNow(WorldSource.Explored));
            book.Show(WorldSource.Explored, SourceLevel.Known);
            Assert.False(book.MarksDungeonsNow(WorldSource.Explored));
            book.Show(WorldSource.Explored, SourceLevel.WithInsides);
            Assert.True(book.MarksDungeonsNow(WorldSource.Explored));
            book.MarkDungeons(WorldSource.Explored, false);
            Assert.False(book.MarksDungeonsNow(WorldSource.Explored));
        }

        [Fact]
        public void AWalkedDungeonIsMarkedWhateverThePins()
        {
            var book = new CompendiumBook();
            var walked = new[] { ("w:1", Map("Crypt2")) };
            var marks = DungeonMarks.Of(book, walked, _ => Enumerable.Empty<(string, DungeonMap)>());
            Assert.Equal(new[] { "w:1" }, marks.Select(m => m.Id));
            Assert.Null(marks[0].From);
            // With no character's book read yet, the walked ones alone.
            Assert.Equal(new[] { "w:1" }, DungeonMarks.Of(null, walked, _ => new[] { ("w:2", Map("Crypt2")) }).Select(m => m.Id));
        }

        [Fact]
        public void ADungeonInGroundShownWithInsidesIsMarkedNamingItsGround()
        {
            var book = new CompendiumBook();
            book.Show(WorldSource.Explored, SourceLevel.WithInsides);
            book.MarkDungeons(WorldSource.Explored, true);
            var inGround = new Dictionary<WorldSource, (string, DungeonMap)[]>
            {
                [WorldSource.Explored] = new[] { ("w:2", Map("Crypt2")), ("w:3", Map("SunkenCrypt4")) },
            };
            var marks = DungeonMarks.Of(book, new (string, DungeonMap)[0], s => inGround.TryGetValue(s, out var maps) ? maps : new (string, DungeonMap)[0]);
            Assert.Equal(new[] { "w:2", "w:3" }, marks.Select(m => m.Id));
            Assert.All(marks, m => Assert.Equal(WorldSource.Explored, m.From));
        }

        [Fact]
        public void APinOffOrARowBelowWithInsidesMarksNothingOfItsGround()
        {
            var inGround = new[] { ("w:2", Map("Crypt2")) };
            var pinOff = new CompendiumBook();
            pinOff.Show(WorldSource.Explored, SourceLevel.WithInsides);
            Assert.Empty(DungeonMarks.Of(pinOff, new (string, DungeonMap)[0], _ => inGround));
            var rowKnown = new CompendiumBook();
            rowKnown.Show(WorldSource.Explored, SourceLevel.Known);
            rowKnown.MarkDungeons(WorldSource.Explored, true);
            Assert.Empty(DungeonMarks.Of(rowKnown, new (string, DungeonMap)[0], _ => inGround));
        }

        [Fact]
        public void EachDungeonIsMarkedOnceWalkedFirstThenByTheFirstGround()
        {
            var book = new CompendiumBook();
            foreach (var source in new[] { WorldSource.Explored, WorldSource.SharedMaps })
            {
                book.Show(source, SourceLevel.WithInsides);
                book.MarkDungeons(source, true);
            }
            var walked = new[] { ("w:1", Map("Crypt2")) };
            var inGround = new Dictionary<WorldSource, (string, DungeonMap)[]>
            {
                [WorldSource.Explored] = new[] { ("w:1", Map("Crypt2")), ("w:2", Map("Crypt2")) },
                [WorldSource.SharedMaps] = new[] { ("w:2", Map("Crypt2")), ("w:3", Map("TrollCave02")) },
            };
            var marks = DungeonMarks.Of(book, walked, s => inGround.TryGetValue(s, out var maps) ? maps : new (string, DungeonMap)[0]);
            Assert.Equal(new[] { "w:1", "w:2", "w:3" }, marks.Select(m => m.Id));
            Assert.Equal(new WorldSource?[] { null, WorldSource.Explored, WorldSource.SharedMaps }, marks.Select(m => m.From));
        }

        [Fact]
        public void TheMarksWordsSayWhereTheyShowFrom()
        {
            Assert.Equal("Burial Chambers\n<size=80%><color=#F5B857>not walked · with insides from Ground you explored</color></size>",
                DungeonMapWords.InsidesLabel("Burial Chambers", WorldSource.Explored, "F5B857", -1));
            Assert.Equal("Mark dungeons on the big map", CompendiumWords.PinName);
            Assert.Equal("Marks on Valheim's big map each dungeon in this ground whose area has loaded, hollow where you have not walked it; a click on one shows its own rooms. Dungeons you walked are marked anyway.\nOff. It works while this row shows With insides.",
                CompendiumWords.PinTip(on: false, insides: false));
            Assert.EndsWith("\nOn.", CompendiumWords.PinTip(on: true, insides: true));
            Assert.EndsWith("\nOn, but nothing is marked until this row shows With insides.", CompendiumWords.PinTip(on: true, insides: false));
        }
    }
}
