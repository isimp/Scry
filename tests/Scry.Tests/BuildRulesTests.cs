using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class BuildRulesTests
    {
        // Where a piece may be placed, as Player.UpdatePlacementGhost refuses it: on the ground,
        // cultivated ground or dirt only, on water or not in it, not on wood, not on a slope,
        // under a ceiling or on a wall only, in a teleport area, in deep snow; and in dungeons
        // only when it allows it. The biomes it keeps to, the pieces it keeps apart from and the
        // one it must stand near are each a chip going to it, under a title saying how.

        [Fact]
        public void APiecesPlacementRulesAreToldInWords()
        {
            var rules = new PlacementRules
            {
                GroundOnly = true, CultivatedOnly = true, DirtOnly = true, OnWater = true, NotInWater = true, NotOnWood = true, Level = true,
                CeilingOnly = true, NotOnFloor = true, TeleportArea = true, InDungeons = true, DeepSnowOnly = true,
            };

            Assert.Equal(new[]
            {
                "on the ground only", "on cultivated ground only", "on dirt only", "on water only", "not in water", "not on wood", "not on a slope",
                "under a ceiling only", "not on a floor", "in a teleport area only", "in the Deep North's deep snow only", "in dungeons too",
            }, BuildWords.Placement(rules));
        }

        [Fact]
        public void APieceWithNoRulesHasNoLine()
        {
            Assert.Empty(BuildWords.Placement(new PlacementRules()));
        }

        [Fact]
        public void SpacingRulesSayHowFarFromWhatTheyName()
        {
            Assert.Equal("Not within 5 m of", BuildWords.KeptApart(5f));
            // The game keeps pieces apart only with a radius to keep them by.
            Assert.Null(BuildWords.KeptApart(0f));
            Assert.Equal("Within 1.5 m of", BuildWords.Near(1.5f, above: false));
            Assert.Equal("On top of, within 2 m", BuildWords.Near(2f, above: true));
        }

        // A station's own reach (CraftingStation.m_rangeBuild, more with each upgrade).
        [Fact]
        public void AStationsReachGrowsWithItsUpgrades()
        {
            Assert.Equal("20 m", BuildWords.Range(20f, 0f));
            Assert.Equal("20 m, 4 m more for each upgrade", BuildWords.Range(20f, 4f));
        }

        // What an area does (EffectArea.Type), each by where the game reads it.
        [Fact]
        public void EachKindOfAreaSaysWhatItDoes()
        {
            var lines = AreaWords.Lines(0x01 | 0x02 | 0x04 | 0x08 | 0x10 | 0x20 | 0x40 | 0x80, 10f);

            Assert.Equal(new List<(string, string)>
            {
                ("Warmth", "within 10 m you are by a fire, which resting and sleeping need"),
                ("Fire", "creatures afraid of fire keep away from within 10 m"),
                ("A base", "within 10 m the world's spawning and creature spawners place nothing, unless set to; three such near you let raids that come for bases come"),
                ("Flames", "a cooking station needing a fire cooks over it"),
                ("Teleport area", "pieces built only in one can be built within 10 m"),
                ("No monsters", "untamed creatures turn away from 15 m out and will not chase you into its 10 m"),
                ("Warm and cozy", "within 10 m cold and freezing do not reach you, and you rest even wet"),
            }, lines);
        }

        [Fact]
        public void AnAreaWithNoKnownSizeSaysAroundIt()
        {
            Assert.Equal(new List<(string, string)> { ("Warmth", "near it you are by a fire, which resting and sleeping need") }, AreaWords.Lines(0x01, 0f));
            Assert.Empty(AreaWords.Lines(0, 10f));
        }

        [Fact]
        public void EachKindOfAreaIsToldOnItsOwn()
        {
            Assert.Equal(new[] { "Fire" }, AreaWords.Lines(0x02, 5f).ConvertAll(l => l.Label));
            Assert.Equal(new[] { "No monsters" }, AreaWords.Lines(0x20, 5f).ConvertAll(l => l.Label));
            Assert.Equal(new[] { "Warm and cozy" }, AreaWords.Lines(0x40, 5f).ConvertAll(l => l.Label));
            Assert.Equal(new[] { "A base", "Flames" }, AreaWords.Lines(0x04 | 0x08, 5f).ConvertAll(l => l.Label));
            Assert.Equal(new[] { "Flames" }, AreaWords.Lines(0x08, 5f).ConvertAll(l => l.Label));
            Assert.Empty(AreaWords.Lines(0x80, 5f));
        }

        // A bed (Bed.Interact): claiming it makes it where you come back to, and needs a roof
        // and most of it covered; sleeping in it needs night, no enemy sensing you, the same
        // cover, a fire near and being dry.
        [Fact]
        public void ABedSaysWhatClaimingAndSleepingNeed()
        {
            Assert.Equal("where you come back to after dying; it needs a roof and at least 80% cover", BuildWords.BedClaim(0.8f));
            Assert.Equal("at night, with no enemy sensing you, under a roof with at least 80% cover, by a fire, and dry", BuildWords.BedSleep(0.8f));
        }
    }
}
