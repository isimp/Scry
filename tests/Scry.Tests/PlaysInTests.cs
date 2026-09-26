using System.Linq;
using Xunit;

namespace Scry.Tests
{
    public class PlaysInTests
    {
        private static EffectUse Use(string owner, string label, object list, params string[] members) =>
            new EffectUse(owner, owner, label, members, list);

        [Fact]
        public void ASoundShowsEachListItPlaysInWithWhoPlaysItAndWhatPlaysAlong()
        {
            var rows = PlaysIn.Rows(new[]
            {
                Use("Troll", "Death", "a", "sfx_troll_death", "vfx_troll_death"),
                Use("Troll", "Hit", "b", "sfx_troll_hit", "vfx_hit"),
            });

            Assert.Equal(2, rows.Count);
            var death = rows.Single(r => r.Label == "Death");
            Assert.Equal(new[] { "Troll" }, death.Owners.Select(o => o.Shown));
            Assert.Equal(new[] { "sfx_troll_death", "vfx_troll_death" }, death.Members);
        }

        [Fact]
        public void TheSameListOnManyPrefabsIsOneRowNamingThemAll()
        {
            var rows = PlaysIn.Rows(new[]
            {
                Use("wood_wall", "Destroyed", "a", "sfx_wood_destroyed", "vfx_destroyed"),
                Use("wood_door", "Destroyed", "b", "vfx_destroyed", "sfx_wood_destroyed"),
                Use("wood_floor", "Destroyed", "c", "sfx_wood_destroyed", "vfx_destroyed"),
            });

            var row = Assert.Single(rows);
            Assert.Equal(new[] { "wood_door", "wood_floor", "wood_wall" }, row.Owners.Select(o => o.Shown));
        }

        [Fact]
        public void ListsWithOtherMembersOrPurposesStayApart()
        {
            var rows = PlaysIn.Rows(new[]
            {
                Use("Troll", "Death", "a", "sfx_death"),
                Use("Troll", "Hit", "b", "sfx_death"),
                Use("Boar", "Death", "c", "sfx_death", "vfx_boar"),
            });

            Assert.Equal(3, rows.Count);
        }

        [Fact]
        public void AnOwnerIsNamedOnceEvenWhenItHasTheListTwice()
        {
            var rows = PlaysIn.Rows(new[]
            {
                Use("Troll", "Hit", "a", "sfx_hit"),
                Use("Troll", "Hit", "b", "sfx_hit"),
            });

            Assert.Single(Assert.Single(rows).Owners);
        }

        [Fact]
        public void RowsGoInTheOrderOfWhoPlaysThem()
        {
            var rows = PlaysIn.Rows(new[]
            {
                Use("Wolf", "Death", "a", "x"),
                Use("Boar", "Death", "b", "y"),
                Use("Troll", "Death", "c", "z"),
            });

            Assert.Equal(new[] { "Boar", "Troll", "Wolf" }, rows.Select(r => r.Owners[0].Shown));
        }

        [Fact]
        public void ARowPlaysTheListOfItsFirstOwner()
        {
            var rows = PlaysIn.Rows(new[]
            {
                Use("wood_wall", "Destroyed", "wall list", "sfx"),
                Use("wood_door", "Destroyed", "door list", "sfx"),
            });

            Assert.Equal("door list", Assert.Single(rows).List);
        }

        [Fact]
        public void ThePlayerOfAListCanBeSomethingNothingJumpsTo()
        {
            var use = new EffectUse("The interface", null, "Click", new[] { "sfx_gui_click" }, "a");

            var row = Assert.Single(PlaysIn.Rows(new[] { use }));

            Assert.Null(row.Owners[0].Key);
        }
    }
}
