using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class CompendiumRecountTests
    {
        // With the panel open, each thing learned is counted into what it shows. Going through the
        // whole catalog each time stalled a frame for tens of milliseconds; as knowledge only
        // rises, what was learned since, with what it brings along, is told on top instead, and
        // the panel shows the same as a count afresh would.

        private static readonly Kind[] Kinds = { Kind.Creature, Kind.Item, Kind.Piece, Kind.Location, Kind.Effect, Kind.Sound, Kind.StatusEffect, Kind.Projectile, Kind.Resource };

        /// <summary>A made-up catalog of a few hundred things, owning and naming one another every way a page can.</summary>
        private static List<Entry> Catalog(Random random)
        {
            var all = Enumerable.Range(0, 300).Select(i => new Entry { Kind = Kinds[random.Next(Kinds.Length)], Name = "thing" + i }).ToList();
            all.Add(new Entry { Kind = Kind.Mod, Name = "SomeMod" });
            Entry Any() => all[random.Next(all.Count - 1)];
            foreach (var entry in all.Take(300))
            {
                for (var l = random.Next(4); l > 0; l--) entry.Links.Add(new Link { Group = "g", Target = Any().Key, Back = random.Next(5) == 0 });
                if (random.Next(6) == 0) entry.UsedBy.Add(Any().Name);
                if (random.Next(8) == 0) entry.LeftBy.Add(Any().Name);
                if (random.Next(8) == 0) entry.LeavesBehind.Add(Any().Name);
                if (random.Next(10) == 0) entry.AddTermLink("drops", Any());
            }
            // A dungeon and its rooms, known all at once with it.
            var dungeon = all.First(e => e.Kind == Kind.Location);
            dungeon.Components = new[] { "Location" };
            dungeon.FoundIn = new[] { "Crypt" };
            foreach (var room in all.Where(e => e.Kind == Kind.Location && e != dungeon).Take(3))
            {
                room.Components = new[] { "Room" };
                room.FoundIn = new[] { "Crypt" };
            }
            return all;
        }

        private static void SameAsAfresh(EntryCatalog catalog, CompendiumView told, CompendiumBook book, CompendiumRule rule, IReadOnlyDictionary<string, int> tiers, int floor)
        {
            var afresh = new CompendiumView(catalog);
            afresh.Milestones(tiers, floor);
            afresh.Tell(book, rule);
            foreach (var entry in catalog.All)
            {
                Assert.True(afresh.Of(entry) == told.Of(entry), $"{entry.Key}: {told.Of(entry)} told on top, {afresh.Of(entry)} afresh");
                Assert.True(afresh.ByFloor(entry) == told.ByFloor(entry), $"{entry.Key}: by the floor {told.ByFloor(entry)} told on top, {afresh.ByFloor(entry)} afresh");
                Assert.True((afresh.Because(entry) == null) == (told.Because(entry) == null), $"{entry.Key}: a reason told on top {told.Because(entry)?.Key}, afresh {afresh.Because(entry)?.Key}");
                // Which of several owners is named may differ; the one named must own or name it, and be known.
                var because = told.Because(entry);
                if (because != null)
                {
                    Assert.Equal(KnownLevel.Known, told.Of(because));
                    Assert.True(told.Owns(because, entry) || told.Named(because).Contains(entry), $"{because.Key} neither owns nor names {entry.Key}");
                }
            }
            foreach (var (owner, owned) in MayOwn(catalog))
            {
                Assert.True(afresh.Owns(owner, owned) == told.Owns(owner, owned), $"{owner.Key} owning {owned.Key}");
            }
        }

        /// <summary>Every pair of which the first may own the second, by any way of owning.</summary>
        private static IEnumerable<(Entry, Entry)> MayOwn(EntryCatalog catalog)
        {
            foreach (var entry in catalog.All)
            {
                foreach (var link in entry.Links) if (catalog.Find(link.Target) is Entry target) yield return (entry, target);
                foreach (var name in entry.UsedBy.Concat(entry.LeftBy)) if (catalog.Find(name) is Entry owner) yield return (owner, entry);
                foreach (var name in entry.LeavesBehind) if (catalog.Find(name) is Entry left) yield return (entry, left);
                if (Places.LabelOf(entry) is string label)
                {
                    foreach (var room in catalog.All.Where(r => r != entry && r.FoundIn.Contains(label))) yield return (entry, room);
                }
            }
        }

        [Theory]
        [InlineData(1, 0)]
        [InlineData(2, 0)]
        [InlineData(3, 1)]
        [InlineData(4, 2)]
        [InlineData(5, 1)]
        public void LearningStepByStepShowsWhatACountAfreshWould(int seed, int floor)
        {
            var random = new Random(seed);
            var all = Catalog(random);
            var catalog = new EntryCatalog(all);
            var tiers = all.Where(_ => random.Next(3) == 0).ToDictionary(e => e.Key, _ => random.Next(1, 3));
            var book = new CompendiumBook();
            var view = new CompendiumView(catalog);
            view.Milestones(tiers, floor);
            view.Tell(book, CompendiumRule.Strict);

            var levels = new[] { KnownLevel.HeardOf, KnownLevel.Seen, KnownLevel.Known };
            for (var step = 0; step < 60; step++)
            {
                for (var raise = random.Next(1, 4); raise > 0; raise--)
                {
                    book.Raise(all[random.Next(all.Count)].Key, levels[random.Next(levels.Length)]);
                }
                view.Tell(book, CompendiumRule.Strict);
                SameAsAfresh(catalog, view, book, CompendiumRule.Strict, tiers, floor);
            }
            Assert.Equal(1, view.WholeCounts);
        }

        [Fact]
        public void OnlyANewBookRulesFloorOrGroupsCountAfresh()
        {
            var all = Catalog(new Random(7));
            var catalog = new EntryCatalog(all);
            var book = new CompendiumBook();
            var view = new CompendiumView(catalog);

            view.Tell(book, CompendiumRule.Strict);
            book.Raise(all[0].Key, KnownLevel.Known);
            var version = view.Version;
            view.Tell(book, CompendiumRule.Strict);
            Assert.True(view.Version > version, "told on top, the view counts as told anew, for the list to show it");
            book.Raise(all[1].Key, KnownLevel.Seen);
            view.Tell(book, CompendiumRule.Strict);
            Assert.Equal(1, view.WholeCounts);
            catalog.Regroup();
            book.Raise(all[4].Key, KnownLevel.Seen);
            view.Tell(book, CompendiumRule.Strict);
            Assert.Equal(2, view.WholeCounts);

            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(3, view.WholeCounts);
            view.Milestones(new Dictionary<string, int> { [all[2].Key] = 1 }, 1);
            view.Tell(book, CompendiumRule.Loose);
            Assert.Equal(4, view.WholeCounts);
            // Another character's book, however much it holds, is counted afresh.
            var other = new CompendiumBook();
            for (var i = 5; i < 12; i++) other.Raise(all[i].Key, KnownLevel.Known);
            view.Tell(other, CompendiumRule.Loose);
            Assert.Equal(5, view.WholeCounts);
            SameAsAfresh(catalog, view, other, CompendiumRule.Loose, new Dictionary<string, int> { [all[2].Key] = 1 }, 1);
        }

        [Fact]
        public void MoreLearnedThanTheBookRemembersIsCountedAfresh()
        {
            var all = Catalog(new Random(8));
            var catalog = new EntryCatalog(all);
            var book = new CompendiumBook();
            var view = new CompendiumView(catalog);
            view.Tell(book, CompendiumRule.Strict);

            for (var i = 0; i <= CompendiumBook.RaisesKept; i++) book.Raise("unlisted" + i, KnownLevel.Seen);
            book.Raise(all[3].Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Strict);

            Assert.Equal(2, view.WholeCounts);
            SameAsAfresh(catalog, view, book, CompendiumRule.Strict, null, 0);
        }

        [Fact]
        public void WhatWasOnlyLearnedWaitsWhileWhatThePlayerChoseIsToldAtOnce()
        {
            // The panel open, what the character learns is told at most once a second; a rule or
            // floor picked, another book, or the catalog regrouped, at once.
            var all = Catalog(new Random(9));
            var catalog = new EntryCatalog(all);
            var book = new CompendiumBook();
            var view = new CompendiumView(catalog);
            view.Tell(book, CompendiumRule.Strict);

            Assert.False(view.OnlyLearnedSince(book, CompendiumRule.Strict));
            book.Raise(all[0].Key, KnownLevel.Seen);
            Assert.True(view.OnlyLearnedSince(book, CompendiumRule.Strict));
            Assert.False(view.OnlyLearnedSince(book, CompendiumRule.Loose));
            var other = new CompendiumBook();
            other.Raise(all[1].Key, KnownLevel.Seen);
            other.Raise(all[2].Key, KnownLevel.Seen);
            Assert.False(view.OnlyLearnedSince(other, CompendiumRule.Strict));
            catalog.Regroup();
            Assert.False(view.OnlyLearnedSince(book, CompendiumRule.Strict));
            view.Tell(book, CompendiumRule.Strict);
            book.Raise(all[3].Key, KnownLevel.Seen);
            view.Milestones(new Dictionary<string, int>(), 1);
            Assert.False(view.OnlyLearnedSince(book, CompendiumRule.Strict));
        }
    }
}
