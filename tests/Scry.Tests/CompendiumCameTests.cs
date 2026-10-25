using Xunit;

namespace Scry.Tests
{
    public class CompendiumCameTests
    {
        // A page tells how and when the character came to know it: the way it did, the game's day
        // and the biome it stood in, kept in its book from the moment it learned it in play.

        private static CompendiumBook Book(int day = 12, string biome = "BlackForest") => new CompendiumBook { Now = () => new Moment(day, biome) };

        [Fact]
        public void WhatIsComeToKnowInPlayIsKeptWithHowWhenAndWhere()
        {
            var book = Book();

            book.Raise("Troll", KnownLevel.Known, way: CameBy.Fought);

            Assert.Equal((CameBy.Fought, new Moment(12, "BlackForest")), book.Came("Troll"));
        }

        [Fact]
        public void OnlyTheFirstComingToKnowIsKept()
        {
            var book = Book();
            book.Raise("Troll", KnownLevel.Seen, way: CameBy.Fought);
            Assert.Null(book.Came("Troll"));

            book.Raise("Troll", KnownLevel.Known, way: CameBy.Tamed);
            book.Now = () => new Moment(40, "Mountain");
            book.Raise("Troll", KnownLevel.Known, way: CameBy.Fought);

            Assert.Equal((CameBy.Tamed, new Moment(12, "BlackForest")), book.Came("Troll"));
        }

        [Fact]
        public void WhatWasKnownBeforeTheBookOrWithoutAWayHasNoComingToKnow()
        {
            var book = Book();
            book.Raise("Troll", KnownLevel.Known, quietly: true, way: CameBy.Killed);
            book.Raise("Boar", KnownLevel.Known);

            Assert.Null(book.Came("Troll"));
            Assert.Null(book.Came("Boar"));
            Assert.Null(book.Came("Neck"));
        }

        [Fact]
        public void WithoutAMomentTheDayAndBiomeAreLeftOpen()
        {
            var book = new CompendiumBook();
            book.Raise("Troll", KnownLevel.Known, way: CameBy.Fought);

            Assert.Equal((CameBy.Fought, Moment.None), book.Came("Troll"));
        }

        [Fact]
        public void HowEachThingCameToBeKnownIsKeptWithTheBook()
        {
            var book = Book();
            book.Raise("Troll", KnownLevel.Known, way: CameBy.Fought);
            book.Now = null;
            book.Raise("loc:Crypt2", KnownLevel.Known, way: CameBy.WentInside);

            var read = CompendiumBook.Load(book.Save());

            Assert.Equal((CameBy.Fought, new Moment(12, "BlackForest")), read.Came("Troll"));
            Assert.Equal((CameBy.WentInside, Moment.None), read.Came("loc:Crypt2"));
            Assert.Null(CompendiumBook.Load("came\tTroll\tNoSuchWay\t3\tMeadows\n").Came("Troll"));
            Assert.Null(CompendiumBook.Load("came\tTroll\tFought\n").Came("Troll"));
        }

        [Fact]
        public void HowItCameToBeKnownIsToldByTheWayTheDayAndTheBiome()
        {
            Assert.Equal("day 12, in the Black Forest, fighting it", CompendiumWords.CameToKnow(CameBy.Fought, 12, "Black Forest"));
            Assert.Equal("in the Black Forest, fighting it", CompendiumWords.CameToKnow(CameBy.Fought, -1, "Black Forest"));
            // The world named where it is not the one played now, as a character's book spans worlds.
            Assert.Equal("day 12 in Elderwood, in the Black Forest, fighting it", CompendiumWords.CameToKnow(CameBy.Fought, 12, "Black Forest", world: "Elderwood"));
            Assert.Equal("in Elderwood, in the Black Forest, fighting it", CompendiumWords.CameToKnow(CameBy.Fought, -1, "Black Forest", world: "Elderwood"));
            Assert.Equal("day 3, unlocking its recipe", CompendiumWords.CameToKnow(CameBy.Recipe, 3, null));
            // The game's record of stations fills by being near one, not by using it.
            Assert.Equal("day 3, being near it", CompendiumWords.CameToKnow(CameBy.Near, 3, null));
            Assert.Equal("been near", JournalWords.LearnedNote(new BookNews(NewsHow.Known, null, CameBy.Near, 3)));
            // A taming seen is not the character's own taming, and petting one is neither.
            Assert.Equal("seen tamed", JournalWords.LearnedNote(new BookNews(NewsHow.Known, null, CameBy.SeenTamed, 3)));
            Assert.Equal("petted", JournalWords.LearnedNote(new BookNews(NewsHow.Known, null, CameBy.Petted, 3)));
            Assert.Equal("day 1, stepping inside", CompendiumWords.CameToKnow(CameBy.WentInside, 1, ""));
            Assert.Equal("along with Troll", CompendiumWords.CameWith("Troll"));
            // The You box has a word for every way Learned has one: a creature seen born or grown up came to be known so, not by "meeting it".
            foreach (CameBy way in System.Enum.GetValues(typeof(CameBy)))
            {
                var line = CompendiumWords.CameLine(way, 3, null, null, 0, true, DayPart.None, null, null, null, null);
                if (way != CameBy.Fought && way != CameBy.Killed) Assert.DoesNotContain("meeting it", line);
            }
            Assert.Contains("seeing it born", CompendiumWords.CameLine(CameBy.Born, 3, null, null, 0, true, DayPart.None, null, null, null, null));
            Assert.Contains("seeing it grow up", CompendiumWords.CameLine(CameBy.GrewUp, 3, null, null, 0, true, DayPart.None, null, null, null, null));
            Assert.Equal("known before Scry kept track of your character", CompendiumWords.KnownBefore);
        }

