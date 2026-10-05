using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class SearchFightTests
    {
        // weak:, resists: and immune: find what takes more, less or none of a damage type, as
        // its resistance grid tells it; damage: what deals a type; skill: what trains a skill.

        private static List<ResistCell> Grid(params (string Type, Degree Degree)[] set)
        {
            var degrees = set.ToDictionary(s => s.Type, s => s.Degree);
            return ResistWords.Types.Select(t => ResistWords.Cell(t, degrees.TryGetValue(t, out var degree) ? degree : Degree.Normal)).ToList();
        }

        [Fact]
        public void AGridIsReadAsItShowsWeakResistingAndTakingNone()
        {
            var grid = Grid(("Fire", Degree.VeryWeak), ("Frost", Degree.SlightlyWeak), ("Pierce", Degree.Resistant), ("Poison", Degree.Immune), ("Spirit", Degree.Ignore), ("Blunt", Degree.SlightlyResistant));
            Assert.Equal(new[] { "fire", "frost" }, SearchFight.Taking(grid, Tone.Weak));
            Assert.Equal(new[] { "blunt", "pierce" }, SearchFight.Taking(grid, Tone.Resists));
            Assert.Equal(new[] { "poison", "spirit" }, SearchFight.Taking(grid, Tone.Immune));
        }

        [Fact]
        public void ACreaturesToolDamageItTakesNoneOfIsNoImmunity()
        {
            // As its grid draws it quietly: tools are for trees and rocks.
            var grid = ResistWords.ForCreature(Grid(("Chop", Degree.Immune), ("Pickaxe", Degree.Ignore), ("Poison", Degree.Immune)));
            Assert.Equal(new[] { "poison" }, SearchFight.Taking(grid, Tone.Immune));
        }

        [Fact]
        public void WhatAHitDealsIsEveryTypeOfItsDamage()
        {
            var figures = new[] { ("true", 0f), ("blunt", 12f), ("slash", 0f), ("fire", 5f), ("spirit", 0.5f), ("fire", 2f) };
            Assert.Equal(new[] { "blunt", "fire", "spirit" }, SearchFight.Dealt(figures));
            Assert.Equal(new[] { "true" }, SearchFight.Dealt(new[] { ("true", 30f) }));
            Assert.Empty(SearchFight.Dealt(new (string, float)[0]));
        }

        [Fact]
        public void ASkillIsItsNameInSmallLetters()
        {
            Assert.Equal(new[] { "elemental magic" }, SearchFight.Skill("Elemental magic"));
            Assert.Empty(SearchFight.Skill(null));
            Assert.Empty(SearchFight.Skill(SkillWords.ModSkill));
        }

        private static List<Entry> Catalog()
        {
            var greydwarf = E("Greydwarf", Kind.Creature, "Greydwarf");
            greydwarf.SetTermWords("weak", new[] { "fire" });
            greydwarf.SetTermWords("damage", new[] { "slash" });
            var draugr = E("Draugr", Kind.Creature, "Draugr");
            draugr.SetTermWords("resists", new[] { "pierce" });
            draugr.SetTermWords("immune", new[] { "poison" });
            draugr.SetTermWords("damage", new[] { "slash", "blunt" });
            var sword = E("SwordSilver", Kind.Item, "Silver sword");
            sword.SetTermWords("damage", new[] { "slash", "spirit" });
            sword.SetTermWords("skill", new[] { "swords" });
            var staff = E("StaffFireball", Kind.Item, "Staff of embers");
            staff.SetTermWords("damage", new[] { "fire" });
            staff.SetTermWords("skill", new[] { "elemental magic" });
            var wolfArmour = E("ArmorWolfChest", Kind.Item, "Wolf armor chest");
            wolfArmour.SetTermWords("resists", new[] { "frost" });
            return new List<Entry> { greydwarf, draugr, sword, staff, wolfArmour };
        }

        private static List<string> Find(string text) =>
            Search.Run(Catalog(), new Query { Text = text }, new List<string>()).Select(e => e.Name).ToList();

        [Fact]
        public void TheFightTermsFindByTheTypesTheyName()
        {
            Assert.Equal(new[] { "Greydwarf" }, Find("weak:fire"));
            Assert.Equal(new[] { "Draugr", "ArmorWolfChest" }, Find("resists:pierce,frost"));
            Assert.Equal(new[] { "Draugr" }, Find("immune:poi"));
            Assert.Equal(new[] { "SwordSilver" }, Find("damage:spirit"));
            Assert.Equal(new[] { "Draugr", "Greydwarf", "SwordSilver" }, Find("damage:slash"));
            Assert.Equal(new[] { "SwordSilver" }, Find("skill:sw"));
            // A skill of two words is found by the start of either.
            Assert.Equal(new[] { "StaffFireball" }, Find("skill:magic"));
            Assert.Equal(new[] { "StaffFireball" }, Find("skill:elementalmagic"));
            Assert.Empty(Find("skill:agic"));
        }

        [Fact]
        public void TheFightTermsAreSuggestedWithTheTypesTheCatalogHas()
        {
            var index = new TermIndex(Catalog());
            Assert.Contains(SearchHelp.Suggest("we", index), s => s.Insert == "weak:" && s.Note == "a damage type it takes more of");
            Assert.Contains(SearchHelp.Suggest("dam", index), s => s.Insert == "damage:" && s.Note == "a damage type it deals");
            var types = SearchHelp.Suggest("damage:", index);
            Assert.Contains(types, s => s.Insert == "damage:slash" && s.Label == "Slash" && s.Count == 3);
            Assert.Equal("skill:elementalmagic", SearchHelp.Suggest("skill:el", index).First().Insert);
        }
    }
}
