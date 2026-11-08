using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class KindsListTests
    {
        // The kinds list on Valheim's map: a small window dragged by its title, folded to it,
        // made wider or narrower, kept where it was left, never off the map.

        [Fact]
        public void AListNeverMovedStandsAtTheMapsTopLeftAtAThirdOfItsWidth()
        {
            var (x, y, width) = KindsList.Place(100f, 50f, 1200f, 800f, null, 1f);
            Assert.Equal(112f, x);
            Assert.Equal(62f, y);
            Assert.Equal(1200f * KindsList.StartShare, width, 3);
        }

        [Fact]
        public void AListStandsWhereItWasLeftAndNeverOffTheMap()
        {
            Assert.Equal((400f, 250f, 300f), KindsList.Place(100f, 50f, 1200f, 800f, (300f, 200f, 300f), 1f));
            // Left past the right or the bottom, as on a smaller screen: brought back inside.
            var (x, y, width) = KindsList.Place(100f, 50f, 600f, 400f, (900f, 900f, 300f), 1f);
            Assert.Equal(100f + 600f - 300f, x);
            Assert.Equal(50f + 400f - KindsList.TitleHeight, y);
            Assert.Equal(300f, width);
        }

        [Fact]
        public void AListIsNeverNarrowerThanAChipStandsNorWiderThanTheMap()
        {
            Assert.Equal(KindsList.LeastWidth * 1.5f, KindsList.Place(0f, 0f, 1200f, 800f, (0f, 0f, 10f), 1.5f).Width);
            Assert.Equal(1200f - 24f, KindsList.Place(0f, 0f, 1200f, 800f, (0f, 0f, 5000f), 1f).Width);
        }

        [Fact]
        public void ChipsFlowIntoRowsAsWideAsTheList()
        {
            var flow = KindsList.Flow(new[] { 100f, 100f, 100f, 50f }, 220f, 4f);
            Assert.Equal(new[] { (0f, 0), (104f, 0), (0f, 1), (104f, 1) }, flow);
            Assert.Equal(2, KindsList.Rows(flow));
            // A chip wider than the list stands alone in its row.
            Assert.Equal(new[] { (0f, 0), (0f, 1) }, KindsList.Flow(new[] { 300f, 50f }, 220f, 4f));
            Assert.Equal(0, KindsList.Rows(KindsList.Flow(new float[0], 220f, 4f)));
        }

        [Fact]
        public void PastSoManyRowsTheListScrolls()
        {
            Assert.Equal(3, KindsList.RowsShown(3));
            Assert.Equal(KindsList.MostRows, KindsList.RowsShown(KindsList.MostRows + 5));
        }

        [Fact]
        public void APlacesNumberedKindsStandAsOneChip()
        {
            // Three excavations and a road post: the excavations one chip, in the order first met.
            var groups = KindsList.Group(new[] { "Excavation", "Road post", "Excavation", "Excavation" });
            Assert.Equal(new[] { "Excavation", "Road post" }, groups.Select(g => g.Key));
            Assert.Equal(new[] { 0, 2, 3 }, groups[0].Members);
            Assert.Equal(new[] { 1 }, groups[1].Members);
        }

        [Fact]
        public void TheListIsKeptWithThePanelsPlace()
        {
            var read = PanelPlace.Read(new PanelPlace { KindsList = (30f, 40f, 320f), KindsFolded = true }.Lines());
            Assert.Equal((30f, 40f, 320f), read.KindsList);
            Assert.True(read.KindsFolded);
            // A file from before the list leaves it where it first stands, open.
            var before = PanelPlace.Read(new PanelPlace { PlanBig = true }.Lines());
            Assert.Null(before.KindsList);
            Assert.Null(before.KindsFolded);
        }
    }
}
