using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class PlacesTests
    {
        [Theory]
        [InlineData("Crypt2", "Crypt")]
        [InlineData("WoodHouse10", "Wood house")]
        [InlineData("TheHole01", "The hole")]
        [InlineData("GoblinCamp2_1", "Goblin camp")]
        [InlineData("DN_Bossroom", "DN bossroom")]
        [InlineData("Mistlands_DvergrBossEntrance1", "Mistlands dvergr boss entrance")]
        [InlineData("Mistlands_GuardTower1_new", "Mistlands guard tower new")]
        public void ALocationGoesByItsNameInWordsWithoutTheNumberOfItsVariant(string prefab, string shown)
        {
            Assert.Equal(shown, Places.LocationLabel(prefab));
        }

        [Theory]
        [InlineData("SunkenCrypt", "Sunken crypt rooms")]
        [InlineData("Crypt", "Crypt rooms")]
        public void DungeonRoomsGoByTheirKindOfDungeon(string theme, string shown)
        {
            Assert.Equal(shown, Places.RoomLabel(theme));
        }

        [Fact]
        public void ANameWithNoWordsInItKeepsItsOwnSoThePlaceIsNeverNameless()
        {
            Assert.Equal("2048", Places.LocationLabel("2048"));
            Assert.Equal("65536 rooms", Places.RoomLabel("65536"));
        }

        private static Dictionary<string, HashSet<string>> Found(params (string Prefab, string Place)[] finds)
        {
            var found = new Dictionary<string, HashSet<string>>();
            foreach (var (prefab, place) in finds)
            {
                if (!found.TryGetValue(prefab, out var places)) found[prefab] = places = new HashSet<string>();
                places.Add(place);
            }
            return found;
        }

        [Fact]
        public void APrefabFoundInPlacesListsEachOnceInOrder()
        {
            var catalog = Game();
            Places.Apply(catalog, Found(("Troll", "Troll cave"), ("Troll", "Crypt"), ("Troll", "Troll cave")));

            Assert.Equal(new[] { "Crypt", "Troll cave" }, catalog.Single(e => e.Name == "Troll").FoundIn);
            Assert.Empty(catalog.Single(e => e.Name == "TrollArmorChest").FoundIn);
        }

        [Fact]
        public void AnEffectNothingWasFoundToPlayButALocationNamesGoesUnderInLocationsBeforeTheUnplayed()
        {
            var unplayed = Groups.Purpose(new string[0], false, false);
            var music = E("Music_StoneHenge", Kind.Sound);
            music.Group = unplayed.Name;
            music.GroupOrder = unplayed.Order;
            var lost = E("sfx_bat_attack", Kind.Sound);
            lost.Group = unplayed.Name;
            lost.GroupOrder = unplayed.Order;
            var catalog = new List<Entry> { music, lost };

            Places.Apply(catalog, Found(("Music_StoneHenge", "Stone henge")));

            Assert.Equal("In locations", music.Group);
            Assert.True(music.GroupOrder < unplayed.Order);
            Assert.True(music.GroupOrder > Groups.Purpose(new[] { "m_effects" }, false, false).Order);
            Assert.Equal(unplayed.Name, lost.Group);
        }

        [Fact]
        public void AnEffectWithAPurposeKeepsItsGroupWhereverItIsFound()
        {
            var hit = Groups.Purpose(new[] { "m_hitEffects" }, false, false);
            var sparks = E("vfx_sparks", Kind.Effect);
            sparks.Group = hit.Name;
            sparks.GroupOrder = hit.Order;

            Places.Apply(new List<Entry> { sparks }, Found(("vfx_sparks", "Crypt")));

            Assert.Equal(hit.Name, sparks.Group);
            Assert.Equal(new[] { "Crypt" }, sparks.FoundIn);
        }

        [Fact]
        public void AProjectileNothingFiresButALocationNamesGoesUnderInLocationsBeforeTheRest()
        {
            var other = Groups.Projectile(new Shooter[0]);
            var bolt = E("FrozenKing_P2_Projectile_Eikthyr", Kind.Projectile);
            bolt.Group = other.Name;
            bolt.GroupOrder = other.Order;

            Places.Apply(new List<Entry> { bolt }, Found(("FrozenKing_P2_Projectile_Eikthyr", "DN bossroom")));

            Assert.Equal("In locations", bolt.Group);
            Assert.True(bolt.GroupOrder < other.Order);
            Assert.True(bolt.GroupOrder > Groups.Projectile(new[] { new Shooter(Kind.Creature, "", false) }).Order);
        }

        [Fact]
        public void AStatusEffectIsNeverFoundInAPlaceByTheNameOfAPrefab()
        {
            var rested = E("Rested", Kind.StatusEffect);
            Places.Apply(new List<Entry> { rested }, Found(("Rested", "Crypt")));

            Assert.Empty(rested.FoundIn);
        }
    }
}
