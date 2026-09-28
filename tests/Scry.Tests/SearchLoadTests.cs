using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    /// <summary>
    /// Searching a catalog twice the size of the game's (15,000 entries, each with parts, biomes,
    /// players, places, a mod and stations, as a heavily modded game has), timed as a player types:
    /// every keystroke is a whole search, and must stay well inside a frame. The bounds are loose
    /// enough for a slow machine; the measured times are written to the test output. They run
    /// on their own, as other tests running beside them would be timed too.
    /// </summary>
    [Collection(nameof(SearchLoadTests))]
    public class SearchLoadTests
    {
        private readonly ITestOutputHelper _out;

        public SearchLoadTests(ITestOutputHelper output) => _out = output;

        private const int Size = 15000;

        /// <summary>A keystroke's search, at most, on this machine: one frame at 60 fps is 16.7 ms.</summary>
        private const double KeystrokeMs = 16;

        private static readonly string[] Words =
        {
            "troll", "greydwarf", "wood", "stone", "iron", "bronze", "silver", "black", "metal", "wolf", "boar", "deer", "neck",
            "draugr", "skeleton", "surtling", "fenring", "lox", "goblin", "seeker", "gjall", "tick", "dverger", "ash", "flame",
            "armor", "helmet", "sword", "axe", "bow", "arrow", "shield", "cape", "legs", "chest", "roof", "wall", "floor", "beam",
            "sfx", "vfx", "hit", "death", "idle", "alert", "attack", "footstep", "spawn", "destroyed", "fx", "projectile", "rock",
        };

        private static readonly string[] Parts =
        {
            "MeshRenderer", "Animator", "Character", "Humanoid", "MonsterAI", "ItemDrop", "Piece", "WearNTear", "ZSFX", "AudioSource",
            "ParticleSystem", "Light", "Projectile", "Aoe", "Pickable", "Destructible", "TreeBase", "MineRock5", "Container", "Fireplace",
        };

        private static readonly string[] Biomes = { "Meadows", "BlackForest", "Swamp", "Mountain", "Plains", "Mistlands", "AshLands", "DeepNorth", "Ocean" };
        private static readonly string[] Places = { "Sunken crypt rooms", "Burial chambers", "Troll cave", "Infested mine rooms", "Fuling village", "Dvergr tower" };
        private static readonly string[] Mods = { "", "", "", "", "CoolStatues", "Epic Loot", "Monstrum", "Plant Everything", "More Vanilla Build Prefabs" };

        private static List<Entry> BigCatalog()
        {
            var random = new Random(7);
            string Word() => Words[random.Next(Words.Length)];
            var kinds = (Kind[])Enum.GetValues(typeof(Kind));
            var catalog = new List<Entry>(Size);
            for (var i = 0; i < Size; i++)
            {
                var kind = kinds[random.Next(kinds.Length)];
                var mod = Mods[random.Next(Mods.Length)];
                var name = $"{Word()}_{Word()}_{Word()}{i}";
                catalog.Add(new Entry
                {
                    Name = name,
                    DisplayName = random.Next(3) == 0 ? "" : $"{Word()} {Word()}",
                    Kind = kind,
                    Origin = mod.Length > 0 ? Origin.Mod : Origin.Vanilla,
                    ModName = mod,
                    Components = Enumerable.Range(0, 3 + random.Next(10)).Select(_ => Parts[random.Next(Parts.Length)]).Distinct().ToArray(),
                    Biomes = Enumerable.Range(0, random.Next(3)).Select(_ => Biomes[random.Next(Biomes.Length)]).Distinct().ToArray(),
                    FoundIn = Enumerable.Range(0, random.Next(3)).Select(_ => Places[random.Next(Places.Length)]).Distinct().ToArray(),
                    UsedBy = Enumerable.Range(0, kind == Kind.Sound || kind == Kind.Effect ? random.Next(8) : 0).Select(_ => $"{Word()}_{Word()}").ToList(),
                    Stations = random.Next(4) == 0 ? new[] { new StationUse("forge", "Forge", 1 + random.Next(7)) } : new StationUse[0],
                    Group = Word(),
                    GroupOrder = random.Next(20),
                });
            }
            return catalog;
        }

        private static (double Median, double Max) Time(int rounds, Action act, Action before = null)
        {
            before?.Invoke();
            act();
            var times = new List<double>();
            for (var i = 0; i < rounds; i++)
            {
                before?.Invoke();
                var watch = Stopwatch.StartNew();
                act();
                times.Add(watch.Elapsed.TotalMilliseconds);
            }
            times.Sort();
            return (times[times.Count / 2], times[times.Count - 1]);
        }

        [Fact]
        public void TypingAWordIsASearchAKeystrokeWellInsideAFrame()
        {
            var explorer = new Explorer(BigCatalog(), new Favourites(Path.Combine(TempDir(), "f.txt")));
            var typed = "troll armor";
            // Each keystroke's time is its best over the rounds, so a pause of the test process
            // (a garbage collection, another process on the machine) is not taken for the search's
            // own cost; the slowest keystroke is then the slowest of those.
            var best = new double[typed.Length];
            for (var i = 0; i < best.Length; i++) best[i] = double.MaxValue;
            for (var round = 0; round < 5; round++)
            {
                explorer.Text = "";
                for (var i = 1; i <= typed.Length; i++)
                {
                    var watch = Stopwatch.StartNew();
                    explorer.Text = typed.Substring(0, i);
                    var ms = watch.Elapsed.TotalMilliseconds;
                    if (round > 0) best[i - 1] = Math.Min(best[i - 1], ms);
                }
            }
            var times = new List<double>(best);
            times.Sort();
            var worst = times[times.Count - 1];
            var median = times[times.Count / 2];
            _out.WriteLine($"typing \"{typed}\" over {Size} entries: median {median:0.00} ms, slowest {worst:0.00} ms a keystroke");
            Assert.True(median < KeystrokeMs / 2, $"median {median:0.00} ms");
            Assert.True(worst < KeystrokeMs, $"slowest {worst:0.00} ms");
        }

        [Theory]
        [InlineData("")]
        [InlineData("t")]
        [InlineData("kind:creature")]
        [InlineData("has:character")]
        [InlineData("biome:swamp")]
        [InlineData("playedby:troll")]
        [InlineData("in:crypt")]
        [InlineData("mod:epic")]
        [InlineData("station:forge3")]
        [InlineData("troll -kind:effect -has:light")]
        public void EveryKindOfTermSearchesWellInsideAFrame(string text)
        {
            var explorer = new Explorer(BigCatalog(), new Favourites(Path.Combine(TempDir(), "f.txt")));
            // Each round starts from a search that matches nothing, which is quick and untimed.
            var (median, max) = Time(9, () => explorer.Text = text, () => explorer.Text = "zzqqzz");
            _out.WriteLine($"\"{text}\": median {median:0.00} ms, slowest {max:0.00} ms");
            Assert.True(median < KeystrokeMs, $"median {median:0.00} ms a search");
        }

        [Fact]
        public void SwitchingTabsIsWellInsideAFrame()
        {
            var explorer = new Explorer(BigCatalog(), new Favourites(Path.Combine(TempDir(), "f.txt"))) { Text = "troll" };
            var kinds = ((Kind[])Enum.GetValues(typeof(Kind))).Cast<Kind?>().Concat(new Kind?[] { null }).ToArray();
            var (median, max) = Time(5, () => { foreach (var kind in kinds) explorer.KindFilter = kind; });
            var each = median / kinds.Length;
            _out.WriteLine($"switching tabs: {each:0.00} ms a tab (median), slowest round {max:0.00} ms for {kinds.Length}");
            Assert.True(each < KeystrokeMs / 2, $"{each:0.00} ms a tab");
        }

        [Fact]
        public void SuggestionsAKeystrokeAreWellInsideAFrame()
        {
            var catalog = BigCatalog();
            var build = Stopwatch.StartNew();
            var index = new TermIndex(catalog);
            var built = build.Elapsed.TotalMilliseconds;

            var words = new[] { "t", "tr", "tro", "has:", "has:c", "biome:s", "playedby:", "playedby:tro", "in:", "mod:e", "station:f", "kind:c" };
            var (median, max) = Time(9, () => { foreach (var word in words) SearchHelp.Suggest(word, index); });
            var each = median / words.Length;
            _out.WriteLine($"term index for {Size} entries built in {built:0.0} ms (once a world); suggestions {each:0.000} ms a keystroke (median), slowest round {max:0.00} ms for {words.Length}");
            Assert.True(each < 2, $"{each:0.00} ms a keystroke");
        }

        [Theory]
        [InlineData("has:")]
        [InlineData("has:c")]
        [InlineData("has:light")]
        [InlineData("biome:")]
        [InlineData("biome:s")]
        [InlineData("in:")]
        [InlineData("in:crypt")]
        [InlineData("mod:")]
        [InlineData("mod:e")]
        [InlineData("playedby:tro")]
        [InlineData("station:f")]
        [InlineData("kind:c")]
        [InlineData("-has:mesh")]
        public void EachSuggestionCountsWhatSearchingForItFinds(string word)
        {
            var catalog = BigCatalog().Take(3000).ToList();
            var index = new TermIndex(catalog);
            var suggestions = SearchHelp.Suggest(word, index);
            Assert.NotEmpty(suggestions);
            foreach (var suggestion in suggestions)
            {
                // What the search itself finds for the term, with any minus taken off.
                var term = suggestion.Insert.TrimStart('-');
                var found = catalog.Count(e => Search.Matches(e, term));
                Assert.Equal(found, suggestion.Count);
            }
        }
    }

    /// <summary>Runs the timed tests with nothing else running.</summary>
    [CollectionDefinition(nameof(SearchLoadTests), DisableParallelization = true)]
    public class SearchLoadCollection
    {
    }
}
