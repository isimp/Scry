using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    /// <summary>
    /// What the character saw of a creature up close, its stars and what it carried, is kept with
    /// the book, and under the strict rules the stage offers those stars and looks alone.
    /// </summary>
    public class CompendiumSightingTests
    {
        [Fact]
        public void ASightingKeepsTheStarsAndGearSeenApartFromLevelsAndPairs()
        {
            var book = new CompendiumBook();
            var version = book.PairsVersion;
            Assert.True(book.Sighted("Draugr", 1, new[] { "draugr_axe", "ShieldWood" }));
            Assert.False(book.Sighted("Draugr", 1, new[] { "draugr_axe" }));
            Assert.True(book.Sighted("Draugr", 0, null));
            Assert.Equal(new[] { 0, 1 }, book.StarsSeen("Draugr").OrderBy(s => s));
            Assert.Equal(new[] { "ShieldWood", "draugr_axe" }, book.WornSeen("Draugr").OrderBy(i => i, System.StringComparer.Ordinal));
            Assert.Empty(book.StarsSeen("Troll"));
            Assert.Empty(book.WornSeen(null));
            Assert.True(book.PairsVersion > version);
            Assert.True(book.Changed);
            Assert.False(book.Sighted("", 1, null));
            Assert.False(book.Sighted("Draugr", -1, new[] { "" }));
            Assert.Equal(KnownLevel.Unknown, book.Level("Draugr"));
        }

        [Fact]
        public void SightingsAreKeptWithTheBook()
        {
            var book = new CompendiumBook();
            book.Sighted("Draugr", 2, new[] { "draugr_bow" });
            var text = book.Save();
            Assert.Contains("sight\tDraugr\tstars:2\n", text);
            Assert.Contains("sight\tDraugr\twears:draugr_bow\n", text);
            var read = CompendiumBook.Load(text);
            Assert.Equal(new[] { 2 }, read.StarsSeen("Draugr"));
            Assert.Equal(new[] { "draugr_bow" }, read.WornSeen("Draugr"));
            Assert.Empty(CompendiumBook.Load("sight\tDraugr\tstars:many\nsight\tDraugr\n").StarsSeen("Draugr"));
        }

        [Fact]
        public void TheWorldsRulesSayHowNearACreatureIsSeenUpClose()
        {
            // The game names a creature and shows its stars within 10 m; a server may set it.
            Assert.Equal(10f, WorldRules.SeenReachSetting.Default);
            var rules = WorldRules.From(WorldRule.PlayersChoose, true, true, _ => SourceLevel.WithInsides, seenReach: 25f);
            Assert.Equal(25f, rules.SeenReach);
            Assert.Equal(25f, rules.Copy().SeenReach);
            Assert.False(rules.SameAs(WorldRules.Open));
            Assert.True(rules.SameAs(rules.Copy()));
            Assert.Equal(0f, WorldRules.From(WorldRule.PlayersChoose, true, true, _ => SourceLevel.WithInsides, seenReach: -5f).SeenReach);
        }

        private sealed class World
        {
            public readonly Entry Draugr = E("Draugr", Kind.Creature);
            public readonly Entry Axe = E("draugr_axe", Kind.Item);
            public readonly Entry Bow = E("draugr_bow", Kind.Item);
            public readonly Entry Forest = E("biome:BlackForest", Kind.Biome);
            public readonly Entry Crypt = E("loc:Crypt2", Kind.Location);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                Draugr.ExtraLevels = 2;
                var all = new[] { Draugr, Axe, Bow, Forest, Crypt }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                Book.Raise(Draugr.Key, KnownLevel.Known);
                Book.Raise(Axe.Key, KnownLevel.Seen);
                Book.Raise(Bow.Key, KnownLevel.Seen);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        [Fact]
        public void UnderStrictTheStageOffersTheStarsSeenUpCloseAndNoOthers()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Draugr);
            // Without stars is always offered: it is the creature itself.
            Assert.Equal(new[] { 1 }, world.Explorer.StarsOffered(world.Draugr));
            Assert.Equal(1, world.Explorer.StarLevelFor(world.Draugr.Key, 3));

            world.Book.Sighted(world.Draugr.Key, 2, null);
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { 1, 3 }, world.Explorer.StarsOffered(world.Draugr));
            Assert.Equal(3, world.Explorer.StarLevelFor(world.Draugr.Key, 3));
            Assert.Equal(1, world.Explorer.StarLevelFor(world.Draugr.Key, 2));
            Assert.Equal(1, world.Explorer.StarLevelFor("Troll", 3));

            // Under the loose rules, off and while peeking every star is offered.
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(new[] { 1, 2, 3 }, world.Explorer.StarsOffered(world.Draugr));
            Assert.Equal(2, world.Explorer.StarLevelFor(world.Draugr.Key, 2));
            world.Rule(CompendiumRule.Off);
            Assert.Equal(new[] { 1, 2, 3 }, world.Explorer.StarsOffered(world.Draugr));
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Peeking = true;
            Assert.Equal(new[] { 1, 2, 3 }, world.Explorer.StarsOffered(world.Draugr));
        }

        [Fact]
        public void TheStageOffersNoMoreStarsThanTheWorldCanGive()
        {
            // A creature the world gives one star at most, or none, is offered no more, whatever looks it has; unknown, every look.
            var world = new World().Rule(CompendiumRule.Off);
            world.Explorer.Select(world.Draugr);
            world.Explorer.MostLevelShownBy = prefab => prefab == world.Draugr.Name ? 2 : (int?)null;
            Assert.Equal(new[] { 1, 2 }, world.Explorer.StarsOffered(world.Draugr));
            world.Explorer.MostLevelShownBy = prefab => 1;
            world.Rule(CompendiumRule.Off);
            Assert.Equal(new[] { 1 }, world.Explorer.StarsOffered(world.Draugr));
            world.Explorer.MostLevelShownBy = prefab => null;
            world.Rule(CompendiumRule.Off);
            Assert.Equal(new[] { 1, 2, 3 }, world.Explorer.StarsOffered(world.Draugr));
        }

        [Fact]
        public void APageShownWholeByKnownUpToOffersEveryStarAndLook()
        {
            // Known up to shows a tier's pages whole under the strict rules, the stage with them.
            var world = new World();
            world.View.Milestones(new System.Collections.Generic.Dictionary<string, int> { [world.Draugr.Key] = 1 }, 1);
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Draugr);
            Assert.Equal(new[] { 1, 2, 3 }, world.Explorer.StarsOffered(world.Draugr));
            Assert.Equal(3, world.Explorer.StarLevelFor(world.Draugr.Key, 3));
            Assert.True(world.Explorer.WearsShown(world.Draugr.Key, "draugr_bow"));
            world.View.Milestones(new System.Collections.Generic.Dictionary<string, int> { [world.Draugr.Key] = 1 }, 0);
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { 1 }, world.Explorer.StarsOffered(world.Draugr));
        }

        [Fact]
        public void TheStarsOfferedAreOneListWhileNothingChanges()
        {
            // The Adjust rows ask every frame: the same list is given again until a sighting, the selection or a peek changes it.
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Draugr);
            var offered = world.Explorer.StarsOffered(world.Draugr);
            Assert.Same(offered, world.Explorer.StarsOffered(world.Draugr));
            Assert.Equal(new[] { 1 }, offered);
            world.Book.Sighted(world.Draugr.Key, 2, null);
            world.Rule(CompendiumRule.Strict);
            var again = world.Explorer.StarsOffered(world.Draugr);
            Assert.NotSame(offered, again);
            Assert.Equal(new[] { 1, 3 }, again);
        }

        [Fact]
        public void AKindWearsWhatAnyKindOfItsFamilyWasSeenWearing()
        {
            // A Dvergr mage and its fire kind go by one name and look alike in the game: the mage
            // seen in the fire robe is what the fire kind wears, under the strict rules too.
            Entry Mage(string name, params string[] attacks)
            {
                var e = E(name, Kind.Creature, "Dvergr Mage");
                e.Tokens = new[] { "$enemy_dvergr_mage" };
                e.Attacks = attacks.Select(a => (a, new[] { "fire" })).ToArray();
                return e;
            }
            var mage = Mage("DvergerMage", "Dverger_melee", "DvergerStaffFire_fireball", "DvergerStaffIce_icebolt");
            var fire = Mage("DvergerMageFire", "Dverger_melee", "DvergerStaffFire_fireball");
            var suit = E("DvergerSuitFire", Kind.Item);
            var explorer = new Explorer(new List<Entry> { mage, fire, suit }, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            book.Raise(mage.Key, KnownLevel.Known);
            book.Raise(fire.Key, KnownLevel.Known);
            book.Sighted(mage.Key, 0, new[] { "DvergerSuitFire" });
            view.Tell(book, CompendiumRule.Strict);
            explorer.Know(view);
            Assert.NotNull(fire.Family);
            explorer.Select(fire);

            Assert.True(explorer.WearsShown(fire.Key, "DvergerSuitFire"));
            Assert.False(explorer.WearsShown(fire.Key, "DvergerSuitIce"));
        }

        [Fact]
        public void AKindShowsEachAttackOfItsOwnSeenOfAnyKindOfItsFamily()
        {
            // The mage and its ice kind go by one name and look alike: an ice bolt seen of the mage
            // is the ice kind's too, on its page and its clips, under the strict rules. What it
            // never showed stays back, a kill stays the mage's alone, and so does its death's blast.
            Entry Mage(string name, params string[] attacks)
            {
                var e = E(name, Kind.Creature, "Dvergr Mage");
                e.Tokens = new[] { "$enemy_dvergr_mage" };
                e.Attacks = attacks.Select(a => (a, new[] { "frost" })).ToArray();
                return e;
            }
            var mage = Mage("DvergerMage", "Dverger_melee", "DvergerStaffIce_icebolt", "DvergerStaffFire_fireball");
            var ice = Mage("DvergerMageIce", "Dverger_melee", "DvergerStaffIce_icebolt", "DvergerStaffNova");
            var explorer = new Explorer(new List<Entry> { mage, ice }, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook { KindsOf = Families.KindsOf(explorer.Entries) };
            book.Raise(mage.Key, KnownLevel.Known);
            book.Raise(ice.Key, KnownLevel.Known);
            book.FoundOut(mage.Key, EntryKeys.Attack("DvergerStaffIce_icebolt"));
            book.FoundOut(mage.Key, EntryKeys.Did(Deed.Killed));
            book.FoundOut(mage.Key, EntryKeys.Attack(EntryKeys.DeathAttack));
            view.Tell(book, CompendiumRule.Strict);
            explorer.Know(view);
            Assert.NotNull(ice.Family);
            explorer.Select(ice);

            Assert.False(explorer.HidesAttack(EntryKeys.PlayAttack("DvergerStaffIce_icebolt")));
            Assert.False(explorer.KeepsBackAttackClip("DvergerStaffIce_icebolt"));
            Assert.True(explorer.HidesAttack(EntryKeys.PlayAttack("DvergerStaffNova")));
            Assert.True(book.Proved(ice.Key, EntryKeys.Attack("DvergerStaffIce_icebolt")));
            Assert.False(book.Proved(ice.Key, EntryKeys.Did(Deed.Killed)));
            Assert.False(book.Proved(ice.Key, EntryKeys.Attack(EntryKeys.DeathAttack)));
            // And back: what the kind showed is the mage's.
            book.FoundOut(ice.Key, EntryKeys.Attack("DvergerStaffNova"));
            Assert.True(book.Proved(mage.Key, EntryKeys.Attack("DvergerStaffNova")));
        }

        [Fact]
        public void AMageSeenInItsRobeWasSeenHoldingTheStaffItsSetPutsOnWithIt()
        {
            // The game hands a mage its set and puts on all of it but the weapons: a mage seen in
            // the fire robe held the fire staff, its utility item, whether or not that was kept
            // with the sighting. Its fire kind too, which goes by its name; the ice look stays shut.
            Entry Mage(string name)
            {
                var e = E(name, Kind.Creature, "Dvergr Mage");
                e.Tokens = new[] { "$enemy_dvergr_mage" };
                return e;
            }
            var mage = Mage("DvergerMage");
            var fire = Mage("DvergerMageFire");
            var items = new[] { "DvergerSuitFire", "DvergerStaffFire", "DvergerSuitIce", "DvergerStaffIce" }.Select(i => E(i, Kind.Item)).ToList();
            var explorer = new Explorer(new List<Entry> { mage, fire }.Concat(items).ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var fireSet = new[] { ("DvergerStaffFire", Slot.Utility), ("DvergerSuitFire", Slot.Chest) };
            var iceSet = new[] { ("DvergerStaffIce", Slot.Utility), ("DvergerSuitIce", Slot.Chest) };
            explorer.WornSetsOf = creature => creature == mage.Key ? WornTogether.Of(new[] { fireSet, iceSet }, new string[0], new Slot[0])
                : creature == fire.Key ? WornTogether.Of(new[] { fireSet }, new string[0], new Slot[0]) : null;
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            book.Raise(mage.Key, KnownLevel.Known);
            book.Raise(fire.Key, KnownLevel.Known);
            foreach (var item in items) book.Raise(item.Key, KnownLevel.Known);
            book.Sighted(mage.Key, 0, new[] { "DvergerSuitFire", "DvergerHairMale" });
            view.Tell(book, CompendiumRule.Strict);
            explorer.Know(view);
            explorer.Select(mage);

            Assert.True(explorer.WearsShown(mage.Key, "DvergerStaffFire"));
            Assert.True(explorer.WearsShown(fire.Key, "DvergerStaffFire"));
            Assert.False(explorer.WearsShown(mage.Key, "DvergerStaffIce"));
            string[][] looks = { new string[0], new[] { "DvergerStaffFire", "DvergerSuitFire" }, new[] { "DvergerStaffIce", "DvergerSuitIce" } };
            Assert.Equal(new[] { 0, 1 }, explorer.LooksShown(looks.Length, look => looks[look]));
        }

        [Fact]
        public void AGearChoiceSeenWornIsNamedThoughNeverMetAsAnItem()
        {
            // A Dvergr's hair is never met as an item, only seen on one: under the strict rules its
            // choice in the gear rows is named once seen worn, as its looks show, and a smudge before.
            var mage = E("DvergerMage", Kind.Creature, "Dvergr Mage");
            var male = E("DvergerHairMale", Kind.Item, "Iron plate armor");
            var female = E("DvergerHairFemale", Kind.Item, "Iron plate armor");
            var explorer = new Explorer(new List<Entry> { mage, male, female }, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            book.Raise(mage.Key, KnownLevel.Known);
            book.Sighted(mage.Key, 0, new[] { "DvergerHairMale" });
            view.Tell(book, CompendiumRule.Strict);
            explorer.Know(view);
            explorer.Select(mage);

            Assert.True(explorer.NotMet(male.Key));
            Assert.False(explorer.GearUnnamed(mage.Key, male.Name));
            Assert.True(explorer.GearUnnamed(mage.Key, female.Name));
            // Met as an item, it is named on any creature.
            book.Raise(female.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Strict);
            explorer.Know(view);
            Assert.False(explorer.GearUnnamed(mage.Key, female.Name));
        }

        [Fact]
        public void EachKindIsJudgedByItsOwnSetsWhatOneWoreSharedWithTheFamily()
        {
            // Two kinds share a robe but roll it with different staffs: the robe seen on the second
            // tells its own set, so it held its own staff, which the family shares; not the first's.
            Entry Kind(string name)
            {
                var e = E(name, Scry.Kind.Creature, "Robed One");
                e.Tokens = new[] { "$enemy_robed" };
                return e;
            }
            var a = Kind("RobedA");
            var b = Kind("RobedB");
            var items = new[] { "RobeX", "StaffA", "StaffB" }.Select(i => E(i, Scry.Kind.Item)).ToList();
            var explorer = new Explorer(new List<Entry> { a, b }.Concat(items).ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            explorer.WornSetsOf = creature => creature == a.Key ? WornTogether.Of(new[] { new[] { ("RobeX", Slot.Chest), ("StaffA", Slot.Utility) } }, new string[0], new Slot[0])
                : creature == b.Key ? WornTogether.Of(new[] { new[] { ("RobeX", Slot.Chest), ("StaffB", Slot.Utility) } }, new string[0], new Slot[0]) : null;
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            book.Raise(a.Key, KnownLevel.Known);
            book.Raise(b.Key, KnownLevel.Known);
            foreach (var item in items) book.Raise(item.Key, KnownLevel.Known);
            book.Sighted(b.Key, 0, new[] { "RobeX" });
            view.Tell(book, CompendiumRule.Strict);
            explorer.Know(view);
            Assert.NotNull(a.Family);
            explorer.Select(a);

            Assert.False(explorer.WearsShown(a.Key, "StaffA"));
            Assert.True(explorer.WearsShown(a.Key, "StaffB"));
            Assert.True(explorer.WearsShown(b.Key, "StaffB"));
        }

        [Fact]
        public void UnderStrictALookShowsOnlyWithEveryItemSeenCarried()
        {
            // Under the loose rules a look of met items is offered, as before; under the strict
            // rules a look is offered once the creature was seen carrying each of its items.
            var world = new World().Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Draugr);
            string[][] looks = { new string[0], new[] { "draugr_axe" }, new[] { "draugr_bow" }, new[] { "draugr_bow", "NotInTheCatalog" } };
            Assert.Equal(new[] { 0, 1, 2, 3 }, world.Explorer.LooksShown(looks.Length, look => looks[look]));
            Assert.True(world.Explorer.WearsShown(world.Draugr.Key, "draugr_axe"));

            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { 0 }, world.Explorer.LooksShown(looks.Length, look => looks[look]));
            Assert.False(world.Explorer.WearsShown(world.Draugr.Key, "draugr_axe"));
            world.Book.Sighted(world.Draugr.Key, 0, new[] { "draugr_axe" });
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { 0, 1 }, world.Explorer.LooksShown(looks.Length, look => looks[look]));
            Assert.True(world.Explorer.WearsShown(world.Draugr.Key, "draugr_axe"));
            Assert.False(world.Explorer.WearsShown(world.Draugr.Key, "draugr_bow"));
            // An item the catalog lacks is no bar under either rules once the rest was seen.
            world.Book.Sighted(world.Draugr.Key, 0, new[] { "draugr_bow" });
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { 0, 1, 2, 3 }, world.Explorer.LooksShown(looks.Length, look => looks[look]));
        }

        [Fact]
        public void WhatACreatureWasSeenWearingIsMetForItsLooks()
        {
            // A creature's own robe and staff can never be picked up, so they are never met as
            // items; seen on the creature up close, they were seen, under either rules.
            var robe = E("DvergerSuitFire", Kind.Item);
            var staff = E("DvergerStaffFire", Kind.Item);
            var hair = E("DvergerHairMale", Kind.Item);
            var mage = E("DvergerMage", Kind.Creature);
            var explorer = new Explorer(new List<Entry> { mage, robe, staff, hair }, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
            explorer.WornSetsOf = creature => creature == mage.Key ? WornTogether.Of(new[] { new[] { ("DvergerStaffFire", Slot.Utility), ("DvergerSuitFire", Slot.Chest) } }, new string[0], new Slot[0]) : null;
            var view = new CompendiumView(explorer.Entries);
            var book = new CompendiumBook();
            book.Raise(mage.Key, KnownLevel.Known);
            string[][] looks = { new string[0], new[] { "DvergerStaffFire", "DvergerSuitFire" }, new[] { "DvergerHairMale" } };
            foreach (var rule in new[] { CompendiumRule.Loose, CompendiumRule.Strict })
            {
                view.Tell(book, rule);
                explorer.Know(view);
                explorer.Select(mage);
                Assert.Equal(new[] { 0 }, explorer.LooksShown(looks.Length, look => looks[look]));
            }
            // Seen in the robe: the staff its set puts on with it was seen too; its hair, which no
            // set gives, was seen as itself.
            book.Sighted(mage.Key, 0, new[] { "DvergerSuitFire", "DvergerHairMale" });
            foreach (var rule in new[] { CompendiumRule.Loose, CompendiumRule.Strict })
            {
                view.Tell(book, rule);
                explorer.Know(view);
                explorer.Select(mage);
                Assert.Equal(new[] { 0, 1, 2 }, explorer.LooksShown(looks.Length, look => looks[look]));
            }
        }

        [Fact]
        public void ACreatureThatAlwaysWearsGearOpensInAGearLookItCanShow()
        {
            // Every creature that rolls a set spawns in one: where the look it opens in cannot
            // show, the first gear look that can stands, the bare one only where none can.
            Assert.Equal(1, LookPick.Opening(new[] { 0, 1, 2 }, 1, gear: true));
            // The look wanted, where it shows, whichever it is, the bare one too.
            Assert.Equal(2, LookPick.Opening(new[] { 0, 1, 2 }, 2, gear: true));
            Assert.Equal(0, LookPick.Opening(new[] { 0, 1, 2 }, 0, gear: true));
            Assert.Equal(2, LookPick.Opening(new[] { 0, 2, 3 }, 1, gear: true));
            Assert.Equal(0, LookPick.Opening(new[] { 0 }, 1, gear: true));
            // Looks not of gear fall back to the first that shows, as before.
            Assert.Equal(0, LookPick.Opening(new[] { 0, 2 }, 1, gear: false));
            Assert.Equal(0, LookPick.Opening(new int[0], 1, gear: true));
        }

        [Fact]
        public void TheLookRowOfACreatureInGearListsTheBareLookLast()
        {
            // Every creature rolling a set is met in one: its sets lead the row, "No gear" after them.
            Assert.Equal(new[] { 1, 2, 3, 0 }, LookPick.InRow(new[] { 0, 1, 2, 3 }, gear: true));
            Assert.Equal(new[] { 2, 0 }, LookPick.InRow(new[] { 0, 2 }, gear: true));
            // Other looks stay in their order.
            Assert.Equal(new[] { 0, 1, 2 }, LookPick.InRow(new[] { 0, 1, 2 }, gear: false));
        }

        [Fact]
        public void ALookWaitsOnlyForTheItemsItShowsOnTheBody()
        {
            // A Dvergr mage's Fire set is its robe, which shows, and staffs and spells, which draw
            // nothing on it: seen wearing the robe, the look is offered under the strict rules.
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Draugr);
            string[][] looks = { new string[0], new[] { "draugr_axe", "draugr_bow" } };
            bool Shows(string item) => item != "draugr_bow";
            Assert.Equal(new[] { 0 }, world.Explorer.LooksShown(looks.Length, look => looks[look], Shows));

            world.Book.Sighted(world.Draugr.Key, 0, new[] { "draugr_axe" });
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { 0, 1 }, world.Explorer.LooksShown(looks.Length, look => looks[look], Shows));
            // Every item counts where nothing says what shows, as before.
            Assert.Equal(new[] { 0 }, world.Explorer.LooksShown(looks.Length, look => looks[look]));
        }

        [Fact]
        public void AnAttackTakenOrSeenBegunCountsAsItsWeaponSeenCarried()
        {
            // The clip of an attack taken plays once the attack is proved; the stage dresses the
            // creature with the weapon it hit with, and the page says it was seen carrying it.
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Draugr);
            Assert.False(world.Explorer.WearsShown(world.Draugr.Key, "draugr_axe"));
            Assert.Empty(world.Explorer.SeenLines(world.Draugr));

            Assert.True(world.Book.FoundOut(world.Draugr.Key, EntryKeys.Attack("draugr_axe")));
            world.Rule(CompendiumRule.Strict);
            Assert.True(world.Explorer.WearsShown(world.Draugr.Key, "draugr_axe"));
            Assert.False(world.Explorer.WearsShown(world.Draugr.Key, "draugr_bow"));
            var lines = world.Explorer.SeenLines(world.Draugr);
            Assert.Equal(new[] { "Seen carrying" }, lines.Select(l => l.Label));
            Assert.Equal(world.Axe.ShownName, lines[0].Value);
            // Seen carrying the bow up close too: each once, in name order, after the stars seen.
            world.Book.Sighted(world.Draugr.Key, 0, new[] { "draugr_bow", "draugr_axe" });
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(world.Axe.ShownName + ", " + world.Bow.ShownName, world.Explorer.SeenLines(world.Draugr).Single(l => l.Label == "Seen carrying").Value);
        }

        [Fact]
        public void ASeenPageTellsWhatSightGave()
        {
            var world = new World();
            world.Book.Sighted(world.Draugr.Key, 1, new[] { "draugr_axe" });
            world.Book.Sighted(world.Draugr.Key, 0, null);
            world.Book.Witness(world.Draugr.Key, world.Forest.Key);
            world.Book.Witness(world.Draugr.Key, world.Crypt.Key);
            world.Rule(CompendiumRule.Strict);

            var lines = world.Explorer.SeenLines(world.Draugr);
            Assert.Equal(new[] { "Seen with", "Seen carrying", "Seen in" }, lines.Select(l => l.Label));
            Assert.Equal("no stars, 1 star", lines[0].Value);
            Assert.Equal(world.Axe.ShownName, lines[1].Value);
            Assert.Equal(world.Forest.ShownName + ", " + world.Crypt.ShownName, lines[2].Value);

            Assert.Empty(world.Explorer.SeenLines(world.Axe));
            world.Rule(CompendiumRule.Off);
            Assert.Empty(world.Explorer.SeenLines(world.Draugr));
        }
    }
}
