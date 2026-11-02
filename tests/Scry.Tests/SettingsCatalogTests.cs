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

        private static IEnumerable<SettingRow> Rows => SettingsCatalog.Sections.SelectMany(s => s.Rows);

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
            foreach (var section in SettingsCatalog.Sections)
            {
                foreach (var row in section.Rows)
                {
                    var server = row.Keys.All(k => bound[k] == "4 - World" || bound[k] == "5 - Server");
                    Assert.Equal(server, row.ServerDecides);
                    Assert.Equal(server, section.Name == "This server");
                    if (server) Assert.Contains("server", row.Why);
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
                }
            }
        }

        [Fact]
        public void ChoicesNameValuesTheirSettingTakes()
        {
            var types = new Dictionary<string, Type>
            {
                ["StageQuality"] = typeof(StageQuality), ["Rules"] = typeof(WorldRule),
                ["ShowOnMapOtherPins"] = typeof(OtherPinsShown), ["ShowOnMapDarken"] = typeof(MapDarkening), ["ShowOnMapDotSize"] = typeof(DotSizeChoice),
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
                var row = Rows.Single(r => r.Keys[0] == each + "UpTo");
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
            Assert.Equal("a pin's size", Slider("DungeonMarkSize").Shown(1f));
            Assert.Equal("0.8 of a pin", Slider("DungeonMarkSize").Shown(0.8f));
            Assert.Equal("14° a second", Slider("SpinSpeed").Shown(14f));
            Assert.Equal("50 m", Slider("SeenOnMapReach").Shown(50f));
            Assert.Equal("none", Slider("SeenOnMapReach").Shown(0f));
        }

        private static SettingRow Slider(string key) => Rows.Single(r => r.Control == SettingControl.Slider && r.Keys[0] == key);

        [Fact]
        public void SeveralThingsAtOnceAreTicksAndOneOfAFewAreChoices()
        {
            // Walking and looking while open, each on its own, and which dungeon marks show.
            Assert.Equal(SettingControl.Switch, Row("WalkWhileOpen").Control);
            Assert.Equal(SettingControl.Switch, Row("LookWithRightMouse").Control);
            Assert.Equal(SettingControl.Ticks, Row("DungeonMarksWalkedByYou").Control);
            Assert.Equal(new[] { "DungeonMarksWalkedByYou", "DungeonMarksWalkedOnServer", "DungeonMarkEntrances" }, Row("DungeonMarksWalkedByYou").Keys);
            Assert.Equal(SettingControl.Choices, Row("StageQuality").Control);
            // The tip's wait keeps any value the config holds.
            Assert.Equal(SettingControl.Slider, Row("TooltipDelay").Control);
        }

        private static SettingRow Row(string key) => Rows.Single(r => r.Keys.Contains(key));
    }
}
