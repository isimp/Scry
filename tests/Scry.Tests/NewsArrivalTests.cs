using Xunit;

namespace Scry.Tests
{
    /// <summary>A row of the learned list opens its page at what changed, the line lit for a moment.</summary>
    public class NewsArrivalTests
    {
        [Fact]
        public void WhatWasFoundOutOpensAtTheLineItShowsElseAtItsTopic()
        {
            // A creature's health found out: its Health tile, else the headline or the fight.
            var health = NewsArrival.Of(new BookNews(NewsHow.FoundOut, EntryKeys.Did(Deed.Killed), null, 12));
            Assert.Equal(EntryKeys.Did(Deed.Killed), health.Spot);
            Assert.Equal(new[] { "overview", "fight" }, health.Topics);

            var fire = NewsArrival.Of(new BookNews(NewsHow.FoundOut, EntryKeys.Damage("Fire"), null, 12));
            Assert.Equal(EntryKeys.Damage("Fire"), fire.Spot);
            Assert.Equal(new[] { "fight" }, fire.Topics);

            var tamed = NewsArrival.Of(new BookNews(NewsHow.FoundOut, EntryKeys.Did(Deed.Tamed), null, 12));
            Assert.Equal(new[] { "taming" }, tamed.Topics);
        }

        [Fact]
        public void WhatCameToBeKnownOrWasSeenOpensAtTheYouBoxLineTellingIt()
        {
            var known = NewsArrival.Of(new BookNews(NewsHow.Known, null, CameBy.Fought, 12));
            Assert.Equal(NewsArrival.You(CompendiumWords.CameLabel), known.Spot);
            Assert.Equal(new[] { NewsArrival.YouTopic }, known.Topics);

            var seen = NewsArrival.Of(new BookNews(NewsHow.Seen, null, null, 12));
            Assert.Equal(NewsArrival.You(CompendiumWords.FirstSeenLabel), seen.Spot);

            // The explored map's places: where it lies, else the You box.
            var map = NewsArrival.Of(new BookNews(NewsHow.Map, EntryKeys.OnMap, null, 12));
            Assert.Null(map.Spot);
            Assert.Equal(new[] { "lives", "where", NewsArrival.YouTopic }, map.Topics);
        }

        [Fact]
        public void TheLineArrivedAtIsLitAMomentThenFades()
        {
            Assert.Equal(1f, NewsArrival.Lit(0f));
            Assert.Equal(1f, NewsArrival.Lit(0.6f));
            Assert.InRange(NewsArrival.Lit(1.3f), 0.45f, 0.55f);
            Assert.Equal(0f, NewsArrival.Lit(2f));
            Assert.Equal(0f, NewsArrival.Lit(10f));
        }
    }
}
