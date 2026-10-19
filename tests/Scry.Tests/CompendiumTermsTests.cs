using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CompendiumTermsTests
    {
        // With the compendium on, the search's suggestions come only from what the character can
        // find: what it knows gives every term its page has, what it has only seen its kind, and
        // a term naming another entry is offered only where that entry was seen.

        private static Entry E(Kind kind, string name) => new Entry { Kind = kind, Name = name };

        private sealed class World
        {
            public readonly Entry Troll = E(Kind.Creature, "Troll");
            public readonly Entry Boar = E(Kind.Creature, "Boar");
            public readonly Entry Fenring = E(Kind.Creature, "Fenring");
            public readonly Entry Wolf = E(Kind.Creature, "Wolf");
            public readonly Entry Hide = E(Kind.Item, "TrollHide");
            public readonly Entry Wood = E(Kind.Item, "Wood");
            public readonly Entry Meat = E(Kind.Item, "RawMeat");
            public readonly Entry Howl = E(Kind.Sound, "sfx_howl");
            public readonly Entry Club = E(Kind.Item, "Club");
            public readonly Entry Axe = E(Kind.Item, "AxeIron");
            public readonly Entry Workbench = E(Kind.Piece, "piece_workbench");
            public readonly Entry Forge = E(Kind.Piece, "forge");
            public readonly Entry Forest = E(Kind.Biome, "BlackForest");
            public readonly Entry Swamp = E(Kind.Biome, "Swamp");
            public readonly Entry Cave = new Entry { Kind = Kind.Location, Name = "TrollCave02", Components = new[] { "Location" }, FoundIn = new[] { "Troll cave" } };
            public readonly Entry OtherCave = new Entry { Kind = Kind.Location, Name = "TrollCave01", Components = new[] { "Location" }, FoundIn = new[] { "Troll cave" } };
            public readonly Entry Crypt = new Entry { Kind = Kind.Location, Name = "Crypt2", Components = new[] { "Location" }, FoundIn = new[] { "Burial chambers" } };
            public readonly List<Entry> All;
            public readonly Dictionary<Entry, KnownLevel> Met = new Dictionary<Entry, KnownLevel>();

            public World()
            {
                Troll.Components = new[] { "Character" };
                Boar.Components = new[] { "Character" };
                Fenring.Components = new[] { "Character" };
                Troll.Biomes = new[] { "BlackForest", "Swamp", "Mountain" };
                Boar.Biomes = new[] { "Meadows" };
                Troll.FoundIn = new[] { "Troll cave", "Burial chambers", "Ruins" };
                Troll.AddTermLink("drops", Hide);
                Troll.AddTermLink("drops", Wood);
                Boar.AddTermLink("drops", Meat);
                Howl.UsedBy.Add("Wolf");
                Howl.UsedBy.Add("Fenring");
                Club.Stations = new[] { new StationUse("piece_workbench", "Workbench", 1) };
                Axe.Stations = new[] { new StationUse("forge", "Forge", 1) };
                Wood.Stations = new[] { new StationUse("piece_workbench", "Workbench", 1) };
                All = new List<Entry> { Troll, Boar, Fenring, Wolf, Hide, Wood, Meat, Howl, Club, Axe, Workbench, Forge, Forest, Swamp, Cave, OtherCave, Crypt };
                foreach (var known in new[] { Troll, Howl, Club, Axe }) Met[known] = KnownLevel.Known;
                foreach (var seen in new[] { Boar, Wolf, Wood, Workbench, Forest, Cave }) Met[seen] = KnownLevel.Seen;
            }

            public List<Suggestion> Suggest(string word) => SearchHelp.Suggest(word, new TermIndex(All, Met), 20);

            public List<Suggestion> SuggestAll(string word) => SearchHelp.Suggest(word, new TermIndex(All), 20);

            public List<Suggestion> SuggestStrict(string word) => SearchHelp.Suggest(word, new TermIndex(All, Met, new HashSet<Entry> { Troll, Club }), 20);
        }

        private static string[] Labels(List<Suggestion> found) => found.Select(s => s.Label).OrderBy(l => l).ToArray();

        [Fact]
        public void ASuggestionNeverNamesWhatTheCharacterHasNotSeen()
        {
            var world = new World();

            Assert.Equal(new[] { "Wood" }, Labels(world.Suggest("drops:")));
            Assert.Equal(new[] { "Wolf" }, Labels(world.Suggest("playedby:")));
            // A place is offered where one of its locations was seen; a biome or place with no page of its own names nothing.
            Assert.Equal(new[] { "Black Forest", "Mountain" }, Labels(world.Suggest("biome:")));
            Assert.Equal(new[] { "Ruins", "Troll cave" }, Labels(world.Suggest("in:")));
            Assert.Equal(new[] { "Workbench" }, Labels(world.Suggest("station:")));
        }

        [Fact]
        public void WhatWasOnlySeenGivesNoTermButItsKind()
        {
            // The boar, only seen: its page is closed, so nothing on it is offered or counted; its kind is plain to see.
            var world = new World();

            var has = world.Suggest("has:");
            Assert.Equal(new[] { "Character" }, Labels(has));
            Assert.Equal(1, has[0].Count);
            Assert.DoesNotContain(world.Suggest("biome:"), s => s.Label == "Meadows");
            Assert.Equal(1, world.Suggest("station:").Single().Count);
            Assert.DoesNotContain(world.Suggest("drops:"), s => s.Label == "RawMeat");
            Assert.Equal(3, world.Suggest("kind:creature").Single(s => s.Label == Kinds.Label(Kind.Creature)).Count);
        }

        [Fact]
        public void UnderStrictAKnownEntryOffersWhatItIsNotWhatItGoesWith()
        {
            // Its relations show on its page only as far as they were seen; suggestions leave them out.
            var world = new World();

            Assert.Empty(world.SuggestStrict("drops:"));
            Assert.Empty(world.SuggestStrict("biome:"));
            Assert.Empty(world.SuggestStrict("in:"));
            Assert.Empty(world.SuggestStrict("station:"));
            Assert.Equal(new[] { "Wolf" }, Labels(world.SuggestStrict("playedby:")));
            var has = world.SuggestStrict("has:");
            Assert.Equal(new[] { "Character" }, Labels(has));
            Assert.Equal(1, has[0].Count);
        }

        [Fact]
        public void UnderStrictHowAKnownCreatureTakesOrDealsADamageTypeIsNotOffered()
        {
            // Its grid and attacks wait for the hits that tell them; the search box offers no term they would find.
            var world = new World();
            world.Troll.SetTermWords("weak", new[] { "fire" });
            world.Troll.SetTermWords("damage", new[] { "blunt" });
            Assert.Empty(world.SuggestStrict("weak:"));
            Assert.Empty(world.SuggestStrict("damage:"));
            Assert.Equal(new[] { "Fire" }, Labels(world.Suggest("weak:")));
        }

        [Fact]
        public void WithTheCompendiumOffEverySuggestionIsOffered()
        {
            var world = new World();

            // Each by the name it is shown by, its prefab's words where the game gives none.
            Assert.Equal(new[] { "Raw meat", "Troll hide", "Wood" }, Labels(world.SuggestAll("drops:")));
            Assert.Equal(new[] { "Fenring", "Wolf" }, Labels(world.SuggestAll("playedby:")));
            Assert.Equal(3, world.SuggestAll("has:").Single(s => s.Label == "Character").Count);
        }
    }
}
