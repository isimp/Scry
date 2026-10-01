using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class ModLinksTests
    {
        // A mod declares the mods it needs (a hard dependency: it does not load without them),
        // those it works with when they are there (a soft one), and those it will not run with,
        // each by its id. Its package names the packages it was installed with. A mod's page
        // tells these both ways, as links to the mods loaded.

        private static ModFacts M(string name, string guid, string package = "", string[] hard = null, string[] soft = null, string[] incompatible = null, string[] packages = null) =>
            new ModFacts
            {
                Name = name, Guid = guid, Package = package,
                Hard = (hard ?? new string[0]).ToList(), Soft = (soft ?? new string[0]).ToList(),
                Incompatible = (incompatible ?? new string[0]).ToList(), PackageDeps = (packages ?? new string[0]).ToList(),
            };

        private static List<ModFacts> Pack() => new List<ModFacts>
        {
            M("Jotunn", "com.jotunn.jotunn", "ValheimModding-Jotunn"),
            M("PlanBuild", "marcopogo.PlanBuild", "MathiasDecrock-PlanBuild", hard: new[] { "com.jotunn.jotunn" }),
            M("OdinHorse", "Raelaziel.OdinHorse", "OdinPlus-OdinHorse", packages: new[] { "ValheimModding-Jotunn-2.30.2", "denikson-BepInExPack_Valheim-5.4.2333" }),
            M("Drop That!", "asharppen.valheim.drop_that", "ASharpPen-Drop_That", soft: new[] { "RockerKitten.BoneAppetit", "com.jotunn.jotunn" }),
            M("Spawn That!", "asharppen.valheim.spawn_that", "ASharpPen-Spawn_That", incompatible: new[] { "some.old.spawner", "asharppen.valheim.drop_that" }),
        };

        [Fact]
        public void AModNeedsWhatItDeclaresByIdOrByPackage()
        {
            var links = ModLinks.Of(Pack());

            Assert.Equal(new[] { "Jotunn" }, links["PlanBuild"].Needs);
            Assert.Equal(new[] { "Jotunn" }, links["OdinHorse"].Needs);
            Assert.Empty(links["Jotunn"].Needs);
        }

        [Fact]
        public void WhatAModIsNeededByIsToldOnItToo()
        {
            var links = ModLinks.Of(Pack());

            Assert.Equal(new[] { "OdinHorse", "PlanBuild" }, links["Jotunn"].NeededBy);
            Assert.Equal(new[] { "Drop That!" }, links["Jotunn"].WorkedWithBy);
        }

        [Fact]
        public void AModItWorksWithCountsOnlyWhenLoaded()
        {
            var links = ModLinks.Of(Pack());

            Assert.Equal(new[] { "Jotunn" }, links["Drop That!"].WorksWith);
            Assert.Empty(links["Drop That!"].Needs);
        }

        [Fact]
        public void AModNeededIsNotAlsoOneItWorksWith()
        {
            var mods = Pack();
            mods.Add(M("Both", "both", hard: new[] { "com.jotunn.jotunn" }, soft: new[] { "com.jotunn.jotunn" }, packages: new[] { "ValheimModding-Jotunn-2.30.2" }));

            var links = ModLinks.Of(mods);

            Assert.Equal(new[] { "Jotunn" }, links["Both"].Needs);
            Assert.Empty(links["Both"].WorksWith);
            Assert.DoesNotContain("Both", links["Jotunn"].WorkedWithBy);
        }

        [Fact]
        public void APackageNeededBringsEveryModItHolds()
        {
            var mods = new List<ModFacts>
            {
                M("Extra Slots", "shudnal.ExtraSlots", "shudnal-ExtraSlots"),
                M("Extra Slots Custom Slots", "shudnal.ExtraSlotsCustomSlots", "shudnal-ExtraSlots"),
                M("Quiver", "quiver", "someone-Quiver", packages: new[] { "shudnal-ExtraSlots-1.0.0" }),
            };

            var links = ModLinks.Of(mods);

            Assert.Equal(new[] { "Extra Slots", "Extra Slots Custom Slots" }, links["Quiver"].Needs);
        }

        [Fact]
        public void AModNeverNeedsItselfNorItsOwnPackage()
        {
            var mods = new List<ModFacts>
            {
                M("Extra Slots", "shudnal.ExtraSlots", "shudnal-ExtraSlots", hard: new[] { "shudnal.ExtraSlots" }, packages: new[] { "shudnal-ExtraSlots-1.0.0" }),
                M("Extra Slots Custom Slots", "shudnal.ExtraSlotsCustomSlots", "shudnal-ExtraSlots", hard: new[] { "shudnal.ExtraSlots" }, packages: new[] { "shudnal-ExtraSlots-1.0.0" }),
            };

            var links = ModLinks.Of(mods);

            Assert.Empty(links["Extra Slots"].Needs);
            Assert.Equal(new[] { "Extra Slots" }, links["Extra Slots Custom Slots"].Needs);
        }

        [Fact]
        public void PackagesAndIdsOfNoModLoadedAreLeftOut()
        {
            var links = ModLinks.Of(Pack());

            Assert.DoesNotContain(links["OdinHorse"].Needs, n => n.Contains("BepInEx"));
            Assert.DoesNotContain(links["Drop That!"].WorksWith, n => n.Contains("Bone"));
        }

        [Fact]
        public void AModItWillNotRunWithIsToldByIdUnlessLoaded()
        {
            var links = ModLinks.Of(Pack());

            Assert.Equal(new[] { "Drop That!", "some.old.spawner" }, links["Spawn That!"].WillNotRunWith);
        }

        [Fact]
        public void PackagesMatchWhateverTheirCase()
        {
            var mods = new List<ModFacts>
            {
                M("Jotunn", "com.jotunn.jotunn", "ValheimModding-Jotunn"),
                M("Lower", "lower", packages: new[] { "valheimmodding-jotunn-2.30.2" }),
            };

            Assert.Equal(new[] { "Jotunn" }, ModLinks.Of(mods)["Lower"].Needs);
        }
    }
}
