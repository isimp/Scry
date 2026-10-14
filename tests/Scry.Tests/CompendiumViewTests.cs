using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CompendiumViewTests
    {
        // What each entry shows as, from what the character knows and the ruleset: under Loose a
        // known entry's whole page, what it owns (its effects, sounds, leftovers) along with it,
        // and what its page names heard of.

        private static Entry E(Kind kind, string name) => new Entry { Kind = kind, Name = name };

        private static CompendiumView View(params Entry[] entries) => new CompendiumView(new EntryCatalog(entries));

        [Fact]
        public void TheModeStepsThroughTheRulesAndStopsAtEitherEnd()
        {
            Assert.Equal(CompendiumRule.Loose, CompendiumRules.Step(CompendiumRule.Off, 1));
            Assert.Equal(CompendiumRule.Strict, CompendiumRules.Step(CompendiumRule.Loose, 1));
            Assert.Equal(CompendiumRule.Strict, CompendiumRules.Step(CompendiumRule.Strict, 1));
            Assert.Equal(CompendiumRule.Loose, CompendiumRules.Step(CompendiumRule.Strict, -1));
            Assert.Equal(CompendiumRule.Off, CompendiumRules.Step(CompendiumRule.Loose, -1));
            Assert.Equal(CompendiumRule.Off, CompendiumRules.Step(CompendiumRule.Off, -1));
        }

        [Fact]
        public void WithTheCompendiumOffEverythingIsKnown()
        {
            var troll = E(Kind.Creature, "Troll");
            var view = View(troll);

            view.Tell(new CompendiumBook(), CompendiumRule.Off);

            Assert.Equal(KnownLevel.Known, view.Of(troll));
        }

        [Fact]
        public void UnderLooseAnEntryIsWhatTheCharacterKnowsOfItAndModsAreAlwaysKnown()
        {
            var troll = E(Kind.Creature, "Troll");
            var boar = E(Kind.Creature, "Boar");
            var neck = E(Kind.Creature, "Neck");
            var mod = E(Kind.Mod, "Adventure Backpacks");
            var view = View(troll, boar, neck, mod);
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);
            book.Raise(boar.Key, KnownLevel.Seen);

            view.Tell(book, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.Known, view.Of(troll));
            Assert.Equal(KnownLevel.Seen, view.Of(boar));
            Assert.Equal(KnownLevel.Unknown, view.Of(neck));
            Assert.Equal(KnownLevel.Known, view.Of(mod));
            Assert.Equal(KnownLevel.Unknown, view.Of(null));
        }

        [Fact]
        public void WithNoBookYetOnlyModsAreKnown()
        {
            var troll = E(Kind.Creature, "Troll");
            var mod = E(Kind.Mod, "Adventure Backpacks");
            var view = View(troll, mod);

            view.Tell(null, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.Unknown, view.Of(troll));
            Assert.Equal(KnownLevel.Known, view.Of(mod));
        }

        [Fact]
        public void WhatAnEntryOnlyHeardOfPlaysStaysUnknown()
        {
            var troll = E(Kind.Creature, "Troll");
            var hit = E(Kind.Effect, "fx_troll_hit");
            hit.UsedBy.Add("Troll");
            var view = View(troll, hit);
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.HeardOf);

            view.Tell(book, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.HeardOf, view.Of(troll));
            Assert.Equal(KnownLevel.Unknown, view.Of(hit));
        }

        [Fact]
        public void ThingsOwningEachOtherAreKnownTogether()
        {
            // An effect whose sound links back to it: the walk through what is owned still ends.
            var troll = E(Kind.Creature, "Troll");
            var hit = E(Kind.Effect, "fx_troll_hit");
            var sound = E(Kind.Sound, "sfx_troll_hit");
            hit.UsedBy.Add("Troll");
            sound.UsedBy.Add("fx_troll_hit");
            hit.UsedBy.Add("sfx_troll_hit");
            var view = View(troll, hit, sound);
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);

            view.Tell(book, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.Known, view.Of(sound));
        }

        [Fact]
        public void WhatAKnownOrSeenEntryPlaysOrLeavesBehindIsKnownWithIt()
        {
            var troll = E(Kind.Creature, "Troll");
            var greydwarf = E(Kind.Creature, "Greydwarf");
            var hit = E(Kind.Effect, "fx_troll_hit");
            var hitSound = E(Kind.Sound, "sfx_troll_hit");
            var ragdoll = E(Kind.Other, "Troll_ragdoll");
            var dwarfSound = E(Kind.Sound, "sfx_greydwarf_hit");
            hit.UsedBy.Add("Troll");
            hitSound.UsedBy.Add("fx_troll_hit");
            ragdoll.LeftBy.Add("Troll");
            dwarfSound.UsedBy.Add("Greydwarf");
            var view = View(troll, greydwarf, hit, hitSound, ragdoll, dwarfSound);
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Seen);

            view.Tell(book, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.Known, view.Of(hit));
            Assert.Equal(KnownLevel.Known, view.Of(hitSound));
            Assert.Equal(KnownLevel.Known, view.Of(ragdoll));
            Assert.Equal(KnownLevel.Unknown, view.Of(dwarfSound));
        }

        [Fact]
        public void WhatIsKnownWithSomethingElseTellsWhatThatWas()
        {
            // So whoever looks after Scry can see why an effect counts as known.
            var troll = E(Kind.Creature, "Troll");
            var hit = E(Kind.Effect, "fx_troll_hit");
            var hitSound = E(Kind.Sound, "sfx_troll_hit");
            var hide = E(Kind.Item, "TrollHide");
            hit.UsedBy.Add("Troll");
            hitSound.UsedBy.Add("fx_troll_hit");
            troll.AddTermLink("drops", hide);
            var view = View(troll, hit, hitSound, hide);
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);

            view.Tell(book, CompendiumRule.Loose);

            Assert.Same(troll, view.Because(hit));
            Assert.Same(hit, view.Because(hitSound));
            Assert.Same(troll, view.Because(hide));
            Assert.Null(view.Because(troll));
            Assert.Equal(new[] { hit, troll }, view.Chain(hitSound).ToArray());

            view.Tell(book, CompendiumRule.Off);
            Assert.Null(view.Because(hit));
        }

        [Fact]
        public void WhatAKnownEntryLinksToAsItsOwnIsKnownWithIt()
        {
            // A status effect an item gives, a projectile a weapon fires, its leftovers.
            var mead = E(Kind.Item, "MeadTasty");
            var rested = E(Kind.StatusEffect, "Rested");
            var arrow = E(Kind.Projectile, "bow_projectile");
            var log = E(Kind.Resource, "Beech_log");
            var tree = E(Kind.Resource, "Beech1");
            mead.Links.Add(new Link { Group = "Gives", Target = rested.Key });
            mead.Links.Add(new Link { Group = "Fires", Target = arrow.Key });
            tree.LeavesBehind.Add("Beech_log");
            var view = View(mead, rested, arrow, log, tree);
            var book = new CompendiumBook();
            book.Raise(mead.Key, KnownLevel.Known);
            book.Raise(tree.Key, KnownLevel.Known);

            view.Tell(book, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.Known, view.Of(rested));
            Assert.Equal(KnownLevel.Known, view.Of(arrow));
            Assert.Equal(KnownLevel.Known, view.Of(log));
        }

        [Fact]
        public void KnowingWhatSomethingGivesDoesNotMakeKnownWhatElseGivesIt()
        {
            // Bees give Poison; a bile bomb gives it too, and Burning, which Fader's fire gives as
            // well. Only what the hive itself gives comes along with it; Poison's page names the
            // bile bomb, heard of.
            var hive = E(Kind.Piece, "piece_beehive");
            var poison = E(Kind.StatusEffect, "Poison");
            var bile = E(Kind.Projectile, "bilebomb_explosion");
            var burning = E(Kind.StatusEffect, "Burning");
            var fire = E(Kind.Other, "Fader_WallOfFire_AOE");
            var all = new[] { hive, poison, bile, burning, fire };
            var links = new LinkBook();
            links.Add(hive.Key, LinkWords.StatusEffects, poison.Key, LinkWords.GivenBy);
            links.Add(bile.Key, LinkWords.StatusEffects, poison.Key, LinkWords.GivenBy);
            links.Add(bile.Key, LinkWords.StatusEffects, burning.Key, LinkWords.GivenBy);
            links.Add(fire.Key, LinkWords.StatusEffects, burning.Key, LinkWords.GivenBy);
            links.Apply(all);
            var view = View(all);
            var book = new CompendiumBook();
            book.Raise(hive.Key, KnownLevel.Known);

            view.Tell(book, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.Known, view.Of(poison));
            Assert.Equal(KnownLevel.HeardOf, view.Of(bile));
            Assert.Equal(KnownLevel.Unknown, view.Of(burning));
            Assert.Equal(KnownLevel.Unknown, view.Of(fire));
        }

        private static Entry Place(string name, string label) => new Entry { Kind = Kind.Location, Name = name, Components = new[] { "Location" }, FoundIn = new[] { label } };

        private static Entry Room(string name, params string[] labels) => new Entry { Kind = Kind.Location, Name = name, Components = new[] { "Room" }, FoundIn = labels };

        [Fact]
        public void ADungeonKnownMakesItsRoomsKnownButOneOnlySeenDoesNot()
        {
            var crypt = Place("Crypt2", "Burial chambers");
            var cryptRoom = Room("sunkencrypt_Room1", "Burial chambers");
            var cave = Place("TrollCave02", "Troll cave");
            var caveRoom = Room("TrollCave_Room1", "Troll cave");
            var frostRoom = Room("Frostcave_Room1", "Frost caves");
            var otherCrypt = Place("Crypt3", "Burial chambers");
            var view = View(crypt, cryptRoom, cave, caveRoom, frostRoom, otherCrypt);
            var book = new CompendiumBook();
            book.Raise(crypt.Key, KnownLevel.Known);
            book.Raise(cave.Key, KnownLevel.Seen);

            view.Tell(book, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.Known, view.Of(cryptRoom));
            Assert.Same(crypt, view.Because(cryptRoom));
            Assert.Equal(KnownLevel.Unknown, view.Of(caveRoom));
            Assert.Equal(KnownLevel.Unknown, view.Of(frostRoom));
            Assert.Equal(KnownLevel.HeardOf, view.Of(otherCrypt));
        }

        [Fact]
        public void WhatIsToldFollowsThePlacesReadAfterTheViewWasMade()
        {
            // Reading the world's places names where things are found and what each place goes by.
            var troll = E(Kind.Creature, "Troll");
            var crypt = Place("Crypt2", "");
            var room = Room("sunkencrypt_Room1");
            var catalog = new EntryCatalog(new[] { troll, crypt, room });
            var view = new CompendiumView(catalog);
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);
            book.Raise(crypt.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Unknown, view.Of(room));

            troll.FoundIn = new[] { "Burial chambers" };
            crypt.FoundIn = new[] { "Burial chambers" };
            room.FoundIn = new[] { "Burial chambers" };
            catalog.Regroup();
            view.Tell(book, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.Known, view.Of(room));
        }

        [Fact]
        public void WhatAKnownPageNamesIsHeardOfButNotWhatOnlyASeenOneNames()
        {
            var troll = E(Kind.Creature, "Troll");
            var hide = E(Kind.Item, "TrollHide");
            var club = E(Kind.Item, "Club");
            var boar = E(Kind.Creature, "Boar");
            var meat = E(Kind.Item, "RawMeat");
            var workbench = E(Kind.Piece, "piece_workbench");
            var cave = new Entry { Kind = Kind.Location, Name = "Crypt2", Components = new[] { "Location" }, FoundIn = new[] { "Burial chambers" } };
            var room = new Entry { Kind = Kind.Location, Name = "sunkencrypt_Room1", Components = new[] { "Room" }, FoundIn = new[] { "Burial chambers" } };
            var stone = E(Kind.Item, "Stone");
            troll.AddTermLink("drops", hide);
            troll.Links.Add(new Link { Group = "Carries", Target = club.Key });
            troll.Stations = new[] { new StationUse("piece_workbench", "Workbench", 1) };
            troll.FoundIn = new[] { "Burial chambers" };
            boar.AddTermLink("drops", meat);
            var view = View(troll, hide, club, boar, meat, workbench, cave, room, stone);
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);
            book.Raise(boar.Key, KnownLevel.Seen);
            book.Raise(stone.Key, KnownLevel.Known);

            view.Tell(book, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.HeardOf, view.Of(hide));
            Assert.Equal(KnownLevel.HeardOf, view.Of(club));
            Assert.Equal(KnownLevel.HeardOf, view.Of(workbench));
            Assert.Equal(KnownLevel.HeardOf, view.Of(cave));
            Assert.Equal(KnownLevel.Unknown, view.Of(room));
            Assert.Equal(KnownLevel.Unknown, view.Of(meat));
            Assert.Equal(KnownLevel.Known, view.Of(stone));
        }

        [Fact]
        public void EveryKindOfLinkTermNamesWhatItLinksTo()
        {
            var troll = E(Kind.Creature, "Troll");
            var named = new List<Entry>();
            var all = new List<Entry> { troll };
            foreach (var key in new[] { "drops", "from", "needs", "gives", "spawns" })
            {
                var other = E(Kind.Item, "Thing_" + key);
                troll.AddTermLink(key, other);
                named.Add(other);
                all.Add(other);
            }
            var view = View(all.ToArray());
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);

            view.Tell(book, CompendiumRule.Loose);

            Assert.All(named, e => Assert.Equal(KnownLevel.HeardOf, view.Of(e)));
        }

        [Fact]
        public void HearingOfSomethingNeverLowersWhatIsKnownOfIt()
        {
            var troll = E(Kind.Creature, "Troll");
            var hide = E(Kind.Item, "TrollHide");
            troll.AddTermLink("drops", hide);
            var view = View(troll, hide);
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);
            book.Raise(hide.Key, KnownLevel.Seen);

            view.Tell(book, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.Seen, view.Of(hide));
        }

        [Fact]
        public void TheViewIsToldAgainOnlyWhenTheBookOrTheRuleChanges()
        {
            var troll = E(Kind.Creature, "Troll");
            var view = View(troll);
            var book = new CompendiumBook();

            view.Tell(book, CompendiumRule.Loose);
            var first = view.Version;
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(first, view.Version);

            book.Raise(troll.Key, KnownLevel.Seen);
            view.Tell(book, CompendiumRule.Loose);
            Assert.NotEqual(first, view.Version);
            Assert.Equal(KnownLevel.Seen, view.Of(troll));

            var second = view.Version;
            view.Tell(book, CompendiumRule.Off);
            Assert.NotEqual(second, view.Version);
            Assert.Equal(KnownLevel.Known, view.Of(troll));

            // Another book of the same version is still another character's.
            var other = new CompendiumBook();
            other.Raise("Boar", KnownLevel.Seen);
            view.Tell(other, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Unknown, view.Of(troll));
        }

        [Fact]
        public void WithTheCompendiumOffWhatTheCharacterLearnsChangesNothingShown()
        {
            var troll = E(Kind.Creature, "Troll");
            var view = View(troll);
            var book = new CompendiumBook();

            view.Tell(book, CompendiumRule.Off);
            var first = view.Version;
            book.Raise(troll.Key, KnownLevel.Seen);
            view.Tell(book, CompendiumRule.Off);
            view.Tell(new CompendiumBook(), CompendiumRule.Off);

            Assert.Equal(first, view.Version);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(KnownLevel.Seen, view.Of(troll));
        }

        [Fact]
        public void AnotherCharactersBookIsToldAnewThoughItsVersionIsTheSame()
        {
            var troll = E(Kind.Creature, "Troll");
            var boar = E(Kind.Creature, "Boar");
            var view = View(troll, boar);
            var first = new CompendiumBook();
            first.Raise(troll.Key, KnownLevel.Seen);
            var second = new CompendiumBook();
            second.Raise(boar.Key, KnownLevel.Seen);

            view.Tell(first, CompendiumRule.Loose);
            view.Tell(second, CompendiumRule.Loose);

            Assert.Equal(KnownLevel.Unknown, view.Of(troll));
            Assert.Equal(KnownLevel.Seen, view.Of(boar));
        }

        [Fact]
        public void TheMainKindsCountWhatWasFoundOfAllThereIs()
        {
            foreach (var kind in new[] { Kind.Creature, Kind.Item, Kind.Piece, Kind.Resource, Kind.Spawner, Kind.StatusEffect, Kind.Location, Kind.Raid, Kind.Biome })
            {
                Assert.True(CompendiumView.Counted(kind), kind.ToString());
            }
            foreach (var kind in new[] { Kind.Effect, Kind.Sound, Kind.Projectile, Kind.Other, Kind.Mod })
            {
                Assert.False(CompendiumView.Counted(kind), kind.ToString());
            }
        }
    }
}