        [Fact]
        public void EveryWayOfComingToKnowHasItsWords()
        {
            foreach (CameBy way in System.Enum.GetValues(typeof(CameBy)))
            {
                var said = CompendiumWords.CameToKnow(way, -1, null);
                Assert.False(string.IsNullOrEmpty(said), way.ToString());
                Assert.DoesNotContain(way.ToString(), said);
            }
        }

        [Fact]
        public void WhatTheGamesRecordsTellAnewIsKeptWithTheWayEachRecordTellsIt()
        {
            static Entry E(Kind kind, string name, params string[] tokens) => new Entry { Kind = kind, Name = name, Tokens = tokens };
            var troll = E(Kind.Creature, "Troll", "$enemy_troll");
            var club = E(Kind.Item, "Club", "$item_club");
            var mead = E(Kind.Item, "MeadHealthMinor", "$item_mead");
            var axe = E(Kind.Item, "AxeBronze", "$item_axe_bronze");
            var wood = E(Kind.Item, "Wood", "$item_wood");
            var sword = E(Kind.Item, "SwordIron", "$item_sword_iron");
            var bench = E(Kind.Piece, "piece_workbench", "$piece_workbench");
            var meadows = E(Kind.Biome, "Meadows", "$biome_meadows");
            meadows.Biomes = new[] { "Meadows" };
            var beech = E(Kind.Resource, "Beech1", "$prop_beech");
            beech.Biomes = new[] { "Meadows" };
            wood.AddTermLink("from", beech);
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { troll, club, mead, axe, wood, sword, bench, meadows, beech }), soldByTrader: _ => false);
            var book = Book();
            var records = new CompendiumRecords();
            records.Killed.Add("$enemy_troll");
            records.Crafted.Add("$item_club");
            records.Eaten.Add("$item_mead");
            records.Recipes.Add("$item_axe_bronze");
            records.PickedUp.Add("$item_wood");
            records.Materials.Add("$item_sword_iron");
            records.Placed.Add("$piece_workbench");
            records.Biomes.Add("$biome_meadows");

            catchUp.Fill(records, book, firstLoad: false);

            Assert.Equal(CameBy.Killed, book.Came(troll.Key)?.Way);
            Assert.Equal(CameBy.Made, book.Came(club.Key)?.Way);
            Assert.Equal(CameBy.Eaten, book.Came(mead.Key)?.Way);
            Assert.Equal(CameBy.Recipe, book.Came(axe.Key)?.Way);
            Assert.Equal(CameBy.Held, book.Came(wood.Key)?.Way);
            Assert.Equal(CameBy.Held, book.Came(sword.Key)?.Way);
            Assert.Equal(CameBy.Built, book.Came(bench.Key)?.Way);
            Assert.Equal(CameBy.StoodIn, book.Came(meadows.Key)?.Way);
            // What gives an item is seen through the records, not known: no way of coming to know it.
            Assert.Equal(KnownLevel.Seen, book.Level(beech.Key));
            Assert.Null(book.Came(beech.Key));
        }

        [Fact]
        public void WhatIsMetInPlayIsKeptWithTheWayItWasMet()
        {
            var portal = new Entry { Kind = Kind.Piece, Name = "portal_wood", Tokens = new[] { "$piece_portal" } };
            var crypt = new Entry { Kind = Kind.Location, Name = "Crypt2" };
            var army = new Entry { Kind = Kind.Raid, Name = "army_eikthyr" };
            var haldor = new Entry { Kind = Kind.Creature, Name = "Haldor" };
            var lantern = new Entry { Kind = Kind.Item, Name = "Lantern", Tokens = new[] { "$item_lantern" } };
            var catchUp = new CompendiumCatchUp(new EntryCatalog(new[] { portal, crypt, army, haldor, lantern }));
            var book = Book();

            catchUp.Used("portal_wood", book);
            catchUp.StoodIn(crypt.Key, hasInside: true, inside: true, book);
            catchUp.LivedThrough(army.Key, book);
            catchUp.Traded("Haldor", new string[0], book);
            catchUp.CardShown("$item_lantern", book);
            catchUp.Met("Troll", KnownLevel.Known, book, CameBy.Fought);

            Assert.Equal(CameBy.Used, book.Came(portal.Key)?.Way);
            Assert.Equal(CameBy.WentInside, book.Came(crypt.Key)?.Way);
            Assert.Equal(CameBy.LivedThrough, book.Came(army.Key)?.Way);
            Assert.Equal(CameBy.Traded, book.Came(haldor.Key)?.Way);
            Assert.Equal(CameBy.Card, book.Came(lantern.Key)?.Way);
        }
    }
}
