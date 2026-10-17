using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlanBoxTests
    {
        /// <summary>A room 4 m each way, or as tall as given, centred at a point, turned as placed.</summary>
        private static PlacedRoom Room(float x, float y, float z, string name = "room", float tall = 4f) =>
            new PlacedRoom { Room = new RoomShape { Name = name, Size = new Vec3(4f, tall, 4f) }, Position = new Vec3(x, y, z) };

        // ----- Levels: each a plan from above, stacked by depth, rooms that would cover each other on levels apart -----

        [Fact]
        public void RoomsSteppingDownLessThanTwoStoreysAtATimeShareALevel()
        {
            // The upper caves: grounds stepping down a little at a time, side by side, further in all than two storeys.
            var rooms = new[] { Room(0f, 0f, 0f), Room(10f, 0f, 0f), Room(20f, 0f, 0f), Room(30f, 0f, 0f) };
            var levels = PlanLevels.Of(rooms, new[] { -3.3f, -6.6f, -9.9f, -3.2f });
            Assert.Single(levels.Levels);
            Assert.Equal(-3.2f, levels.Levels[0].Top, 3);
            Assert.Equal(-9.9f, levels.Levels[0].Bottom, 3);
        }

        [Fact]
        public void ARoomMoreThanTwoStoreysUnderTheRestStandsOnALevelOfItsOwnBelowThem()
        {
            var rooms = new[] { Room(0f, 0f, 0f), Room(10f, 0f, 0f), Room(20f, 0f, 0f) };
            var levels = PlanLevels.Of(rooms, new[] { -3f, -20f, -4f });
            Assert.Equal(2, levels.Levels.Count);
            Assert.Equal(new[] { 0, 1, 0 }, rooms.Select(levels.LevelOf).ToArray());
            Assert.Equal(-20f, levels.Levels[1].Top, 3);
        }

        [Fact]
        public void ARoomThatWouldCoverOneOfItsLevelFromAboveStandsOnALevelBelowIt()
        {
            // A shaft: two rooms one over the other, a storey and a half apart, and one beside them.
            var rooms = new[] { Room(0f, 0f, 0f), Room(1f, 0f, 1f), Room(10f, 0f, 0f) };
            var levels = PlanLevels.Of(rooms, new[] { 0f, -4f, -1f });
            Assert.Equal(2, levels.Levels.Count);
            Assert.Equal(new[] { 0, 1, 0 }, rooms.Select(levels.LevelOf).ToArray());
        }

        [Fact]
        public void RoomsWallToWallShareALevel()
        {
            // Each 4 m wide, their walls touching.
            Assert.Single(PlanLevels.Of(new[] { Room(0f, 0f, 0f), Room(4f, 0f, 0f) }, new[] { 0f, -1f }).Levels);
            // And north to south.
            Assert.Single(PlanLevels.Of(new[] { Room(0f, 0f, 0f), Room(0f, 0f, 4f) }, new[] { 0f, -1f }).Levels);
        }

        [Fact]
        public void LevelsStandHighestFirstEachRoomOnOneAndNoRoomOfALevelCoversAnotherFromAbove()
        {
            var rooms = new[] { Room(0f, 0f, 0f), Room(2f, 0f, 3f), Room(1f, 0f, 9f), Room(-2f, 0f, 4f), Room(3f, 0f, -6f), Room(5f, 0f, 1f), Room(-1f, 0f, -3f) };
            var grounds = new[] { 0f, -1f, 0f, -2f, 1f, -0.5f, -2.5f };
            var levels = PlanLevels.Of(rooms, grounds);
            Assert.Equal(rooms.Length, levels.Levels.Sum(l => l.Rooms.Count));
            Assert.All(rooms, r => Assert.Single(levels.Levels, l => l.Rooms.Contains(r)));
            for (var i = 1; i < levels.Levels.Count; i++) Assert.True(levels.Levels[i - 1].Top >= levels.Levels[i].Top);
            foreach (var level in levels.Levels)
            {
                for (var a = 0; a < level.Rooms.Count; a++)
                {
                    for (var b = a + 1; b < level.Rooms.Count; b++) Assert.False(PlanLevels.Covers(level.Rooms[a], level.Rooms[b]), $"{level.Rooms[a].Position.X},{level.Rooms[a].Position.Z} and {level.Rooms[b].Position.X},{level.Rooms[b].Position.Z} share a level");
                }
            }
        }

        // ----- The card of the room gone into and the plan box share the stage's left side, neither covering the other -----

        [Fact]
        public void WithNoRoomGoneIntoThePlanTakesWhatTheStageLeaves()
        {
            Assert.Equal(330f, CornerFit.Plan(700f, 100f, 330f, card: false, scale: 1f), 3);
            Assert.Equal(300f - CornerFit.Bottom - CornerFit.Top - 100f, CornerFit.Plan(300f, 100f, 330f, card: false, scale: 1f), 3);
        }

        // A box whose other rows, its trail and Entrance and Deepest among them, take 110, and 62 without those.
        private const float WholeRows = 110f, LeanRows = 62f;

        [Fact]
        public void OnAStageTallEnoughThePlanBoxKeepsItsTrailAndButtons()
        {
            Assert.Equal(BoxShape.Whole, CornerFit.Shape(600f, WholeRows, LeanRows, card: true, scale: 1f));
            Assert.Equal(BoxShape.Whole, CornerFit.Shape(CornerFit.Top + CornerFit.PlanLeast + WholeRows + CornerFit.Bottom, WholeRows, LeanRows, card: false, scale: 1f));
        }

        [Fact]
        public void OnAStageTooShortThePlanBoxDropsItsTrailAndButtonsFirst()
        {
            // With a room gone into, the card's least stands over the box too.
            var withCard = CornerFit.Top + CornerFit.CardLeast + CornerFit.Gap + CornerFit.PlanLeast + LeanRows + CornerFit.Bottom;
            Assert.Equal(BoxShape.Lean, CornerFit.Shape(withCard, WholeRows, LeanRows, card: true, scale: 1f));
            Assert.Equal(BoxShape.Lean, CornerFit.Shape(CornerFit.Top + CornerFit.PlanLeast + LeanRows + CornerFit.Bottom, WholeRows, LeanRows, card: false, scale: 1f));
            Assert.Equal(BoxShape.Lean, CornerFit.Shape(2f * (CornerFit.Top + CornerFit.PlanLeast + CornerFit.Bottom) + LeanRows * 2f, WholeRows * 2f, LeanRows * 2f, card: false, scale: 2f));
        }

        [Fact]
        public void WithARoomGoneIntoAndStillNoRoomThePlanBoxFoldsToItsTab()
        {
            var withCard = CornerFit.Top + CornerFit.CardLeast + CornerFit.Gap + CornerFit.PlanLeast + LeanRows + CornerFit.Bottom;
            Assert.Equal(BoxShape.Folded, CornerFit.Shape(withCard - 1f, WholeRows, LeanRows, card: true, scale: 1f));
            Assert.Equal(BoxShape.Folded, CornerFit.Shape(240f, WholeRows, LeanRows, card: true, scale: 1f));
        }

        [Fact]
        public void WithNoRoomGoneIntoThePlanBoxNeverFoldsItself()
        {
            Assert.Equal(BoxShape.Lean, CornerFit.Shape(100f, WholeRows, LeanRows, card: false, scale: 1f));
        }

        [Fact]
        public void TheShapeChosenHoldsTheBoxAndTheCardAtTheirLeast()
        {
            foreach (var card in new[] { false, true })
            {
                for (var stage = 150f; stage <= 700f; stage += 5f)
                {
                    var shape = CornerFit.Shape(stage, WholeRows, LeanRows, card, scale: 1f);
                    if (shape == BoxShape.Folded) continue;
                    var rows = shape == BoxShape.Whole ? WholeRows : LeanRows;
                    var over = card ? CornerFit.Top + CornerFit.CardLeast + CornerFit.Gap : CornerFit.Top;
                    if (shape == BoxShape.Lean && stage < over + CornerFit.PlanLeast + LeanRows + CornerFit.Bottom) continue;
                    Assert.True(over + CornerFit.PlanLeast + rows + CornerFit.Bottom <= stage + 0.01f, $"a stage {stage} tall, {shape}, card {card}");
                }
            }
        }

        [Fact]
        public void ThePlanBoxStaysUnderTheKindsTagWhereTheStageHoldsItAtItsLeast()
        {
            foreach (var card in new[] { false, true })
            {
                for (var stage = 150f; stage <= 1200f; stage += 25f)
                {
                    var plan = CornerFit.Plan(stage, 90f, 330f, card, scale: 1f);
                    var boxTop = stage - CornerFit.Bottom - 90f - plan;
                    if (stage < CornerFit.Top + CornerFit.PlanLeast + 90f + CornerFit.Bottom) continue;
                    Assert.True(boxTop >= CornerFit.Top - 0.01f, $"a stage {stage} tall: the box's top at {boxTop}");
                }
            }
        }

        [Fact]
        public void TheCardGivesWayToThePlanBoxAndScrollsTheRest()
        {
            // The plan box's top 400 under the stage's: the card stands under the kind's tag, a gap over the box.
            var card = CornerFit.Card(500f, stageTop: 0f, boxTop: 400f, scale: 1f);
            Assert.Equal(400f - CornerFit.Gap - CornerFit.Top, card, 3);
            Assert.True(CornerFit.Scrolls(500f, card));
            Assert.Equal(200f, CornerFit.Card(200f, stageTop: 0f, boxTop: 400f, scale: 1f), 3);
            Assert.False(CornerFit.Scrolls(200f, 200f));
        }

        [Fact]
        public void ThePlanGivesWayOnlyOnceTheCardIsAtItsLeast()
        {
            var plan = CornerFit.Plan(400f, 100f, 330f, card: true, scale: 1f);
            Assert.Equal(400f - CornerFit.Bottom - 100f - CornerFit.Top - CornerFit.CardLeast - CornerFit.Gap, plan, 3);
            var boxTop = 400f - CornerFit.Bottom - 100f - plan;
            Assert.Equal(CornerFit.CardLeast, CornerFit.Card(500f, stageTop: 0f, boxTop: boxTop, scale: 1f), 3);
        }

        [Fact]
        public void NeitherThePlanNorTheCardShrinksUnderItsLeast()
        {
            Assert.Equal(CornerFit.PlanLeast, CornerFit.Plan(100f, 100f, 330f, card: true, scale: 1f), 3);
            Assert.Equal(CornerFit.CardLeast * 2f, CornerFit.Card(500f, stageTop: 0f, boxTop: 20f, scale: 2f), 3);
        }

        [Fact]
        public void TheCardAndThePlanBoxNeverCoverEachOtherWhereBothFitAtTheirLeast()
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                for (var stage = 150f; stage <= 1200f; stage += 25f)
                {
                    foreach (var rows in new[] { 80f, 124f })
                    {
                        foreach (var wanted in new[] { 150f, 330f })
                        {
                            foreach (var cardWanted in new[] { 60f, 200f, 600f })
                            {
                                var plan = CornerFit.Plan(stage, rows * scale, wanted * scale, card: true, scale: scale);
                                var boxTop = stage - CornerFit.Bottom * scale - rows * scale - plan;
                                var card = CornerFit.Card(cardWanted * scale, stageTop: 0f, boxTop: boxTop, scale: scale);
                                Assert.True(plan >= CornerFit.PlanLeast * scale - 0.01f);
                                if (stage < (CornerFit.Top + CornerFit.CardLeast + CornerFit.Gap + CornerFit.PlanLeast + CornerFit.Bottom + rows) * scale) continue;
                                Assert.True(CornerFit.Top * scale + card + CornerFit.Gap * scale <= boxTop + 0.01f, $"stage {stage} at {scale}: card {card} over the box at {boxTop}");
                            }
                        }
                    }
                }
            }
        }

        // ----- The tree: rows by rooms in from the entrance, each room under the room it is reached from -----

        private static PlacedRoom Placed(string name, float x, float z, int order, params Vec3[] doorways)
        {
            var shape = new RoomShape { Name = name, Size = new Vec3(4f, 4f, 4f) };
            var position = new Vec3(x, 0f, z);
            foreach (var at in doorways) shape.Doorways.Add(new Doorway { Position = at - position });
            return new PlacedRoom { Room = shape, Position = position, PlaceOrder = order };
        }

        private static Vec3 At(float x, float z) => new Vec3(x, 0f, z);

        /// <summary>
        /// An entrance with a branch to the west and one to the east; the west room leads on to one
        /// room, standing far east of everything; the east room to two, the nearer west first. The
        /// rooms lie from 10 m west to 40 m east.
        /// </summary>
        private static (DungeonWays Ways, PlacedRoom Entrance, PlacedRoom West, PlacedRoom East, PlacedRoom FarOff, PlacedRoom Near, PlacedRoom Far) Branches()
        {
            var entrance = Placed("entrance", 0f, 0f, 0, At(-5f, 0f), At(5f, 0f));
            var west = Placed("west", -10f, 0f, 1, At(-5f, 0f), At(-10f, 5f));
            var east = Placed("east", 10f, 0f, 2, At(5f, 0f), At(8f, 5f), At(14f, 5f));
            var farOff = Placed("far off", 40f, 30f, 3, At(-10f, 5f));
            var near = Placed("near", 8f, 10f, 4, At(8f, 5f));
            var far = Placed("far", 14f, 10f, 5, At(14f, 5f));
            return (DungeonWays.Of(new[] { entrance, west, east, farOff, near, far }), entrance, west, east, farOff, near, far);
        }

        [Fact]
        public void EachRowOfTheTreeIsOneMoreRoomInFromTheEntrance()
        {
            var (ways, entrance, west, east, farOff, near, far) = Branches();
            var tree = TreeRows.Of(ways);
            Assert.Equal(3, tree.Rows);
            Assert.Equal(0, tree.RowOf(entrance));
            Assert.Equal(new[] { 1, 1 }, new[] { tree.RowOf(west), tree.RowOf(east) });
            Assert.Equal(new[] { 2, 2, 2 }, new[] { tree.RowOf(farOff), tree.RowOf(near), tree.RowOf(far) });
        }

        [Fact]
        public void EachRoomStandsOverTheMiddleOfTheRoomsFirstReachedFromIt()
        {
            var (ways, entrance, west, east, farOff, near, far) = Branches();
            var tree = TreeRows.Of(ways);
            Assert.Equal((tree.AcrossOf(near) + tree.AcrossOf(far)) / 2f, tree.AcrossOf(east), 3);
            Assert.Equal(tree.AcrossOf(farOff), tree.AcrossOf(west), 3);
            Assert.Equal((tree.AcrossOf(west) + tree.AcrossOf(east)) / 2f, tree.AcrossOf(entrance), 3);
        }

        [Fact]
        public void EachRoomWithNoneReachedFromItHasAPlaceOfItsOwnEvenlyAcross()
        {
            // Three rooms lead nowhere further: each a third of the tree's width.
            var (ways, _, _, _, farOff, near, far) = Branches();
            var tree = TreeRows.Of(ways);
            Assert.Equal(1f / 6f, tree.AcrossOf(farOff), 3);
            Assert.Equal(0.5f, tree.AcrossOf(near), 3);
            Assert.Equal(5f / 6f, tree.AcrossOf(far), 3);
        }

        [Fact]
        public void RoomsReachedFromOneRoomStandInTheOrderTheyWerePlaced()
        {
            var (ways, _, west, east, _, near, far) = Branches();
            var tree = TreeRows.Of(ways);
            Assert.True(tree.AcrossOf(west) < tree.AcrossOf(east));
            Assert.True(tree.AcrossOf(near) < tree.AcrossOf(far));
        }

        [Fact]
        public void TheBranchesOfTheTreeNeverCross()
        {
            // A loop: the middle room joins both the west and the east room; each also leads to one of its own.
            var entrance = Placed("entrance", 0f, 0f, 0, At(-5f, 0f), At(5f, 0f));
            var west = Placed("west", -10f, 0f, 1, At(-5f, 0f), At(-10f, 5f), At(-6f, 5f));
            var east = Placed("east", 10f, 0f, 2, At(5f, 0f), At(10f, 5f), At(6f, 5f));
            var middle = Placed("middle", 1f, 10f, 3, At(-6f, 5f), At(6f, 5f));
            var westOwn = Placed("west's own", -9f, 10f, 4, At(-10f, 5f));
            var eastOwn = Placed("east's own", 0f, 10f, 5, At(10f, 5f));
            foreach (var ways in new[] { DungeonWays.Of(new[] { entrance, west, east, middle, westOwn, eastOwn }), Branches().Ways })
            {
                var tree = TreeRows.Of(ways);
                // In each row, the rooms stand in the order of the rooms they are first reached from.
                foreach (var row in ways.Rooms.Where(r => ways.CameFrom(r) != null).GroupBy(tree.RowOf))
                {
                    var under = row.OrderBy(tree.AcrossOf).Select(r => tree.AcrossOf(ways.CameFrom(r))).ToList();
                    for (var i = 1; i < under.Count; i++) Assert.True(under[i] >= under[i - 1] - 0.001f);
                }
                Assert.All(ways.Rooms, r => Assert.InRange(tree.AcrossOf(r), 0f, 1f));
            }
        }

        [Fact]
        public void ARoomNoWayReachesStandsInARowOfItsOwnUnderTheRest()
        {
            var entrance = Placed("entrance", 0f, 0f, 0, At(5f, 0f));
            var hall = Placed("hall", 10f, 0f, 1, At(5f, 0f));
            var apart = Placed("apart", 50f, 50f, 2, At(60f, 50f));
            var tree = TreeRows.Of(DungeonWays.Of(new[] { entrance, hall, apart }));
            Assert.Equal(3, tree.Rows);
            Assert.Equal(2, tree.RowOf(apart));
        }

        [Fact]
        public void TheSameWaysLayOutTheSameEachTimeWhicheverWayTheViewTurns()
        {
            var (ways, _, _, _, _, _, _) = Branches();
            var one = TreeRows.Of(ways);
            var two = TreeRows.Of(ways);
            Assert.All(ways.Rooms, r => Assert.Equal(one.AcrossOf(r), two.AcrossOf(r)));
        }

        // ----- What the mouse lights: one room, in the box and on the stage alike -----

        [Fact]
        public void ARoomUnderTheMouseOnTheStageIsLitInTheBoxAndOnTheStage()
        {
            var hall = Room(0f, 0f, 0f, "hall");
            Assert.Same(hall, RoomLight.Lit(null, null, hall));
        }

        [Fact]
        public void ARoomUnderTheMouseInTheBoxOrItsTrailIsLit()
        {
            var hall = Room(0f, 0f, 0f, "hall");
            Assert.Same(hall, RoomLight.Lit(null, hall, null));
        }

        [Fact]
        public void AWayChipOfTheCardLightsItsRoomBeforeAnyUnderTheMouse()
        {
            var card = Room(0f, 0f, 0f, "card");
            var box = Room(5f, 0f, 0f, "box");
            var stage = Room(10f, 0f, 0f, "stage");
            Assert.Same(card, RoomLight.Lit(card, box, stage));
            Assert.Same(box, RoomLight.Lit(null, box, stage));
        }

        [Fact]
        public void NothingUnderTheMouseLightsNoRoom()
        {
            Assert.Null(RoomLight.Lit(null, null, null));
        }

        [Fact]
        public void OnlyTheRoomLitIsLitNotOthersOfItsName()
        {
            var lit = Room(0f, 0f, 0f, "corridor");
            var twin = Room(5f, 0f, 0f, "corridor");
            Assert.True(RoomLight.Lights(lit, lit));
            Assert.False(RoomLight.Lights(lit, twin));
            Assert.False(RoomLight.Lights(null, twin));
            Assert.False(RoomLight.Lights(null, null));
        }

        [Fact]
        public void WithAFloorOpenedARoomBelowItCanBeChosenOnTheStage()
        {
            Assert.True(RoomLight.Choosable(goneInto: false, RoomLook.Whole, PlanRoomShown.Faint));
            Assert.True(RoomLight.Choosable(goneInto: false, RoomLook.Whole, PlanRoomShown.Whole));
        }

        [Fact]
        public void WithAFloorOpenedARoomAboveItCutAwayCannotBeChosen()
        {
            Assert.False(RoomLight.Choosable(goneInto: false, RoomLook.Whole, PlanRoomShown.None));
        }

        [Fact]
        public void WithARoomGoneIntoEveryRoomDimmedAroundItCanBeChosenButNotThosePutAway()
        {
            Assert.True(RoomLight.Choosable(goneInto: true, RoomLook.Below, PlanRoomShown.None));
            Assert.True(RoomLight.Choosable(goneInto: true, RoomLook.Near, PlanRoomShown.None));
            Assert.True(RoomLight.Choosable(goneInto: true, RoomLook.Whole, PlanRoomShown.None));
            Assert.False(RoomLight.Choosable(goneInto: true, RoomLook.Away, PlanRoomShown.Whole));
        }

        [Fact]
        public void TheWayFromTheEntranceGoesToTheRoomLitElseTheRoomGoneInto()
        {
            var lit = Room(0f, 0f, 0f, "lit");
            var gone = Room(5f, 0f, 0f, "gone");
            Assert.Same(lit, RoomLight.RouteTo(lit, gone));
            Assert.Same(gone, RoomLight.RouteTo(null, gone));
            Assert.Null(RoomLight.RouteTo(null, null));
        }

        // ----- What the box says -----

        [Fact]
        public void TheBoxSaysWhatItShows()
        {
            Assert.Equal("Whole dungeon, 12 rooms", PlanBoxWords.Caption(12));
            Assert.Equal("Whole dungeon, 1 room", PlanBoxWords.Caption(1));
            Assert.Equal("Above", PlanBoxWords.ViewName(PlanView.Above));
            Assert.Equal("Levels", PlanBoxWords.ViewName(PlanView.Levels));
            Assert.Equal("Tree", PlanBoxWords.ViewName(PlanView.Tree));
        }

        [Fact]
        public void ALevelTellsHowDeepItsHighestRoomIs()
        {
            Assert.Equal("-16 m", PlanBoxWords.LevelDepth(-15.6f));
            Assert.Equal("0 m", PlanBoxWords.LevelDepth(0.2f));
        }

        [Fact]
        public void ATreeNodeTellsHowDeepItsRoomIs()
        {
            Assert.Equal("0", PlanBoxWords.Depth(0.3f));
            Assert.Equal("-12", PlanBoxWords.Depth(-12.4f));
            Assert.Equal("+3", PlanBoxWords.Depth(3.2f));
        }
    }
}
