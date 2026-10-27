using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Scry.Tests.TestCatalog;

namespace Scry.Tests
{
    public class FamiliesTests
    {
        // Several prefabs that are one thing to a player make a family, by the game's own naming:
        // an item and its copies for creatures to wear (the same name, the original made by a
        // recipe, each copy none and named after it, FW_ and SP_), a creature and its copies
        // placed asleep or without a chest (the same name and attacks, named after it) and its kinds
        // (the same name, other attacks), and the kinds of a place (the same name but its number).
        // The list shows one row a family, the page a switch among the members the character met.

        private static Entry Item(string name, string token, bool crafted, string tag = null)
        {
            var e = E(name, Kind.Item, "Blackmetal tower shield");
            e.Tokens = new[] { token };
            if (crafted) e.Stations = new[] { new StationUse("forge", "Forge", 2) };
            if (tag != null)
            {
                e.Tag = tag;
                e.TagNamesCarrier = true;
            }
            return e;
        }

        private static Entry Creature(string name, string token, params string[] attacks)
        {
            var e = E(name, Kind.Creature, "Draugr");
            e.Tokens = new[] { token };
            e.Attacks = attacks.Select(a => (a, new[] { "slash" })).ToArray();
            return e;
        }

        private static Entry Place(string name, string shown) => E(name, Kind.Location, shown);

        [Fact]
        public void AnItemsCopiesForCreaturesFoldIntoIt()
        {
            var shield = Item("ShieldBlackmetalTower", "$item_shield_blackmetal_tower", crafted: true);
            var fallen = Item("FW_ShieldBlackmetalTower", "$item_shield_blackmetal_tower", crafted: false, tag: "Fallen Warrior");
            var shadow = Item("SP_ShieldBlackmetalTower", "$item_shield_blackmetal_tower", crafted: false);
            var families = Families.Of(new[] { fallen, shield, shadow });
            var family = Assert.Single(families);
            Assert.Same(shield, family.Head);
            Assert.Same(shield, family.CopyOf(fallen));
            Assert.Same(shield, family.CopyOf(shadow));
            Assert.Null(family.CopyOf(shield));
            Assert.Same(family, fallen.Family);
            // Each copy told by who carries it, else by its prefab's own words.
            Assert.Equal("Fallen Warrior", family.Label(fallen));
            Assert.Equal("SP", family.Label(shadow));
            // Carried by a creature, by its name.
            var bearer = E("ShadowPerson", Kind.Creature, "Shadow Person");
            shadow.Links.Add(new Link { Group = LinkWords.CarriedBy, Target = bearer.Key, Back = true });
            Assert.Equal("Shadow Person", Assert.Single(Families.Of(new[] { shield, fallen, shadow, bearer })).Label(shadow));
        }

        [Fact]
        public void ThingsThatOnlyShareANameStayApart()
        {
            // Neither made by a recipe, or both, or one not named after the other: no copies.
            var ecto = Item("Ectoplasm", "$item_ectoplasm", crafted: false);
            var voidplasm = Item("Voidplasm", "$item_ectoplasm", crafted: false);
            var feast = Item("FeastPlains", "$item_feastplains", crafted: true);
            var material = Item("FeastPlains_Material", "$item_feastplains", crafted: true);
            var arbalest = Item("CrossbowArbalest", "$item_crossbow_arbalest", crafted: true);
            var dverger = Item("DvergerArbalest", "$item_crossbow_arbalest", crafted: false);
            Assert.Empty(Families.Of(new[] { ecto, voidplasm, feast, material, arbalest, dverger }));
            Assert.Null(ecto.Family);
        }

        [Fact]
        public void ACreaturesSleepingCopyFoldsIntoItAndItsOtherKindsShareAPage()
        {
            var draugr = Creature("Draugr", "$enemy_draugr", "draugr_axe", "draugr_sword");
            var asleep = Creature("Draugr_sleeping", "$enemy_draugr", "draugr_sword", "draugr_axe");
            var ranged = Creature("Draugr_Ranged", "$enemy_draugr", "draugr_bow");
            var rangedAsleep = Creature("Draugr_Ranged_sleeping", "$enemy_draugr", "draugr_bow");
            var family = Assert.Single(Families.Of(new[] { ranged, asleep, rangedAsleep, draugr }));
            Assert.Same(draugr, family.Head);
            Assert.Same(draugr, family.CopyOf(asleep));
            Assert.Same(ranged, family.CopyOf(rangedAsleep));
            Assert.Null(family.CopyOf(ranged));
            Assert.Equal(new[] { draugr, ranged }, family.Kinds);
            Assert.Equal("Ranged", family.Label(ranged));
            Assert.Equal("Draugr", family.Label(draugr));
            Assert.Equal("Sleeping", family.Label(asleep));
            // Named alike without a prefab named after the other's, two kinds still.
            var goblin = Creature("Goblin", "$enemy_goblin", "goblin_club");
            var archer = Creature("GoblinArcher", "$enemy_goblin", "goblin_bow");
            var goblins = Assert.Single(Families.Of(new[] { goblin, archer }));
            Assert.Equal(new[] { goblin, archer }, goblins.Kinds);
            Assert.Null(goblins.CopyOf(archer));
        }

