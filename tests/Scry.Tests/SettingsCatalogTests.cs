using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Scry.Tests
{
    public class SettingsCatalogTests
    {
        // The settings card draws every setting from one catalogue: each in one section, with a
        // label and a plain explanation, a control fit for it, and what the server decides kept
        // apart, so nothing is left only in the config file and nothing is told twice.

        /// <summary>Every config entry Settings binds, by its key, with its section, as the file binds it.</summary>
        private static Dictionary<string, string> Bound()
        {
            var source = File.ReadAllText(Path.Combine(ScrySource.Root(), "src", "Core", "Settings.cs"));
            var bound = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Regex.Matches(source, "config\\.Bind\\(\"([^\"]+)\", \"([^\"]+)\"")) bound.Add(match.Groups[2].Value, match.Groups[1].Value);
            // The world's levels, one for each source, bound in a loop.
            foreach (var each in WorldSources.All) bound.Add(each + "UpTo", "4 - World");
            return bound;
        }

        /// <summary>Every row of the card, what the server allows of a row standing under it among them.</summary>
        private static IEnumerable<SettingRow> Rows => SettingsCatalog.AllRows;

        private static SettingSection Section(string name) => SettingsCatalog.Sections.Single(s => s.Name == name);

        [Fact]
        public void YourCharactersCompendiumIsSetInTheCompendiumSectionAlone()
        {
            // The rules, Known up to and each source of the world, as the compendium's box sets them: one home on the card.
            var compendium = Section("Compendium");
            var book = compendium.Rows.Where(r => r.Control == SettingControl.Book).ToList();
            Assert.Equal(new[] { BookPart.Rules, BookPart.KnownUpTo }.Concat(WorldSources.All.Select(_ => BookPart.Source)), book.Select(r => r.Book));
            Assert.Equal(WorldSources.All, book.Where(r => r.Book == BookPart.Source).Select(r => r.Source));
            Assert.Equal(new[] { "Rules", "Known up to" }.Concat(WorldSources.All.Select(CompendiumWords.SourceName)), book.Select(r => r.Label));
            // Elsewhere only the Map section's row of which dungeons are marked, the pins among its ticks.
            Assert.All(SettingsCatalog.Sections.Where(s => s != compendium).SelectMany(s => s.Rows), r => Assert.True(r.Book == BookPart.None || r.Book == BookPart.DungeonPins, r.Label));
            Assert.All(book, r => Assert.Empty(r.Keys));
        }

        [Fact]
        public void WhatTheServerAllowsStandsUnderTheRowItLimitsNotOnARowOfItsOwn()
        {
            var compendium = Section("Compendium");
            SettingRow Book(BookPart part, WorldSource source = default) => compendium.Rows.Single(r => r.Book == part && (part != BookPart.Source || r.Source == source));
            Assert.Equal(new[] { "Rules" }, Book(BookPart.Rules).Limit.Keys);
            Assert.Equal(new[] { "KnownUpTo" }, Book(BookPart.KnownUpTo).Limit.Keys);
            foreach (var source in WorldSources.All) Assert.Equal(new[] { source + "UpTo" }, Book(BookPart.Source, source).Limit.Keys);
            Assert.All(compendium.Rows.Where(r => r.Limit != null), r => Assert.True(r.Limit.ServerDecides, r.Label + ": what it allows is the server's"));
            // This server's section keeps what is no choice of the player's.
            var server = Section("This server").Rows.SelectMany(r => r.Keys).ToList();
            Assert.Equal(new[] { "Peeking", "SeenReach", "SeenOnMapReach", "SquaresOnMap", "ReadTracesOnStart", "TakeWalks" }, server);
        }

        [Fact]
        public void EverySettingTheServerSendsSaysSoInTheConfigFile()
        {
            // A mod manager's config editor shows these words alone: each says the server's value wins.
            var source = File.ReadAllText(Path.Combine(ScrySource.Root(), "src", "Core", "Settings.cs"));
            var binds = Regex.Matches(source, "config\\.Bind\\(\"4 - World\", (\"[^\"]+\"|source \\+ \"UpTo\")[\\s\\S]*?\\);\\r?\\n").Cast<Match>().ToList();
            Assert.Equal(7, binds.Count);
            foreach (var bind in binds) Assert.True(bind.Value.Contains("SettingsWords.Synced(") || bind.Value.Contains("UpToSetting") || bind.Value.Contains("Only the server's own setting counts"), bind.Groups[1].Value + " does not say the server decides it");
            foreach (var each in WorldSources.All) Assert.EndsWith(SettingsWords.WorldSynced, WorldRulesWords.UpToSetting(each));
            Assert.StartsWith(" Set on the world's server", SettingsWords.WorldSynced);
            Assert.Equal("Whether." + SettingsWords.WorldSynced, SettingsWords.Synced("Whether."));
        }

        [Fact]
        public void DungeonMarksSaysWhatItPutsAwayAndWhatStays()
        {
            // Off, no dungeon mark shows on either map, others' and entrances too; a page's Show on map is its own.
            var why = Section("Map").Rows.Single(r => r.Keys.Contains("DungeonMarksOnMap")).Why;
            Assert.Contains("Off, no dungeon mark shows", why);
            Assert.Contains("Show on map", why);
            Assert.Equal(why, SettingsWords.DungeonMarksWhy);
            Assert.Contains("SettingsWords.DungeonMarksWhy", File.ReadAllText(Path.Combine(ScrySource.Root(), "src", "Core", "Settings.cs")));
        }

        [Fact]
        public void WhichDungeonsAreMarkedIsOneRowByWhereYouKnowThemFrom()
        {
            // You walked it, it lies in ground you explored, or on a map you read: the last two the compendium's pins.
            var map = Section("Map");
            var row = map.Rows.Single(r => r.Book == BookPart.DungeonPins);
            Assert.Equal("Mark the dungeons", row.Label);
            Assert.Equal(new[] { "DungeonMarksWalkedByYou" }, row.Keys);
            Assert.Equal(new[] { "You walked", "In ground you explored", "On shared maps" }, row.TickNames);
            Assert.Equal(new[] { WorldSource.Explored, WorldSource.SharedMaps }, SettingsCatalog.PinnedGrounds);
            Assert.All(SettingsCatalog.PinnedGrounds, s => Assert.True(WorldSources.HasGround(s)));
            Assert.Contains("compendium", row.Why);
            // The filter by how a mark was walked is gone.
            Assert.DoesNotContain("DungeonMarksWalkedOnServer", Bound().Keys);
            Assert.DoesNotContain("DungeonMarkEntrances", Bound().Keys);
            Assert.Contains("Retire(config, \"DungeonMarksWalkedOnServer\", \"DungeonMarkEntrances\")", File.ReadAllText(Path.Combine(ScrySource.Root(), "src", "Core", "Settings.cs")));
        }

        [Fact]
        public void SizesAreChosenOneOfThreeAndSlidersKeepToValuesWithAUnit()
        {
            var marks = Rows.Single(r => r.Keys.FirstOrDefault() == "DungeonMarkSize");
            var dots = Rows.Single(r => r.Keys.FirstOrDefault() == "ShowOnMapDotSize");
            Assert.Equal(SettingControl.Choices, marks.Control);
            Assert.Equal(dots.Choices.Select(c => c.Name), marks.Choices.Select(c => c.Name));
            Assert.Equal(MapLook.MarkScales.Select(Stored.Exact), marks.Choices.Select(c => c.Value));
            // A slider stands only for seconds, metres, a loudness, a speed, a count or the panel's own size.
            var sliders = Rows.Where(r => r.Control == SettingControl.Slider).Select(r => r.Keys[0]).OrderBy(k => k).ToArray();
            Assert.Equal(new[] { "CatalogDelay", "LighterFilmFromMs", "PanelScale", "PreviewLoudness", "RecentCount", "SeenOnMapReach", "SeenReach", "SpinSpeed", "TooltipDelay" }, sliders);
        }

        [Fact]
        public void AChoiceStandsLitForTheValueSetHoweverTheConfigFileWritesItsNumber()
        {
            // The config file writes 0.7 where a choice was worded 0.699999988: the same value, lit.
            Assert.True(SettingsCatalog.SameValue(Stored.Exact(0.7f), "0.7"));
            Assert.True(SettingsCatalog.SameValue(Stored.Exact(1.4f), "1.4"));
            Assert.True(SettingsCatalog.SameValue("Small", "small"));
            Assert.False(SettingsCatalog.SameValue("0.7", "1"));
            Assert.False(SettingsCatalog.SameValue("Small", "Large"));
            Assert.False(SettingsCatalog.SameValue("0.7", null));
        }

        [Fact]
        public void TheCompendiumsSourcesAreShortRowsTheirWordsAShownLineAndTheirTips()
        {
            // As the box: what the sources share once under their heading, what each covers on its name's tip.
            var sources = Section("Compendium").Rows.Where(r => r.Book == BookPart.Source).ToList();
            Assert.All(sources, r => Assert.True(r.WhyAsTip, r.Label));
            Assert.All(Rows.Where(r => r.Book != BookPart.Source), r => Assert.False(r.WhyAsTip, r.Label));
            Assert.StartsWith(CompendiumWords.SourcesHint, SettingsCatalog.GroupIntro(CompendiumWords.SourcesCaption));
            Assert.Null(SettingsCatalog.GroupIntro("Dungeon marks"));
        }

        [Fact]
        public void ALockStandsBesideTheRowShortTheWholeSentenceOnItsTip()
        {
            Assert.Null(WorldRulesWords.OnRowShort(BookPart.Rules, default, WorldRules.Open));
            var set = WorldRules.From(WorldRule.Strict, true, false, s => s == WorldSource.SharedMaps ? SourceLevel.Known : SourceLevel.WithInsides);
            Assert.Equal("this server sets them", WorldRulesWords.OnRowShort(BookPart.Rules, default, set));
            Assert.Equal("off on this server", WorldRulesWords.OnRowShort(BookPart.KnownUpTo, default, set));
            Assert.Equal("up to Known here", WorldRulesWords.OnRowShort(BookPart.Source, WorldSource.SharedMaps, set));
            Assert.Null(WorldRulesWords.OnRowShort(BookPart.Source, WorldSource.Explored, set));
            foreach (BookPart part in Enum.GetValues(typeof(BookPart)))
                Assert.Equal(WorldRulesWords.OnRow(part, WorldSource.SharedMaps, set) == null, WorldRulesWords.OnRowShort(part, WorldSource.SharedMaps, set) == null);
        }

        [Fact]
        public void NoTwoRowsOfTheCardShareALabel()
        {
            // The same name in two places reads as the same setting, though they hold different values.
            var labels = SettingsCatalog.Sections.SelectMany(s => s.Rows).Select(r => r.Label).ToList();
            var twice = labels.GroupBy(l => l).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(twice.Count == 0, "Named twice on the card: " + string.Join(", ", twice));
        }

        [Fact]
        public void ARowSaysWhatTheServerKeepsFromItAndNothingWhereItKeepsNothing()
        {
            var open = WorldRules.Open;
            Assert.Null(WorldRulesWords.OnRow(BookPart.Rules, default, open));
            Assert.Null(WorldRulesWords.OnRow(BookPart.KnownUpTo, default, open));
            foreach (var source in WorldSources.All) Assert.Null(WorldRulesWords.OnRow(BookPart.Source, source, open));
            var set = WorldRules.From(WorldRule.Strict, true, false, s => s == WorldSource.SharedMaps ? SourceLevel.Known : SourceLevel.WithInsides);
            Assert.Equal("This server sets the rules: Strict.", WorldRulesWords.OnRow(BookPart.Rules, default, set));
            Assert.Equal("This server keeps Known up to off.", WorldRulesWords.OnRow(BookPart.KnownUpTo, default, set));
            Assert.Equal("This server allows Shared maps up to Known.", WorldRulesWords.OnRow(BookPart.Source, WorldSource.SharedMaps, set));
            Assert.Null(WorldRulesWords.OnRow(BookPart.Source, WorldSource.Explored, set));
        }

        [Fact]
        public void TheSectionSaysTheServerLimitsYourCompendiumOnlyWhereItDoes()
        {
            // A server leaving every row whole says so, rather than pointing at limits that are not there.
            Assert.Equal(SettingsWords.LimitsNone, SettingsWords.LimitsServer(WorldRules.Open));
            Assert.Equal(SettingsWords.LimitsSome, SettingsWords.LimitsServer(WorldRules.From(WorldRule.PlayersChoose, true, true, s => s == WorldSource.SharedMaps ? SourceLevel.Known : SourceLevel.WithInsides)));
            Assert.Equal(SettingsWords.LimitsSome, SettingsWords.LimitsServer(WorldRules.From(WorldRule.Loose, true, true, _ => SourceLevel.WithInsides)));
            // Peeking or the reach are no row of the compendium's, so they leave it whole.
            Assert.Equal(SettingsWords.LimitsNone, SettingsWords.LimitsServer(WorldRules.From(WorldRule.PlayersChoose, false, true, _ => SourceLevel.WithInsides, 0f)));
            // This server's own section says where the rest of what it decides stands.
            Assert.Contains("Compendium section", SettingsWords.ServerDecides);
            Assert.Contains("Compendium section", SettingsWords.ServerYours);
        }

        [Fact]
        public void EverySettingHasExactlyOneRowOnTheCard()
        {
            var keys = Rows.SelectMany(r => r.Keys).ToList();
            var twice = keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            Assert.True(twice.Count == 0, "On the card twice: " + string.Join(", ", twice));
            var bound = Bound().Keys;
            var missing = bound.Except(keys).ToList();
            Assert.True(missing.Count == 0, "Not on the card: " + string.Join(", ", missing));
            var unknown = keys.Except(bound).ToList();
            Assert.True(unknown.Count == 0, "On the card but bound nowhere: " + string.Join(", ", unknown));
        }

        [Fact]
        public void TheSectionsGoFromThePanelToTheServer()
        {
            Assert.Equal(new[] { "Panel", "Preview", "Compendium", "Map", "Background", "This server", "For checking Scry" },
                SettingsCatalog.Sections.Select(s => s.Name));
        }

        [Fact]
        public void EveryRowIsExplainedInWordsAPlayerReads()
        {
            foreach (var row in Rows)
            {
                Assert.False(string.IsNullOrWhiteSpace(row.Label), "A row has no label: " + string.Join(", ", row.Keys));
                Assert.True(row.Label.Length <= 28, $"{row.Label}: a label stands on one line, 28 letters at most");
                Assert.False(string.IsNullOrWhiteSpace(row.Why), row.Label + ": no explanation");
                Assert.EndsWith(".", row.Why);
                // A config key's own name (AutoSpin, LightDimmedRooms) means nothing on the card.
                foreach (var key in Bound().Keys) Assert.DoesNotContain(key, row.Why);
            }
        }

        [Fact]
        public void WhatTheServerDecidesStandsInItsOwnSectionAndSaysSo()
        {
            var bound = Bound();
            bool Server(SettingRow row) => row.Keys.Count > 0 && row.Keys.All(k => bound[k] == "4 - World" || bound[k] == "5 - Server");
            foreach (var section in SettingsCatalog.Sections)
            {
                foreach (var row in section.Rows)
                {
                    Assert.Equal(Server(row), row.ServerDecides);
                    Assert.Equal(Server(row), section.Name == "This server");
                    if (Server(row)) Assert.Contains("server", row.Why);
                    // What the server allows of a row of the player's stands under it, saying so.
                    if (row.Limit == null) continue;
                    Assert.True(Server(row.Limit) && row.Limit.ServerDecides, row.Label + ": what stands under it is the server's");
                    Assert.Contains("server", row.Limit.Why);
                    Assert.Null(row.Limit.Limit);
                }
            }
        }

        [Fact]
        public void EachControlHasWhatItNeeds()
        {
            foreach (var row in Rows)
            {
                switch (row.Control)
                {
                    case SettingControl.Key:
                    case SettingControl.Switch:
                        Assert.Single(row.Keys);
                        break;
                    case SettingControl.Choices:
                        Assert.Single(row.Keys);
                        Assert.True(row.Choices.Count >= 2, row.Label + ": one of at least two");
                        Assert.Equal(row.Choices.Count, row.Choices.Select(c => c.Value).Distinct().Count());
                        Assert.Equal(row.Choices.Count, row.Choices.Select(c => c.Name).Distinct().Count());
                        break;
                    case SettingControl.Ticks:
                        Assert.True(row.Keys.Count >= 2, row.Label + ": ticks are several at once");
                        Assert.Equal(row.Keys.Count, row.TickNames.Count);
                        break;
                    case SettingControl.Slider:
                        Assert.Single(row.Keys);
                        Assert.True(row.Min < row.Max && row.Step > 0f, row.Label + ": a range and a step");
                        Assert.False(string.IsNullOrEmpty(row.Shown(row.Min)), row.Label + ": its value says its unit");
                        break;
                    case SettingControl.Book:
                        Assert.NotEqual(BookPart.None, row.Book);
                        break;
                }
            }
        }

        [Fact]
        public void ChoicesNameValuesTheirSettingTakes()
        {
            var types = new Dictionary<string, Type>
            {
                ["StageQuality"] = typeof(StageQuality), ["Rules"] = typeof(WorldRule),
                ["ShowOnMapOtherPins"] = typeof(OtherPinsShown), ["ShowOnMapDarken"] = typeof(MapDarkening), ["ShowOnMapDotSize"] = typeof(DotSizeChoice), ["News"] = typeof(NewsShown),
            };
            foreach (var each in WorldSources.All) types[each + "UpTo"] = typeof(SourceLevel);
            foreach (var row in Rows.Where(r => r.Control == SettingControl.Choices))
            {
                var key = row.Keys[0];
                foreach (var choice in row.Choices)
                {
                    var fits = types.TryGetValue(key, out var type) ? Enum.GetNames(type).Contains(choice.Value)
                        : choice.Value == "true" || choice.Value == "false" || float.TryParse(choice.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
                    Assert.True(fits, $"{row.Label}: {choice.Value} is no value of {key}");
                }
            }
            // A source's levels are those it offers.
            foreach (var each in WorldSources.All)
            {
                var row = Rows.Single(r => r.Keys.FirstOrDefault() == each + "UpTo");
                Assert.Equal(WorldSources.Offered(each).Select(l => l.ToString()), row.Choices.Select(c => c.Value));
            }
        }

        [Fact]
        public void ASliderSaysItsValueInItsUnit()
        {
            Assert.Equal("1.25 times", Slider("PanelScale").Shown(1.25f));
            Assert.Equal("0.35 s", Slider("TooltipDelay").Shown(0.35f));
            Assert.Equal("at once", Slider("TooltipDelay").Shown(0f));
            Assert.Equal("30 entries", Slider("RecentCount").Shown(30f));
            Assert.Equal("14° a second", Slider("SpinSpeed").Shown(14f));
            Assert.Equal("50 m", Slider("SeenOnMapReach").Shown(50f));
            Assert.Equal("none", Slider("SeenOnMapReach").Shown(0f));
            Assert.Equal("100%", Slider("PreviewLoudness").Shown(1f));
            Assert.Equal("silent", Slider("PreviewLoudness").Shown(0f));
        }

        private static SettingRow Slider(string key) => Rows.Single(r => r.Control == SettingControl.Slider && r.Keys[0] == key);

        [Fact]
        public void SeveralThingsAtOnceAreTicksAndOneOfAFewAreChoices()
        {
            // Walking and looking while open, each on its own, and which dungeon marks show.
            Assert.Equal(SettingControl.Switch, Row("WalkWhileOpen").Control);
            Assert.Equal(SettingControl.Switch, Row("LookWithRightMouse").Control);
            Assert.Equal(SettingControl.Book, Row("DungeonMarksWalkedByYou").Control);
            Assert.Equal(SettingControl.Choices, Row("StageQuality").Control);
            // The tip's wait keeps any value the config holds.
            Assert.Equal(SettingControl.Slider, Row("TooltipDelay").Control);
        }

        private static SettingRow Row(string key) => Rows.Single(r => r.Keys.Contains(key));
    }
}
