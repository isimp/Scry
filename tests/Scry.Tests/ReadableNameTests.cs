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
        public void ANamelessThingIsNamedByItsPrefabsWords(string prefab, string named)
        {
            Assert.Equal(named, Naming.Readable(prefab));
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

        [Fact]
        public void ANameOfNothingButNumbersOrTagsStaysAsItIs()
        {
            Assert.Equal("12", Naming.Readable("12"));
            Assert.Equal("frac_01", Naming.Readable("frac_01"));
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
