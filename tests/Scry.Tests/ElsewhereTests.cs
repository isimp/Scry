using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class ElsewhereTests
    {
        // With other players near, one player's game runs each thing around them, and only that
        // game sees it fall, break, be picked, spawn, be born, grow up or eat. The strict rules
        // tell the character what its own kills, breaks and picks ran in another player's game,
        // so went uncounted, and say where a count or a hint is seen only where its game runs it.

        private sealed class World
        {
            public readonly Entry Troll = E("Troll", Kind.Creature);
            public readonly Entry Hide = E("TrollHide", Kind.Item);
            public readonly Entry Beech = E("Beech1", Kind.Resource);
            public readonly Entry Raspberry = E("RaspberryBush", Kind.Resource);
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();

            public World()
            {
                Troll.AddTermLink("drops", Hide);
                var all = new[] { Troll, Hide, Beech, Raspberry }.ToList();
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
        public void KillsBreaksAndPicksThatRanInAnotherPlayersGameAreCountedAndKept()
        {
            var book = new CompendiumBook();
            book.RanElsewhere(Elsewhere.Kill, "Troll");
            book.RanElsewhere(Elsewhere.Kill, "Troll");
            book.RanElsewhere(Elsewhere.Break, "Beech1");
            book.RanElsewhere(Elsewhere.Pick, "RaspberryBush");
            Assert.True(book.Changed);

            var read = CompendiumBook.Load(book.Save());
            Assert.Equal(2, read.CountElsewhere(Elsewhere.Kill, "Troll"));
            Assert.Equal(1, read.CountElsewhere(Elsewhere.Break, "Beech1"));
            Assert.Equal(1, read.CountElsewhere(Elsewhere.Pick, "RaspberryBush"));
            Assert.Equal(0, read.CountElsewhere(Elsewhere.Break, "Troll"));
            Assert.Equal(0, read.CountElsewhere(Elsewhere.Kill, "Boar"));

            // Nothing is counted for no thing.
            var none = new CompendiumBook();
            none.RanElsewhere(Elsewhere.Kill, "");
            Assert.False(none.Changed);
        }

        [Fact]
        public void PlayingWithOthersIsKeptWithTheCharacterOnceSeen()
        {
            var book = new CompendiumBook();
            Assert.False(book.WithOthers);
            book.PlayedWithOthers();
            Assert.True(book.WithOthers);
            Assert.True(CompendiumBook.Load(book.Save()).WithOthers);
            Assert.False(CompendiumBook.Load(new CompendiumBook().Save()).WithOthers);

            // Seen again, nothing changes to keep.
            var saved = CompendiumBook.Load(book.Save());
            saved.Save();
            saved.PlayedWithOthers();
            Assert.False(saved.Changed);
        }

        [Fact]
        public void WhatRanElsewhereIsToldInOneLineForEachWay()
        {
            Assert.Equal("5 more of your kills ran in another player's game, so their drops were not counted", ElsewhereWords.Line(Elsewhere.Kill, 5));
            Assert.Equal("1 more of your kills ran in another player's game, so its drops were not counted", ElsewhereWords.Line(Elsewhere.Kill, 1));
            Assert.Equal("2 more you hit broke in another player's game, so what they gave was not counted", ElsewhereWords.Line(Elsewhere.Break, 2));
            Assert.Equal("1 more you hit broke in another player's game, so what it gave was not counted", ElsewhereWords.Line(Elsewhere.Break, 1));
            Assert.Equal("3 more of your picks ran in another player's game, so what they gave was not counted", ElsewhereWords.Line(Elsewhere.Pick, 3));
            Assert.Equal("1 more of your picks ran in another player's game, so what it gave was not counted", ElsewhereWords.Line(Elsewhere.Pick, 1));
            Assert.Null(ElsewhereWords.Line(Elsewhere.Kill, 0));
            Assert.Equal(" · 5 in another player's game", ElsewhereWords.Killed(5));
            Assert.Null(ElsewhereWords.Killed(0));
            Assert.StartsWith("With other players near, one player's game runs each thing around you", ElsewhereWords.Tip);
        }

        [Fact]
        public void OnlyWhatTheGameRunsForTheOwnerMayRunElsewhere()
        {
            foreach (var deed in new[] { Deed.Born, Deed.GrewUp, Deed.Broke, Deed.StarredFell, Deed.Spawned }) Assert.True(OtherGames.MayRun(EntryKeys.Did(deed)), deed.ToString());
            foreach (var deed in new[] { Deed.Killed, Deed.Tamed, Deed.GrewBack, Deed.Collected }) Assert.False(OtherGames.MayRun(EntryKeys.Did(deed)), deed.ToString());
            Assert.False(OtherGames.MayRun(EntryKeys.WeakSpot("Head")));
            Assert.False(OtherGames.MayRun((string)null));
            Assert.True(OtherGames.MayRun(Tally.Fell));
            Assert.True(OtherGames.MayRun(Tally.Gave));
            Assert.False(OtherGames.MayRun(Tally.Held));
            Assert.False(OtherGames.MayRun(Tally.Weather));
            Assert.False(OtherGames.MayRun(Tally.None));
        }

        [Fact]
        public void AfterPlayingWithOthersAHintSaysItIsSeenOnlyWhereYourGameRunsIt()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Explorer.Select(world.Troll);
            var born = EntryKeys.Did(Deed.Born);
            var killed = EntryKeys.Did(Deed.Killed);

            // Alone, as before.
            Assert.Equal(" · shows once you have seen one born", world.Explorer.KeptBackHint(born));
            Assert.Equal("How many shows once you have seen it fall", world.Explorer.NotYetCounted(Tally.Fell));
            Assert.Equal("Health by stars shows once you have seen a starred one fall", world.Explorer.ShowsOnce("Health by stars", EntryKeys.Did(Deed.StarredFell)));

            world.Book.PlayedWithOthers();
            Assert.True(world.Explorer.MayRunElsewhere(born));
            Assert.Equal(" · shows once you have seen one born, where your game runs it", world.Explorer.KeptBackHint(born));
            Assert.Equal("??? · shows once you have broken one, where your game runs it", world.Explorer.KeptBack(EntryKeys.Did(Deed.Broke)));
            Assert.Equal("Health by stars shows once you have seen a starred one fall, where your game runs it", world.Explorer.ShowsOnce("Health by stars", EntryKeys.Did(Deed.StarredFell)));
            Assert.Equal("How many shows once you have seen it fall, where your game runs it", world.Explorer.NotYetCounted(Tally.Fell));
            Assert.Equal("How many shows once you have seen it given, where your game runs it", world.Explorer.NotYetCounted(Tally.Gave));

            // What every game sees, or the character's own game alone, says nothing more.
            Assert.False(world.Explorer.MayRunElsewhere(killed));
            Assert.Equal(" · shows once you have killed one", world.Explorer.KeptBackHint(killed));
            Assert.Equal("How many shows once you have opened one", world.Explorer.NotYetCounted(Tally.Held));

            // Nor under the loose rules, which keep nothing back.
            world.Rule(CompendiumRule.Loose);
            Assert.False(world.Explorer.MayRunElsewhere(born));
        }

        [Fact]
        public void UnderStrictAPageTellsWhatOfItRanInAnotherPlayersGame()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            world.Book.RanElsewhere(Elsewhere.Kill, world.Troll.Key);
            world.Book.RanElsewhere(Elsewhere.Kill, world.Troll.Key);
            world.Book.RanElsewhere(Elsewhere.Break, world.Beech.Key);
            world.Book.RanElsewhere(Elsewhere.Pick, world.Raspberry.Key);

            world.Explorer.Select(world.Troll);
            Assert.Equal(new[] { ElsewhereWords.Line(Elsewhere.Kill, 2) }, world.Explorer.ElsewhereLines(Tally.Fell, world.Troll.Key));
            Assert.Equal(ElsewhereWords.Killed(2), world.Explorer.KilledElsewhere(world.Troll.Key));
            world.Explorer.Select(world.Beech);
            Assert.Equal(new[] { ElsewhereWords.Line(Elsewhere.Break, 1) }, world.Explorer.ElsewhereLines(Tally.Gave, world.Beech.Key));
            world.Explorer.Select(world.Raspberry);
            Assert.Equal(new[] { ElsewhereWords.Line(Elsewhere.Pick, 1) }, world.Explorer.ElsewhereLines(Tally.Gave, world.Raspberry.Key));
            Assert.Empty(world.Explorer.ElsewhereLines(Tally.Held, world.Raspberry.Key));

            // Under the loose rules the game's chances show, so nothing is said.
            world.Rule(CompendiumRule.Loose);
            world.Explorer.Select(world.Troll);
            Assert.Empty(world.Explorer.ElsewhereLines(Tally.Fell, world.Troll.Key));
            Assert.Null(world.Explorer.KilledElsewhere(world.Troll.Key));
        }

        [Fact]
        public void WhatIsStillToFindOutSaysWhichOfItIsSeenOnlyWhereYourGameRunsIt()
        {
            var deeds = new[] { EntryKeys.Did(Deed.Killed), EntryKeys.Did(Deed.Born), EntryKeys.Did(Deed.Broke) };
            var tallies = new[] { Tally.Fell, Tally.Held };
            var alone = CompendiumWords.StillToFindOut(deeds, new string[0], 0, 0, tallies);
            Assert.DoesNotContain(alone, r => r.Value.EndsWith(ElsewhereWords.Suffix));

            var together = CompendiumWords.StillToFindOut(deeds, new string[0], 0, 0, tallies, withOthers: true);
            Assert.Contains(("Kill one", "shows its health"), together);
            Assert.Contains(("See one born", "shows how long it is pregnant, where your game runs it"), together);
            Assert.Contains(("Break one", "shows its health, where your game runs it"), together);
            Assert.Contains(("See it drop", "how many of each, where your game runs it"), together);
            Assert.Contains(("Open one", "how many of each it holds"), together);
        }

        [Fact]
        public void AThingHitLatelyIsToldOnceWhenItGoesWithinTheWindow()
        {
            var lately = new Lately<int>(30.0);
            lately.Hit(7, "Beech1", 100.0);
            lately.Hit(8, "Rock", 100.0);

            Assert.Null(lately.Take(9, 101.0));
            Assert.Equal("Beech1", lately.Take(7, 129.0));
            Assert.Null(lately.Take(7, 129.0));
            Assert.Null(lately.Take(8, 131.0));

            // A later hit starts the window again.
            lately.Hit(7, "Beech1", 200.0);
            lately.Hit(7, "Beech1", 225.0);
            Assert.Equal("Beech1", lately.Of(7, 250.0));
            Assert.Equal("Beech1", lately.Take(7, 250.0));
        }

        [Fact]
        public void ACreatureHitLatelyIsFoundByItsNameAsOftenAsAsked()
        {
            var lately = new Lately<string>(60.0);
            lately.Hit("$enemy_greydwarf", "Greydwarf_Elite", 10.0);
            lately.Hit("$enemy_greydwarf", "Greydwarf", 20.0);

            Assert.Equal("Greydwarf", lately.Of("$enemy_greydwarf", 30.0));
            Assert.Equal("Greydwarf", lately.Of("$enemy_greydwarf", 79.0));
            Assert.Null(lately.Of("$enemy_greydwarf", 81.0));
            Assert.Null(lately.Of("$enemy_troll", 30.0));
        }

        [Fact]
        public void ThingsHitLatelyAreFewAndForgottenWithTheWorld()
        {
            var lately = new Lately<int>(30.0);
            for (var i = 0; i < Lately<int>.Kept + 5; i++) lately.Hit(i, "Rock", i);
            Assert.Equal(Lately<int>.Kept, lately.Count);
            Assert.Null(lately.Of(0, Lately<int>.Kept));
            Assert.Equal("Rock", lately.Of(Lately<int>.Kept + 4, Lately<int>.Kept + 5));

            lately.Forget();
            Assert.Equal(0, lately.Count);

            // A thing with no entry is no hit.
            lately.Hit(1, "", 0.0);
            Assert.Null(lately.Of(1, 0.0));
        }

        [Fact]
        public void TheOneCreatureGoingByANameIsFoundByIt()
        {
            var troll = new Entry { Kind = Kind.Creature, Name = "Troll", Tokens = new[] { "$enemy_troll" } };
            var skeleton = new Entry { Kind = Kind.Creature, Name = "Skeleton", Tokens = new[] { "$enemy_skeleton" } };
            var poison = new Entry { Kind = Kind.Creature, Name = "Skeleton_Poison", Tokens = new[] { "$enemy_skeleton" } };
            var trophy = new Entry { Kind = Kind.Item, Name = "TrophyTroll", Tokens = new[] { "$enemy_troll" } };
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { troll, skeleton, poison, trophy }));

            Assert.Equal(troll.Key, catchUp.OnlyNamed("$enemy_troll", Kind.Creature));
            Assert.Null(catchUp.OnlyNamed("$enemy_skeleton", Kind.Creature));
            Assert.Null(catchUp.OnlyNamed("$enemy_boar", Kind.Creature));
            Assert.Null(catchUp.OnlyNamed(null, Kind.Creature));
        }
    }
}
