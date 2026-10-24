using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    /// <summary>
    /// A dungeon walked by you and others on this server: the rooms either walked count together,
    /// so one walked whole between you is told as done on its mark, in its words and on its page,
    /// whatever the compendium shows of the server's rooms.
    /// </summary>
    public class WalkedTogetherTests
    {
        private static readonly IEnumerable<(string, DungeonMap)> None = new (string, DungeonMap)[0];

        /// <summary>A dungeon of nine rooms, a hash each, those given walked.</summary>
        private static DungeonMap Map(params int[] walked)
        {
            var map = new DungeonMap { Location = "TrollCave02", Rooms = Enumerable.Range(0, 9).Select(i => (i, new Vec3(i, 0f, 0f), new Vec3(0f, 0f, 0f))).ToList(), Entrance = new Vec3(1f, 2f, 3f) };
            foreach (var room in walked) map.Walked.Add(room);
            return map;
        }

        [Fact]
        public void TheRoomsYouAndOthersWalkedCountTogether()
        {
            // You walked five, your mate the other four: nine of nine, five of them by you.
            var yours = Map(0, 1, 2, 3, 4);
            var server = Map(0, 1, 5, 6, 7, 8);
            Assert.Equal((9, 9, 5), DungeonMaps.WalkedTogether(yours, server, _ => null));
            // No record from the server: yours alone.
            Assert.Equal((5, 9, 5), DungeonMaps.WalkedTogether(yours, null, _ => null));
            // A record laid out otherwise is not joined room by room: whichever walked more.
            var other = new DungeonMap { Location = "TrollCave02", Rooms = Enumerable.Range(0, 9).Select(i => (100 + i, new Vec3(i, 0f, 0f), new Vec3(0f, 0f, 0f))).ToList() };
            foreach (var room in new[] { 0, 1, 2, 3, 4, 5, 6 }) other.Walked.Add(room);
            Assert.Equal((7, 9, 5), DungeonMaps.WalkedTogether(yours, other, _ => null));
        }

        [Fact]
        public void ADungeonWalkedWholeTogetherIsTickedYoursWithOthersHollow()
        {
            var walked = new[] { ("w:1", Map(0, 1, 2, 3, 4)) };
            var records = new Dictionary<string, DungeonMap> { ["w:1"] = Map(5, 6, 7, 8) };
            var marks = DungeonMarks.Of(new CompendiumBook(), walked, _ => None, _ => null, id => records.TryGetValue(id, out var map) ? map : null);
            Assert.Equal(MarkLook.Whole, marks[0].Look);
            Assert.Same(records["w:1"], marks[0].Server);
            // Not yet whole between you: yours, as before.
            records["w:1"] = Map(5, 6);
            Assert.Equal(MarkLook.Yours, DungeonMarks.Of(new CompendiumBook(), walked, _ => None, _ => null, id => records[id])[0].Look);

            // One only others walked, shown with insides by a ground's pin: hollow, ticked once whole.
            var book = new CompendiumBook();
            book.MarkDungeons(WorldSource.Explored, true);
            book.Show(WorldSource.Explored, SourceLevel.WithInsides);
            var inGround = new[] { ("g:1", Map(0, 1, 2, 3, 4, 5, 6, 7, 8)), ("g:2", Map(0, 1)) };
            var ground = DungeonMarks.Of(book, None, s => s == WorldSource.Explored ? inGround : None, _ => null);
            Assert.Equal(new[] { MarkLook.ServerWhole, MarkLook.Server }, ground.Select(m => m.Look));
        }

        [Fact]
        public void ItsWordsSayItWasWalkedWholeOnThisServerAndHowMuchByYou()
        {
            Assert.Equal("<size=130%>Troll Cave</size>\n<size=115%><color=#F5B857>Walked whole on this server: all 9 rooms\n5 of them by you</color></size>",
                DungeonMapWords.TogetherLabel("Troll Cave", 9, 9, 5, "F5B857"));
            Assert.Equal("<size=130%>Troll Cave</size>\n<size=115%><color=#F5B857>Walked on this server: 7 of 9 rooms\n5 of them by you</color></size>",
                DungeonMapWords.TogetherLabel("Troll Cave", 7, 9, 5, "F5B857"));
            Assert.Equal("<size=130%>Troll Cave</size>\n<size=115%><color=#F5B857>Walked on this server: 7 of 9 rooms\n1 of them by you</color></size>",
                DungeonMapWords.TogetherLabel("Troll Cave", 7, 9, 1, "F5B857"));
            // Its page's line: whole on this server, then your share.
            Assert.Equal("Where you stand · walked whole on this server · you 5 of 9 rooms",
                DungeonMapWords.Line(new WalkLine { Found = true, Yours = (5, 9), Server = (9, 9, 0), Together = (9, 9) }));
            Assert.Equal("Where you stand · you 5 of 9 rooms · on this server 7 of 9 rooms",
                DungeonMapWords.Line(new WalkLine { Found = true, Yours = (5, 9), Server = (7, 9, 0), Together = (7, 9) }));
        }

        [Fact]
        public void YourOwnLineCountsTheServersRoomsAndItsOthersStayAsTheSourcesShowThem()
        {
            var yours = new[] { ("w:1", Map(0, 1, 2, 3, 4), 5, 9) };
            var server = new[] { ("w:1", Map(5, 6, 7, 8), 4, 9) };
            var lines = WalkLines.Of(yours, server, _ => null, new Vec3(0f, 0f, 0f), (mine, theirs) => DungeonMaps.WalkedTogether(mine, theirs, _ => null));
            Assert.Equal((9, 9), lines[0].Together);
            Assert.True(lines[0].WalkedWhole);
            // Your own whole walk is whole too, with or without the server.
            var alone = WalkLines.Of(new[] { ("w:2", Map(0, 1, 2, 3, 4, 5, 6, 7, 8), 9, 9) }, new (string, DungeonMap, int, int)[0], _ => null, new Vec3(0f, 0f, 0f));
            Assert.True(alone[0].WalkedWhole);
            Assert.Null(alone[0].Together);
        }
    }
}
