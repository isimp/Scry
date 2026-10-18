using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class DungeonTrailTests
    {
        // Inside a dungeon's example, a breadcrumb in the stage's top left says where the view is:
        // the dungeon, the level open or the one the room gone into is on, and the room, the last
        // the one the view is at.

        [Fact]
        public void TheWholeDungeonIsTheDungeonAlone()
        {
            var trail = DungeonTrail.Of("Howling Cavern", level: null, room: null);
            var part = Assert.Single(trail);
            Assert.Equal(TrailPart.Dungeon, part.Part);
            Assert.Equal("Howling Cavern", part.Text);
            Assert.True(part.Current);
        }

        [Fact]
        public void ALevelOpenFollowsTheDungeonAndIsWhereTheViewIs()
        {
            var trail = DungeonTrail.Of("Howling Cavern", level: "Level -17 m", room: null);
            Assert.Equal(new[] { TrailPart.Dungeon, TrailPart.Level }, trail.Select(t => t.Part).ToArray());
            Assert.Equal(new[] { false, true }, trail.Select(t => t.Current).ToArray());
            Assert.Equal("Level -17 m", trail[1].Text);
        }

        [Fact]
        public void ARoomGoneIntoFollowsItsLevel()
        {
            var trail = DungeonTrail.Of("Howling Cavern", level: "Level -35 m", room: "Shrine 03 hildir");
            Assert.Equal(new[] { TrailPart.Dungeon, TrailPart.Level, TrailPart.Room }, trail.Select(t => t.Part).ToArray());
            Assert.Equal(new[] { false, false, true }, trail.Select(t => t.Current).ToArray());
            Assert.Equal("Shrine 03 hildir", trail[2].Text);
        }

        [Fact]
        public void ARoomOnNoLevelFollowsTheDungeon()
        {
            var trail = DungeonTrail.Of("Howling Cavern", level: null, room: "Corridor");
            Assert.Equal(new[] { TrailPart.Dungeon, TrailPart.Room }, trail.Select(t => t.Part).ToArray());
            Assert.True(trail[1].Current);
        }

        [Fact]
        public void EachPartSaysWhatAClickOnItDoes()
        {
            Assert.Equal("The whole dungeon, 31 rooms: click to see it all, the roof on", PlanBoxWords.TrailDungeonTip(31));
            Assert.Equal("The whole dungeon, 1 room: click to see it all, the roof on", PlanBoxWords.TrailDungeonTip(1));
            Assert.Equal("Click to come back out of the room to this level, as Esc does", PlanBoxWords.TrailLevelTip(inRoom: true));
            Assert.Equal("Click to frame the level again", PlanBoxWords.TrailLevelTip(inRoom: false));
            Assert.Equal("Click to frame the room again", PlanBoxWords.TrailRoomTip);
            // The deepest is the room furthest in, not one of the lowest.
            Assert.Equal("Goes into the deepest room, the most rooms in from the entrance", PlanBoxWords.DeepestTip);
            Assert.Equal("Goes into the entrance or the deepest room", PlanBoxWords.TrailMenuTip(switches: false));
            Assert.Equal("Goes into the entrance or the deepest room, or switches between this dungeon and an example", PlanBoxWords.TrailMenuTip(switches: true));
        }

        // The dungeon the player is in, or at, is shown as it stands in their world, a tag at the
        // head of the breadcrumb saying so; the menu beside the dungeon switches to an example of
        // it and back.

        [Fact]
        public void TheMenuGoesIntoTheEntranceOrTheDeepestRoomAndSwitchesToAnExample()
        {
            Assert.Equal(new[] { TrailMenuRow.Entrance, TrailMenuRow.Deepest, TrailMenuRow.ShowExample }, DungeonTrail.MenuRows(showsWorlds: true, worldsAtHand: true));
            Assert.Equal(new[] { TrailMenuRow.Entrance, TrailMenuRow.Deepest, TrailMenuRow.ShowExample }, DungeonTrail.MenuRows(showsWorlds: true, worldsAtHand: false));
        }

        [Fact]
        public void AnExampleOfTheDungeonThePlayerIsAtSwitchesBackToIt()
        {
            Assert.Equal(new[] { TrailMenuRow.Entrance, TrailMenuRow.Deepest, TrailMenuRow.ShowThisDungeon }, DungeonTrail.MenuRows(showsWorlds: false, worldsAtHand: true));
        }

        [Fact]
        public void AnExampleOfADungeonElsewhereOnlyGoesIntoItsRooms()
        {
            Assert.Equal(new[] { TrailMenuRow.Entrance, TrailMenuRow.Deepest }, DungeonTrail.MenuRows(showsWorlds: false, worldsAtHand: false));
        }

        [Fact]
        public void TheTagAndTheSwitchSayWhatTheyShow()
        {
            Assert.Equal("This dungeon", PlanBoxWords.ThisDungeon);
            Assert.Equal("The rooms as they stand in your world; the menu beside the dungeon shows an example instead", PlanBoxWords.ThisDungeonTip);
            Assert.Equal("Show an example", PlanBoxWords.ShowExample);
            Assert.Equal("Lays out an example in place of this dungeon, as the game might in another world", PlanBoxWords.ShowExampleTip);
            Assert.Equal("Show this dungeon", PlanBoxWords.ShowThisDungeon);
            Assert.Equal("Shows this dungeon as it stands in your world, in place of the example", PlanBoxWords.ShowThisDungeonTip);
        }
    }
}
