using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    /// <summary>
    /// With the compendium on, the search asks what the character met: is:new what it learned
    /// today, is:seen what it has only seen, is:walked the dungeons and rooms it walked; each can be
    /// left out with a minus and joined with a comma. With the compendium off nothing was met.
    /// </summary>
    public class SearchWhatYouMetTests
    {
        private sealed class World
        {
            public readonly Entry Troll = E("Troll", Kind.Creature);
            public readonly Entry Boar = E("Boar", Kind.Creature);
            public readonly Entry Neck = E("Neck", Kind.Creature);
            public readonly Entry Crypt = new Entry { Kind = Kind.Location, Name = "Crypt2", Components = new[] { "Location" }, FoundIn = new[] { "Burial chambers" } };
            public readonly Entry Walked = new Entry { Kind = Kind.Location, Name = "sunkencrypt_Room1", Components = new[] { "Room" }, FoundIn = new[] { "Burial chambers" } };
            public readonly Entry NotWalked = new Entry { Kind = Kind.Location, Name = "sunkencrypt_Room2", Components = new[] { "Room" }, FoundIn = new[] { "Burial chambers" } };
            public readonly Explorer Explorer;
            public readonly CompendiumView View;
            public readonly CompendiumBook Book = new CompendiumBook();
            public int Day = 3;

            public World()
            {
                foreach (var creature in new[] { Troll, Boar, Neck }) creature.Components = new[] { "Character" };
                Explorer = new Explorer(new[] { Troll, Boar, Neck, Crypt, Walked, NotWalked }.ToList(), new Favourites(Path.Combine(TempDir(), "favourites.txt")));
                View = new CompendiumView(Explorer.Entries);
                Book.Now = () => new Moment(Day, "Meadows");
                Book.Raise(Troll.Key, KnownLevel.Known);
                Book.Raise(Crypt.Key, KnownLevel.Known);
                Day = 4;
                Book.Raise(Boar.Key, KnownLevel.Seen);
                var stood = new HashSet<string> { Walked.Name };
                View.RoomsWalked(stood.Contains, 1);
            }

            public World Rule(CompendiumRule rule)
            {
                View.Tell(Book, rule);
                Explorer.Know(View);
                return this;
            }

            public Entry[] Found(string text)
            {
                Explorer.Text = text;
                return Explorer.Results.OrderBy(e => e.Name, System.StringComparer.Ordinal).ToArray();
            }
        }

        [Fact]
        public void HasFindsNoComponentWhoseTopicHingesOnAThingNotMet()
        {
            // Breeding hinges on the young: the page leaves the topic out whole while it was not met, so its component names nothing to find either.
            var world = new World();
            world.Troll.Components = new[] { "Humanoid", "Procreation" };
            world.Troll.DeedHinges[Deed.Born] = world.Neck.Key;
            world.Rule(CompendiumRule.Loose);
            Assert.Empty(world.Found("has:Procreation"));
            Assert.Equal(new[] { world.Troll }, world.Found("has:Humanoid"));
            world.Book.Raise(world.Neck.Key, KnownLevel.Seen);
            world.Rule(CompendiumRule.Loose);
            Assert.Equal(new[] { world.Troll }, world.Found("has:Procreation"));
            // The compendium off names every component.
            world.Rule(CompendiumRule.Off);
            Assert.Equal(new[] { world.Troll }, world.Found("has:Procreation"));
        }

        [Fact]
        public void IsLeftPassesOverADeedThatHingesOnAThingNotMet()
        {
            // Riding hinges on the saddle: the page leaves the topic out whole while it was not met, so nothing of it is left to find.
            var world = new World();
            world.Troll.Deeds = new[] { Deed.Ridden };
            world.Troll.DeedHinges[Deed.Ridden] = world.Neck.Key;
            world.Rule(CompendiumRule.Strict);
            Assert.Empty(world.Found("is:left"));
            world.Book.Raise(world.Neck.Key, KnownLevel.Seen);
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { world.Troll }, world.Found("is:left"));
        }

        [Fact]
        public void IsLeftFindsWhatStillHasANumberToFindOutUnderTheStrictRules()
        {
            // A to-do list for the strict rules: a known Troll whose health waits for a kill, and a
            // known Neck whose health was found out, so nothing is left of it; the loose rules keep
            // nothing back, so nothing is left there.
            var world = new World();
            world.Troll.Deeds = new[] { Deed.Killed };
            world.Neck.Deeds = new[] { Deed.Killed };
            world.Book.Raise(world.Neck.Key, KnownLevel.Known);
            world.Book.FoundOut(world.Neck.Key, EntryKeys.Did(Deed.Killed));
            world.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { world.Troll }, world.Found("is:left"));
            Assert.Contains(world.Neck, world.Found("kind:creature -is:left"));
            world.Rule(CompendiumRule.Loose);
            Assert.Empty(world.Found("is:left"));

            // is:left reads what the page's chip counts: an attack not yet taken or seen begun, a
            // type not yet told, stars not yet seen to fall, and health that riding showed as well.
            var fuller = new World();
            fuller.Troll.Deeds = new[] { Deed.Killed, Deed.Ridden };
            fuller.Troll.Attacks = new[] { ("Troll_log", new[] { "Blunt" }) };
            fuller.Neck.Deeds = new[] { Deed.StarredFell };
            fuller.Book.Raise(fuller.Neck.Key, KnownLevel.Known);
            fuller.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { fuller.Neck, fuller.Troll }, fuller.Found("is:left"));
            fuller.Book.FoundOut(fuller.Troll.Key, EntryKeys.Did(Deed.Ridden));
            fuller.Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { fuller.Neck, fuller.Troll }, fuller.Found("is:left"));
            fuller.Book.FoundOut(fuller.Troll.Key, EntryKeys.Attack("Troll_log"));
            fuller.Book.FoundOut(fuller.Neck.Key, EntryKeys.Did(Deed.StarredFell));
            fuller.Book.FoundOut(fuller.Neck.Key, EntryKeys.StarsFell(1));
            fuller.Book.FoundOut(fuller.Neck.Key, EntryKeys.StarsFell(2));
            fuller.Rule(CompendiumRule.Strict);
            Assert.Empty(fuller.Found("is:left"));
            // A trader is never killed, a piece never broken: those deeds fit only creatures and resources.
            Assert.True(StrictDeeds.Fits(Deed.Killed, Kind.Creature));
            Assert.False(StrictDeeds.Fits(Deed.Killed, Kind.Trader));
            Assert.False(StrictDeeds.Fits(Deed.Broke, Kind.Piece));
            Assert.True(StrictDeeds.Fits(Deed.Tamed, Kind.Creature));
        }

        [Fact]
        public void IsNewFindsWhatTheCharacterLearnedToday()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            Assert.Equal(new[] { world.Boar }, world.Found("is:new"));
            Assert.DoesNotContain(world.Boar, world.Found("-is:new"));
            Assert.Contains(world.Troll, world.Found("-is:new"));

            // The next day, nothing learned is new.
            world.Day = 5;
            Assert.Empty(world.Found("is:new"));
        }

        [Fact]
        public void IsSeenFindsWhatTheCharacterHasOnlySeen()
        {
            var world = new World().Rule(CompendiumRule.Loose);
            Assert.Equal(new[] { world.Boar }, world.Found("is:seen"));
            Assert.Equal(new[] { world.Troll }, world.Found("kind:creature -is:seen"));
        }

        [Fact]
        public void IsWalkedFindsTheRoomsWalkedAndTheDungeonsHoldingThem()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            Assert.Equal(new[] { world.Crypt, world.Walked }.OrderBy(e => e.Name, System.StringComparer.Ordinal), world.Found("is:walked"));
            Assert.DoesNotContain(world.NotWalked, world.Found("is:walked"));
        }

        [Fact]
        public void ACommaJoinsTheWordsAsOr()
        {
            var world = new World().Rule(CompendiumRule.Strict);
            var found = world.Found("is:seen,walked");
            Assert.Contains(world.Boar, found);
            Assert.Contains(world.Walked, found);
            Assert.DoesNotContain(world.Troll, found);
        }

        [Fact]
        public void WithTheCompendiumOffNothingWasMet()
        {
            var world = new World().Rule(CompendiumRule.Off);
            Assert.Empty(world.Found("is:new"));
            Assert.Empty(world.Found("is:seen"));
            Assert.Equal(6, world.Found("-is:walked").Length);
            Assert.False(Search.Matches(world.Boar, "is:seen"));
        }

        [Fact]
        public void OnlyTheWholeWordAsksWhatWasMet()
        {
            // is:w could be a weapon or wearable: a word cut short stays the catalog's.
            Assert.False(Search.AsksMine("is", "w", out _));
            Assert.False(Search.AsksMine("is", "new,boss", out _));
            Assert.True(Search.AsksMine("is", "New,seen", out var words));
            Assert.Equal(new[] { "new", "seen" }, words);
            Assert.False(Search.AsksMine("kind", "new", out _));
        }
    }
}
