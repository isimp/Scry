using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class SeenSpotsTests
    {
        // Where your character saw each resource or place, so Show on map can dot them on the
        // large map: one spot a thing, by world and kind, kept from session to session.

        private const string World = "Valhalla-123", Other = "Ashland-9";

        [Fact]
        public void ASpotSeenIsKeptByWorldAndKindToTheMetre()
        {
            var spots = new SeenSpots();
            Assert.True(spots.Saw(World, "Copper deposit", 120.4f, -88.6f));
            Assert.Equal(new[] { (120, -89) }, spots.Of(World, "Copper deposit"));
            // Another kind, or the same kind in another world, is kept apart.
            Assert.Empty(spots.Of(World, "Tin"));
            Assert.Empty(spots.Of(Other, "Copper deposit"));
            Assert.True(spots.Changed);
        }

        [Fact]
        public void TheSameThingSeenAgainIsOneSpot()
        {
            // Seen from another side, or opened into its broken form where it stood: the same spot.
            var spots = new SeenSpots();
            spots.Saw(World, "Copper deposit", 120f, 80f);
            Assert.False(spots.Saw(World, "Copper deposit", 120.9f, 80.8f));
            Assert.Single(spots.Of(World, "Copper deposit"));
        }

        [Fact]
        public void AThingWhoseSpotIsKeptIsKnownSoTheSightLooksNoFurther()
        {
            var spots = new SeenSpots();
            Assert.False(spots.Has(World, "Copper deposit", 120f, 80f));
            spots.Saw(World, "Copper deposit", 120f, 80f);
            Assert.True(spots.Has(World, "Copper deposit", 120.9f, 80.8f));
            // Another kind there, the same kind a few metres off or in another world: not kept yet.
            Assert.False(spots.Has(World, "Tin", 120f, 80f));
            Assert.False(spots.Has(World, "Copper deposit", 123f, 80f));
            Assert.False(spots.Has(Other, "Copper deposit", 120f, 80f));
        }

        [Fact]
        public void TwoThingsOfAKindAFewMetresApartAreTwoSpots()
        {
            var spots = new SeenSpots();
            spots.Saw(World, "Raspberry bush", 10f, 10f);
            Assert.True(spots.Saw(World, "Raspberry bush", 13f, 10f));
            // One just across a zone's edge from the other is still told apart from it, and still one with itself.
            spots.Saw(World, "Raspberry bush", 63.4f, 5f);
            Assert.False(spots.Saw(World, "Raspberry bush", 64.3f, 5f));
            Assert.Equal(3, spots.Of(World, "Raspberry bush").Count);
        }

        [Fact]
        public void AThingGoneTakesItsOwnSpotAwayAndNoOther()
        {
            var spots = new SeenSpots();
            spots.Saw(World, "Copper deposit", 100f, 100f);
            spots.Saw(World, "Copper deposit", 140f, 100f);
            spots.Saw(World, "Tin", 100f, 100f);
            spots.Saw(Other, "Copper deposit", 100f, 100f);
            Assert.True(spots.Gone(World, "Copper deposit", 100.6f, 99.7f));
            Assert.Equal(new[] { (140, 100) }, spots.Of(World, "Copper deposit"));
            Assert.Single(spots.Of(World, "Tin"));
            Assert.Single(spots.Of(Other, "Copper deposit"));
        }

        [Fact]
        public void AThingGoneThatWasNeverKeptChangesNothing()
        {
            var spots = SeenSpots.Load(new SeenSpots().Save());
            Assert.False(spots.Gone(World, "Copper deposit", 5f, 5f));
            spots.Saw(World, "Copper deposit", 5f, 5f);
            spots.Save();
            Assert.False(spots.Gone(World, "Copper deposit", 9f, 5f));
            Assert.False(spots.Changed);
        }

        [Fact]
        public void TheSpotsNearAPlaceAreFoundToCheckWhatStillStandsThere()
        {
            var spots = new SeenSpots();
            spots.Saw(World, "Copper deposit", 0f, 0f);
            spots.Saw(World, "Tin", 30f, 40f);
            spots.Saw(World, "Tin", 20f, 20f);
            spots.Saw(World, "Tin", 200f, 0f);
            spots.Saw(Other, "Tin", 1f, 1f);
            var near = spots.Near(World, 0f, 0f, 50f).OrderBy(s => s.X).ToList();
            Assert.Equal(new[] { ("Copper deposit", 0, 0), ("Tin", 20, 20), ("Tin", 30, 40) }, near);
            // Exactly at the reach counts; a step beyond does not.
            Assert.Equal(2, spots.Near(World, 0f, 0f, 50f).Count(s => s.Kind == "Tin"));
            Assert.Equal(1, spots.Near(World, 0f, 0f, 49.9f).Count(s => s.Kind == "Tin"));
        }

        [Fact]
        public void SpotsComeBackAsTheyWereKeptFromOneSessionToTheNext()
        {
            var spots = new SeenSpots();
            spots.Saw(World, "Copper deposit", 120f, -89f);
            spots.Saw(World, "Fuling village", -3000f, 4100f);
            spots.Saw(Other, "Tin\tfrom a mod", 7f, 8f);
            var text = spots.Save();
            Assert.False(spots.Changed);
            var back = SeenSpots.Load(text);
            Assert.Equal(new[] { (120, -89) }, back.Of(World, "Copper deposit"));
            Assert.Equal(new[] { (-3000, 4100) }, back.Of(World, "Fuling village"));
            Assert.Equal(new[] { (7, 8) }, back.Of(Other, "Tin\tfrom a mod"));
            Assert.False(back.Changed);
            // Written in a fixed order, so the same spots read back write the same text.
            Assert.Equal(text, back.Save());
        }

        [Fact]
        public void AFileOfAnotherMakeReadsAsNoSpotsAndABrokenLineIsPassedOver()
        {
            Assert.Equal(0, SeenSpots.Load("").Count);
            Assert.Equal(0, SeenSpots.Load("something else\nValhalla\tTin\t1\t2\n").Count);
            var spots = new SeenSpots();
            spots.Saw(World, "Tin", 1f, 2f);
            var text = spots.Save() + "Valhalla\tTin\tone\t2\nonly a world\n";
            var back = SeenSpots.Load(text);
            Assert.Equal(1, back.Count);
            Assert.Equal(new[] { (1, 2) }, back.Of(World, "Tin"));
        }

        [Fact]
        public void ForgettingTakesAWorldsSpotsAwayOrEverySpot()
        {
            var spots = new SeenSpots();
            spots.Saw(World, "Tin", 1f, 2f);
            spots.Saw(Other, "Tin", 1f, 2f);
            spots.Saw(Other, "Copper deposit", 9f, 2f);
            spots.Save();
            Assert.Equal(1, spots.CountIn(World));
            spots.Forget(World);
            Assert.Empty(spots.Of(World, "Tin"));
            Assert.Equal(0, spots.CountIn(World));
            Assert.Single(spots.Of(Other, "Tin"));
            Assert.Equal(2, spots.CountIn(Other));
            Assert.True(spots.Changed);
            spots.Forget();
            Assert.Equal(0, spots.Count);
        }

        [Fact]
        public void AThingIsSeenForTheMapAsFarAsItsSizeIsSeenAndNoFartherThanTheWorldAllows()
        {
            // A person-sized thing from 30 m, a big one farther, never past the world's reach for the map.
            Assert.Equal(30f, SeenOnMap.Reach(Sighting.PersonSize, SeenOnMap.DefaultReach), 3);
            Assert.Equal(50f, SeenOnMap.Reach(Sighting.PersonSize * 4f, SeenOnMap.DefaultReach), 3);
            Assert.Equal(20f, SeenOnMap.Reach(Sighting.PersonSize, 20f), 3);
            // A world that allows no reach records nothing.
            Assert.Equal(0f, SeenOnMap.Reach(Sighting.PersonSize, 0f), 3);
        }

        [Fact]
        public void AWorldKeepsAtMostSoManySpotsAndOthersStillTheirOwn()
        {
            // So a record can never grow without end: past the most a world keeps, a new spot is not kept.
            var spots = new SeenSpots(most: 3);
            for (var i = 0; i < 5; i++) spots.Saw(World, "Tin", i * 10f, 0f);
            Assert.Equal(3, spots.Of(World, "Tin").Count);
            Assert.True(spots.Saw(Other, "Tin", 0f, 0f));
            // A spot gone makes room again.
            spots.Gone(World, "Tin", 0f, 0f);
            Assert.True(spots.Saw(World, "Tin", 90f, 0f));
            Assert.Equal(20000, SeenSpots.MostAWorld);
        }

        [Fact]
        public void TheTextIsMadeFromAMomentsCopySoPlayCanGoOnWhileItIsWritten()
        {
            // The spots are copied at once, the text made later off the game's frame: what is seen meanwhile is not in it.
            var spots = new SeenSpots();
            spots.Saw(World, "Tin", 1f, 2f);
            var later = spots.ToSave();
            Assert.False(spots.Changed);
            spots.Saw(World, "Tin", 30f, 2f);
            var text = later();
            Assert.Equal(new[] { (1, 2) }, SeenSpots.Load(text).Of(World, "Tin"));
            Assert.True(spots.Changed);
        }

        [Fact]
        public void OnlyWhatIsWorthFindingAgainIsRecorded()
        {
            // A place, a pickable and a deposit giving something besides what lies everywhere.
            Assert.True(SeenOnMap.Findable(location: true, gatheredBy: Gathered.None, gives: new string[0]));
            Assert.True(SeenOnMap.Findable(false, Gathered.Picked, new[] { "Raspberry" }));
            Assert.True(SeenOnMap.Findable(false, Gathered.Picked, new[] { "Flint" }));
            Assert.True(SeenOnMap.Findable(false, Gathered.Mined, new[] { "TinOre", "Stone" }));
            Assert.True(SeenOnMap.Findable(false, Gathered.Mined, new[] { "CopperOre" }));
            // Branches, stones and plain rocks give only what lies everywhere; trees, shrubs and logs are not gathered so.
            Assert.False(SeenOnMap.Findable(false, Gathered.Picked, new[] { "Wood" }));
            Assert.False(SeenOnMap.Findable(false, Gathered.Picked, new[] { "Stone" }));
            Assert.False(SeenOnMap.Findable(false, Gathered.Mined, new[] { "Stone" }));
            Assert.False(SeenOnMap.Findable(false, Gathered.None, new[] { "Wood", "Resin" }));
            Assert.False(SeenOnMap.Findable(false, Gathered.Mined, new string[0]));
        }

        [Fact]
        public void ForgettingSaysHowManyThingsWereForgottenOrThatNoneAreRead()
        {
            Assert.Equal("forgot where this character saw 12 things worth finding again in this world.", ConsoleWords.SpotsForgotten(12));
            Assert.Equal("forgot where this character saw 1 thing worth finding again in this world.", ConsoleWords.SpotsForgotten(1));
            Assert.Equal("no character is played in a world, so there is nothing seen to forget.", ConsoleWords.SpotsForgotten(null));
        }

        [Fact]
        public void NothingAWishboneFindsIsRecordedUntilOpened()
        {
            // A buried vein or chest carries the beacon a Wishbone finds; opened, it carries none.
            Assert.False(SeenOnMap.Records(foundByWishbone: true));
            Assert.True(SeenOnMap.Records(foundByWishbone: false));
        }
    }
}
