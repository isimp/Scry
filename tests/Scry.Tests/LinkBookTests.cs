using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class LinkBookTests
    {
        private static List<Entry> Catalog()
        {
            var catalog = Game();
            catalog.Add(E("arrow_wood", Kind.Item, "Wood arrow"));
            catalog.Add(E("arrow_fire", Kind.Item, "Fire arrow"));
            catalog.Add(E("bolt_bone", Kind.Item, "Bone bolt"));
            catalog.Add(E("Crossbow", Kind.Item, "Crossbow"));
            catalog.Add(E("TrollArmorLegs", Kind.Item, "Troll leather pants"));
            catalog.Add(E("TrollHelmet", Kind.Item, "Troll leather helmet"));
            catalog.Add(E("SetEffect_TrollArmor", Kind.StatusEffect, "Sneaky"));
            catalog.Add(E("sfx_troll_step", Kind.Sound));
            catalog.Add(E("FW_ArmorTrollLeatherChest", Kind.Item, "Troll leather tunic"));
            catalog.Add(E("SP_ArmorTrollLeatherChest", Kind.Item, "Troll leather tunic"));
            return catalog;
        }

        private static Entry Find(List<Entry> catalog, string key) => catalog.Single(e => e.Key == key);

        private static List<string> Targets(Entry entry, string group) =>
            entry.Links.Where(l => l.Group == group).Select(l => l.Target).ToList();

        [Fact]
        public void ALinkShowsOnBothEndsUnderEachEndsOwnHeading()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.Add("Troll", "Footsteps", "sfx_troll_step", "Footstep of", "walk");
            book.Apply(catalog);

            Assert.Equal(new[] { "sfx_troll_step" }, Targets(Find(catalog, "Troll"), "Footsteps"));
            Assert.Equal(new[] { "Troll" }, Targets(Find(catalog, "sfx_troll_step"), "Footstep of"));
        }

        [Fact]
        public void WhatALinkIsAboutIsKeptWithIt()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.Add("Troll", "Footsteps", "sfx_troll_step", "Footstep of", "walk on stone");
            book.Add("Troll", "Footsteps", "sfx_troll_step", "Footstep of", "run on stone");
            book.Add("Troll", "Footsteps", "sfx_troll_step", "Footstep of", "walk on stone");
            book.Apply(catalog);

            var link = Assert.Single(Find(catalog, "Troll").Links);
            Assert.Equal(new[] { "walk on stone", "run on stone" }, link.Notes);
        }

        [Fact]
        public void ALinkToSomethingNotInTheCatalogIsLeftOut()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.Add("Troll", "Spawns", "NoSuchThing", "Spawned by");
            book.Apply(catalog);

            Assert.Empty(Find(catalog, "Troll").Links);
        }

        [Fact]
        public void NothingLinksToItself()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.Add("Troll", "Spawns", "Troll", "Spawned by");
            book.Apply(catalog);

            Assert.Empty(Find(catalog, "Troll").Links);
        }

        [Fact]
        public void AStatusEffectIsLinkedByItsOwnKey()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.Add("TrollArmorChest", "Status effects", "se:SetEffect_TrollArmor", "Given by", "set bonus");
            book.Apply(catalog);

            Assert.Equal(new[] { "se:SetEffect_TrollArmor" }, Targets(Find(catalog, "TrollArmorChest"), "Status effects"));
            Assert.Equal(new[] { "TrollArmorChest" }, Targets(Find(catalog, "se:SetEffect_TrollArmor"), "Given by"));
        }

        [Fact]
        public void HeadingsKeepTheOrderTheyFirstCameIn()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.Add("Troll", "Carries", "Bow", "Carried by");
            book.Add("Troll", "Footsteps", "sfx_troll_step", "Footstep of");
            book.Add("Troll", "Carries", "TrollArmorChest", "Carried by");
            book.Apply(catalog);

            Assert.Equal(new[] { "Carries", "Footsteps" }, Find(catalog, "Troll").LinkGroups().Select(g => g.Key));
        }

        [Fact]
        public void PiecesOfASetAreLinkedToEachOther()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.AddSets(new[]
            {
                ("TrollArmorChest", "troll", "Troll leather tunic", true), ("TrollArmorLegs", "troll", "Troll leather pants", true),
                ("TrollHelmet", "troll", "Troll leather helmet", true), ("Bow", "", "Crude bow", true),
            });
            book.Apply(catalog);

            Assert.Equal(new[] { "TrollArmorLegs", "TrollHelmet" }, Targets(Find(catalog, "TrollArmorChest"), LinkBook.SameSet));
            Assert.Empty(Find(catalog, "Bow").Links);
        }

        [Fact]
        public void CopiesOfAPieceUnderTheSameNameAreItsVariantsNotMorePiecesOfTheSet()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.AddSets(new[]
            {
                ("TrollArmorChest", "troll", "Troll leather tunic", true), ("TrollArmorLegs", "troll", "Troll leather pants", true),
                ("FW_ArmorTrollLeatherChest", "troll", "Troll leather tunic", false), ("SP_ArmorTrollLeatherChest", "troll", "Troll leather tunic", false),
            });
            book.Apply(catalog);

            Assert.Equal(new[] { "TrollArmorLegs" }, Targets(Find(catalog, "TrollArmorChest"), LinkBook.SameSet));
            Assert.Equal(new[] { "TrollArmorChest" }, Targets(Find(catalog, "TrollArmorLegs"), LinkBook.SameSet));
            Assert.Equal(new[] { "FW_ArmorTrollLeatherChest", "SP_ArmorTrollLeatherChest" }, Targets(Find(catalog, "TrollArmorChest"), LinkBook.Variants));
            Assert.Equal(new[] { "TrollArmorChest" }, Targets(Find(catalog, "FW_ArmorTrollLeatherChest"), LinkBook.VariantOf));
            Assert.Empty(Targets(Find(catalog, "FW_ArmorTrollLeatherChest"), LinkBook.SameSet));
        }

        [Fact]
        public void WithoutOneThatIsMadeTheShortestNameStandsForItsCopies()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.AddSets(new[]
            {
                ("SP_ArmorTrollLeatherChest", "troll", "Troll leather tunic", false), ("TrollArmorChest", "troll", "Troll leather tunic", false),
            });
            book.Apply(catalog);

            Assert.Equal(new[] { "SP_ArmorTrollLeatherChest" }, Targets(Find(catalog, "TrollArmorChest"), LinkBook.Variants));
        }

        [Fact]
        public void ALinkCanBeShownAtItsFarEndOnly()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.Add("TrollArmorChest", null, "se:SetEffect_TrollArmor", "Given by", "set bonus");
            book.Apply(catalog);

            Assert.Empty(Find(catalog, "TrollArmorChest").Links);
            Assert.Equal(new[] { "TrollArmorChest" }, Targets(Find(catalog, "se:SetEffect_TrollArmor"), "Given by"));
        }

        [Fact]
        public void WeaponsAndAmmoMeetByTheirAmmoType()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.AddAmmo(new[]
            {
                ("Bow", "$ammo_arrows", false), ("Crossbow", "$ammo_bolts", false),
                ("arrow_wood", "$ammo_arrows", true), ("arrow_fire", "$ammo_arrows", true), ("bolt_bone", "$ammo_bolts", true),
            });
            book.Apply(catalog);

            Assert.Equal(new[] { "arrow_wood", "arrow_fire" }, Targets(Find(catalog, "Bow"), LinkBook.Shoots));
            Assert.Equal(new[] { "Crossbow" }, Targets(Find(catalog, "bolt_bone"), LinkBook.ShotFrom));
        }

        [Fact]
        public void AmmoDoesNotShootAmmo()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.AddAmmo(new[] { ("arrow_wood", "$ammo_arrows", true), ("arrow_fire", "$ammo_arrows", true) });
            book.Apply(catalog);

            Assert.Empty(Find(catalog, "arrow_wood").Links);
        }
    }
}
