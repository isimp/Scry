using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class AttackTagTests
    {
        // A creature's attack is an item the game names for its weapon alone ("Club") or in its
        // own shorthand ("bjorn bite"), so eighteen clubs read alike in the list, told apart only
        // as "3 of 18". Each is tagged with the creature carrying it, and told apart by it.

        private static Entry Creature(string name, string shown) => new Entry { Name = name, DisplayName = shown, Kind = Kind.Creature };

        private static Entry Carried(string name, string shown, Group group, params Entry[] carriers)
        {
            var item = new Entry { Name = name, DisplayName = shown, Kind = Kind.Item, Group = group.Name, GroupOrder = group.Order };
            foreach (var carrier in carriers) item.Links.Add(new Link { Group = LinkWords.CarriedBy, Target = carrier.Key });
            return item;
        }

        private static Entry Attack(string name, string shown, params Entry[] carriers) => Carried(name, shown, Groups.CreatureAttacks, carriers);

        private static void Told(List<Entry> all)
        {
            AttackTags.Tell(all);
            Search.OrderNames(all);
        }

        [Fact]
        public void ACreaturesAttackIsToldApartByTheCreatureCarryingIt()
        {
            var troll = Creature("Troll", "Troll");
            var brute = Creature("GoblinBrute", "Fuling berserker");
            var trolls = Attack("troll_club", "Club", troll);
            var brutes = Attack("goblinbrute_club", "Club", brute);
            Told(new List<Entry> { troll, brute, trolls, brutes });

            Assert.Equal("Troll", trolls.Tag);
            Assert.Equal("Troll", trolls.Alike);
            Assert.Equal("Fuling berserker", brutes.Alike);
        }

        [Fact]
        public void WhatOnlyCreaturesWearIsTaggedAsTheirAttacksAre()
        {
            var charred = Creature("Charred_Melee", "Charred Warrior");
            var plate = Carried("Charred_Breastplate", "Iron plate armor", Groups.CarriedByCreatures, charred);
            Told(new List<Entry> { charred, plate });
            Assert.Equal("Charred Warrior", plate.Tag);
            // Alone of its name, nothing tells it apart from others; its tag stands beside it in the list.
            Assert.Null(plate.Alike);
        }

        [Fact]
        public void OneCreaturesAttacksAlikeAreToldApartAfterIt()
        {
            var bear = Creature("Bjorn", "Bear");
            var bite = Attack("bjorn_bite", "bjorn bite", bear);
            var claws = Attack("bjorn_claws", "bjorn bite", bear);
            var left = Attack("bjorn_swipe_l", "bjorn bite", bear);
            var right = Attack("bjorn_swipe_r", "bjorn bite", bear);
            Told(new List<Entry> { bear, bite, claws });
            Assert.Equal("Bear · bite", bite.Alike);
            Assert.Equal("Bear · claws", claws.Alike);

            // Where their prefabs' words tell nothing, which of them each is.
            Told(new List<Entry> { bear, left, right });
            Assert.Equal("Bear · 1 of 2", left.Alike);
            Assert.Equal("Bear · 2 of 2", right.Alike);
        }

        [Fact]
        public void CarriersAreNamedAsCountsGrow()
        {
            var troll = Creature("Troll", "Troll");
            var summoned = Creature("Troll_Summoned", "Troll");
            var brute = Creature("GoblinBrute", "Fuling berserker");
            var golem = Creature("StoneGolem", "Stone Golem");

            // Variants going by one name are one carrier.
            var one = Attack("troll_log", "Log", troll, summoned);
            var two = Attack("big_log", "Log", troll, brute);
            var three = Attack("any_log", "Log", troll, brute, golem);
            Told(new List<Entry> { troll, summoned, brute, golem, one, two, three });

            Assert.Equal("Troll", one.Tag);
            Assert.Equal("Fuling berserker and Troll", two.Tag);
            Assert.Equal("3 creatures", three.Tag);
        }

        [Fact]
        public void APlayersWeaponACreatureAlsoCarriesKeepsNoTag()
        {
            var greydwarf = Creature("Greydwarf_Elite", "Greydwarf brute");
            var club = Carried("Club", "Club", Groups.Item("OneHandedWeapon", "Clubs"), greydwarf);
            var nobodys = Attack("Axe1h_JotunWarrior", "Club");
            Told(new List<Entry> { greydwarf, club, nobodys });

            Assert.Null(club.Tag);
            // An attack no creature carries has none to be tagged with.
            Assert.Null(nobodys.Tag);
        }
    }
}
