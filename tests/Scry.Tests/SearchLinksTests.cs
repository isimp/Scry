using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class SearchLinksTests
    {
        // The link terms follow what the catalog links: drops:resin what drops resin, from:troll
        // what a troll drops, needs:bronze what is made or built with bronze, gives:rested what
        // puts Rested on you, spawns:greydwarf what spawns or brings greydwarfs. A linked thing
        // is named by the game's name or its prefab name.

        private static List<Entry> Catalog()
        {
            var troll = E("Troll", Kind.Creature, "Troll");
            var greydwarf = E("Greydwarf", Kind.Creature, "Greydwarf");
            var brute = E("Greydwarf_Elite", Kind.Creature, "Greydwarf brute");
            var nest = E("Spawner_GreydwarfNest", Kind.Spawner, "Greydwarf nest");
            var raid = E("foresttrolls", Kind.Raid, "The ground is shaking");
            var resin = E("Resin", Kind.Item, "Resin");
            var hide = E("TrollHide", Kind.Item, "Troll hide");
            var tunic = E("ArmorTrollLeatherChest", Kind.Item, "Troll leather tunic");
            var bronze = E("Bronze", Kind.Item, "Bronze");
            var bed = E("bed", Kind.Piece, "Bed");
            var rested = E("Rested", Kind.StatusEffect, "Rested");

            troll.AddTermLink("drops", hide);
            hide.AddTermLink("from", troll);
            greydwarf.AddTermLink("drops", resin);
            resin.AddTermLink("from", greydwarf);
            tunic.AddTermLink("needs", hide);
            tunic.AddTermLink("needs", bronze);
            bed.AddTermLink("gives", rested);
            nest.AddTermLink("spawns", greydwarf);
            nest.AddTermLink("spawns", brute);
            raid.AddTermLink("spawns", troll);
            return new List<Entry> { troll, greydwarf, brute, nest, raid, resin, hide, tunic, bronze, bed, rested };
        }

        private static List<string> Find(string text) =>
            Search.Run(Catalog(), new Query { Text = text }, new List<string>()).Select(e => e.Name).ToList();

        [Fact]
        public void EachLinkTermFindsWhatIsLinkedThatWay()
        {
            Assert.Equal(new[] { "Greydwarf" }, Find("drops:resin"));
            Assert.Equal(new[] { "TrollHide" }, Find("from:troll"));
            Assert.Equal(new[] { "ArmorTrollLeatherChest" }, Find("needs:bronze"));
            Assert.Equal(new[] { "bed" }, Find("gives:rested"));
            Assert.Equal(new[] { "Spawner_GreydwarfNest" }, Find("spawns:greydwarf"));
            Assert.Equal(new[] { "foresttrolls" }, Find("spawns:troll"));
        }

        [Fact]
        public void AThingIsNamedByTheGamesNameOrItsPrefabName()
        {
            Assert.Equal(new[] { "Spawner_GreydwarfNest" }, Find("spawns:greydwarfbrute"));
            Assert.Equal(new[] { "Spawner_GreydwarfNest" }, Find("spawns:greydwarf_elite"));
            Assert.Equal(new[] { "ArmorTrollLeatherChest" }, Find("needs:trollhide"));
            Assert.Empty(Find("needs:resin"));
        }

        [Fact]
        public void ALinkIsKeptOnceAndNeverToItself()
        {
            var troll = E("Troll", Kind.Creature, "Troll");
            var hide = E("TrollHide", Kind.Item, "Troll hide");
            troll.AddTermLink("drops", hide);
            troll.AddTermLink("drops", hide);
            troll.AddTermLink("drops", troll);
            Assert.Single(troll.TermLinks("drops"));
            Assert.Empty(troll.TermLinks("from"));
        }

        [Fact]
        public void TheLinkTermsAreSuggestedByTheNamesOfWhatTheyLinkTo()
        {
            var index = new TermIndex(Catalog());
            Assert.Contains(SearchHelp.Suggest("dr", index), s => s.Insert == "drops:" && s.Note == "something it drops or gives");
            Assert.Contains(SearchHelp.Suggest("fr", index), s => s.Insert == "from:" && s.Note == "what drops or gives it");
            var spawned = SearchHelp.Suggest("spawns:grey", index);
            Assert.Equal(new[] { "spawns:greydwarf", "spawns:greydwarfbrute" }, spawned.Select(s => s.Insert));
            Assert.Equal("Greydwarf brute", spawned[1].Label);
            Assert.Equal(1, spawned[1].Count);
        }
    }
}
