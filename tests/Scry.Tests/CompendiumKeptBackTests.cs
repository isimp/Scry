using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class CompendiumKeptBackTests
    {
        // Under the strict rules a known page shows the numbers the game itself never shows only
        // once the character found them out: an attack's damage once it hit them, a creature's
        // health once they killed one, how long taming, a pregnancy, growing up or growing back
        // take once they saw it, a weak spot once a hit found it, and what drops and yields are
        // as they saw them fall, not the game's chances. Each says what shows it.

        private sealed class World
        {
            public readonly Entry Troll = E("Troll", Kind.Creature);
            public readonly Entry Hide = E("TrollHide", Kind.Item);
            public readonly Entry Coins = E("Coins", Kind.Item);
            public readonly Entry Beech = E("Beech1", Kind.Resource);
            public readonly Entry Wood = E("Wood", Kind.Item);
            public readonly Entry Meadows = E("Meadows", Kind.Biome);
            public readonly Entry Forest = E("BlackForest", Kind.Biome);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                Troll.AddTermLink("drops", Hide);
                Troll.AddTermLink("drops", Coins);
                var all = new[] { Troll, Hide, Coins, Beech, Wood, Meadows, Forest }.ToList();
                Explorer = new Explorer(all, new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                foreach (var entry in all) Book.Raise(entry.Key, KnownLevel.Known);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }
        }

        [Fact]
        public void AnAttacksDamageIsKeptBackUntilTheAttackHitTheCharacter()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);
            var slam = EntryKeys.PlayAttack("Troll_groundslam");

            Assert.True(world.Explorer.HidesAttack(slam));
            world.Book.Witness(world.Troll.Key, "Troll_groundslam");
            Assert.False(world.Explorer.HidesAttack(slam));
            Assert.True(world.Explorer.HidesAttack(EntryKeys.PlayAttack("Troll_log")));
            Assert.False(world.Explorer.HidesAttack(world.Hide.Key));
            Assert.Equal("2 more not yet taken", CompendiumWords.NotYetTaken(2));
            Assert.Null(CompendiumWords.NotYetTaken(0));

            // Not under the loose rules, and not on a page that is no creature's.
            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.HidesAttack(EntryKeys.PlayAttack("Troll_log")));
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Hide);
            Assert.False(world.Explorer.HidesAttack(EntryKeys.PlayAttack("Troll_log")));
        }

        [Fact]
        public void UnderStrictASearchFindsACreatureByHowItTakesOrDealsDamageOnlyAsFoundOut()
        {
            // The grid keeps back how a type lands and the attacks table an attack's damage; a
            // search must not find the creature by either before the page shows it.
            var world = new World();
            world.Troll.SetTermWords("weak", new[] { "fire", "pierce" });
            world.Troll.SetTermWords("resists", new[] { "frost" });
            world.Troll.SetTermWords("damage", new[] { "blunt", "frost" });
            world.Troll.Attacks = new[] { ("Troll_log", new[] { "blunt" }), ("Troll_breath", new[] { "frost" }) };
            world.Book.Witness(world.Troll.Key, EntryKeys.Damage("Fire"));
            world.Book.Witness(world.Troll.Key, "Troll_log");
            world.Rule(CompendiumRule.Strict);

            bool Finds(string text)
            {
                world.Explorer.Text = text;
                return world.Explorer.Results.Contains(world.Troll);
            }
            Assert.True(Finds("weak:fire"));
            Assert.False(Finds("weak:pierce"));
            Assert.False(Finds("resists:frost"));
            Assert.True(Finds("damage:blunt"));
            Assert.False(Finds("damage:frost"));
            // Gear says what it resists on its card: no hit needed to find it by that.
            world.Hide.SetTermWords("resists", new[] { "frost" });
            world.Explorer.Text = "resists:frost";
            Assert.Contains(world.Hide, world.Explorer.Results);

            world.Rule(CompendiumRule.Loose);
            Assert.True(Finds("weak:pierce"));
            Assert.True(Finds("damage:frost"));
        }

        [Fact]
        public void WhatToBringShowsOnlyOnceWhatSuggestsItWasFoundOut()
        {
            var world = new World();
            world.Book.Witness(world.Troll.Key, EntryKeys.Damage("Fire"));
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);

            Assert.False(world.Explorer.KeepsBackAll(new[] { EntryKeys.Damage("Fire") }));
            Assert.True(world.Explorer.KeepsBackAll(new[] { EntryKeys.Damage("Pierce") }));
            Assert.True(world.Explorer.KeepsBackAll(new[] { "Troll_breath", "Troll_frostlog" }));
            world.Book.Witness(world.Troll.Key, "Troll_frostlog");
            Assert.False(world.Explorer.KeepsBackAll(new[] { "Troll_breath", "Troll_frostlog" }));
            Assert.False(world.Explorer.KeepsBackAll(null));
            Assert.False(world.Explorer.KeepsBackAll(new string[0]));

            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.KeepsBackAll(new[] { EntryKeys.Damage("Pierce") }));
        }

        [Fact]
        public void ANumberTheGameNeverShowsIsKeptBackUntilWhatTellsItWasSeen()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);
            var killed = EntryKeys.Did(Deed.Killed);

            Assert.True(world.Explorer.KeepsBack(killed));
            Assert.True(world.Explorer.KeepsBack(EntryKeys.Did(Deed.Tamed)));
            world.Book.Witness(world.Troll.Key, killed);
            Assert.False(world.Explorer.KeepsBack(killed));
            Assert.False(world.Explorer.KeepsBack(null));

            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.KeepsBack(EntryKeys.Did(Deed.Tamed)));
        }

        [Fact]
        public void WhatKeepsANumberBackSaysWhatShowsIt()
        {
            // One form for every hint of what shows a number: "shows once you have ...", after its ???.
            Assert.Equal("shows once you have killed one", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.Killed)));
            Assert.Equal("shows once you have tamed one", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.Tamed)));
            Assert.Equal("shows once you have seen one born", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.Born)));
            Assert.Equal("shows once you have seen one grow up", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.GrewUp)));
            Assert.Equal("shows once you have seen one grow back", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.GrewBack)));
            Assert.Equal("??? · shows once you have killed one", CompendiumWords.KeptBack(EntryKeys.Did(Deed.Killed)));
            Assert.Equal("Health shows once you have killed one", CompendiumWords.ShowsOnce("Health", EntryKeys.Did(Deed.Killed)));
        }

        [Fact]
        public void ACropsTimeAndAStationsWorkingWaitToBeSeen()
        {
            // A crop growing in sight shows how long it takes; a station handing out what it made, how long each takes and what it burns.
            Assert.Equal("shows once you have seen one grow", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.Grew)));
            Assert.Equal("shows once you have seen one finish", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.Finished)));
            var rows = CompendiumWords.StillToFindOut(new[] { EntryKeys.Did(Deed.Grew), EntryKeys.Did(Deed.Finished) }, new string[0], attacks: 0, spots: 0, new Tally[0]);
            Assert.Equal(new[] { ("See one grow", "shows how long that takes"), ("See one finish", "shows how long each takes and what it burns") }, rows.ToArray());
            Assert.Equal("its growing found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.Grew)));
            Assert.Equal("its working found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.Finished)));
        }

        [Fact]
        public void APageSaysAtItsTopWhatIsStillToFindOutByWhatToDo()
        {
            // The You box gathers every hint the page gives further down: what to do, and what it shows.
            var rows = CompendiumWords.StillToFindOut(
                new[] { EntryKeys.Did(Deed.Tamed), EntryKeys.Did(Deed.Killed), EntryKeys.Did(Deed.Tamed) },
                new[] { "Pierce", "Frost" }, attacks: 2, spots: 1, new[] { Tally.Fell });

            Assert.Equal(new[]
            {
                ("Kill one", "shows its health"),
                ("Tame one", "shows how long taming and a meal take"),
                ("Hit it with", "Pierce, Frost: how they land"),
                ("Let it hit you", "2 attacks' damage"),
                ("Hit its weak spot", "1 not yet found"),
                ("See it drop", "how many of each"),
            }, rows);

            var one = CompendiumWords.StillToFindOut(new[] { EntryKeys.Did(Deed.Born), EntryKeys.Did(Deed.GrewUp), EntryKeys.Did(Deed.GrewBack) },
                new[] { "Fire" }, attacks: 1, spots: 2, new[] { Tally.Gave });
            Assert.Equal(new[]
            {
                ("See one born", "shows how long it is pregnant"),
                ("See one grow up", "shows how long that takes"),
                ("See one grow back", "shows how long that takes"),
                ("Hit it with", "Fire: how it lands"),
                ("Let it hit you", "1 attack's damage"),
                ("Hit its weak spots", "2 not yet found"),
                ("Break one", "how many of each it gives"),
            }, one);

            Assert.Empty(CompendiumWords.StillToFindOut(new string[0], new string[0], 0, 0, new Tally[0]));
        }

        [Fact]
        public void WhatIsFoundOutComesBackToTheTopOfWhatWasLearnedLatelySayingWhat()
        {
            var book = new CompendiumBook();
            book.Raise("Troll", KnownLevel.Known);
            book.Raise("Boar", KnownLevel.Known);
            var mark = book.Learned;

            Assert.True(book.FoundOut("Troll", EntryKeys.Did(Deed.Killed)));
            Assert.Equal(new[] { "Troll", "Boar" }, book.Lately);
            Assert.Equal(new[] { "Troll" }, book.LearnedSince(mark));
            Assert.Equal(EntryKeys.Did(Deed.Killed), book.FoundOutOf("Troll"));
            Assert.True(book.Witnessed("Troll", EntryKeys.Did(Deed.Killed)));
            Assert.True(book.Changed);
            // Found out once; again is nothing new.
            Assert.False(book.FoundOut("Troll", EntryKeys.Did(Deed.Killed)));
            Assert.Null(book.FoundOutOf("Boar"));

            // Kept with the book; a level learned since is the newer news.
            var read = CompendiumBook.Load(book.Save());
            Assert.Equal(EntryKeys.Did(Deed.Killed), read.FoundOutOf("Troll"));
            Assert.Equal(new[] { "Troll", "Boar" }, read.Lately);
            book.FoundOut("Neck", EntryKeys.WeakSpot("head"));
            book.Raise("Neck", KnownLevel.Known);
            Assert.Null(book.FoundOutOf("Neck"));
        }

        [Fact]
        public void WhatWasFoundOutIsToldInAFewWords()
        {
            Assert.Equal("its health found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.Killed)));
            Assert.Equal("its taming found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.Tamed)));
            Assert.Equal("its pregnancy found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.Born)));
            Assert.Equal("its growing up found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.GrewUp)));
            Assert.Equal("its growing back found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.GrewBack)));
            // A creature ridden shows its stamina and speed when ridden, which the game never tells.
            Assert.Equal("its riding found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.Ridden)));
            Assert.Equal("how Lox rides", JournalWords.FoundOutOf(EntryKeys.Did(Deed.Ridden), "Lox"));
            Assert.Equal("shows once you have ridden one", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.Ridden)));
            Assert.Equal("a weak spot found", CompendiumWords.FoundOutNote(EntryKeys.WeakSpot("head")));
            Assert.Equal("how Fire lands found out", CompendiumWords.FoundOutNote(EntryKeys.Damage("Fire")));
            Assert.Equal("an attack's damage found out", CompendiumWords.FoundOutNote("Troll_log"));
            Assert.Null(CompendiumWords.FoundOutNote(null));
        }

        [Fact]
        public void WhatTheExploredMapBroughtIsToldApartForTheNotice()
        {
            var world = new World();
            world.Book.NoteNews(world.Meadows.Key, EntryKeys.OnMap);
            world.Rule(CompendiumRule.Loose);

            Assert.True(world.Explorer.FromMap(world.Meadows));
            Assert.False(world.Explorer.FromMap(world.Troll));
            Assert.Equal("seen on your explored map", world.Explorer.LearnedNote(world.Meadows));
            world.Rule(CompendiumRule.Off);
            Assert.False(world.Explorer.FromMap(world.Meadows));
        }

        [Fact]
        public void TheLearnedListSaysWhatWasFoundOut()
        {
            var world = new World();
            world.Book.FoundOut(world.Troll.Key, EntryKeys.Did(Deed.Killed));
            world.Rule(CompendiumRule.Strict);

            Assert.Equal("its health found out", world.Explorer.LearnedNote(world.Troll));
            Assert.Equal("known", world.Explorer.LearnedNote(world.Hide));

            // Open, the list puts what was just found out first, under either rules.
            world.Rule(CompendiumRule.Loose);
            world.Explorer.LearnedOnly = true;
            world.Book.FoundOut(world.Hide.Key, "TrollHide_kept");
            world.Rule(CompendiumRule.Loose);
            Assert.Same(world.Hide, world.Explorer.Results[0]);
            world.Rule(CompendiumRule.Off);
            Assert.Null(world.Explorer.LearnedNote(world.Troll));
        }

        [Fact]
        public void TheOtherNumbersTheGameNeverShowsWaitForADeedOfTheirOwn()
        {
            // A tree's or rock's health until one broke, what stars change until a starred one
            // fell, a spawner's pool and pace until it spawned, a producer's pace until collected from.
            Assert.Equal("??? · shows once you have broken one", CompendiumWords.KeptBack(EntryKeys.Did(Deed.Broke)));
            Assert.Equal("shows once you have seen a starred one fall", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.StarredFell)));
            Assert.Equal("shows once you have seen it spawn", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.Spawned)));
            Assert.Equal("shows once you have collected from one", CompendiumWords.ShowsOnce(EntryKeys.Did(Deed.Collected)));
            Assert.Equal("its health found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.Broke)));
            Assert.Equal("what stars change found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.StarredFell)));
            Assert.Equal("its spawning found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.Spawned)));
            Assert.Equal("its pace found out", CompendiumWords.FoundOutNote(EntryKeys.Did(Deed.Collected)));

            var rows = CompendiumWords.StillToFindOut(
                new[] { EntryKeys.Did(Deed.Broke), EntryKeys.Did(Deed.StarredFell), EntryKeys.Did(Deed.Spawned), EntryKeys.Did(Deed.Collected) },
                new string[0], 0, 0, new[] { Tally.Held, Tally.Weather });
            Assert.Equal(new[]
            {
                ("Break one", "shows its health"),
                ("See a starred one fall", "shows what stars change"),
                ("See it spawn", "shows its pool and pace"),
                ("Collect from one", "shows its pace"),
                ("Open one", "how many of each it holds"),
                ("Spend time in it", "how often each weather comes"),
            }, rows);
        }

        [Fact]
        public void AChestIsCountedTheFirstTimeTheCharacterOpensEachOne()
        {
            var book = new CompendiumBook();
            Assert.True(book.FirstOpen("world:1:2"));
            Assert.False(book.FirstOpen("world:1:2"));
            Assert.True(book.FirstOpen("world:1:3"));
            book.Held("TreasureChest", new[] { ("Coins", 30), ("Amber", 1) });
            book.Held("TreasureChest", new[] { ("Coins", 50) });

            var read = CompendiumBook.Load(book.Save());
            Assert.False(read.FirstOpen("world:1:2"));
            Assert.Equal(2, read.Tallied(Tally.Held).Kills("TreasureChest"));
            Assert.Equal("30–50 (2 of 2 opened)", TallyWords.Line(Tally.Held, read.Tallied(Tally.Held).Of("TreasureChest").First(d => d.Item == "Coins"), 2));
            Assert.Equal("How many shows once you have opened one", TallyWords.NotYetCounted(Tally.Held));
            Assert.Equal("Held when you opened it (2 times)", TallyWords.Title(Tally.Held, null, 2));
        }

        [Fact]
        public void AChestOpenedOnceIsNotCountedAgainAfterTheWorldLoadsAnew()
        {
            // The game numbers its objects anew on each load, so a chest goes by where it stands.
            var book = new CompendiumBook();
            Assert.True(book.FirstOpen(CompendiumBook.ChestId("world", "TreasureChest_forestcrypt", 120.04f, 5021.5f, -33.26f)));
            var read = CompendiumBook.Load(book.Save());
            Assert.False(read.FirstOpen(CompendiumBook.ChestId("world", "TreasureChest_forestcrypt", 120.04f, 5021.5f, -33.26f)));
            Assert.True(read.FirstOpen(CompendiumBook.ChestId("world", "TreasureChest_forestcrypt", 120.04f, 5024.5f, -33.26f)));
            Assert.True(read.FirstOpen(CompendiumBook.ChestId("world", "TreasureChest_forestcrypt", 125.04f, 5021.5f, -33.26f)));
            Assert.True(read.FirstOpen(CompendiumBook.ChestId("other", "TreasureChest_forestcrypt", 120.04f, 5021.5f, -33.26f)));
            Assert.True(read.FirstOpen(CompendiumBook.ChestId("world", "TreasureChest_mountaincave", 120.04f, 5021.5f, -33.26f)));
        }

        [Fact]
        public void ChestsKeptByTheGamesPassingNumbersAreLetGoAsTheBookIsRead()
        {
            // Kept by a number the game gives anew on each load, such an id never matches again.
            var book = new CompendiumBook();
            book.FirstOpen("world/2318823923:41");
            book.FirstOpen(CompendiumBook.ChestId("world", "TreasureChest_heath", 10f, 30f, 20f));
            var text = CompendiumBook.Load(book.Save()).Save();
            Assert.DoesNotContain("2318823923:41", text);
            Assert.Contains("TreasureChest_heath", text);
        }

        [Fact]
        public void TheWeathersOfABiomeAreCountedAsTheCharacterSeesThem()
        {
            var world = new World();
            world.Book.Weathered("Meadows", "Clear");
            world.Book.Weathered("Meadows", "Rain");
            world.Book.Weathered("Meadows", "Clear");
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Meadows);

            Assert.Equal("2 of 3 seen", world.Explorer.StrictAmount(Tally.Weather, "Meadows", "Clear"));
            Assert.Equal("???", world.Explorer.StrictAmount(Tally.Weather, "Meadows", "Misty"));
            Assert.Equal("How often shows as you see its weathers", TallyWords.NotYetCounted(Tally.Weather));
            Assert.Equal(3, CompendiumBook.Load(world.Book.Save()).Tallied(Tally.Weather).Kills("Meadows"));
        }

        [Fact]
        public void AChestsLootABushsExtrasAndAProducersPaceAreHeldBackOnAnItemsPage()
        {
            var world = new World();
            world.Book.Held("TreasureChest", new[] { ("Coins", 30) });
            world.Rule(CompendiumRule.Strict);
            var roll = new DropTableInfo();
            roll.Drops.Add(new DropInfo("Coins", 10, 50, 1f));
            world.Explorer.Select(world.Coins);

            Assert.Equal(Tally.Held, SourceWords.CountedBy(SourceFacts.FromTable("TreasureChest", "Treasure chest", TableOf.Container, roll, roll.Drops[0])));
            Assert.Equal(Tally.Gave, SourceWords.CountedBy(SourceFacts.FromTable("RaspberryBush", "Raspberry bush", TableOf.Pickable, roll, roll.Drops[0])));
            Assert.Equal("Found in Treasure chest, 30 (1 of 1 opened)", world.Explorer.StrictSourceLine(SourceFacts.FromTable("TreasureChest", "Treasure chest", TableOf.Container, roll, roll.Drops[0])));

            // A producer's pace until collected from.
            var hive = SourceFacts.Made("piece_beehive", "Beehive", 1200f, 4, "");
            Assert.Equal("Made by Beehive, ???", world.Explorer.StrictSourceLine(hive));
            Assert.Equal("Its pace shows once you have collected from one", SourceWords.HeldTip(hive));
            world.Book.Witness("piece_beehive", EntryKeys.Did(Deed.Collected));
            world.Rule(CompendiumRule.Strict);
            Assert.Null(world.Explorer.StrictSourceLine(hive));
            Assert.Equal(TallyWords.NeverCounted, SourceWords.HeldTip(SourceFacts.FromTable("x", "x", TableOf.Other, roll, roll.Drops[0])));
            Assert.Equal(TallyWords.NotYetCounted(Tally.Fell), SourceWords.HeldTip(SourceFacts.Dropped("Troll", "Troll", 1, 2, false, 1f)));
            Assert.Null(SourceWords.HeldTip(SourceFacts.Of(SourceWay.Picked, "x", "x")));
        }

        [Fact]
        public void AWeakSpotIsLeftOutUntilAHitFoundIt()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);
            var head = EntryKeys.WeakSpot("head");

            Assert.True(world.Explorer.KeepsBack(head));
            world.Book.Witness(world.Troll.Key, head);
            Assert.False(world.Explorer.KeepsBack(head));
            Assert.True(EntryKeys.IsWeakSpot(head));
            Assert.False(EntryKeys.IsWeakSpot(EntryKeys.Did(Deed.Killed)));
            Assert.Equal("1 more weak spot not yet found", CompendiumWords.WeakSpotsNotFound(1));
            Assert.Equal("3 more weak spots not yet found", CompendiumWords.WeakSpotsNotFound(3));
            Assert.Null(CompendiumWords.WeakSpotsNotFound(0));
        }

        [Fact]
        public void DropsShowWhatTheCharacterSawFallInsteadOfTheGamesChances()
        {
            var world = new World();
            world.Book.Dropped("Troll", new[] { ("TrollHide", 2), ("Coins", 10) });
            world.Book.Dropped("Troll", new[] { ("TrollHide", 1) });
            world.Book.Dropped("Troll", new[] { ("TrollHide", 2) });
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);

            // In the game's own form, the count in the bracket where the game puts its chance.
            Assert.Equal("1–2 (3 of 3)", world.Explorer.StrictAmount(Tally.Fell, "Troll", "TrollHide"));
            Assert.Equal("10 (1 of 3)", world.Explorer.StrictAmount(Tally.Fell, "Troll", "Coins"));
            Assert.Equal("???", world.Explorer.StrictAmount(Tally.Fell, "Troll", "Wood"));
            Assert.Equal("Drops you saw fall (3 kills)", world.Explorer.StrictTitle(Tally.Fell, "Troll", null));
            Assert.Null(world.Explorer.StrictAmount(Tally.None, "Troll", "TrollHide"));
            Assert.Null(world.Explorer.StrictTitle(Tally.None, "Troll", null));

            // The game's own numbers under the loose rules.
            world.Rule(CompendiumRule.Loose);
            Assert.Null(world.Explorer.StrictAmount(Tally.Fell, "Troll", "TrollHide"));
            Assert.Null(world.Explorer.StrictTitle(Tally.Fell, "Troll", null));
        }

        [Fact]
        public void WhatATreeOrRockGaveIsCountedEachTimeItGaveSomething()
        {
            var world = new World();
            world.Book.Yielded("Beech1", new[] { ("Wood", 3) });
            world.Book.Yielded("Beech1", new[] { ("Wood", 5) });
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Beech);

            Assert.Equal("3–5 (2 of 2)", world.Explorer.StrictAmount(Tally.Gave, "Beech1", "Wood"));
            Assert.Equal("When felled, gave you (2 times)", world.Explorer.StrictTitle(Tally.Gave, "Beech1", "When felled, "));
            Assert.Equal("Each piece gave you (none yet)", world.Explorer.StrictTitle(Tally.Gave, "rock4_copper_frac", "Each piece "));
            Assert.Equal("Drops you saw fall (none yet)", TallyWords.Title(Tally.Fell, null, 0));
            Assert.Equal("How many shows once you have seen it fall", TallyWords.NotYetCounted(Tally.Fell));
            Assert.Equal("How many shows once you have seen it given", TallyWords.NotYetCounted(Tally.Gave));
        }

        [Fact]
        public void AnItemsLinesOfWhereItComesFromShowWhatTheCharacterSawInsteadOfTheGamesChances()
        {
            var world = new World();
            // The troll dropped coins more often than its hide: the hide's line counts the hide.
            world.Book.Dropped("Troll", new[] { ("TrollHide", 2), ("Coins", 20) });
            world.Book.Dropped("Troll", new[] { ("TrollHide", 3), ("Coins", 10) });
            world.Book.Dropped("Troll", new[] { ("Coins", 5) });
            world.Book.Yielded("Beech1", new[] { ("Wood", 3) });
            world.Rule(CompendiumRule.Strict);
            var oneRoll = new DropTableInfo();
            oneRoll.Drops.Add(new DropInfo("Wood", 1, 3, 1f));

            world.Explorer.Select(world.Hide);
            Assert.Equal("Dropped by Troll, 2–3 (2 of 3 kills)", world.Explorer.StrictSourceLine(SourceFacts.Dropped("Troll", "Troll", 5, 9, false, 1f)));
            Assert.Equal("Dropped by Greydwarf, ???", world.Explorer.StrictSourceLine(SourceFacts.Dropped("Greydwarf", "Greydwarf", 1, 2, false, 0.25f)));
            // A chest never opened: held back until it is.
            Assert.Equal("Found in Treasure chest, ???", world.Explorer.StrictSourceLine(SourceFacts.FromTable("TreasureChest", "Treasure chest", TableOf.Container, oneRoll, oneRoll.Drops[0])));
            // A way with no chance in it stays as the game tells it.
            Assert.Null(world.Explorer.StrictSourceLine(SourceFacts.Of(SourceWay.Picked, "RaspberryBush", "Raspberry bush")));
            // The giver's chip says the amount alone, after the giver's name.
            Assert.Equal("2–3 (2 of 3 kills)", world.Explorer.StrictSourceAmount(SourceFacts.Dropped("Troll", "Troll", 5, 9, false, 1f)));
            Assert.Equal("???", world.Explorer.StrictSourceAmount(SourceFacts.Dropped("Greydwarf", "Greydwarf", 1, 2, false, 0.25f)));
            Assert.Null(world.Explorer.StrictSourceAmount(SourceFacts.Of(SourceWay.Picked, "RaspberryBush", "Raspberry bush")));

            world.Explorer.Select(world.Wood);
            Assert.Equal("Felled from Beech, 3 (1 of 1 time)", world.Explorer.StrictSourceLine(SourceFacts.FromTable("Beech1", "Beech", TableOf.Tree, oneRoll, oneRoll.Drops[0])));
            Assert.Equal("Chopped from Beech log, ???", world.Explorer.StrictSourceLine(SourceFacts.FromTable("BeechLog", "Beech log", TableOf.Log, oneRoll, oneRoll.Drops[0])));

            world.Rule(CompendiumRule.Loose);
            Assert.Null(world.Explorer.StrictSourceLine(SourceFacts.Dropped("Troll", "Troll", 5, 9, false, 1f)));
        }

        [Fact]
        public void EachWayOfComingByAThingIsCountedOrNot()
        {
            Assert.Equal(Tally.Fell, SourceWords.CountedBy(SourceFacts.Dropped("Troll", "Troll", 1, 2, false, 1f)));
            foreach (var table in new[] { TableOf.Tree, TableOf.Log, TableOf.Rock, TableOf.Broken })
            {
                Assert.Equal(Tally.Gave, SourceWords.CountedBy(SourceFacts.FromTable("x", "x", table, new DropTableInfo(), new DropInfo("y", 1, 1, 1f))));
            }
            // Counted nowhere: a mod's own part.
            Assert.Equal(Tally.None, SourceWords.CountedBy(SourceFacts.FromTable("x", "x", TableOf.Other, new DropTableInfo(), new DropInfo("y", 1, 1, 1f))));
            // No chance to hold back: as the game tells it.
            Assert.Null(SourceWords.CountedBy(SourceFacts.Of(SourceWay.Picked, "x", "x")));
            Assert.Null(SourceWords.CountedBy(SourceFacts.Made("x", "x", 60f, 4, "")));
            Assert.Null(SourceWords.CountedBy(null));
            Assert.Equal("Held back: what falls and what breaks are counted, not this", TallyWords.NeverCounted);
            // What a line held back says on hover: how it comes to show, or that nothing counts it.
            Assert.Equal(TallyWords.NeverCounted, TallyWords.HeldTip(Tally.None));
            Assert.Equal("How many shows once you have seen it fall", TallyWords.HeldTip(Tally.Fell));
            Assert.Equal("How many shows once you have seen it given", TallyWords.HeldTip(Tally.Gave));
            Assert.Null(TallyWords.HeldTip(null));
        }

        [Fact]
        public void TheCountsAreKeptWithTheBookAndSavedAsTheyStood()
        {
            var book = new CompendiumBook();
            // A break that gave nothing is no time it gave.
            book.Yielded("Beech1", new (string, int)[0]);
            Assert.False(book.Changed);
            Assert.Equal(0, book.Tallied(Tally.Gave).Kills("Beech1"));
            book.Dropped("Troll", new[] { ("TrollHide", 2) });
            Assert.True(book.Changed);
            Assert.Equal(0, book.Tallied(Tally.None).Kills("Troll"));
            Assert.Equal(0, book.Tallied(Tally.Gave).Kills("Troll"));
            book.Yielded("Beech1", new[] { ("Wood", 3) });
            Assert.Equal(0, book.Tallied(Tally.None).Kills("Beech1"));
            var later = book.ToSave();
            book.Dropped("Troll", new[] { ("Coins", 4) });

            var read = CompendiumBook.Load(later());
            Assert.Equal(1, read.Tallied(Tally.Fell).Kills("Troll"));
            Assert.Equal(2, read.Tallied(Tally.Fell).Of("Troll").Single().Most);
            Assert.Equal(3, read.Tallied(Tally.Gave).Of("Beech1").Single().Total);
            Assert.Equal(2, CompendiumBook.Load(book.Save()).Tallied(Tally.Fell).Kills("Troll"));
        }

        [Fact]
        public void ACreaturesSpawnLinesShowOnlyForBiomesItWasMetIn()
        {
            var world = new World();
            world.Book.Witness(world.Troll.Key, world.Forest.Key);
            world.Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);

            Assert.False(world.Explorer.LeavesOutAll(new[] { world.Forest.Key }));
            Assert.True(world.Explorer.LeavesOutAll(new[] { world.Meadows.Key }));
            Assert.False(world.Explorer.LeavesOutAll(new[] { world.Meadows.Key, world.Forest.Key }));
            Assert.False(world.Explorer.LeavesOutAll(new string[0]));
            Assert.False(world.Explorer.LeavesOutAll(null));
            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.LeavesOutAll(new[] { world.Meadows.Key }));
        }
    }
}
