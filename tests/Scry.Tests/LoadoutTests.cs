using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class LoadoutTests
    {
        private static Loadout Draugr() => new Loadout(
            weapons: new[] { "AxeDraugr", "SwordDraugr", "BowDraugr" },
            shields: new[] { "ShieldWood", "ShieldBanded" },
            armours: new[] { "ArmorDraugrChest" },
            extras: new Loadout.Extra[0]);

        [Fact]
        public void EachWeaponACreatureCanRollIsOffered()
        {
            var loadout = Draugr();

            Assert.Equal(new[] { "AxeDraugr", "SwordDraugr", "BowDraugr" }, loadout.Options(Loadout.Row.Weapon));
            Assert.Equal(new[] { "ShieldWood", "ShieldBanded" }, loadout.Options(Loadout.Row.Shield));
        }

        [Fact]
        public void ItStartsWearingTheFirstOfEach()
        {
            var loadout = Draugr();

            Assert.Equal(new[] { "AxeDraugr", "ShieldWood", "ArmorDraugrChest" }, loadout.Worn());
        }

        [Fact]
        public void ChoosingAWeaponWearsItInsteadOfTheFirst()
        {
            var loadout = Draugr();

            loadout.Choose(Loadout.Row.Weapon, 2);

            Assert.Equal(2, loadout.Chosen(Loadout.Row.Weapon));
            Assert.Contains("BowDraugr", loadout.Worn());
            Assert.DoesNotContain("AxeDraugr", loadout.Worn());
        }

        [Fact]
        public void ChoosingSomethingNotOfferedChangesNothing()
        {
            var loadout = Draugr();
            loadout.Choose(Loadout.Row.Weapon, 1);

            loadout.Choose(Loadout.Row.Weapon, 3);
            loadout.Choose(Loadout.Row.Weapon, -1);

            Assert.Equal(1, loadout.Chosen(Loadout.Row.Weapon));
        }

        [Fact]
        public void AnItemListedTwiceToMakeItCommonerIsOfferedOnce()
        {
            var loadout = new Loadout(new[] { "Club", "Club", "Sword" }, new string[0], new string[0], new Loadout.Extra[0]);

            Assert.Equal(new[] { "Club", "Sword" }, loadout.Options(Loadout.Row.Weapon));
        }

        [Fact]
        public void AnEmptyEntryIsOfferedAsNothingAndWearsNothing()
        {
            var loadout = new Loadout(new[] { "Club" }, new[] { null, "ShieldWood", "" }, new string[0], new Loadout.Extra[0]);

            Assert.Equal(new[] { "", "ShieldWood" }, loadout.Options(Loadout.Row.Shield));
            Assert.Equal(new[] { "Club" }, loadout.Worn());
        }

        [Fact]
        public void ARowWithOneItemOffersNoChoiceButIsStillWorn()
        {
            var loadout = Draugr();

            Assert.False(loadout.Offered(Loadout.Row.Armour));
            Assert.True(loadout.Offered(Loadout.Row.Weapon));
            Assert.Contains("ArmorDraugrChest", loadout.Worn());
        }

        [Fact]
        public void ACreatureWithoutRandomGearOffersNothing()
        {
            var loadout = new Loadout(new string[0], new string[0], new string[0], new Loadout.Extra[0]);

            Assert.False(loadout.Offered(Loadout.Row.Weapon));
            Assert.False(loadout.HasChoices);
            Assert.Empty(loadout.Worn());
        }

        private static Loadout Archer() => new Loadout(
            weapons: new[] { "AxeDraugr", "BowDraugr" },
            shields: new[] { "ShieldWood" },
            armours: new string[0],
            extras: new Loadout.Extra[0],
            bothHands: new[] { "BowDraugr" });

        [Fact]
        public void AWeaponHeldInBothHandsLeavesNoHandForAShield()
        {
            var loadout = Archer();

            loadout.Choose(Loadout.Row.Weapon, 1);

            Assert.False(loadout.Held(Loadout.Row.Shield));
            Assert.DoesNotContain("ShieldWood", loadout.Worn());
            Assert.Contains("BowDraugr", loadout.Worn());
        }

        [Fact]
        public void AOneHandedWeaponKeepsTheShield()
        {
            var loadout = Archer();

            Assert.True(loadout.Held(Loadout.Row.Shield));
            Assert.Contains("ShieldWood", loadout.Worn());
        }

        [Fact]
        public void TheShieldChosenComesBackWhenAHandIsFreeAgain()
        {
            var loadout = Archer();
            loadout.Choose(Loadout.Row.Weapon, 1);

            loadout.Choose(Loadout.Row.Weapon, 0);

            Assert.Contains("ShieldWood", loadout.Worn());
        }

        [Fact]
        public void ExtrasStartWithTheFirstOfEachKindOn()
        {
            var loadout = new Loadout(new string[0], new string[0], new string[0], new[]
            {
                new Loadout.Extra("HelmetA", 6), new Loadout.Extra("HelmetB", 6), new Loadout.Extra("Cape", 17),
            });

            Assert.True(loadout.HasChoices);
            Assert.Equal(new[] { "HelmetA", "Cape" }, loadout.Worn());
        }

        [Fact]
        public void AnExtraOfTheSameKindReplacesTheOneWorn()
        {
            var loadout = new Loadout(new string[0], new string[0], new string[0], new[]
            {
                new Loadout.Extra("HelmetA", 6), new Loadout.Extra("HelmetB", 6), new Loadout.Extra("Cape", 17),
            });

            loadout.ToggleExtra(1);

            Assert.False(loadout.ExtraOn(0));
            Assert.True(loadout.ExtraOn(1));
            Assert.Equal(new[] { "HelmetB", "Cape" }, loadout.Worn());
        }

        [Fact]
        public void AnExtraCanBeTakenOff()
        {
            var loadout = new Loadout(new string[0], new string[0], new string[0], new[] { new Loadout.Extra("Cape", 17) });

            loadout.ToggleExtra(0);

            Assert.Empty(loadout.Worn());
            loadout.ToggleExtra(0);
            Assert.Equal(new[] { "Cape" }, loadout.Worn().ToArray());
        }
    }
}
