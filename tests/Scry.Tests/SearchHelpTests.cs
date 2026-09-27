using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class SearchHelpTests
    {
        private static List<Entry> Catalog()
        {
            var troll = E("Troll", Kind.Creature, "Troll");
            troll.Components = new[] { "Humanoid", "MonsterAI" };
            troll.Biomes = new[] { "BlackForest" };
            troll.FoundIn = new[] { "Troll cave" };

            var draugr = E("Draugr", Kind.Creature, "Draugr");
            draugr.Components = new[] { "Humanoid", "MonsterAI" };
            draugr.Biomes = new[] { "Swamp" };
            draugr.FoundIn = new[] { "Crypt", "Sunken crypt rooms" };

            var blob = E("BlobElite", Kind.Creature, "Oozer");
            blob.Components = new[] { "MonsterAI", "Aoe" };
            blob.Biomes = new[] { "Swamp" };
            blob.FoundIn = new[] { "Old swamp camp" };

            var guck = E("Guck", Kind.Resource, "Guck sack");
            guck.Biomes = new[] { "Swamp" };
            guck.FoundIn = new[] { "Old swamp camp" };

            var sword = E("SwordIron", Kind.Item, "Iron sword");
            sword.Stations = new[] { new StationUse("forge", "Forge", 2) };
            sword.FoundIn = new[] { "Swamp cave" };

            var club = E("Club", Kind.Item, "Club");
            club.Stations = new[] { new StationUse("hand", "By hand", 1) };

            var statue = E("CoolMod_TrollStatue", Kind.Piece, "Troll statue", Origin.Mod);
            statue.ModName = "Cool Statues";
            statue.Stations = new[] { new StationUse("piece_workbench", "Workbench", 1) };

            var hit = E("sfx_troll_hit", Kind.Sound);
            hit.UsedBy.Add("Troll");
            hit.UsedBy.Add("status effect Burning");

            return new List<Entry> { troll, draugr, blob, guck, sword, club, statue, hit };
        }

        private static List<Suggestion> Suggest(string word, Kind? kind = null) => SearchHelp.Suggest(word, new TermIndex(Catalog()), kind);

        private static List<string> Find(string text) =>
            Search.Run(Catalog(), new Query { Text = text }, new List<string>()).Select(e => e.Name).ToList();

        // ----- The word being typed -----

        [Fact]
        public void TheWordBeingTypedIsTheOneAroundTheCaret()
        {
            var span = SearchHelp.WordAt("troll biome:sw", 14);
            Assert.Equal("biome:sw", span.Word);
            Assert.Equal(6, span.Start);
            Assert.Equal(14, span.End);

            Assert.Equal("troll", SearchHelp.WordAt("troll biome:sw", 3).Word);
            Assert.Equal("", SearchHelp.WordAt("troll ", 6).Word);
        }

        [Fact]
        public void TakingASuggestionReplacesOnlyTheWordAndPutsTheCaretAfterIt()
        {
            var text = SearchHelp.Replace("troll bi -has:x", SearchHelp.WordAt("troll bi -has:x", 8), "biome:", out var caret);
            Assert.Equal("troll biome: -has:x", text);
            Assert.Equal(12, caret);
        }

        // ----- What is suggested -----

        [Fact]
        public void TheStartOfAKeySuggestsTheKey()
        {
            var suggested = Suggest("bi");
            Assert.Equal("biome:", suggested.Single().Insert);
            Assert.True(suggested.Single().IsKey);
            Assert.Empty(Suggest("troll"));
        }

        [Fact]
        public void AfterAColonTheValuesThatExistAreSuggestedWithHowManyHaveThem()
        {
            var suggested = Suggest("biome:");
            Assert.Equal(new[] { "biome:swamp", "biome:blackforest" }, suggested.Select(s => s.Insert));
            Assert.Equal("Swamp", suggested[0].Label);
            Assert.Equal(3, suggested[0].Count);
            Assert.Equal("Black forest", suggested[1].Label);
        }

        [Fact]
        public void ValuesThatStartWithWhatIsTypedComeBeforeThoseThatOnlyContainIt()
        {
            Assert.Equal(new[] { "has:monsterai", "has:humanoid" }, Suggest("has:m").Select(s => s.Insert));
        }

        [Fact]
        public void AValueStartingWithWhatIsTypedComesFirstEvenWhenOneThatOnlyHoldsItIsMoreCommon()
        {
            Assert.Equal(new[] { "in:swampcave", "in:oldswampcamp" }, Suggest("in:swampc").Select(s => s.Insert));
        }

        [Fact]
        public void OnAKindsTabTheValuesAreThoseOfThatKindCountedThere()
        {
            var swamp = Suggest("biome:sw", Kind.Creature).Single();
            Assert.Equal(2, swamp.Count);
            Assert.Equal("biome:swamp", Suggest("biome:sw", Kind.Resource).Single().Insert);
            Assert.Equal(1, Suggest("biome:sw", Kind.Resource).Single().Count);
        }

        [Fact]
        public void ATabWithoutAnyOfTheValuesStillOffersAllOfThem()
        {
            Assert.Equal(2, Suggest("biome:", Kind.Item).Count);
        }

        [Fact]
        public void AMinusIsKeptInTheSuggestion()
        {
            Assert.Equal("-has:aoe", Suggest("-has:ao").Single().Insert);
            Assert.Equal("-kind:", Suggest("-ki").Single().Insert);
        }

        [Fact]
        public void EverySuggestedValueFindsWhatItSaysItFinds()
        {
            foreach (var key in Search.Keys)
            {
                foreach (var suggestion in Suggest(key + ":"))
                {
                    Assert.Equal(suggestion.Count, Find(suggestion.Insert).Count);
                }
            }
        }

        [Fact]
        public void StationsModsPlacesAndPlayersAreSuggestedAsOneWord()
        {
            Assert.Contains("station:byhand", Suggest("station:").Select(s => s.Insert));
            Assert.Equal("mod:coolstatues", Suggest("mod:c").Single().Insert);
            Assert.Contains("in:sunkencryptrooms", Suggest("in:").Select(s => s.Insert));
            Assert.Contains("used:statuseffectburning", Suggest("used:").Select(s => s.Insert));
        }

        [Fact]
        public void KindsAreSuggestedByTheirNames()
        {
            var kinds = Suggest("kind:").Select(s => s.Insert).ToList();
            Assert.Contains("kind:creature", kinds);
            Assert.Equal("kind:statuseffect", Suggest("kind:sta").Single().Insert);
        }

        [Fact]
        public void AnUnknownKeySuggestsNothing()
        {
            Assert.Empty(Suggest("colour:red"));
        }

        [Fact]
        public void AtMostAFewAreSuggested()
        {
            Assert.True(SearchHelp.Suggest("has:", new TermIndex(Catalog()), null, 2).Count == 2);
        }

        // ----- Inline completion -----

        [Fact]
        public void TheRestOfTheBestSuggestionShowsAfterTheWord()
        {
            Assert.Equal("ome:", SearchHelp.Ghost("bi", Suggest("bi")));
            Assert.Equal("amp", SearchHelp.Ghost("biome:sw", Suggest("biome:sw")));
            Assert.Equal("", SearchHelp.Ghost("has:ai", Suggest("has:ai")));
            Assert.Equal("", SearchHelp.Ghost("", Suggest("")));
        }

        // ----- Tab -----

        [Fact]
        public void TabCompletesTheOnlySuggestionAndTheNextTabGoesOnToItsValues()
        {
            var index = new TermIndex(Catalog());
            var cycle = new TabCycle();

            var (text, caret) = cycle.Next("troll bi", 8, w => SearchHelp.Suggest(w, index, null), false);
            Assert.Equal("troll biome:", text);
            Assert.Equal(12, caret);

            (text, caret) = cycle.Next(text, caret, w => SearchHelp.Suggest(w, index, null), false);
            Assert.Equal("troll biome:swamp", text);
        }

        [Fact]
        public void TabAgainCyclesThroughTheSuggestionsAndShiftTabGoesBack()
        {
            var index = new TermIndex(Catalog());
            var cycle = new TabCycle();
            System.Func<string, List<Suggestion>> suggest = w => SearchHelp.Suggest(w, index, null);

            var (text, caret) = cycle.Next("has:", 4, suggest, false);
            Assert.Equal("has:monsterai", text);
            Assert.Equal(0, cycle.Index);
            (text, caret) = cycle.Next(text, caret, suggest, false);
            Assert.Equal("has:humanoid", text);
            (text, caret) = cycle.Next(text, caret, suggest, false);
            Assert.Equal("has:aoe", text);
            (text, caret) = cycle.Next(text, caret, suggest, false);
            Assert.Equal("has:monsterai", text);
            (text, caret) = cycle.Next(text, caret, suggest, true);
            Assert.Equal("has:aoe", text);
        }

        [Fact]
        public void TypingSomethingElseStartsTheCycleAfresh()
        {
            var index = new TermIndex(Catalog());
            var cycle = new TabCycle();
            System.Func<string, List<Suggestion>> suggest = w => SearchHelp.Suggest(w, index, null);

            var (text, caret) = cycle.Next("has:", 4, suggest, false);
            Assert.Equal("has:monsterai", text);
            Assert.True(cycle.IsAt(text, caret));
            Assert.False(cycle.IsAt("has:h", 5));
            (text, _) = cycle.Next("has:h", 5, suggest, false);
            Assert.Equal("has:humanoid", text);
        }

        [Fact]
        public void TabWithNothingToSuggestChangesNothing()
        {
            var cycle = new TabCycle();
            var (text, caret) = cycle.Next("troll", 5, w => SearchHelp.Suggest(w, new TermIndex(Catalog()), null), false);
            Assert.Equal("troll", text);
            Assert.Equal(5, caret);
        }

        // ----- Chips -----

        [Fact]
        public void TheTermsInTheSearchAreListedToBeTakenOutOneByOne()
        {
            var active = SearchHelp.ActiveTerms("troll biome:swamp -has:aoe biome:");
            Assert.Equal(new[] { "biome:swamp", "-has:aoe" }, active);
            Assert.Equal("troll -has:aoe", SearchHelp.Toggle("troll biome:swamp -has:aoe", "biome:swamp"));
        }

        [Fact]
        public void AChipNotInTheSearchIsAddedToIt()
        {
            Assert.Equal("troll biome:swamp", SearchHelp.Toggle("troll ", "biome:swamp"));
            Assert.Equal("biome:swamp", SearchHelp.Toggle("", "biome:swamp"));
        }

        [Fact]
        public void ATabOffersTheValuesItsEntriesHaveMostOfFirst()
        {
            var chips = SearchHelp.Chips(new TermIndex(Catalog()), Kind.Creature, "");
            Assert.Equal("biome:swamp", chips[0].Insert);
            Assert.Equal(2, chips[0].Count);
            Assert.Contains("in:crypt", chips.Select(c => c.Insert));
            Assert.DoesNotContain("station:forge", chips.Select(c => c.Insert));

            var items = SearchHelp.Chips(new TermIndex(Catalog()), Kind.Item, "");
            Assert.Contains("station:forge", items.Select(c => c.Insert));
        }

        [Fact]
        public void AChipAlreadyInTheSearchIsNotOfferedAgain()
        {
            var chips = SearchHelp.Chips(new TermIndex(Catalog()), Kind.Creature, "biome:swamp");
            Assert.DoesNotContain("biome:swamp", chips.Select(c => c.Insert));
        }

        // ----- Terms written as one word -----

        [Fact]
        public void ModAndUsedMatchWithTheSpacesOfTheNameLeftOut()
        {
            Assert.Equal(new[] { "CoolMod_TrollStatue" }, Find("mod:coolstatues"));
            Assert.Equal(new[] { "sfx_troll_hit" }, Find("used:statuseffectburning"));
        }
    }
}
