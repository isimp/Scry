using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlacePlanTests
    {
        // On entering a world, what was kept of its places is shown at once, and only what may
        // have changed is read again in the background: a place whose stamp (the build of Scry
        // that read it and what it was read from) is the same now holds the same; one whose
        // stamp changed, or whose names the catalog holds otherwise than when it was read, is
        // shown as it was until it is read again; one never read is read.

        private static readonly HashSet<string> Catalog = new HashSet<string> { "Skeleton", "TreasureChest_forestcrypt" };

        private static PlaceRecord Kept(string prefab, string stamp, bool room = false, params string[] names) =>
            new PlaceRecord { Prefab = prefab, Room = room, Stamp = stamp, Names = names.ToList(), Known = names.Count(Catalog.Contains) };

        private static KeptPlaces Of(params PlaceRecord[] records)
        {
            var kept = new KeptPlaces();
            foreach (var record in records) kept.Put(record);
            return kept;
        }

        private static PlacePlan Plan(KeptPlaces kept, params (string Prefab, bool Room, string Stamp)[] world) =>
            PlacePlan.Of(kept, world, Catalog.Contains);

        [Fact]
        public void APlaceReadBeforeIsKnownAtOnceAndNotReadAgain()
        {
            var plan = Plan(Of(Kept("Crypt2", "b1 crypt")), ("Crypt2", false, "b1 crypt"));
            Assert.Equal("Crypt2", Assert.Single(plan.Shown).Prefab);
            Assert.Empty(plan.ToRead);
            Assert.Equal((1, 0, 0), (plan.Fresh, plan.Stale, plan.Missing));
        }

        [Fact]
        public void APlaceWhoseStampChangedIsShownAsItWasUntilReadAgain()
        {
            // A game update that rewrote its bundle, or another build of Scry.
            var plan = Plan(Of(Kept("Crypt2", "b1 crypt")), ("Crypt2", false, "b2 crypt"));
            Assert.Equal("Crypt2", Assert.Single(plan.Shown).Prefab);
            Assert.Equal(new[] { 0 }, plan.ToRead);
            Assert.Equal((0, 1, 0), (plan.Fresh, plan.Stale, plan.Missing));
        }

        [Fact]
        public void APlaceNeverReadIsReadWithNothingShownForItMeanwhile()
        {
            var plan = Plan(Of(Kept("Crypt2", "b1 crypt")), ("Crypt2", false, "b1 crypt"), ("Mistlands_Giant1", false, "b1 giant"));
            Assert.Equal(new[] { "Crypt2" }, plan.Shown.Select(r => r.Prefab));
            Assert.Equal(new[] { 1 }, plan.ToRead);
            Assert.Equal((1, 0, 1), (plan.Fresh, plan.Stale, plan.Missing));
        }

        [Fact]
        public void APlaceWhoseStampIsNotKnownIsReadAgainEveryTime()
        {
            var plan = Plan(Of(Kept("Crypt2", "")), ("Crypt2", false, ""));
            Assert.Single(plan.Shown);
            Assert.Equal(new[] { 0 }, plan.ToRead);
        }

        [Fact]
        public void APlaceNamingSomethingTheGameNoLongerHasIsReadAgain()
        {
            // A prefab renamed or taken away by an update of the game or a mod.
            var gone = Kept("Crypt2", "b1 crypt", false, "Skeleton", "TreasureChest_forestcrypt");
            gone.Names[1] = "TreasureChest_old";
            var plan = Plan(Of(gone), ("Crypt2", false, "b1 crypt"));
            Assert.Single(plan.Shown);
            Assert.Equal(new[] { 0 }, plan.ToRead);

            var still = Plan(Of(Kept("Crypt2", "b1 crypt", false, "Skeleton", "TreasureChest_forestcrypt")), ("Crypt2", false, "b1 crypt"));
            Assert.Empty(still.ToRead);
        }

        [Fact]
        public void WhatAPlaceNamesThatWasNeverInTheCatalogSendsNothingToBeReadAgain()
        {
            // Effects nothing registers, say: they were not there then and are not now.
            var plan = Plan(Of(Kept("Crypt2", "b1 crypt", false, "Skeleton", "vfx_unregistered")), ("Crypt2", false, "b1 crypt"));
            Assert.Empty(plan.ToRead);
        }

        [Fact]
        public void APlaceNamingSomethingTheCatalogHasGainedIsReadAgain()
        {
            var gained = Kept("Crypt2", "b1 crypt", false, "Skeleton", "TreasureChest_forestcrypt");
            gained.Known = 1;
            Assert.Equal(new[] { 0 }, Plan(Of(gained), ("Crypt2", false, "b1 crypt")).ToRead);
        }

        [Fact]
        public void PlacesShowInTheOrderTheWorldListsThemAndOnlyThoseItHas()
        {
            var kept = Of(Kept("crypt2_Room_skull", "s", true), Kept("Elsewhere", "s"), Kept("Crypt2", "s"));
            var plan = Plan(kept, ("Crypt2", false, "s"), ("crypt2_Room_skull", true, "s"));
            Assert.Equal(new[] { "Crypt2", "crypt2_Room_skull" }, plan.Shown.Select(r => r.Prefab));
            Assert.Equal(3, kept.Records.Count);
        }

        [Fact]
        public void ARoomIsNotTakenForALocationOfTheSameName()
        {
            var plan = Plan(Of(Kept("same", "s", room: true)), ("same", false, "s"));
            Assert.Empty(plan.Shown);
            Assert.Equal(new[] { 0 }, plan.ToRead);
        }

        [Fact]
        public void NothingKeptReadsEveryPlace()
        {
            var plan = Plan(new KeptPlaces(), ("Crypt2", false, "s"), ("crypt2_Room_skull", true, "s"));
            Assert.Empty(plan.Shown);
            Assert.Equal(new[] { 0, 1 }, plan.ToRead);
            Assert.Equal((0, 0, 2), (plan.Fresh, plan.Stale, plan.Missing));
        }
    }
}
