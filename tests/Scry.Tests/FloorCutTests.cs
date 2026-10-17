using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class FloorCutTests
    {
        // A place shown is cut open over one of its floors: its floors from the top down, where
        // each is cut, which is opened (one past the last for the roof on), the one last opened,
        // and how far the cut was moved from its floor.

        private static readonly float[] Three = { 8f, 4f, 0f };

        private static FloorCut Taken(bool open = false)
        {
            var cut = new FloorCut();
            cut.Take(E("Crypt2", Kind.Location), Three, open);
            return cut;
        }

        [Fact]
        public void ARoomGoneIntoIsCutAtItsOwnHeightWhateverTheFloors()
        {
            // A dungeon's room focused is cut at head height over its own ground, no floor of the place's.
            var cut = Taken(open: true);

            cut.Hold(1.2f);

            Assert.True(cut.Cutting);
            Assert.True(cut.Held);
            Assert.Equal(1.2f, cut.At, 3);
            Assert.Equal(Three.Length, cut.Level);
            cut.CutBy(0.5f);
            Assert.Equal(1.7f, cut.At, 3);
        }

        [Fact]
        public void ARoomGoneIntoIsCutEvenWhereThePlaceHasNoFloors()
        {
            var cut = new FloorCut();
            cut.Take(E("MorkBorg", Kind.Location), new float[0], open: false);

            cut.Hold(-40f);

            Assert.True(cut.Cutting);
            Assert.Equal(-40f, cut.At, 3);
        }

        [Fact]
        public void ComingOutOfTheRoomPutsTheRoofBackOn()
        {
            var cut = Taken();
            cut.Hold(1.2f);
            cut.CutBy(0.5f);

            cut.Hold(null);

            Assert.False(cut.Held);
            Assert.False(cut.Cutting);
            cut.Open(1);
            Assert.Equal(4f + PlaceView.CutAboveFloor, cut.At, 3);
        }

        [Fact]
        public void OpeningAFloorOrTheRoofComesOutOfTheRoom()
        {
            var opened = Taken();
            opened.Hold(1.2f);
            opened.Open(0);
            Assert.False(opened.Held);
            Assert.Equal(0, opened.Level);

            var roofed = Taken();
            roofed.Hold(1.2f);
            roofed.ToggleRoof();
            Assert.False(roofed.Held);
            Assert.False(roofed.Cutting);

            var bare = new FloorCut();
            bare.Take(E("MorkBorg", Kind.Location), new float[0], open: false);
            bare.Hold(-40f);
            bare.ToggleRoof();
            Assert.False(bare.Cutting);
        }

        [Fact]
        public void FloorsFoundAnewKeepTheRoomGoneInto()
        {
            // An example's floors come in as its rooms do; the room gone into stays cut.
            var cut = Taken();
            cut.Hold(1.2f);

            cut.Refresh(new[] { 8f, 4f, 0f, -4f });

            Assert.True(cut.Held);
            Assert.Equal(1.2f, cut.At, 3);
            Assert.Equal(4, cut.Level);
        }

        [Fact]
        public void GoingIntoAnotherRoomCutsAtItsHeightNotWhereTheLastCutWasMoved()
        {
            var cut = Taken();
            cut.Hold(1.2f);
            cut.CutBy(0.5f);

            cut.Hold(-3f);

            Assert.Equal(-3f, cut.At, 3);
        }

        [Fact]
        public void LettingGoOfEverythingComesOutOfTheRoom()
        {
            var cut = Taken();
            cut.Hold(1.2f);

            cut.Clear();

            Assert.False(cut.Held);
            Assert.False(cut.Cutting);
        }

        [Fact]
        public void AnotherPlaceShownComesOutOfTheRoom()
        {
            var cut = Taken();
            cut.Hold(1.2f);

            cut.Take(E("Crypt3", Kind.Location), Three, open: false);

            Assert.False(cut.Held);
            Assert.False(cut.Cutting);
        }

        [Fact]
        public void EachFloorIsCutUnderTheCeilingOverIt()
        {
            var cut = new FloorCut();
            cut.Take(E("Hildir_plainsfortress", Kind.Location), new[] { 40f, 36f }, open: true, ceilingOver: floor => floor == 40f ? 42f : (float?)null);
            Assert.Equal(new[] { 41.9f, 38.5f }, cut.Cuts.Select(c => (float)System.Math.Round(c, 3)));
            Assert.Equal(41.9f, cut.At, 3);

            // Floors found anew keep cutting under their ceilings.
            cut.Refresh(new[] { 44f, 40f }, floor => floor == 40f ? 42f : (float?)null);
            Assert.Equal(new[] { 46.5f, 41.9f }, cut.Cuts.Select(c => (float)System.Math.Round(c, 3)));
        }

        [Fact]
        public void TheCeilingOverEachFloorIsToldAsFound()
        {
            var cut = new FloorCut();
            cut.Take(E("WoodHouse12", Kind.Location), new[] { 3f, 0f }, open: true, ceilingOver: floor => floor == 0f ? 1.8f : (float?)null);

            Assert.Null(cut.CeilingOver(0));
            Assert.Equal(1.8f, cut.CeilingOver(1));
            Assert.Null(cut.CeilingOver(2));
            Assert.Null(cut.CeilingOver(-1));

            // Taken without ceilings, or let go of, none is told.
            cut.Take(E("Crypt2", Kind.Location), Three, open: true);
            Assert.Null(cut.CeilingOver(2));
            cut.Take(E("WoodHouse12", Kind.Location), new[] { 0f }, open: true, ceilingOver: floor => 1.8f);
            Assert.Equal(1.8f, cut.CeilingOver(0));
            cut.Clear();
            Assert.Null(cut.CeilingOver(0));
        }

        [Fact]
        public void NothingTakenHasNoFloorsAndIsNotCut()
        {
            var cut = new FloorCut();

            Assert.False(cut.HasFloors);
            Assert.False(cut.Cutting);
            Assert.Equal(float.PositiveInfinity, cut.At);
            cut.Step(true);
            cut.ToggleRoof();
            cut.Open(0);
            cut.CutTo(3f);
            Assert.False(cut.Cutting);
        }

        [Fact]
        public void APlaceTakenFirstKeepsItsRoofAndARoomOpensOnItsTopFloor()
        {
            var place = Taken(open: false);
            var room = Taken(open: true);

            Assert.False(place.Cutting);
            Assert.Equal(3, place.Level);
            Assert.True(room.Cutting);
            Assert.Equal(0, room.Level);
            Assert.Equal(PlaceView.CutHeights(Three), room.Cuts);
            Assert.Equal(room.Cuts[0], room.At);
        }

        [Fact]
        public void TakingTellsWhetherTheFloorsAreAnotherEntrys()
        {
            var cut = new FloorCut();
            var crypt = E("Crypt2", Kind.Location);

            Assert.True(cut.Take(crypt, Three, open: true));
            Assert.False(cut.Take(crypt, Three, open: true));
            Assert.True(cut.Take(E("Crypt3", Kind.Location), Three, open: true));
        }

        [Fact]
        public void ANewCopyOfTheSameKeepsItsCutWithinItsFloors()
        {
            var cut = new FloorCut();
            var crypt = E("Crypt2", Kind.Location);
            cut.Take(crypt, Three, open: true);
            cut.Open(2);
            cut.CutBy(0.5f);

            cut.Take(crypt, Three, open: false);
            Assert.Equal(2, cut.Level);

            cut.Take(crypt, new[] { 4f }, open: false);
            Assert.Equal(1, cut.Level);
            Assert.False(cut.Cutting);
        }

        [Fact]
        public void ANewCopyOfTheSameWithMoreFloorsKeepsItsRoofOn()
        {
            var cut = new FloorCut();
            var ruin = E("Ruin1", Kind.Location);
            cut.Take(ruin, new[] { 4f }, open: false);
            cut.CutBy(2f);

            cut.Take(ruin, Three, open: false);

            Assert.False(cut.Cutting);
            Assert.Equal(3, cut.Level);
            cut.ToggleRoof();
            Assert.Equal(cut.Cuts[0], cut.At);
        }

        [Fact]
        public void TheRoofComesOffOverTheLowestFloorLeftWhereTheOneLastOpenedIsGone()
        {
            var cut = new FloorCut();
            var crypt = E("Crypt2", Kind.Location);
            cut.Take(crypt, Three, open: true);
            cut.Open(2);
            cut.ToggleRoof();

            cut.Take(crypt, new[] { 4f, 0f }, open: false);
            cut.ToggleRoof();

            Assert.True(cut.Cutting);
            Assert.Equal(1, cut.Level);
        }

        [Fact]
        public void AnotherEntryStartsAfresh()
        {
            var cut = Taken(open: true);
            cut.Open(2);
            cut.CutBy(1f);

            cut.Take(E("Crypt3", Kind.Location), Three, open: true);

            Assert.Equal(0, cut.Level);
            Assert.Equal(cut.Cuts[0], cut.At);
            cut.ToggleRoof();
            cut.ToggleRoof();
            Assert.Equal(0, cut.Level);
        }

        [Fact]
        public void ForgettingTheEntryStartsAfreshWithTheSame()
        {
            var cut = new FloorCut();
            var crypt = E("Crypt2", Kind.Location);
            cut.Take(crypt, Three, open: false);
            cut.Open(1);

            cut.ForgetEntry();

            Assert.True(cut.Take(crypt, Three, open: false));
            Assert.False(cut.Cutting);
        }

        [Fact]
        public void StepsGoAFloorDownAndUpAsThePlanSays()
        {
            var cut = Taken();

            cut.Step(down: true);
            Assert.Equal(0, cut.Level);
            cut.Step(down: true);
            Assert.Equal(1, cut.Level);
            cut.Step(down: false);
            cut.Step(down: false);
            Assert.False(cut.Cutting);
        }

        [Fact]
        public void TheRoofComesOffOverTheFloorLastOpenedAndGoesBackOn()
        {
            var cut = Taken();
            cut.Open(2);
            cut.ToggleRoof();
            Assert.False(cut.Cutting);

            cut.ToggleRoof();
            Assert.Equal(2, cut.Level);
        }

        [Fact]
        public void OpeningPastTheFloorsPutsTheRoofOnAndBelowNoneOpensTheTop()
        {
            var cut = Taken();

            cut.Open(9);
            Assert.False(cut.Cutting);
            Assert.Equal(3, cut.Level);
            cut.Open(-2);
            Assert.Equal(0, cut.Level);
        }

        [Fact]
        public void OpeningAFloorLetsGoOfTheCutsMove()
        {
            var cut = Taken(open: true);
            cut.CutBy(1.5f);
            Assert.Equal(cut.Cuts[0] + 1.5f, cut.At);

            cut.Open(1);
            Assert.Equal(cut.Cuts[1], cut.At);
        }

        [Fact]
        public void TheCutMovesOnlyWhileCut()
        {
            var cut = Taken();
            cut.CutBy(2f);
            cut.Open(0);

            Assert.Equal(cut.Cuts[0], cut.At);
        }

        [Fact]
        public void CuttingAtAHeightOpensTheFloorUnderItThere()
        {
            var cut = Taken();

            cut.CutTo(6f);

            Assert.Equal(PlaceView.LevelAt(Three, 6f), cut.Level);
            Assert.Equal(6f, cut.At, 4);
        }

        [Fact]
        public void CuttingAtAHeightOnTheFloorOpenedKeepsItOpen()
        {
            var cut = Taken(open: true);
            cut.Open(1);
            cut.CutBy(0.25f);

            cut.CutTo(5f);

            Assert.Equal(1, cut.Level);
            Assert.Equal(5f, cut.At, 4);
        }

        [Fact]
        public void FloorsFoundAnewKeepTheFloorOpenedWhereTheyCan()
        {
            var cut = Taken(open: true);
            cut.Open(2);

            cut.Refresh(new[] { 6f, 2f });
            Assert.Equal(1, cut.Level);
            Assert.Equal(PlaceView.CutHeights(new[] { 6f, 2f }), cut.Cuts);

            cut.Refresh(new float[0]);
            Assert.Equal(0, cut.Level);
            Assert.False(cut.Cutting);
        }

        [Fact]
        public void FloorsFoundAnewKeepTheRoofOn()
        {
            var cut = Taken();

            cut.Refresh(new[] { 9f, 5f, 3f, 0f });

            Assert.False(cut.Cutting);
            Assert.Equal(4, cut.Level);
        }

        [Fact]
        public void TheLabelSaysWhatIsOpened()
        {
            var cut = Taken(open: true);

            Assert.Equal(PlaceView.CutLabel(0, 3), cut.Label);
        }

        [Fact]
        public void ClearingLetsGoOfEverything()
        {
            var cut = new FloorCut();
            var crypt = E("Crypt2", Kind.Location);
            cut.Take(crypt, Three, open: true);
            cut.Open(1);

            cut.Clear();

            Assert.False(cut.HasFloors);
            Assert.Empty(cut.Cuts);
            Assert.Equal(0, cut.Level);
            Assert.True(cut.Take(crypt, Three, open: false));
            cut.ToggleRoof();
            Assert.Equal(0, cut.Level);
            Assert.Equal(Three, cut.Floors.ToArray());
        }

        // ----- Shift and the wheel: the cut up and down through the floors, opening the one it is over -----

        [Fact]
        public void ShiftAndTheWheelDownWithTheRoofOnOpensTheTopFloorAtItsCut()
        {
            var cut = Taken();
            Assert.False(cut.Cutting);
            cut.Wheel(-0.75f);
            Assert.Equal(0, cut.Level);
            Assert.Equal(cut.Cuts[0], cut.At, 3);
            // Up with the roof on, nothing happens.
            var roofed = Taken();
            roofed.Wheel(0.75f);
            Assert.False(roofed.Cutting);
        }

        [Fact]
        public void ShiftAndTheWheelMoveTheCutAndOpenTheFloorItIsOver()
        {
            var cut = Taken();
            cut.Open(0);
            cut.Wheel(cut.Cuts[1] - cut.Cuts[0]);
            Assert.Equal(1, cut.Level);
            Assert.Equal(cut.Cuts[1], cut.At, 3);
            // A little up from there keeps the floor under it opened.
            cut.Wheel(0.25f);
            Assert.Equal(1, cut.Level);
            Assert.Equal(cut.Cuts[1] + 0.25f, cut.At, 3);
        }

        [Fact]
        public void ShiftAndTheWheelNeverTakeTheCutUnderTheLowestFloor()
        {
            var cut = Taken();
            cut.Open(2);
            cut.Wheel(-10f);
            Assert.Equal(2, cut.Level);
            Assert.Equal(Three[2] + FloorCut.LeastOver, cut.At, 3);
        }

        [Fact]
        public void RaisedWellOverTheTopFloorsCutTheRoofGoesBackOn()
        {
            var cut = Taken();
            cut.Open(0);
            cut.Wheel(FloorCut.RoofOver - 0.5f);
            Assert.True(cut.Cutting);
            cut.Wheel(1f);
            Assert.False(cut.Cutting);
        }

        [Fact]
        public void WithARoomGoneIntoShiftAndTheWheelMoveItsOwnCut()
        {
            var cut = Taken();
            cut.Hold(3f);
            cut.Wheel(-1f);
            Assert.True(cut.Held);
            Assert.Equal(2f, cut.At, 3);
        }
    }
}
