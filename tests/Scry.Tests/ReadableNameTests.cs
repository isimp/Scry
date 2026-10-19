using Xunit;

namespace Scry.Tests
{
    public class ReadableNameTests
    {
        // Where the game gives a thing no name, a page names it by its prefab's words, as a
        // player would say them: no numbers, no tags that only tell its makers how it is built.

        [Theory]
        [InlineData("rock4_heath_frac", "Rock heath")]
        [InlineData("cliff_ashlands6_frac", "Cliff ashlands")]
        [InlineData("Pickable_MountainCaveRandom", "Mountain cave random")]
        [InlineData("Morkhalla Eye (1)", "Morkhalla eye")]
        [InlineData("troll_throw_projectile", "Troll throw projectile")]
        [InlineData("Rock_destructible_test", "Rock destructible")]
        [InlineData("prop_hearth", "Hearth")]
        [InlineData("marker01", "Marker")]
        [InlineData("RockDolmen_1", "Rock dolmen")]
        [InlineData("Troll(Clone)", "Troll")]
        [InlineData("StoneTower3Ruin", "Stone tower ruin")]
        // The makers' marks for an effect and an area of effect.
        [InlineData("fx_Puke", "Puke")]
        [InlineData("aoe_nova", "Area nova")]
        // A word said twice in a row is said once.
        [InlineData("cave_shrine_shrine02", "Cave shrine")]
        public void ANamelessThingIsNamedByItsPrefabsWords(string prefab, string named)
        {
            Assert.Equal(named, Naming.Readable(prefab));
        }

        [Theory]
        [InlineData("sfx_troll_hit", "Troll hit sound")]
        [InlineData("vfx_troll_hit", "Troll hit visual")]
        [InlineData("vfx_spawn", "Spawn visual")]
        [InlineData("fx_troll_hit", "Troll hit")]
        // Said once where the prefab says it already.
        [InlineData("sfx_hit_sound", "Hit sound")]
        public void ASoundOrAVisualSaysWhatItIs(string prefab, string named)
        {
            // The makers mark a sound "sfx", a visual "vfx" and an effect of both "fx"; read
            // without the marks, a creature's hit, its look and its sound would go by one name.
            Assert.Equal(named, Naming.Readable(prefab));
        }

        [Fact]
        public void AnEffectAndItsSoundSideBySideReadApart()
        {
            Assert.NotEqual(Naming.Readable("fx_Abomination_arise_end"), Naming.Readable("sfx_abomination_arise_end"));
            Assert.Equal("Abomination arise end sound", Naming.Readable("sfx_abomination_arise_end"));
        }

        [Theory]
        [InlineData("slap", "Slap")]
        [InlineData("LOG", "Log")]
        [InlineData("fireballattack", "Fireball attack")]
        [InlineData("attack", "Attack")]
        public void AnAttackTheGameNamesInItsOwnShorthandReadsAsWords(string given, string named)
        {
            Assert.Equal(named, Naming.Readable(given));
        }

        [Theory]
        [InlineData("GrapplingHook", "Grappling hook")]
        [InlineData("Troll", "Troll")]
        [InlineData("Wood RoundWall Odin Window", "Wood RoundWall Odin Window")]
        [InlineData("BBQ", "BBQ")]
        public void AGameNameRunTogetherInOneWordReadsAsWords(string given, string named)
        {
            // An untranslated name of the game's or a mod's: one word with capitals inside.
            Assert.Equal(named, Naming.SpacedOut(given));
        }

        [Fact]
        public void ANameOfNothingButNumbersOrTagsStaysAsItIs()
        {
            Assert.Equal("12", Naming.Readable("12"));
            Assert.Equal("frac_01", Naming.Readable("frac_01"));
            Assert.Equal("vfx", Naming.Readable("vfx"));
            Assert.Equal("", Naming.Readable(null));
            Assert.Equal("", Naming.Readable(""));
        }

        [Theory]
        [InlineData("Rock heath", "Rock")]
        [InlineData("Cliff ashlands", "Cliff")]
        [InlineData("Rock deepnorth", "Rock")]
        [InlineData("Heath rock pillar", "Rock pillar")]
        [InlineData("Mountain grave stone", "Grave stone")]
        [InlineData("Plains", "Plains")]
        [InlineData("Copper deposit", "Copper deposit")]
        public void ThingsAlikeButForTheirBiomeGoByOneName(string named, string kin)
        {
            Assert.Equal(kin, Naming.WithoutBiome(named));
        }
    }
}
