using Xunit;

namespace Scry.Tests
{
    public class CatalogWordingTests
    {
        // What a player reads of the catalog, worded in the model: how far reading it has got,
        // where a creature comes from, which mod a thing was matched to by, the names of a
        // creature's gear sets and a thing's looks, and a creature's footsteps.

        [Fact]
        public void ReadingTheCatalogSaysWhatAndHowFar()
        {
            Assert.Equal("Reading where things live", CatalogWords.Reading("where things live"));
            Assert.Equal("Linking entries: 1,200", CatalogWords.Progress("Linking entries", 1200));
            Assert.Equal("Reading prefabs: 3 of 4,000", CatalogWords.Progress("Reading prefabs", 3, 4000));
            Assert.Equal("Making entries: 16 of about 5,000", CatalogWords.ProgressAbout("Making entries", 16, 5000));
        }

        [Fact]
        public void ReadingTheLocationsSaysHowFarItHasGot()
        {
            Assert.Equal("already reading the locations and dungeons, 12 of 1,400 so far.", LocationWords.AlreadyReading(12, 1400));
            Assert.Equal("reading 1,400 locations and dungeon rooms in the background.", LocationWords.ReadingInBackground(1400));
            Assert.Equal("Reading locations and dungeons: 12 of 1,400", LocationWords.ReadingProgress(12, 1400));
            Assert.Equal("Stop reading  12 of 1,400", LocationWords.StopReading(12, 1400));
        }

        [Fact]
        public void ARaidSaysItsNameWhereAndForWhomItComes()
        {
            var facts = new SpawnFacts { Biomes = "Meadows" };
            Assert.Equal("Comes in the raid \"The forest is moving\", in Meadows", SpawnWords.Raid("The forest is moving", facts, perPlayer: false, k => k));
            Assert.Equal("Comes in the raid \"Bats\", for a player whose own progress calls for it", SpawnWords.Raid("Bats", new SpawnFacts { Biomes = "" }, perPlayer: true, k => k));
        }

        [Fact]
        public void ASpawnPointOrSpawnerSaysWhatPlacesTheCreature()
        {
            Assert.Equal("In dungeons or locations, from the spawn point Spawner_Draugr", SpawnWords.FromSpawnPoint("Spawner_Draugr", new SpawnFacts { Biomes = "" }, k => k));
            Assert.Equal("Comes from Greydwarf nest, every spawn, no stars", SpawnWords.FromSpawner("Greydwarf nest", 1f, 1f, 1, 1));
        }

        [Fact]
        public void ANameShownSaysItsPrefabWhereTheyDiffer()
        {
            Assert.Equal("Greydwarf nest (Spawner_GreydwarfNest)", Naming.WithPrefab("Greydwarf nest", "Spawner_GreydwarfNest"));
            Assert.Equal("Spawner_GreydwarfNest", Naming.WithPrefab("", "Spawner_GreydwarfNest"));
            Assert.Equal("Troll", Naming.WithPrefab("Troll", "Troll"));
        }

        [Fact]
        public void AModFoundByItsBundlesSaysHow()
        {
            Assert.Equal("a bundle holding it", UnsureWords.BundleClue(byName: true));
            Assert.Equal("the bundles holding what it uses", UnsureWords.BundleClue(byName: false));
        }

        [Fact]
        public void AGearSetIsNamedByWhatOfItIsDrawn()
        {
            Assert.Equal("Club + Shield", GearWords.Set(new[] { "Club", "Shield" }, 0));
            Assert.Equal("Set 2, nothing drawn", GearWords.Set(new string[0], 1));
            Assert.Equal("club_mesh,shield_mesh", GearWords.Meshes(new[] { "club_mesh", "shield_mesh" }));
            Assert.Null(GearWords.Meshes(new string[0]));
        }

        [Fact]
        public void ALookIsNamedByItsPlace()
        {
            Assert.Equal("Grown", LookWords.Grown(0, 1));
            Assert.Equal("Grown 2", LookWords.Grown(1, 3));
            Assert.Equal("Style 3", LookWords.Style(2));
        }

        [Fact]
        public void AFootstepSaysHowTheCreatureMovesAndOnWhat()
        {
            Assert.Equal("walk/run on stone", StepWords.Note(new[] { "Walk", "Run" }, new[] { "Stone" }, anyGround: false));
            Assert.Equal("any gait on any ground", StepWords.Note(new string[0], new string[0], anyGround: true));
            Assert.Equal("jog on plain ground/ground", StepWords.Note(new[] { "Jog" }, new[] { "Default", "GenericGround" }, anyGround: false));
            Assert.Equal("sneak on nothing", StepWords.Note(new[] { "Sneak" }, new string[0], anyGround: false));
        }

        [Fact]
        public void AnEffectListOnAPartSaysWhichPart()
        {
            Assert.Equal("Troll club: hit", Naming.OfPart("Troll club", "Hit"));
            Assert.Equal("Hit (troll club)", Naming.WithPart("Hit", "Troll club"));
            Assert.Equal("Hit (item drop)", Naming.WithPart("Hit", "ItemDrop"));
        }

        [Fact]
        public void DamageThatPutsAStatusEffectOnIsNamedByItsType() => Assert.Equal("fire damage", CombatWords.OfDamage("fire"));
    }
}
