using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class FishPageTests
    {
        // A fish's page tells where it lives, when it comes, how it finds a bait and fights, by the
        // game's own figures (Fish, SpawnSystem.SpawnData, FishingFloat): the Perch's here, as
        // 1.0.20 sets them. A fish no world spawn places tells only what it is itself.

        private static FishTraits Perch() => new FishTraits
        {
            MinDepth = 1.5f, MaxDepth = 3f, SwimRange = 10f, WaypointMin = 5f, WaypointMax = 15f, HookChance = 0.1f,
            StaminaUse = 3f, EscapeStaminaUse = 10f, EscapeMin = 0.5f, EscapeMax = 3f, EscapePerLevel = 1.5f, RestMin = 1.5f, RestMax = 5f,
            JumpChance = 0.25f, JumpMaxLevel = 2f, ScaleByQuality = 0.4f, FloatRange = 50f, LineBreakPast = 10f, LineMost = 30f,
            RawAmount = 1, RawPerQuality = 3f,
        };

        private static FishSpawnRule PerchSpawn() => new FishSpawnRule
        {
            MinAltitude = -3f, MaxAltitude = -1.5f, Interval = 120f, Chance = 50f, MaxSpawned = 5, SpawnDistance = 20f,
            GroupMin = 2, GroupMax = 6, NearPlayer = 40f, InsideBase = false, MinLevel = 1, MaxLevel = 5, LevelUpChance = 20f,
        };

        private static string Line(FishPage page, string label) => page.Lines.Where(l => l.Label == label).Select(l => l.Value).SingleOrDefault();

        [Fact]
        public void APerchTellsWhereItLivesAndWhenItComes()
        {
            var page = FishPage.Of(Perch(), new[] { PerchSpawn() });
            Assert.Equal("1.5–3 m down", Line(page, FishWords.Swims));
            Assert.Equal("2–6", Line(page, FishWords.GroupsOf));
            Assert.Equal("up to 4", Line(page, FishWords.Stars));
            Assert.Equal("where the bottom lies 1.5 to 3 m below the surface; it swims 1.5 to 3 m down, within 10 m of where it came", Line(page, FishWords.Water));
            Assert.Equal("within 40 m of a player, inside a base, or where another of its kind swims within 20 m", Line(page, FishWords.Never));
            Assert.Equal("up to 5 near you", Line(page, FishWords.HowMany));
            Assert.Equal("one try every 2 min, half of them bring a group", Line(page, FishWords.Comes));
            Assert.Equal("most at dawn and dusk, least at noon and midnight; one with 2 or more stars never leaps", Line(page, FishWords.Leaps));
        }

        [Fact]
        public void ADeepWaterFishSaysHowDeepTheBottomMustBeAtLeast()
        {
            var tuna = PerchSpawn();
            tuna.MinAltitude = -999f;
            tuna.MaxAltitude = -5f;
            tuna.NearPlayer = 0f;
            tuna.InsideBase = true;
            tuna.SpawnDistance = 0f;
            var page = FishPage.Of(Perch(), new[] { tuna });
            Assert.StartsWith("where the bottom lies 5 m or more below the surface", Line(page, FishWords.Water));
            // Nothing keeps it away: no line.
            Assert.Null(Line(page, FishWords.Never));
        }

        [Fact]
        public void HowItFindsTheFloatAndWhatDrivesItOff()
        {
            var page = FishPage.Of(Perch(), new[] { PerchSpawn() });
            Assert.Equal("within 50 m, one time in ten each time it swims somewhere new, every 5 to 15 s: about once in 100 s for each one near", Line(page, FishWords.FindsFloat));
            Assert.Equal(FishWords.SwimsOffLine, Line(page, FishWords.SwimsOff));
            Assert.Equal(FishWords.WrongBaitLine, Line(page, FishWords.WrongBait));
            Assert.Equal("breaks once the float is 10 m past the line, or 30 m out", Line(page, FishWords.Line));
        }

        [Fact]
        public void TheFightGoesByStars()
        {
            var table = FishPage.Of(Perch(), new[] { PerchSpawn() }).Fight;
            Assert.Equal(new[] { "", "no star", "1", "2", "3", "4" }, table.Columns);
            string[] Row(string label) => table.Lines.Single(l => l[0] == label).Skip(1).ToArray();
            Assert.Equal(new[] { "80%", "16%", "3.2%", "0.6%", "0.2%" }, Row(FishWords.ComesWith));
            Assert.Equal(new[] { "4.5 s", "6 s", "7.5 s", "9 s", "10.5 s" }, Row(FishWords.PullsFor));
            Assert.Equal(new[] { "3 / 10", "6 / 20", "9 / 30", "12 / 40", "15 / 50" }, Row(FishWords.Drains));
            Assert.Equal(new[] { "×1", "×1.4", "×1.8", "×2.2", "×2.6" }, Row(FishWords.Size));
            Assert.Equal(new[] { "1", "4", "7", "10", "13" }, Row(FishWords.RawFish));
            // Weight that does not grow with quality has no row.
            Assert.DoesNotContain(table.Lines, l => l[0] == FishWords.WeightRow);
        }

        [Fact]
        public void AFishNoSpawnPlacesTellsOnlyWhatItIs()
        {
            var page = FishPage.Of(Perch(), new FishSpawnRule[0]);
            Assert.Equal("1.5–3 m down", Line(page, FishWords.Swims));
            Assert.Null(Line(page, FishWords.Comes));
            Assert.Null(Line(page, FishWords.GroupsOf));
            // Its own water alone: how it swims.
            Assert.Equal("it swims 1.5 to 3 m down, within 10 m of where it came", Line(page, FishWords.Water));
            // One star column alone: the fight with no star, no odds to tell.
            Assert.Equal(new[] { "", "no star" }, page.Fight.Columns);
            Assert.DoesNotContain(page.Fight.Lines, l => l[0] == FishWords.ComesWith);
        }

        [Fact]
        public void AFishThatNeverLeapsSaysSo()
        {
            var calm = Perch();
            calm.JumpChance = 0f;
            Assert.Equal(FishWords.NeverLeaps, Line(FishPage.Of(calm, new[] { PerchSpawn() }), FishWords.Leaps));
        }

        [Fact]
        public void TheLevelUpChanceScalesTheStarOdds()
        {
            var spawn = PerchSpawn();
            spawn.LevelUpChance = 50f;
            spawn.MaxLevel = 3;
            var table = FishPage.Of(Perch(), new[] { spawn }).Fight;
            Assert.Equal(new[] { "50%", "25%", "25%" }, table.Lines.Single(l => l[0] == FishWords.ComesWith).Skip(1).ToArray());
        }
    }
}
