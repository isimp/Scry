using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DungeonMarksTests
    {
        // A dungeon is marked on Valheim's big map where its place stands as Show on map would
        // show it: where you walked it, or where your character saw its entrance. One you walked
        // is marked as yours, ticked once walked whole; one you only saw by its entrance, hollow
        // where others on this server walked it and Ground you explored shows With insides. A
        // dungeon lying in ground you never came near gets no mark of its own: its page's Show on
        // map shades its square. No switch of the character's picks which grounds are marked.

        private static DungeonMap Map(string location, params int[] walked)
        {
            var map = new DungeonMap { Location = location, Rooms = Enumerable.Range(0, 4).Select(i => (i, new Vec3(i, 0f, 0f), new Vec3(0f, 0f, 0f))).ToList(), Entrance = new Vec3(1f, 2f, 3f) };
            foreach (var room in walked) map.Walked.Add(room);
            return map;
        }

        /// <summary>A dungeon of one room, walked.</summary>
        private static DungeonMap OneRoom(string location)
        {
            var map = new DungeonMap { Location = location, Rooms = new List<(int, Vec3, Vec3)> { (0, new Vec3(0f, 0f, 0f), new Vec3(0f, 0f, 0f)) }, Entrance = new Vec3(1f, 2f, 3f) };
            map.Walked.Add(0);
            return map;
        }

        private static readonly (string, DungeonMap)[] NoneWalked = new (string, DungeonMap)[0];
        private static readonly (string, string, int, int)[] NoneSeen = new (string, string, int, int)[0];

        private static CompendiumBook Explored(SourceLevel level)
        {
            var book = new CompendiumBook();
            book.Show(WorldSource.Explored, level);
            return book;
        }

        [Fact]
        public void ADungeonYouWalkedWholeIsMarkedWithATick()
        {
            // Every room walked, end caps and dividers left out, stands out from one walked in part.
            var walked = new[] { ("w:1", Map("Crypt2", 0, 1, 2, 3)), ("w:2", Map("Crypt2", 0)) };
            var marks = DungeonMarks.Of(new CompendiumBook(), walked, NoneSeen, _ => null);
            Assert.Equal(new[] { MarkLook.Whole, MarkLook.Yours }, marks.Select(m => m.Look));
            // A dungeon of one room walked, too.
            Assert.Equal(MarkLook.Whole, DungeonMarks.Of(null, new[] { ("w:3", OneRoom("Crypt2")) }, NoneSeen, _ => null)[0].Look);
            // Where the rooms' kinds cannot be read, none is told whole.
            Assert.Equal(MarkLook.Yours, DungeonMarks.Of(null, walked, NoneSeen)[0].Look);
        }

        [Fact]
        public void AnEntranceYouSawIsMarkedWhereYouSawIt()
        {
            var seen = new[] { ("w:5", "TrollCave02", 120, -40) };
            var marks = DungeonMarks.Of(new CompendiumBook(), NoneWalked, seen);
            var mark = Assert.Single(marks);
            Assert.Equal("w:5", mark.Id);
            Assert.Equal(MarkLook.Entrance, mark.Look);
            Assert.Equal("TrollCave02", mark.Map.Location);
            Assert.Equal(120f, mark.Map.Entrance.X);
            Assert.Equal(-40f, mark.Map.Entrance.Z);
            // With no character's book read yet, the same.
            Assert.Equal(MarkLook.Entrance, Assert.Single(DungeonMarks.Of(null, NoneWalked, seen)).Look);
        }

        [Fact]
        public void ADungeonYouWalkedAndSawIsMarkedOnceAsYours()
        {
            var walked = new[] { ("w:1", Map("Crypt2", 0)) };
            var seen = new[] { ("w:1", "Crypt2", 1, 3), ("w:2", "Crypt2", 300, 300) };
            var marks = DungeonMarks.Of(new CompendiumBook(), walked, seen);
            Assert.Equal(new[] { "w:1", "w:2" }, marks.Select(m => m.Id));
            Assert.Equal(new[] { MarkLook.Yours, MarkLook.Entrance }, marks.Select(m => m.Look));
        }

        [Fact]
        public void ASeenEntranceWalkedOnThisServerIsHollowWhereYourGroundShowsWithInsides()
        {
            // The ground you saw it in is ground you explored: its row decides whether others' walks show.
            var seen = new[] { ("w:1", "Crypt2", 10, 10), ("w:2", "Crypt2", 20, 20), ("w:3", "Crypt2", 30, 30) };
            var records = new Dictionary<string, DungeonMap> { ["w:1"] = Map("Crypt2", 0, 1, 2, 3), ["w:2"] = Map("Crypt2", 1) };
            records["w:2"].Generator = new Vec3(5f, 5000f, 6f);
            DungeonMap Record(string id) => records.TryGetValue(id, out var map) ? map : null;
            var insides = DungeonMarks.Of(Explored(SourceLevel.WithInsides), NoneWalked, seen, _ => null, Record);
            Assert.Equal(new[] { MarkLook.ServerWhole, MarkLook.Server, MarkLook.Entrance }, insides.Select(m => m.Look));
            // Its mark stands where it was seen, telling the rooms walked.
            Assert.Equal(20f, insides[1].Map.Entrance.X);
            Assert.Equal(new[] { 1 }, insides[1].Map.Walked);
            Assert.Equal(4, insides[1].Map.Rooms.Count);
            // The rooms stand where the server's record lays them out.
            Assert.Equal(records["w:2"].Generator, insides[1].Map.Generator);
            // Below With insides, each by its entrance alone, its words still knowing others walked it, no rooms to open.
            foreach (var level in new[] { SourceLevel.Seen, SourceLevel.Known })
            {
                var below = DungeonMarks.Of(Explored(level), NoneWalked, seen, _ => null, Record);
                Assert.All(below, m => Assert.Equal(MarkLook.Entrance, m.Look));
                Assert.All(below, m => Assert.Empty(m.Map.Rooms));
                Assert.Equal(new[] { 1 }, below[1].Map.Walked);
            }
        }

        [Fact]
        public void TheDungeonsSeenAreTheDungeonPlacesYourSightKeptEachByItsEntrance()
        {
            var spots = new Dictionary<string, (int, int)[]>
            {
                ["loc:Crypt2"] = new[] { (10, 20), (-5, 7) },
                ["loc:TrollCave02"] = new[] { (100, 100) },
                ["loc:Runestone_Meadows"] = new[] { (1, 1) },
            };
            var seen = DungeonMarks.Seen("w", new[] { "Crypt2", "TrollCave02" }, kind => spots.TryGetValue(kind, out var at) ? at : new (int, int)[0]).ToList();
            Assert.Equal(new[] { "Crypt2", "Crypt2", "TrollCave02" }, seen.Select(s => s.Location));
            Assert.Equal(DungeonMaps.EntranceIdOf("w", 10f, 20f), seen[0].Id);
            Assert.Equal((-5, 7), (seen[1].X, seen[1].Z));
        }

        [Fact]
        public void TheBookKeepsNoPinsAndAnOlderBooksPinsAreLeftBehind()
        {
            var book = CompendiumBook.Load("marks\tExplored\nmarks\tSharedMaps\n");
            Assert.DoesNotContain("marks", book.Save());
        }

        [Fact]
        public void TheMarksWordsSayWhoWalkedItAndHowFar()
        {
            Assert.Equal("<size=130%>Burial Chambers</size>\n<size=115%><color=#F5B857>Walked by you: 6 of 18 rooms</color></size>", DungeonMapWords.MarkLabel("Burial Chambers", 6, 18, "F5B857"));
            // One room alone, walked, is no "all".
            Assert.Equal("<size=130%>Tomb</size>\n<size=115%><color=#F5B857>Walked by you: 1 of 1 room</color></size>", DungeonMapWords.MarkLabel("Tomb", 1, 1, "F5B857"));
            Assert.Equal("<size=130%>Sunken Crypt</size>\n<size=115%><color=#F5B857>Walked whole on this server: all 9 rooms</color></size>", DungeonMapWords.ServerLabel("Sunken Crypt", 9, 9, "F5B857", 0));
            Assert.Equal("<size=130%>Sunken Crypt</size>\n<size=115%><color=#F5B857>Walked on this server: 4 of 9 rooms</color></size>", DungeonMapWords.ServerLabel("Sunken Crypt", 4, 9, "F5B857", 0));
            Assert.Equal("<size=130%>Troll Cave</size>\n<size=115%><color=#B3B4BD>Nobody on this server walked it</color></size>", DungeonMapWords.EntranceLabel("Troll Cave", false, "B3B4BD"));
            Assert.Equal("<size=130%>Troll Cave</size>\n<size=115%><color=#B3B4BD>Walked on this server; With insides shows what was walked</color></size>", DungeonMapWords.EntranceLabel("Troll Cave", true, "B3B4BD"));
        }
    }
}
