using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlaceTellingTests
    {
        // A place is read once, as its bundle holds it, and told in the world and the language it
        // is shown in: its names and runestones in the game's words now, the music the game gives
        // the weather it sets, and whether its creatures' stars are rolled at all in this world.
        // What is kept of it from one session to the next is what it was read to hold, so the
        // same place reads right after the game's language or a world's settings change.

        private static readonly Dictionary<string, string> German = new Dictionary<string, string>
        {
            ["$location_crypt"] = "Grabkammern", ["$npc_haldor"] = "Haldor", ["$enemy_eikthyr"] = "Eikthyr",
            ["$rune_name"] = "Runenstein", ["$lore_1_topic"] = "Der Eber", ["$lore_1"] = "Text eins", ["$lore_2"] = "Text zwei",
        };

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            ["$location_crypt"] = "Burial Chambers", ["$npc_haldor"] = "Haldor", ["$enemy_eikthyr"] = "Eikthyr",
            ["$rune_name"] = "Rune stone", ["$lore_1_topic"] = "The boar", ["$lore_1"] = "Text one", ["$lore_2"] = "Text two",
        };

        private static PlaceTerms In(Dictionary<string, string> words, Dictionary<string, string> weathers = null, bool starsRoll = true) => new PlaceTerms
        {
            Words = key => words.TryGetValue(key, out var text) ? text : "",
            CreatureName = prefab => prefab == "Eikthyr" ? words["$enemy_eikthyr"] : "",
            WeatherMusic = environment => weathers != null && weathers.TryGetValue(environment, out var music) ? music : "",
            StarsRoll = starsRoll,
        };

        private static PlaceRunestone Stone(string name, params (string Topic, string Text)[] texts)
        {
            var stone = new PlaceRunestone { Name = name };
            stone.Texts.AddRange(texts.Select(t => new RuneText { Topic = t.Topic, Text = t.Text }));
            return stone;
        }

        [Fact]
        public void APlaceIsNamedInTheLanguageTheGameIsInWhenShown()
        {
            var contents = new PlaceContents { GameNameKey = "$location_crypt", TraderKey = "$npc_haldor", BossPrefab = "Eikthyr" };

            contents.Tell(In(German));
            Assert.Equal("Grabkammern", contents.GameName);
            Assert.Equal("Haldor", contents.Trader);
            Assert.Equal("Eikthyr", contents.Boss);

            contents.Tell(In(English));
            Assert.Equal("Burial Chambers", contents.GameName);
        }

        [Fact]
        public void ANameTheGameHasNoWordsForIsLeftOut()
        {
            var contents = new PlaceContents { GameNameKey = "$unknown", TraderKey = "", BossPrefab = "NoSuchBoss" };
            contents.Tell(In(English));
            Assert.Equal("", contents.GameName);
            Assert.Equal("", contents.Trader);
            Assert.Equal("", contents.Boss);
        }

        [Fact]
        public void RunestonesAreToldInTheGamesLanguageEachStoneOnceAndNoneWithNothingToRead()
        {
            var contents = new PlaceContents();
            contents.Stones.Add(Stone("$rune_name", ("$lore_1_topic", "$lore_1"), ("", "$lore_2")));
            contents.Stones.Add(Stone("$rune_name", ("$lore_1_topic", "$lore_1"), ("", "$lore_2")));
            contents.Stones.Add(Stone("$rune_name", ("", "$no_words")));

            contents.Tell(In(German));
            var stone = Assert.Single(contents.Runestones);
            Assert.Equal("Runenstein", stone.Name);
            Assert.Equal(new[] { "Der Eber", "" }, stone.Texts.Select(t => t.Topic));
            Assert.Equal(new[] { "Text eins", "Text zwei" }, stone.Texts.Select(t => t.Text));

            contents.Tell(In(English));
            Assert.Equal(new[] { "Text one", "Text two" }, Assert.Single(contents.Runestones).Texts.Select(t => t.Text));
        }

        [Fact]
        public void AWeathersMusicIsWhatTheGameGivesThatWeatherAfterThePlacesOwnMusic()
        {
            var contents = new PlaceContents();
            contents.OwnMusic.Add(new PlaceMusic { Name = "MusicLocation_Altar", When = MusicWhen.Near });
            contents.OwnMusic.Add(new PlaceMusic { Name = "crypt", When = MusicWhen.Inside, Chance = 0.5f });
            contents.Weathers.AddRange(new[] { "Crypt", "Crypt", "Unknown", "Cave" });

            contents.Tell(In(English, new Dictionary<string, string> { ["Crypt"] = "crypt", ["Cave"] = "dungeon" }));

            Assert.Equal(new[] { "MusicLocation_Altar", "crypt", "crypt", "dungeon" }, contents.Music.Select(m => m.Name));
            Assert.Equal(new[] { MusicWhen.Near, MusicWhen.Inside, MusicWhen.Weather, MusicWhen.Weather }, contents.Music.Select(m => m.When));
            Assert.Equal(0.5f, contents.Music[1].Chance);
            Assert.Equal(1f, contents.Music[2].Chance);
        }

        [Fact]
        public void AWeathersMusicFollowsWhatTheGameGivesItWhenShownAgain()
        {
            var contents = new PlaceContents();
            contents.Weathers.Add("Crypt");

            contents.Tell(In(English, new Dictionary<string, string> { ["Crypt"] = "crypt" }));
            contents.Tell(In(English, new Dictionary<string, string> { ["Crypt"] = "dungeon" }));

            Assert.Equal(new[] { "dungeon" }, contents.Music.Select(m => m.Name));
        }

        [Fact]
        public void StarsLeaveAPlaceToChanceOnlyInAWorldThatLevelsCreaturesUp()
        {
            var stars = new PlaceContents { StarsRolled = true };
            stars.Tell(In(English, starsRoll: false));
            Assert.False(stars.LeftToChance);
            stars.Tell(In(English, starsRoll: true));
            Assert.True(stars.LeftToChance);

            var parts = new PlaceContents { PartsRolled = true };
            parts.Tell(In(English, starsRoll: false));
            Assert.True(parts.LeftToChance);

            var groups = new PlaceContents { GroupsRolled = true };
            groups.Tell(In(English, starsRoll: false));
            Assert.True(groups.LeftToChance);

            var none = new PlaceContents();
            none.Tell(In(English));
            Assert.False(none.LeftToChance);
        }

        [Fact]
        public void TellingAPlaceAgainTellsItTheSame()
        {
            var contents = new PlaceContents();
            contents.OwnMusic.Add(new PlaceMusic { Name = "crypt", When = MusicWhen.Inside });
            contents.Weathers.Add("Crypt");
            contents.Stones.Add(Stone("$rune_name", ("", "$lore_1")));
            var terms = In(English, new Dictionary<string, string> { ["Crypt"] = "dungeon" });

            contents.Tell(terms);
            contents.Tell(terms);

            Assert.Equal(2, contents.Music.Count);
            Assert.Single(contents.Runestones);
            Assert.Single(contents.OwnMusic);
            Assert.Single(contents.Stones);
        }
    }
}
