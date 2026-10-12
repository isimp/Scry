using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    // What the places hold is told on the catalog's entries each time they are read or found
    // kept: a location goes by the name its place was read to have, everything a place names is
    // found in it, and an effect or projectile only places play goes under "In locations".
    // Telling again, as on reading the places again, leaves the catalog as telling once would.
    public class PlaceFindsTests
    {
        private static string Label(string name, string biome) => name + Places.BiomeMark + biome;

        private static Entry Location(string prefab, string label, params string[] biomes)
        {
            var entry = E(prefab, Kind.Location, Places.NameOf(label));
            entry.Components = new[] { "Location" };
            entry.FoundIn = new[] { label };
            entry.Biomes = biomes;
            return entry;
        }

        private static Entry Room(string prefab, params string[] dungeons)
        {
            var entry = E(prefab, Kind.Location, "Room " + prefab);
            entry.Components = new[] { "Room" };
            entry.FoundIn = dungeons;
            return entry;
        }

        private static Entry Grouped(string name, Kind kind, (string Name, int Order) group)
        {
            var entry = E(name, kind);
            entry.Group = group.Name;
            entry.GroupOrder = group.Order;
            return entry;
        }

        private static (string, bool)? PlaceOf(Entry entry) =>
            entry.Kind == Kind.Location ? (entry.Name, entry.Components.Contains("Room")) : ((string, bool)?)null;

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

        private static Dictionary<(string, bool), string[]> Labels(params (string Prefab, bool Room, string[] Labels)[] labels) =>
            labels.ToDictionary(l => (l.Prefab, l.Room), l => l.Labels);

        private static readonly (string Name, int Order) Unplayed = (Groups.Purpose(new string[0], false, false).Name, Groups.Purpose(new string[0], false, false).Order);
        private static readonly (string Name, int Order) Unfired = (Groups.Projectile(new Shooter[0]).Name, Groups.Projectile(new Shooter[0]).Order);

        /// <summary>A small world: a ruin, a crypt known by its prefab's words until read, a cave, a crypt room, and what they hold.</summary>
        private static List<Entry> World() => new List<Entry>
        {
            Location("Ruin3", Label("Ruin", "Meadows"), "Meadows"),
            Location("Crypt2", Label("Crypt", "Black Forest"), "BlackForest"),
            Location("MountainCave02", Label("Mountain cave", "Mountain"), "Mountain"),
            Room("forestcrypt_Corridor1", Places.AnyDungeon),
            E("Amber", Kind.Item),
            E("Coins", Kind.Item),
            Grouped("Music_Crypt", Kind.Sound, Unplayed),
            Grouped("sfx_bat_attack", Kind.Sound, Unplayed),
            Grouped("Crypt_Projectile", Kind.Projectile, Unfired),
        };

        private static readonly string Chambers = Label("Burial Chambers", "Black Forest");

        /// <summary>The crypt read: it goes by the name shown on entering it, and its rooms by it.</summary>
        private static Dictionary<(string, bool), string[]> Read() => Labels(
            ("Ruin3", false, new[] { Label("Ruin", "Meadows") }),
            ("Crypt2", false, new[] { Chambers }),
            ("MountainCave02", false, new[] { Label("Mountain cave", "Mountain") }),
            ("forestcrypt_Corridor1", true, new[] { Chambers }));

        private static Entry Of(List<Entry> catalog, string name) => catalog.Single(e => e.Name == name);

        private static string Told(Entry e) => string.Join("|", e.FoundIn) + " # " + e.DisplayName + " # " + e.Group + " # " + e.GroupOrder;

        [Fact]
        public void APlaceNamedOnlyOnceReadGoesInItsBiomeTheFirstTimeItIsTold()
        {
            var catalog = World();

            new PlaceFinds().Tell(catalog, Found(("Amber", Label("Mountain cave", "Mountain")), ("Amber", Chambers), ("Amber", Label("Ruin", "Meadows"))), Read(), PlaceOf);

            Assert.Equal(new[] { Label("Ruin", "Meadows"), Chambers, Label("Mountain cave", "Mountain") }, Of(catalog, "Amber").FoundIn);
        }

        [Fact]
        public void ALocationGoesByTheNameItsPlaceWasReadToHaveAndARoomByItsDungeons()
        {
            var catalog = World();

            new PlaceFinds().Tell(catalog, Found(), Read(), PlaceOf);

            var crypt = Of(catalog, "Crypt2");
            Assert.Equal(new[] { Chambers }, crypt.FoundIn);
            Assert.Equal("Burial Chambers", crypt.DisplayName);
            var room = Of(catalog, "forestcrypt_Corridor1");
            Assert.Equal(new[] { Chambers }, room.FoundIn);
            Assert.Equal("Room forestcrypt_Corridor1", room.DisplayName);
        }

        [Fact]
        public void APlaceNotReadKeepsWhatItWentBy()
        {
            var catalog = World();

            new PlaceFinds().Tell(catalog, Found(), Labels(("Crypt2", false, new string[0])), PlaceOf);

            Assert.Equal(new[] { Label("Crypt", "Black Forest") }, Of(catalog, "Crypt2").FoundIn);
            Assert.Equal("Crypt", Of(catalog, "Crypt2").DisplayName);
            Assert.Equal(new[] { Places.AnyDungeon }, Of(catalog, "forestcrypt_Corridor1").FoundIn);
        }

        [Fact]
        public void WhatOnlyPlacesPlayOrFireGoesUnderInLocationsAndTheRestStays()
        {
            var catalog = World();

            var told = new PlaceFinds().Tell(catalog, Found(("Music_Crypt", Chambers), ("Crypt_Projectile", Chambers), ("Amber", Chambers)), Read(), PlaceOf);

            Assert.Equal("In locations", Of(catalog, "Music_Crypt").Group);
            Assert.Equal("In locations", Of(catalog, "Crypt_Projectile").Group);
            Assert.Equal(Unplayed.Name, Of(catalog, "sfx_bat_attack").Group);
            Assert.Equal(2, told.Moved);
            Assert.Equal(3, told.InCatalog);
        }

        [Fact]
        public void PlacesNoLongerToldGoBackToWhatTheyWentByAsMade()
        {
            // What was kept forgotten: every place is told as never read until read again.
            var catalog = World();
            var before = catalog.Select(Told).ToList();
            var finds = new PlaceFinds();
            finds.Tell(catalog, Found(("Amber", Chambers), ("Music_Crypt", Chambers)), Read(), PlaceOf);
            Assert.Equal("Burial Chambers", Of(catalog, "Crypt2").DisplayName);

            var told = finds.Tell(catalog, Found(), Labels(), PlaceOf);

            Assert.Equal(before, catalog.Select(Told));
            Assert.Equal((0, 0), told);
        }

        [Fact]
        public void APlaceNoLongerNamedRanksWhereThingsAreFoundByWhatItWentByAsMade()
        {
            var catalog = World();
            var finds = new PlaceFinds();
            finds.Tell(catalog, Found(), Read(), PlaceOf);

            finds.Tell(catalog, Found(("Amber", Label("Mountain cave", "Mountain")), ("Amber", Label("Crypt", "Black Forest"))),
                Labels(("MountainCave02", false, new[] { Label("Mountain cave", "Mountain") })), PlaceOf);

            Assert.Equal(new[] { Label("Crypt", "Black Forest"), Label("Mountain cave", "Mountain") }, Of(catalog, "Amber").FoundIn);
        }

        [Fact]
        public void APlaceReadWithNoNameRanksWhereThingsAreFoundByWhatItWentBy()
        {
            var catalog = World();

            new PlaceFinds().Tell(catalog, Found(("Amber", Label("Mountain cave", "Mountain")), ("Amber", Label("Crypt", "Black Forest"))),
                Labels(("Crypt2", false, new string[0]), ("MountainCave02", false, new[] { Label("Mountain cave", "Mountain") })), PlaceOf);

            Assert.Equal(new[] { Label("Crypt", "Black Forest"), Label("Mountain cave", "Mountain") }, Of(catalog, "Amber").FoundIn);
        }

        [Fact]
        public void WhatSomethingElseFiresOrAStatusEffectOfTheSameNameStaysAsItWas()
        {
            var catalog = World();
            catalog.Add(Grouped("bow_projectile", Kind.Projectile, ("Bows and crossbows", 1)));
            catalog.Add(E("Crypt_Projectile", Kind.StatusEffect));

            new PlaceFinds().Tell(catalog, Found(("bow_projectile", Chambers), ("Crypt_Projectile", Chambers)), Read(), PlaceOf);

            Assert.Equal("Bows and crossbows", Of(catalog, "bow_projectile").Group);
            Assert.Equal(new[] { Chambers }, catalog.Single(e => e.Name == "Crypt_Projectile" && e.Kind == Kind.Projectile).FoundIn);
            Assert.Empty(catalog.Single(e => e.Name == "Crypt_Projectile" && e.Kind == Kind.StatusEffect).FoundIn);
        }

        [Fact]
        public void TellingAgainLeavesEveryEntryAsTellingOnce()
        {
            // First as kept, the crypt not yet read and naming more; then as read again.
            var catalog = World();
            var finds = new PlaceFinds();
            finds.Tell(catalog, Found(("Music_Crypt", Label("Crypt", "Black Forest")), ("Crypt_Projectile", Label("Crypt", "Black Forest")), ("Coins", Label("Crypt", "Black Forest")), ("Amber", Label("Crypt", "Black Forest"))),
                Labels(("Ruin3", false, new[] { Label("Ruin", "Meadows") })), PlaceOf);
            var again = Found(("Amber", Label("Mountain cave", "Mountain")), ("Amber", Chambers), ("Amber", Label("Ruin", "Meadows")), ("Music_Crypt", Chambers));
            var told = finds.Tell(catalog, again, Read(), PlaceOf);

            var once = World();
            var toldOnce = new PlaceFinds().Tell(once, again, Read(), PlaceOf);

            Assert.Equal(once.Select(Told), catalog.Select(Told));
            Assert.Equal(toldOnce, told);
        }

        [Fact]
        public void WhatAStepAfterTellingRegroupedGoesBackToItsGroupWhenToldAgain()
        {
            // An item only creatures seemed to carry goes back to its kind of item once a place
            // holds it (Grouping.FoundInLocations); no place holding it any more, it goes back.
            var carried = Groups.CarriedByCreatures;
            var catalog = World();
            var coins = Of(catalog, "Coins");
            coins.Group = carried.Name;
            coins.GroupOrder = carried.Order;
            var finds = new PlaceFinds();
            finds.Tell(catalog, Found(("Coins", Chambers)), Read(), PlaceOf);
            coins.Group = "Valuables";
            coins.GroupOrder = carried.Order + 1;

            finds.Tell(catalog, Found(), Read(), PlaceOf);

            Assert.Equal(carried.Name, coins.Group);
            Assert.Equal(carried.Order, coins.GroupOrder);
            Assert.Empty(coins.FoundIn);
        }

        [Fact]
        public void ToldInStepsTheCatalogStaysAsItWasUntilPublishedThenIsAsToldAtOnce()
        {
            // Told over several frames: no frame shows half of it.
            var found = Found(("Amber", Label("Mountain cave", "Mountain")), ("Amber", Chambers), ("Amber", Label("Ruin", "Meadows")), ("Music_Crypt", Chambers), ("Crypt_Projectile", Chambers));
            var catalog = World();
            var before = catalog.Select(Told).ToList();
            var finds = new PlaceFinds();

            foreach (var _ in finds.Steps(catalog, found, Read(), PlaceOf)) Assert.Equal(before, catalog.Select(Told));
            Assert.Equal(before, catalog.Select(Told));
            var told = finds.Publish();

            var once = World();
            var toldOnce = new PlaceFinds().Tell(once, found, Read(), PlaceOf);
            Assert.Equal(once.Select(Told), catalog.Select(Told));
            Assert.Equal(toldOnce, told);
        }

        [Fact]
        public void ManyThingsFoundAreOrderedASliceAtATime()
        {
            var found = Found(Enumerable.Range(0, 2000).Select(i => ("Thing" + i, Chambers)).ToArray());

            var steps = new PlaceFinds().Steps(World(), found, Read(), PlaceOf).Count();

            Assert.True(steps >= 4, steps.ToString());
        }

        [Fact]
        public void TellingAgainBeforeTheLastWasPublishedTellsOnlyTheNew()
        {
            var catalog = World();
            var finds = new PlaceFinds();
            foreach (var _ in finds.Steps(catalog, Found(("Coins", Chambers)), Read(), PlaceOf)) { }

            foreach (var _ in finds.Steps(catalog, Found(("Amber", Chambers)), Read(), PlaceOf)) { }
            finds.Publish();

            Assert.Empty(Of(catalog, "Coins").FoundIn);
            Assert.Equal(new[] { Chambers }, Of(catalog, "Amber").FoundIn);
        }

        [Fact]
        public void ATellingBegunAgainAndNotWorkedOutPutsNothingOn()
        {
            // The world left, or the places read again, while it was being worked out.
            var catalog = World();
            var before = catalog.Select(Told).ToList();
            var finds = new PlaceFinds();
            foreach (var _ in finds.Steps(catalog, Found(("Coins", Chambers)), Read(), PlaceOf)) { }

            using (var again = finds.Steps(catalog, Found(("Amber", Chambers)), Read(), PlaceOf).GetEnumerator()) again.MoveNext();

            Assert.Equal((0, 0), finds.Publish());
            Assert.Equal(before, catalog.Select(Told));
        }

        [Fact]
        public void ACatalogToldForTheFirstTimeIsTakenAsItWasMade()
        {
            // A new world's catalog: what one finds told before does not carry over.
            var finds = new PlaceFinds();
            finds.Tell(World(), Found(("Music_Crypt", Chambers)), Read(), PlaceOf);
            var next = World();

            finds.Tell(next, Found(), Read(), PlaceOf);

            Assert.Equal(Unplayed.Name, Of(next, "Music_Crypt").Group);
        }
    }
}
