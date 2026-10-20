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

        private static LinkBook Busy()
        {
            var book = new LinkBook();
            book.Add("Troll", "Footsteps", "sfx_troll_step", "Footstep of", "walk");
            book.Add("Troll", "Footsteps", "sfx_troll_step", "Footstep of", "run");
            book.Add("Troll", "Carries", "Bow", "Carried by");
            book.AddAmmo(new[] { ("Bow", "$ammo_arrows", false), ("arrow_wood", "$ammo_arrows", true), ("arrow_fire", "$ammo_arrows", true) });
            book.AddSets(new[]
            {
                ("TrollArmorChest", "troll", "Troll leather tunic", true),
                ("TrollArmorLegs", "troll", "Troll leather pants", true),
                ("FW_ArmorTrollLeatherChest", "troll", "Troll leather tunic", false),
            });
            book.Add("TrollArmorChest", null, "se:SetEffect_TrollArmor", "Given by", "set");
            return book;
        }

        private static List<string> Everything(List<Entry> catalog) =>
            catalog.SelectMany(e => e.Links.Select(l => $"{e.Key} {l.Group} {l.Target} [{string.Join(",", l.Notes)}]")).ToList();

        [Fact]
        public void AStationsUpgradesAreThePiecesNamingAnyStationOfItsNameAsTheGameCountsThem()
        {
            // The game counts a piece as an upgrade of every station going by the name its own
            // station goes by (StationExtension.FindExtensions compares the names), whichever prefab it names.
            var catalog = Game();
            foreach (var key in new[] { "piece_workbench", "piece_workbench_ext1", "mod_workbench", "mod_toolrack", "forge", "forge_ext1", "gone_station", "lost_ext", "unnamed", "other_unnamed", "odd_ext", "null_ext" })
                catalog.Add(E(key, Kind.Piece));
            var book = new LinkBook();
            book.AddUpgrades(
                new[]
                {
                    ("piece_workbench_ext1", "piece_workbench", "$piece_workbench"),
                    // A mod's piece naming its own copy of the workbench upgrades the game's too.
                    ("mod_toolrack", "mod_workbench", "$piece_workbench"),
                    ("forge_ext1", "forge", "$piece_forge"),
                    // One naming a station no prefab of the catalog goes by is linked to the station it names, as before.
                    ("lost_ext", "gone_station", "$piece_gone"),
                    // So is one whose station has no name: no name is matched.
                    ("odd_ext", "unnamed", ""),
                    ("null_ext", "unnamed", null),
                    // Named twice, shown once, as every link is.
                    ("piece_workbench_ext1", "piece_workbench", "$piece_workbench"),
                },
                new[] { ("piece_workbench", "$piece_workbench"), ("forge", "$piece_forge"), ("mod_workbench", "$piece_workbench"), ("unnamed", ""), ("other_unnamed", ""), ("null_station", null) });
            book.Apply(catalog);
            Assert.Equal(new[] { "piece_workbench_ext1", "mod_toolrack" }, Targets(Find(catalog, "piece_workbench"), LinkWords.Upgrades));
            Assert.Equal(new[] { "piece_workbench_ext1", "mod_toolrack" }, Targets(Find(catalog, "mod_workbench"), LinkWords.Upgrades));
            Assert.Equal(new[] { "forge_ext1" }, Targets(Find(catalog, "forge"), LinkWords.Upgrades));
            Assert.Equal(new[] { "piece_workbench", "mod_workbench" }, Targets(Find(catalog, "mod_toolrack"), LinkWords.UpgradeOf));
            Assert.Equal(new[] { "lost_ext" }, Targets(Find(catalog, "gone_station"), LinkWords.Upgrades));
            Assert.Equal(new[] { "odd_ext", "null_ext" }, Targets(Find(catalog, "unnamed"), LinkWords.Upgrades));
            Assert.Empty(Targets(Find(catalog, "other_unnamed"), LinkWords.Upgrades));
        }

        [Fact]
        public void LinksPutOnAFewAtATimeComeOutAsWhenPutOnAtOnce()
        {
            // Reading the catalog is spread over frames; linking its entries is too.
            var atOnce = Catalog();
            Busy().Apply(atOnce);

            var inSteps = Catalog();
            var steps = Busy().ApplyInSteps(inSteps, 2).Count();

            Assert.True(steps > 1, "the links went on in one step");
            Assert.Equal(Everything(atOnce), Everything(inSteps));
            Assert.NotEmpty(Everything(inSteps));
        }

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
        public void CopiesOfAPieceAreNotListedBesideItElsewhere()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.AddSets(new[]
            {
                ("TrollArmorChest", "troll", "Troll leather tunic", true),
                ("FW_ArmorTrollLeatherChest", "troll", "Troll leather tunic", false),
                ("SP_ArmorTrollLeatherChest", "troll", "Troll leather tunic", false),
            });
            book.Add("TrollArmorChest", null, "se:SetEffect_TrollArmor", "Given by", "set");
            book.Add("FW_ArmorTrollLeatherChest", null, "se:SetEffect_TrollArmor", "Given by", "set");
            book.Add("SP_ArmorTrollLeatherChest", null, "se:SetEffect_TrollArmor", "Given by", "set");
            book.Apply(catalog);

            Assert.Equal(new[] { "TrollArmorChest" }, Targets(Find(catalog, "se:SetEffect_TrollArmor"), "Given by"));
        }

        [Fact]
        public void ACopyWhosePieceIsNotThereStillShows()
        {
            var catalog = Catalog();
            var book = new LinkBook();

            book.AddSets(new[]
            {
                ("TrollArmorChest", "troll", "Troll leather tunic", true),
                ("FW_ArmorTrollLeatherChest", "troll", "Troll leather tunic", false),
            });
            book.Add("FW_ArmorTrollLeatherChest", "Carried by", "Troll", "Carries");
            book.Apply(catalog);

            Assert.Equal(new[] { "FW_ArmorTrollLeatherChest" }, Targets(Find(catalog, "Troll"), "Carries"));
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
