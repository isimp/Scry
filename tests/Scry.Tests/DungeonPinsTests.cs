using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DungeonPinsTests
    {
        // A source of ground, the ground you explored or ground from shared maps, has a pin at the
        // end of its row: on, and while the row shows anything, each dungeon lying in that ground
        // gets a mark of its own on Valheim's big map. Below With insides it marks the entrance
        // alone; with insides, one walked on this server is marked hollow, showing what was walked,
        // and one nobody walked still marks only its entrance. A dungeon walked by you is marked as
        // before, whatever the pins. The pins are kept with the character, off until turned on.

        private static DungeonMap Map(string location, params int[] walked)
        {
            var map = new DungeonMap { Location = location, Rooms = Enumerable.Range(0, 4).Select(i => (i, new Vec3(i, 0f, 0f), new Vec3(0f, 0f, 0f))).ToList(), Entrance = new Vec3(1f, 2f, 3f) };
            foreach (var room in walked) map.Walked.Add(room);
            return map;
        }

        /// <summary>A dungeon whose area never loaded: its entrance known, no rooms.</summary>
        private static DungeonMap Entrance(string location) =>
            new DungeonMap { Location = location, Rooms = new List<(int, Vec3, Vec3)>(), Entrance = new Vec3(4f, 5f, 6f) };

        private static readonly (string, DungeonMap)[] None = new (string, DungeonMap)[0];

        private static CompendiumBook Pinned(SourceLevel level, params WorldSource[] sources)
        {
            var book = new CompendiumBook();
            foreach (var source in sources)
            {
                book.Show(source, level);
                book.MarkDungeons(source, true);
            }
            return book;
        }

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
        public void APinMarksAtEveryLevelItsRowShowsAnything()
        {
            var book = new CompendiumBook();
            book.MarkDungeons(WorldSource.SharedMaps, true);
            Assert.False(book.MarksDungeonsNow(WorldSource.SharedMaps));
            foreach (var level in new[] { SourceLevel.Seen, SourceLevel.Known, SourceLevel.WithInsides })
            {
                book.Show(WorldSource.SharedMaps, level);
                Assert.True(book.MarksDungeonsNow(WorldSource.SharedMaps));
            }
            book.MarkDungeons(WorldSource.SharedMaps, false);
            Assert.False(book.MarksDungeonsNow(WorldSource.SharedMaps));

            // Your own ground shows at least seen, so its pin marks once turned on.
            var own = new CompendiumBook();
            Assert.False(own.MarksDungeonsNow(WorldSource.Explored));
            own.MarkDungeons(WorldSource.Explored, true);
            Assert.True(own.MarksDungeonsNow(WorldSource.Explored));
        }

        [Fact]
        public void ADungeonWalkedByYouIsMarkedWhateverThePins()
        {
            var walked = new[] { ("w:1", Map("Crypt2", 0)) };
            var marks = DungeonMarks.Of(new CompendiumBook(), walked, _ => None);
            Assert.Equal(new[] { "w:1" }, marks.Select(m => m.Id));
            Assert.Equal(MarkLook.Yours, marks[0].Look);
            Assert.Null(marks[0].From);
            // With no character's book read yet, the walked ones alone.
            Assert.Equal(new[] { "w:1" }, DungeonMarks.Of(null, walked, _ => new[] { ("w:2", Map("Crypt2")) }).Select(m => m.Id));
        }

        [Fact]
        public void BelowWithInsidesEveryDungeonInTheGroundIsMarkedByItsEntranceAlone()
        {
            var inGround = new[] { ("w:2", Map("Crypt2", 1, 2)), ("w:3", Entrance("SunkenCrypt4")) };
            foreach (var level in new[] { SourceLevel.Seen, SourceLevel.Known })
            {
                var marks = DungeonMarks.Of(Pinned(level, WorldSource.Explored), None, s => s == WorldSource.Explored ? inGround : None);
                Assert.Equal(new[] { "w:2", "w:3" }, marks.Select(m => m.Id));
                Assert.All(marks, m => Assert.Equal(MarkLook.Entrance, m.Look));
                Assert.All(marks, m => Assert.Equal(WorldSource.Explored, m.From));
            }
        }

        [Fact]
        public void WithInsidesADungeonWalkedOnThisServerIsMarkedHollowOneNobodyWalkedByItsEntrance()
        {
            var inGround = new[] { ("w:2", Map("Crypt2", 1)), ("w:3", Map("Crypt2")), ("w:4", Entrance("SunkenCrypt4")) };
            var marks = DungeonMarks.Of(Pinned(SourceLevel.WithInsides, WorldSource.Explored), None, s => s == WorldSource.Explored ? inGround : None);
            Assert.Equal(new[] { "w:2", "w:3", "w:4" }, marks.Select(m => m.Id));
            Assert.Equal(new[] { MarkLook.Server, MarkLook.Entrance, MarkLook.Entrance }, marks.Select(m => m.Look));
        }

        [Fact]
        public void APinOffOrARowOffMarksNothingOfItsGround()
        {
            var inGround = new[] { ("w:2", Map("Crypt2", 1)) };
            var pinOff = new CompendiumBook();
            pinOff.Show(WorldSource.SharedMaps, SourceLevel.WithInsides);
            Assert.Empty(DungeonMarks.Of(pinOff, None, _ => inGround));
            var rowOff = new CompendiumBook();
            rowOff.MarkDungeons(WorldSource.SharedMaps, true);
            Assert.Equal(SourceLevel.Off, rowOff.Shows(WorldSource.SharedMaps));
            Assert.Empty(DungeonMarks.Of(rowOff, None, _ => inGround));
        }

        [Fact]
        public void EachDungeonIsMarkedOnceWalkedByYouFirstThenByTheFirstGround()
        {
            var book = Pinned(SourceLevel.WithInsides, WorldSource.Explored, WorldSource.SharedMaps);
            var walked = new[] { ("w:1", Map("Crypt2", 0)) };
            var inGround = new Dictionary<WorldSource, (string, DungeonMap)[]>
            {
                [WorldSource.Explored] = new[] { ("w:1", Map("Crypt2", 0, 1)), ("w:2", Map("Crypt2")) },
                [WorldSource.SharedMaps] = new[] { ("w:2", Map("Crypt2", 3)), ("w:3", Map("TrollCave02", 2)) },
            };
            var marks = DungeonMarks.Of(book, walked, s => inGround.TryGetValue(s, out var maps) ? maps : None);
            Assert.Equal(new[] { "w:1", "w:2", "w:3" }, marks.Select(m => m.Id));
            Assert.Equal(new WorldSource?[] { null, WorldSource.Explored, WorldSource.SharedMaps }, marks.Select(m => m.From));
            Assert.Equal(new[] { MarkLook.Yours, MarkLook.Entrance, MarkLook.Server }, marks.Select(m => m.Look));
        }

        [Fact]
        public void TheMarksWordsSayWhoWalkedItAndHowFar()
        {
            Assert.Equal("<size=130%>Burial Chambers</size>\n<size=115%><color=#F5B857>Walked by you: 6 of 18 rooms</color></size>", DungeonMapWords.MarkLabel("Burial Chambers", 6, 18, "F5B857"));
            // One room alone, walked, is no "all".
            Assert.Equal("<size=130%>Tomb</size>\n<size=115%><color=#F5B857>Walked by you: 1 of 1 room</color></size>", DungeonMapWords.MarkLabel("Tomb", 1, 1, "F5B857"));
            Assert.Equal("<size=130%>Sunken Crypt</size>\n<size=115%><color=#F5B857>Walked on this server: all 9 rooms</color></size>", DungeonMapWords.ServerLabel("Sunken Crypt", 9, 9, "F5B857", 0));
            Assert.Equal("<size=130%>Sunken Crypt</size>\n<size=115%><color=#F5B857>Walked on this server: 4 of 9 rooms</color></size>", DungeonMapWords.ServerLabel("Sunken Crypt", 4, 9, "F5B857", 0));
            Assert.Equal("<size=130%>Troll Cave</size>\n<size=115%><color=#B3B4BD>Nobody on this server walked it</color></size>", DungeonMapWords.EntranceLabel("Troll Cave", false, "B3B4BD"));
            Assert.Equal("<size=130%>Troll Cave</size>\n<size=115%><color=#B3B4BD>Walked on this server; With insides shows what was walked</color></size>", DungeonMapWords.EntranceLabel("Troll Cave", true, "B3B4BD"));
        }

        [Fact]
        public void ThePinsTipSaysItMarksEveryEntranceAndWhatWithInsidesAdds()
        {
            Assert.Equal("Mark dungeons on the big map", CompendiumWords.PinName);
            Assert.Equal("Marks every dungeon entrance in this ground on Valheim's big map. With insides, each one walked on this server shows what was walked. A click opens it in Scry. Dungeons you walked are marked anyway.\nOff.",
                CompendiumWords.PinTip(on: false));
            Assert.EndsWith("\nOn.", CompendiumWords.PinTip(on: true));
        }
    }
}
