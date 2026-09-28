using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class SearchTests
    {
        private static List<string> Find(string text, Kind? kind = null, OriginFilter origin = OriginFilter.All,
            bool favouritesOnly = false, ICollection<string> favourites = null)
        {
            var query = new Query { Text = text, Kind = kind, Origin = origin, FavouritesOnly = favouritesOnly };
            return Search.Run(Game(), query, favourites ?? new List<string>()).Select(e => e.Name).ToList();
        }

        [Fact]
        public void TypingTrollShowsTheTrollItselfFirst()
        {
            Assert.Equal("Troll", Find("troll").First());
        }

        [Fact]
        public void NamesThatStartWithTheTextComeBeforeNamesThatOnlyContainIt()
        {
            var found = Find("troll");

            Assert.True(found.IndexOf("TrollArmorChest") < found.IndexOf("MountainTroll"));
            Assert.True(found.IndexOf("Troll_Summoned") < found.IndexOf("vfx_troll_death"));
        }

        [Fact]
        public void EachCapitalisedWordInAPrefabNameCountsAsTheStartOfAWord()
        {
            var catalog = new List<Entry> { E("Stroller", Kind.Other), E("MountainTroll", Kind.Creature) };

            var found = Search.Run(catalog, new Query { Text = "troll" }, new List<string>());

            Assert.Equal("MountainTroll", found[0].Name);
        }

        [Fact]
        public void AWordAfterAnUnderscoreBeatsAMatchInsideAWord()
        {
            var catalog = new List<Entry> { E("Stroller", Kind.Other), E("vfx_troll_death", Kind.Effect) };

            var found = Search.Run(catalog, new Query { Text = "troll" }, new List<string>());

            Assert.Equal("vfx_troll_death", found[0].Name);
        }

        [Fact]
        public void SearchMatchesTheNameShownInGameNotOnlyThePrefabName()
        {
            Assert.Equal(new[] { "Bow" }, Find("crude"));
        }

        [Fact]
        public void EveryWordTypedHasToMatch()
        {
            Assert.Equal(new[] { "TrollArmorChest" }, Find("leather troll"));
        }

        [Fact]
        public void CaseDoesNotMatter()
        {
            Assert.Equal(Find("troll"), Find("TROLL"));
        }

        [Fact]
        public void NothingTypedListsEverythingByTheNameItShows()
        {
            // The list shows the game's name first, so that is the order it reads in; a prefab
            // with none shows, and is ordered by, its own name.
            var found = Search.Run(Game(), new Query(), new List<string>());
            string Shown(Entry e) => string.IsNullOrEmpty(e.DisplayName) ? e.Name : e.DisplayName;

            Assert.Equal(Game().Count, found.Count);
            Assert.Equal(found.Select(Shown).OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase), found.Select(Shown));
        }

        [Fact]
        public void EntriesShownAlikeAreOrderedByTheirPrefabNames()
        {
            var catalog = new List<Entry> { E("Troll_Summoned", Kind.Creature, "Troll"), E("Troll", Kind.Creature, "Troll") };

            var found = Search.Run(catalog, new Query(), new List<string>());

            Assert.Equal(new[] { "Troll", "Troll_Summoned" }, found.Select(e => e.Name).ToArray());
        }

        [Fact]
        public void AnEntryWhoseShownNameChangesTakesItsNewPlace()
        {
            var catalog = new List<Entry> { E("a_prefab", Kind.Other, "Apple"), E("b_prefab", Kind.Other, "Banana") };
            Search.Run(catalog, new Query(), new List<string>());

            catalog[0].DisplayName = "Cherry";
            var found = Search.Run(catalog, new Query(), new List<string>());

            Assert.Equal(new[] { "Banana", "Cherry" }, found.Select(e => e.DisplayName).ToArray());
        }

        [Fact]
        public void TextThatMatchesNothingListsNothing()
        {
            Assert.Empty(Find("dragonfruit"));
        }

        [Fact]
        public void AKindShowsOnlyThatKind()
        {
            Assert.Equal(new[] { "Troll", "Troll_Summoned", "MountainTroll" }, Find("troll", Kind.Creature));
        }

        [Fact]
        public void FavouritesOnlyShowsTheFavourites()
        {
            var favourites = new List<string> { "Bow", "se:Rested" };

            Assert.Equal(new[] { "Bow", "Rested" }, Find("", favouritesOnly: true, favourites: favourites));
        }

        [Fact]
        public void OriginFiltersSeparateTheGamesPrefabsFromAMods()
        {
            Assert.Equal(new[] { "CoolMod_TrollStatue" }, Find("troll", origin: OriginFilter.Mods));
            Assert.DoesNotContain("CoolMod_TrollStatue", Find("troll", origin: OriginFilter.Vanilla));
        }
    }
}
