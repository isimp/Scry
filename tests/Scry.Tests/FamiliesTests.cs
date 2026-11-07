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
        public void ACreaturesKindsAreToldApartByTheirOwnWordsShortOnesToo()
        {
            // A Dvergr mage's kinds are the mage and its fire, ice and support kinds; "Ice" is a
            // short word but the only one telling that kind apart.
            var mage = Creature("DvergerMage", "$enemy_dvergr_mage", "Dverger_melee", "DvergerStaffFire_fireball", "DvergerStaffIce_icebolt");
            var fire = Creature("DvergerMageFire", "$enemy_dvergr_mage", "Dverger_melee", "DvergerStaffFire_fireball");
            var ice = Creature("DvergerMageIce", "$enemy_dvergr_mage", "Dverger_melee", "DvergerStaffIce_icebolt");
            var support = Creature("DvergerMageSupport", "$enemy_dvergr_mage", "Dverger_melee", "DvergerStaffHeal_heal");
            var family = Assert.Single(Families.Of(new[] { mage, fire, ice, support }));
            Assert.Equal(new[] { "Draugr", "Fire", "Ice", "Support" }, new[] { mage, fire, ice, support }.Select(family.Label));
        }

        /// <summary>The Dvergr mage as the game has it: the mage a spawner places rolls a fire, an ice or a healer's set; the three others are given one of those sets and placed by nothing.</summary>
        private static (Entry Mage, Entry Fire, Entry Ice, Entry Support) Mages()
        {
            var fireSet = new[] { "Dverger_melee", "DvergerStaffFire_fireball", "DvergerStaffFire", "DvergerSuitFire", "DvergerStaffFire_clusterbomb" };
            var iceSet = new[] { "Dverger_melee", "DvergerStaffIce_icebolt", "DvergerStaffIce", "DvergerStaffNova", "DvergerSuitIce" };
            var healSet = new[] { "DvergerStaffSupport_buff", "DvergerStaffHeal_heal", "DvergerStaffHeal", "DvergerSuitSupport", "DvergerMistile" };
            var mage = Creature("DvergerMage", "$enemy_dvergr_mage", "Dverger_melee", "DvergerStaffFire_fireball", "DvergerStaffIce_icebolt", "DvergerStaffHeal_heal");
            mage.GearSets = new[] { fireSet, iceSet, healSet };
            mage.Links.Add(new Link { Group = LinkWords.SpawnedBy, Target = "Spawner_DvergerMage" });
            var fire = Creature("DvergerMageFire", "$enemy_dvergr_mage", "Dverger_melee", "DvergerStaffFire_fireball");
            fire.GivenGear = fireSet.Reverse().ToArray();
            var ice = Creature("DvergerMageIce", "$enemy_dvergr_mage", "Dverger_melee", "DvergerStaffIce_icebolt");
            ice.GivenGear = iceSet;
            var support = Creature("DvergerMageSupport", "$enemy_dvergr_mage", "DvergerStaffHeal_heal");
            support.GivenGear = healSet;
            return (mage, fire, ice, support);
        }

        [Fact]
        public void AKindTheWorldNeverPlacesThatIsOneOfItsBasesSetsIsThatLookOfItNotAKindOfItsOwn()
        {
            var (mage, fire, ice, support) = Mages();
            var family = Assert.Single(Families.Of(new[] { support, mage, ice, fire }));
            // Each is the mage in that set's look, the looks after "No gear".
            Assert.Equal((mage, 1), family.WornAs(fire));
            Assert.Equal((mage, 2), family.WornAs(ice));
            Assert.Equal((mage, 3), family.WornAs(support));
            Assert.Equal(((Entry)null, 0), family.WornAs(mage));
            // The page switches among no look of the mage, met or not, nor counts them as kinds met.
            Assert.Equal(new[] { mage }, family.Met(null));
            Assert.Equal(new[] { mage }, family.Met(e => KnownLevel.Known));
            Assert.Equal(new[] { mage }, family.KindsShown);
            Assert.Same(mage, family.Lead(e => KnownLevel.Known));
            Assert.Same(mage, family.Lead(e => e == mage ? KnownLevel.Seen : KnownLevel.Known));
            // Met as one of them alone, the list still shows it by that one.
            Assert.Same(fire, family.Lead(e => e == fire ? KnownLevel.Seen : KnownLevel.Unknown));
            // Still kinds of one family, so what the book holds of one's attacks holds for each.
            Assert.Equal(new[] { mage, ice, fire, support }, family.Kinds);
            Assert.Equal(new[] { ice, fire, support }.Select(k => k.Key), Families.KindsOf(new EntryCatalog(new[] { mage, fire, ice, support }))(mage.Key));
        }

        [Fact]
        public void TheLookKindsOwnPageSaysWhereItIsMet()
        {
            Assert.Equal("The world places none of its own: it is met as the Dvergr Mage in its Fire gear, a look of that page", GearWords.WornAs("Dvergr Mage", "Fire"));
        }

        [Fact]
        public void AKindTheWorldPlacesOrOfOtherGearStaysAKindOfItsOwn()
        {
            // Placed in a biome of its own: a kind to meet.
            var (mage, fire, ice, support) = Mages();
            fire.Biomes = new[] { "Mistlands" };
            var family = Assert.Single(Families.Of(new[] { mage, fire, ice, support }));
            Assert.Equal(((Entry)null, 0), family.WornAs(fire));
            Assert.Equal(new[] { mage, fire }, family.Met(null));
            // Found in a place, or spawned by something: the same.
            (mage, fire, ice, support) = Mages();
            ice.FoundIn = new[] { "Dvergr outpost" };
            support.Links.Add(new Link { Group = LinkWords.SpawnedBy, Target = "Spawner_DvergerMageSupport" });
            family = Assert.Single(Families.Of(new[] { mage, fire, ice, support }));
            Assert.Equal(new[] { mage, ice, support }, family.Met(null));
            Assert.Equal(new[] { mage, ice, support }, family.KindsShown);
            // Given more or less than a set of the base's: no look of it.
            (mage, fire, ice, support) = Mages();
            fire.GivenGear = fire.GivenGear.Concat(new[] { "DvergerStaffNova" }).ToArray();
            ice.GivenGear = ice.GivenGear.Skip(1).ToArray();
            family = Assert.Single(Families.Of(new[] { mage, fire, ice, support }));
            Assert.Equal(new[] { mage, ice, fire }, family.KindsShown);
            // A base the world does not place, or one with a single set, has no looks to fold into.
            (mage, fire, ice, support) = Mages();
            mage.Links.Clear();
            Assert.All(new[] { fire, ice, support }, k => Assert.Null(Assert.Single(Families.Of(new[] { mage, fire, ice, support })).WornAs(k).Base));
            (mage, fire, ice, support) = Mages();
            mage.GearSets = mage.GearSets.Take(1).ToArray();
            Assert.Null(Assert.Single(Families.Of(new[] { mage, fire })).WornAs(fire).Base);
        }

        [Fact]
        public void AKindRollingASingleSetOfItsOwnPlusWhatItIsGivenIsThatLookToo()
        {
            // A kind may roll one set and be given the rest: together they are what it wears.
            var (mage, fire, _, _) = Mages();
            fire.GivenGear = new[] { "Dverger_melee", "DvergerSuitFire" };
            fire.GearSets = new[] { new[] { "DvergerStaffFire_fireball", "DvergerStaffFire", "DvergerStaffFire_clusterbomb" } };
            Assert.Equal((mage, 1), Assert.Single(Families.Of(new[] { mage, fire })).WornAs(fire));
            // One rolling two sets is no one look, whatever it is given besides.
            fire.GearSets = new[] { fire.GearSets[0], mage.GearSets[1] };
            Assert.Null(Assert.Single(Families.Of(new[] { mage, fire })).WornAs(fire).Base);
            fire.GivenGear = mage.GearSets[0];
            Assert.Null(Assert.Single(Families.Of(new[] { mage, fire })).WornAs(fire).Base);
            // What the base is always given counts with each of its sets.
            (mage, fire, _, _) = Mages();
            mage.GivenGear = new[] { "Dverger_kick" };
            Assert.Null(Assert.Single(Families.Of(new[] { mage, fire })).WornAs(fire).Base);
            fire.GivenGear = fire.GivenGear.Concat(mage.GivenGear).ToArray();
            Assert.Equal((mage, 1), Assert.Single(Families.Of(new[] { mage, fire })).WornAs(fire));
        }

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

        /// <summary>A resource as the catalog holds it: the name the game shows when it is looked at, and the kind the map keeps its spots by.</summary>
        private static Entry Resource(string name, string hover, string shown, string spotKind)
        {
            var e = E(name, Kind.Resource, Naming.Capital(Naming.Readable(name)));
            e.HoverName = hover;
            e.HoverShownName = shown;
            e.SpotKind = spotKind;
            return e;
        }

        [Fact]
        public void ResourcesTheGameNamesAlikeAndTheMapMarksAreOneRowTheirBrokenFormsBesideThem()
        {
            // Petrified Bone: the ribs and the skull, each breaking into a form kept on the map as it.
            var ribs = Resource("giant_ribs", "$piece_giant_bone", "Petrified Bone", "giant_ribs");
            var skull = Resource("giant_skull", "$piece_giant_bone", "Petrified Bone", "giant_skull");
            var ribsBroken = Resource("giant_ribs_frac", null, null, "giant_ribs");
            var skullBroken = Resource("giant_skull_frac", null, null, "giant_skull");
            var helmet1 = Resource("giant_helmet1", "$piece_giant_helmet", "Ancient Armour", "giant_helmet1");
            var helmet2 = Resource("giant_helmet2", "$piece_giant_helmet", "Ancient Armour", "giant_helmet2");
            var mud = Resource("mudpile", "$piece_mudpile", "Muddy Scrap Pile", "mudpile");
            var mud2 = Resource("mudpile2", "$piece_mudpile", "Muddy Scrap Pile", "mudpile2");
            var vein = Resource("goldvein", "$piece_deposit_goldvein", "Petrified Gammeltroll", "goldvein");
            var troll = Resource("TrollFrost_Dead", "$piece_deposit_goldvein", "Petrified Gammeltroll", "TrollFrost_Dead");
            // Named alike but not marked on the map (trees give wood), and a name only one find goes by.
            var beech = Resource("Beech1", "$prop_beech", "Beech", null);
            var smallBeech = Resource("Beech_small1", "$prop_beech", "Beech", null);
            var copper = Resource("rock4_copper", "$piece_deposit_copper", "Copper", "rock4_copper");
            var families = Families.Of(new[] { skullBroken, skull, beech, ribs, helmet2, mud2, copper, ribsBroken, helmet1, smallBeech, mud, troll, vein });

            var bone = ribs.Family;
            Assert.NotNull(bone);
            Assert.Equal("Petrified Bone", bone.Name);
            Assert.Equal(new[] { ribs, skull }, bone.Kinds);
            Assert.Equal(new[] { ribs, ribsBroken, skull, skullBroken }, bone.Members);
            Assert.Equal(new[] { "Ribs", "Ribs, broken", "Skull", "Skull, broken" }, bone.Members.Select(bone.Label));
            Assert.Equal(ribs, bone.CopyOf(ribsBroken));
            Assert.Equal("Ancient Armour", helmet1.Family.Name);
            Assert.Equal(new[] { "1", "2" }, helmet1.Family.Kinds.Select(helmet1.Family.Label));
            // A kind with no number beside numbered ones is the first.
            Assert.Equal(new[] { "1", "2" }, mud.Family.Kinds.Select(mud.Family.Label));
            // Kinds of no shared start go by their whole names, in their prefabs' order, as places' kinds do.
            Assert.Equal(new[] { "Troll frost dead", "Goldvein" }, vein.Family.Kinds.Select(vein.Family.Label));
            Assert.Null(beech.Family);
            Assert.Null(copper.Family);
            Assert.Equal(4, families.Count);

            // A bare name beside one already numbered 1 is told by its words, never a second 1.
            var heap = Resource("scrap", "$piece_scrap", "Scrap", "scrap");
            var heap1 = Resource("scrap1", "$piece_scrap", "Scrap", "scrap1");
            var scraps = Families.Of(new[] { heap, heap1 }).Single();
            Assert.Equal(new[] { "Scrap", "1" }, scraps.Kinds.Select(scraps.Label));
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