        [Fact]
        public void APlacesKindsShareTheirNameButItsNumber()
        {
            var one = Place("TarPit1", "Tar pit 1");
            var two = Place("TarPit2", "Tar pit 2");
            var three = Place("TarPit3", "Tar pit 3");
            var crypt2 = Place("Crypt2", "Burial Chambers");
            var crypt3 = Place("Crypt3", "Burial Chambers");
            var lone = Place("Dolmen01", "Dolmen");
            var room = E("crypt_room1", Kind.Location, "Burial Chambers");
            room.Components = new[] { "Room" };
            var families = Families.Of(new[] { three, crypt3, one, two, crypt2, lone, room });
            Assert.Equal(2, families.Count);
            var pits = one.Family;
            Assert.Equal(new[] { one, two, three }, pits.Kinds);
            Assert.Equal(new[] { "1", "2", "3" }, pits.Kinds.Select(pits.Label));
            Assert.Equal(new[] { crypt2, crypt3 }, crypt2.Family.Kinds);
            Assert.Null(lone.Family);
            Assert.Null(room.Family);

            // Kinds whose numbers repeat (TarPit1 and TarPit1_1) are told by what follows their shared stem.
            var again = Place("TarPit1_1", "Tar pit");
            var pits2 = Families.Of(new[] { one, two, three, again }).Single();
            Assert.Equal(new[] { one, two, three, again }, pits2.Kinds);
            Assert.Equal(new[] { "1", "2", "3", "1-1" }, pits2.Kinds.Select(pits2.Label));
            // The stem ends where the names part, before the digits it would end with.
            var ruin = Families.Of(new[] { Place("Ruin1", "Ruin"), Place("Ruin1_1", "Ruin") }).Single();
            Assert.Equal(new[] { "1", "1-1" }, ruin.Kinds.Select(ruin.Label));
            var camps = Families.Of(new[] { Place("CampA1", "Camp"), Place("CampB1", "Camp"), Place("CampA1_1", "Camp") }).Single();
            Assert.Equal(new[] { "A1", "A1-1", "B1" }, camps.Kinds.Select(camps.Label));
        }

        [Fact]
        public void TheListShowsAFamilyByItsBestKnownMember()
        {
            var one = Place("TarPit1", "Tar pit 1");
            var two = Place("TarPit2", "Tar pit 2");
            var three = Place("TarPit3", "Tar pit 3");
            var family = Assert.Single(Families.Of(new[] { one, two, three }));
            var levels = new Dictionary<Entry, KnownLevel> { [two] = KnownLevel.Seen, [three] = KnownLevel.Known };
            KnownLevel Level(Entry e) => levels.TryGetValue(e, out var l) ? l : KnownLevel.Unknown;
            Assert.Same(three, family.Lead(Level));
            levels[one] = KnownLevel.Known;
            Assert.Same(one, family.Lead(Level));
            Assert.Null(family.Lead(e => KnownLevel.Unknown));
            // With the compendium off, the head.
            Assert.Same(one, family.Lead(null));
        }

        [Fact]
        public void APageSwitchesOnlyAmongTheMembersTheCharacterMet()
        {
            var shield = Item("ShieldBlackmetalTower", "$item_shield_blackmetal_tower", crafted: true);
            var fallen = Item("FW_ShieldBlackmetalTower", "$item_shield_blackmetal_tower", crafted: false, tag: "Fallen Warrior");
            var shadow = Item("SP_ShieldBlackmetalTower", "$item_shield_blackmetal_tower", crafted: false);
            var family = Assert.Single(Families.Of(new[] { shield, fallen, shadow }));
            KnownLevel Level(Entry e) => e == shadow ? KnownLevel.Unknown : e == fallen ? KnownLevel.Seen : KnownLevel.Known;
            Assert.Equal(new[] { shield, fallen }, family.Met(Level));
            Assert.Equal(new[] { shield, fallen, shadow }, family.Met(null));
            // A copy met with its original unmet still shows the family by the copy.
            Assert.Same(fallen, family.Lead(e => e == fallen ? KnownLevel.Seen : KnownLevel.Unknown));
            // A kind is preferred over a copy known as well.
            Assert.Same(shield, family.Lead(e => e == shadow ? KnownLevel.Unknown : KnownLevel.Known));
        }

        [Fact]
        public void FindingFamiliesKeepsNothingBetweenCallsSoTwoAtOnceNameTheirCopiesEachByItsOwnCatalog()
        {
            // The search finds families on a worker thread: a lookup kept between calls would let
            // one search name another's copies by the carriers of its own catalog.
            var kept = typeof(Families).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .Where(f => !f.IsInitOnly && !f.IsLiteral).Select(f => f.Name).ToList();
            Assert.Empty(kept);

            var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
            System.Threading.Tasks.Parallel.For(0, 200, i =>
            {
                var shield = Item("ShieldBlackmetalTower", "$item_shield_blackmetal_tower", crafted: true);
                var shadow = Item("SP_ShieldBlackmetalTower", "$item_shield_blackmetal_tower", crafted: false);
                var catalog = new List<Entry> { shield, shadow };
                if (i % 2 == 0)
                {
                    var bearer = E("ShadowPerson", Kind.Creature, "Shadow Person");
                    shadow.Links.Add(new Link { Group = LinkWords.CarriedBy, Target = bearer.Key, Back = true });
                    catalog.Add(bearer);
                }
                var label = Families.Of(catalog).Single().Label(shadow);
                if (label != (i % 2 == 0 ? "Shadow Person" : "SP")) errors.Add(label);
            });
            Assert.Empty(errors);
        }
    }
}
