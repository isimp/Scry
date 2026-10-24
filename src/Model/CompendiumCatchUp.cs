using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Writes into what a character knows (<see cref="CompendiumBook"/>) what the game's own
    /// records of it tell (<see cref="CompendiumRecords"/>), so a character played before does
    /// not start from nothing. Read by the loose rules: a creature killed, an item picked up,
    /// crafted, eaten or whose recipe is unlocked, a piece unlocked, used or placed and a biome
    /// entered are known, as are a trophy held and the creatures it falls from, every resource
    /// giving an item so known that grows in a biome the character entered, and every status
    /// effect something so known gives (a bed's Rested, a biome's weather), paired with its
    /// givers among them; an item ever held is known, but one a trader sells only seen, as the
    /// game counts what a trader offers among what was held. A record counts only for the
    /// kinds it is about, and for every entry going by its name (<see cref="Entry.Tokens"/>).
    /// For the strict rules it pairs what the records show went together: a recipe unlocked, an
    /// item crafted or a piece placed with its ingredients and station, a trophy with the
    /// creatures it falls from, a thing picked or picked up with the resources giving it in the
    /// biomes entered, a food eaten with the status effects it gives, a piece placed with the one
    /// tool that builds it.
    /// </summary>
    internal sealed class CompendiumCatchUp
    {
        private readonly EntryCatalog _catalog;
        private readonly Func<string, IReadOnlyList<string>> _toolsOf;
        private readonly Func<string, bool> _soldByTrader;
        private readonly IReadOnlyDictionary<string, string> _biomesShown;
        private Func<IReadOnlyDictionary<string, IReadOnlyList<string>>> _readEveryLanguage;
        private IReadOnlyDictionary<string, IReadOnlyList<string>> _everyLanguage;
        private readonly Dictionary<string, List<Entry>> _byToken = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);
        private readonly List<Entry> _biomes = new List<Entry>();

        /// <param name="catalog">The entries the records are told in.</param>
        /// <param name="toolsOf">The tools that build a piece, by its prefab; none where not told.</param>
        /// <param name="soldByTrader">Whether a trader sells an item, by its prefab; where not told, any might be.</param>
        /// <param name="biomesShown">Each biome's name as the game shows it in the player's language, by the biome's entry name.</param>
        /// <param name="biomesInEveryLanguage">Each biome's names in every language the game ships, by the biome's entry name; asked for once, only when an area names no biome in the player's language.</param>
        public CompendiumCatchUp(EntryCatalog catalog, Func<string, IReadOnlyList<string>> toolsOf = null, Func<string, bool> soldByTrader = null,
            IReadOnlyDictionary<string, string> biomesShown = null, Func<IReadOnlyDictionary<string, IReadOnlyList<string>>> biomesInEveryLanguage = null)
        {
            _catalog = catalog;
            _toolsOf = toolsOf;
            _soldByTrader = soldByTrader;
            _biomesShown = biomesShown;
            _readEveryLanguage = biomesInEveryLanguage;
            foreach (var entry in catalog.All)
            {
                if (entry.Kind == Kind.Biome) _biomes.Add(entry);
                foreach (var token in entry.Tokens)
                {
                    if (string.IsNullOrEmpty(token)) continue;
                    if (!_byToken.TryGetValue(token, out var list)) _byToken[token] = list = new List<Entry>();
                    list.Add(entry);
                }
            }
        }

        /// <summary>
        /// Raises in the book what the records tell, as news; how many entries it learned more of.
        /// On the first reading as a character loads (<paramref name="firstLoad"/>) the records do
        /// not say when or where a thing came to be known, so no such record is kept of it, and
        /// its kills are news by the creatures learned rather than their health found out as well;
        /// what it learns is caught up (<see cref="CompendiumBook.CatchingUp"/>), with no moment or day.
        /// </summary>
        public int Fill(CompendiumRecords records, CompendiumBook book, bool firstLoad)
        {
            var caught = book.CatchingUp;
            book.CatchingUp = caught || firstLoad;
            try
            {
                return FillFrom(records, book, firstLoad);
            }
            finally
            {
                book.CatchingUp = caught;
            }
        }

        private int FillFrom(CompendiumRecords records, CompendiumBook book, bool firstLoad)
        {
            // An unlocked recipe's item is known: the crafting menu shows it whole, its look, stats and what it takes.
            var items = Named(records.PickedUp, Kind.Item).Concat(Named(records.Crafted, Kind.Item)).Concat(Named(records.Eaten, Kind.Item)).Concat(Named(records.Recipes, Kind.Item)).ToList();
            items.AddRange(Prefabs(records.Trophies.Concat(records.Picked), Kind.Item));

            // Each record raises what it tells by its own way, the first way to tell a thing kept with it.
            var known = new List<(Entry Entry, CameBy Way)>();
            void Add(IEnumerable<Entry> entries, CameBy way) => known.AddRange(entries.Select(e => (e, way)));
            var entered = Entered(records);
            var killed = Killed(records, entered).ToList();
            Add(killed, CameBy.Killed);
            Add(Named(records.Crafted, Kind.Item), CameBy.Made);
            Add(Named(records.Eaten, Kind.Item), CameBy.Eaten);
            Add(Named(records.Recipes, Kind.Item), CameBy.Recipe);
            Add(Named(records.PickedUp, Kind.Item), CameBy.Held);
            Add(Prefabs(records.Picked, Kind.Item), CameBy.Picked);
            Add(Prefabs(records.Trophies, Kind.Item), CameBy.Held);
            // The creatures a trophy held falls from, those living in a biome the character entered:
            // not the game's own TheHive, placed nowhere, which drops the drake's trophy too.
            foreach (var trophy in Prefabs(records.Trophies, Kind.Item)) Add(CreaturesDropping(trophy, entered), CameBy.Trophy);
            // What gives an item: only where it grows in a biome the character entered, as Wood
            // picked up in the Meadows says nothing of the Ashlands' logs.
            foreach (var item in items) Add(ResourcesGiving(item, entered), CameBy.Gives);
            Add(Named(records.Placed, Kind.Piece), CameBy.Built);
            Add(Named(records.Stations, Kind.Piece), CameBy.Used);
            Add(Named(records.Recipes, Kind.Piece), CameBy.Recipe);
            Add(BiomesEntered(records), CameBy.StoodIn);
            var givers = Givers(known.Select(k => k.Entry));
            Add(givers.Keys, CameBy.Given);
            // The guardian power chosen: its stone told of it, and the character carries it.
            var powers = records.Powers.Select(p => _catalog.Find(EntryKeys.For(Kind.StatusEffect, p))).Where(e => e != null && e.Kind == Kind.StatusEffect).ToList();
            Add(powers, CameBy.Chosen);

            // The game's known materials: everything ever in the inventory, and what a trader
            // offered too; one no trader sells was held, so known, the rest seen.
            var materials = Named(records.Materials, Kind.Item).ToList();
            var held = materials.Where(e => _soldByTrader != null && !_soldByTrader(e.Name)).ToList();
            Add(held, CameBy.Held);
            var raised = known.Count(k => book.Raise(k.Entry.Key, KnownLevel.Known, way: firstLoad ? null : k.Way))
                + Raise(book, materials.Except(held), KnownLevel.Seen);
            Pair(records, book, entered);
            // A creature the character killed shows its health under the strict rules: found out, as news after the first reading.
            foreach (var creature in killed)
            {
                if (firstLoad) book.Prove(creature.Key, EntryKeys.Did(Deed.Killed));
                else book.FoundOut(creature.Key, EntryKeys.Did(Deed.Killed));
            }
            foreach (var stray in Unkilled(records, book, known.Select(k => k.Entry))) book.TakeBack(stray.Key, EntryKeys.Did(Deed.Killed));
            foreach (var given in givers)
            {
                foreach (var giver in given.Value) book.Witness(given.Key.Key, giver);
            }
            // A power is chosen at its stone: paired with the stones that give it the character has seen.
            foreach (var power in powers)
            {
                foreach (var link in power.Links) if (link.Group == LinkWords.GivenBy && book.Level(link.Target) >= KnownLevel.Seen) book.Witness(power.Key, link.Target);
            }
            return raised;
        }

        /// <summary>
        /// The status effects something known gives (a bed's Rested, a fire's Resting, a biome's
        /// weather), with the givers among the known: the game keeps no record of status effects,
        /// only of what gives them.
        /// </summary>
        private Dictionary<Entry, List<string>> Givers(IEnumerable<Entry> known)
        {
            var keys = new HashSet<string>(known.Select(e => e.Key), StringComparer.Ordinal);
            var givers = new Dictionary<Entry, List<string>>();
            foreach (var effect in _catalog.All)
            {
                if (effect.Kind != Kind.StatusEffect) continue;
                foreach (var link in effect.Links)
                {
                    if (link.Group != LinkWords.GivenBy || !keys.Contains(link.Target)) continue;
                    if (!givers.TryGetValue(effect, out var list)) givers[effect] = list = new List<string>();
                    list.Add(link.Target);
                }
            }
            return givers;
        }

        /// <summary>Pairs in the book what the records show went together, for the strict rules.</summary>
        private void Pair(CompendiumRecords records, CompendiumBook book, HashSet<string> entered)
        {
            var made = Named(records.Recipes.Concat(records.Crafted).Concat(records.Placed), Kind.Item)
                .Concat(Named(records.Recipes.Concat(records.Placed), Kind.Piece));
            foreach (var thing in made)
            {
                foreach (var part in thing.TermLinks("needs")) book.Witness(thing.Key, part.Key);
                foreach (var station in thing.Stations)
                {
                    var place = _catalog.Find(station.Name);
                    if (place != null) book.Witness(thing.Key, place.Key);
                }
            }
            foreach (var trophy in Prefabs(records.Trophies, Kind.Item))
            {
                foreach (var creature in CreaturesDropping(trophy, entered)) book.Witness(trophy.Key, creature.Key);
            }
            foreach (var item in Prefabs(records.Picked, Kind.Item).Concat(Named(records.PickedUp, Kind.Item)))
            {
                foreach (var resource in ResourcesGiving(item, entered)) book.Witness(item.Key, resource.Key);
            }
            // The records keep what was placed, not with what: a piece one tool alone builds was built with it.
            if (_toolsOf != null)
            {
                foreach (var piece in Named(records.Placed, Kind.Piece))
                {
                    var tools = _toolsOf(piece.Name);
                    var tool = tools != null && tools.Count == 1 ? _catalog.Find(tools[0]) : null;
                    if (tool != null) book.Witness(piece.Key, tool.Key);
                }
            }
            foreach (var food in Named(records.Eaten, Kind.Item))
            {
                foreach (var link in food.Links) if (link.Group == LinkWords.StatusEffects) book.Witness(food.Key, link.Target);
            }
        }

        /// <summary>Learns of an entry met in play, by its key, as news; true when that is more than was known. What the catalog has no entry for is passed over.</summary>
        public bool Met(string key, KnownLevel level, CompendiumBook book, CameBy? way = null, int stars = -1) => Lists(key) && book.Raise(key, level, way: way, stars: stars);

        /// <summary>Whether the catalog has an entry by a key, for anything to be learned of it at all.</summary>
        public bool Lists(string key) => _catalog.Find(key) != null;

        /// <summary>
        /// Something the character used, by its prefab, as news: known, as a portal, bed, ship or
        /// chest a friend built shows what it does in use. A creature is known by fighting or
        /// taming it and an item by holding it, so neither counts. True when it was learned more of.
        /// </summary>
        public bool Used(string key, CompendiumBook book)
        {
            var entry = _catalog.Find(key);
            if (entry == null || entry.Kind == Kind.Creature || entry.Kind == Kind.Item) return false;
            return book.Raise(entry.Key, KnownLevel.Known, way: CameBy.Used);
        }

        /// <summary>
        /// An item's tooltip card the game showed, in the inventory, a chest or a trader's store,
        /// by the item's name token, as news: every item going by it is known, the card telling
        /// all its page would. True when any was learned more of.
        /// </summary>
        public bool CardShown(string token, CompendiumBook book)
        {
            if (string.IsNullOrEmpty(token)) return false;
            var raised = false;
            foreach (var item in Named(new[] { token }, Kind.Item)) raised |= book.Raise(item.Key, KnownLevel.Known, way: CameBy.Card);
            return raised;
        }

        /// <summary>
        /// A trader the character trades with, as news: known, its wares on show seen, and each
        /// paired with it, for the strict rules. True when it was learned more of; a trader the
        /// catalog lacks gives nothing.
        /// </summary>
        public bool Traded(string trader, IEnumerable<string> wares, CompendiumBook book)
        {
            var entry = _catalog.Find(trader);
            if (entry == null) return false;
            var raised = book.Raise(entry.Key, KnownLevel.Known, way: CameBy.Traded);
            foreach (var ware in wares ?? Array.Empty<string>())
            {
                var item = _catalog.Find(ware);
                if (item == null) continue;
                raised |= book.Raise(item.Key, KnownLevel.Seen);
                book.Witness(entry.Key, item.Key);
            }
            return raised;
        }

        /// <summary>
        /// The place the character stands in, as news: known, but a place with an inside (a
        /// dungeon, whose rooms come along with it) only once the character is inside it; standing
        /// at its entrance it is seen. Paired with the biome it stands in, for the strict rules. True
        /// when it was learned more of.
        /// </summary>
        public bool StoodIn(string place, bool hasInside, bool inside, CompendiumBook book, string biome = null)
        {
            var learned = Met(place, !hasInside || inside ? KnownLevel.Known : KnownLevel.Seen, book, hasInside ? CameBy.WentInside : CameBy.StoodIn);
            var entry = _catalog.Find(place);
            var where = biome != null ? _catalog.Find(biome) : null;
            if (entry != null && where != null) book.Witness(entry.Key, where.Key);
            return learned;
        }

        /// <summary>A crafting station seen with its upgrades standing near it, as the game counts them, each paired with it, for the strict rules; one the catalog lacks pairs nothing.</summary>
        public void Upgrades(string station, IEnumerable<string> upgrades, CompendiumBook book)
        {
            var entry = _catalog.Find(station);
            if (entry == null) return;
            foreach (var upgrade in upgrades)
            {
                var piece = _catalog.Find(upgrade);
                if (piece != null) book.Witness(entry.Key, piece.Key);
            }
        }

        /// <summary>
        /// The raid the character is in, as news: known, and paired with the creatures it brings
        /// that the character has met, for the strict rules. True when it was learned more of.
        /// </summary>
        public bool LivedThrough(string raid, CompendiumBook book)
        {
            var entry = _catalog.Find(raid);
            if (entry == null) return false;
            var raised = book.Raise(entry.Key, KnownLevel.Known, way: CameBy.LivedThrough);
            foreach (var creature in entry.TermLinks("spawns"))
            {
                if (book.Level(creature.Key) >= KnownLevel.Seen) book.Witness(entry.Key, creature.Key);
            }
            return raised;
        }

        /// <summary>The biomes the character entered, as the game names them (<see cref="Entry.Biomes"/>).</summary>
        private HashSet<string> Entered(CompendiumRecords records) =>
            new HashSet<string>(BiomesEntered(records).SelectMany(b => b.Biomes), StringComparer.Ordinal);

        /// <summary>The biomes the character entered, from the areas the game keeps (<see cref="BiomesIn"/>).</summary>
        private List<Entry> BiomesEntered(CompendiumRecords records) => records.Biomes.SelectMany(BiomesIn).ToList();

        /// <summary>
        /// The biomes an area the game keeps as entered names (<c>Player.m_knownBiome</c>). The game
        /// keeps an area by the name it showed then, in the language played in then, an area's
        /// prefix before it where there is one ("Dunkler Sumpf"), so a biome counts where its shown
        /// name stands in an area as whole words: in the player's language first, in any other the
        /// game ships where that names none (<see cref="InAnyLanguage"/>). A save from before the
        /// game kept those has each biome turned into its name token as the character loads
        /// (<c>BiomeSector.GetBiomeName</c>, "$biome_meadows") and saved so again, so an area going
        /// by a token counts too, and is no language's.
        /// </summary>
        private List<Entry> BiomesIn(string area)
        {
            var named = Named(area.Split(' '), Kind.Biome).ToList();
            if (_biomesShown != null)
            {
                foreach (var biome in _biomes)
                {
                    if (named.Contains(biome) || !_biomesShown.TryGetValue(biome.Name, out var shown) || string.IsNullOrEmpty(shown)) continue;
                    if (StandsIn(area, shown)) named.Add(biome);
                }
            }
            if (named.Count == 0 && !area.StartsWith("$", StringComparison.Ordinal)) named.AddRange(InAnyLanguage(area));
            return named;
        }

        /// <summary>
        /// The biome an area kept while the game ran in another language names: the biomes whose
        /// name in a language the game ships stands in the area, a name standing inside a longer
        /// one found being part of that one (the Black Forest in Bulgarian, "Черна Гора", holds the
        /// Mountain in Russian, "Гора"). Names left standing for more than one biome tell none for
        /// certain, so none is told.
        /// </summary>
        private IEnumerable<Entry> InAnyLanguage(string area)
        {
            var everyLanguage = EveryLanguage();
            if (everyLanguage == null) return Enumerable.Empty<Entry>();
            var found = new List<(Entry Biome, string Name)>();
            foreach (var biome in _biomes)
            {
                if (!everyLanguage.TryGetValue(biome.Name, out var names)) continue;
                foreach (var name in names)
                {
                    if (StandsIn(area, name)) found.Add((biome, name));
                }
            }
            var biomes = found.Where(f => !found.Any(g => g.Name.Length > f.Name.Length && StandsIn(g.Name, f.Name))).Select(f => f.Biome).Distinct().ToList();
            return biomes.Count == 1 ? biomes : Enumerable.Empty<Entry>();
        }

        /// <summary>Each biome's names in every language, asked for once and only once needed; none where not told.</summary>
        private IReadOnlyDictionary<string, IReadOnlyList<string>> EveryLanguage()
        {
            if (_readEveryLanguage == null) return _everyLanguage;
            // Let go first, so a reading that fails is not tried again on every look at the records.
            var read = _readEveryLanguage;
            _readEveryLanguage = null;
            return _everyLanguage = read();
        }

        /// <summary>Whether a name stands in an area's name as whole words.</summary>
        private static bool StandsIn(string area, string name) => (" " + area + " ").IndexOf(" " + name + " ", StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>
        /// The areas the records keep as entered whose biome the book does not know: once the
        /// records are read in, only an area naming no biome of the catalog, as one given a name of
        /// its own in place of its biome's, one whose words name different biomes in different
        /// languages, or one of a biome a mod adds kept in a language not played in now. The game's
        /// no-biome (<see cref="BiomeWords.NoBiome"/>) is left out, being no biome a character enters.
        /// </summary>
        [ForTest]
        public List<string> AreasNotKnown(CompendiumRecords records, CompendiumBook book) =>
            records.Biomes.Where(area => area != BiomeWords.NoBiome && !BiomesIn(area).Any(b => book.Level(b.Key) == KnownLevel.Known)).ToList();

        /// <summary>The resources giving an item that grow in a biome the character entered; one growing nowhere the world places it is left to be met.</summary>
        private static IEnumerable<Entry> ResourcesGiving(Entry item, HashSet<string> entered) =>
            item.TermLinks("from").Where(e => e.Kind == Kind.Resource && e.Biomes.Any(entered.Contains));

        /// <summary>
        /// The creatures the kills counted stand for. The game counts a kill by the name a creature
        /// shows, which several can share, as the frozen greydwarf, living only in the Deep North,
        /// shows the greydwarf's: of those, the ones living in a biome the character entered;
        /// where none does, the ones the world brings about (<see cref="Comes"/>), never the
        /// bosses' aspects, which nothing in the game spawns though their roots go by the Elder's
        /// roots' name; where none comes about either, a kill was still one of them, and all count.
        /// </summary>
        private IEnumerable<Entry> Killed(CompendiumRecords records, HashSet<string> entered)
        {
            foreach (var token in records.Killed)
            {
                if (token == null || !_byToken.TryGetValue(token, out var entries)) continue;
                var lived = false;
                var comes = false;
                foreach (var entry in entries)
                {
                    if (entry.Kind != Kind.Creature) continue;
                    lived |= entry.Biomes.Any(entered.Contains);
                    comes |= Comes(entry);
                }
                foreach (var entry in entries)
                {
                    if (entry.Kind == Kind.Creature && (lived ? entry.Biomes.Any(entered.Contains) : !comes || Comes(entry))) yield return entry;
                }
            }
        }

        /// <summary>
        /// Whether the world brings a creature about: it lives in a biome, or something it brings
        /// about spawns it, a place, a spawner, a raid or a creature summoning it.
        /// </summary>
        private bool Comes(Entry creature) => ComesBy(creature, new HashSet<Entry>());

        private bool ComesBy(Entry entry, HashSet<Entry> seen)
        {
            if (entry.Biomes.Length > 0) return true;
            if (!seen.Add(entry)) return false;
            foreach (var by in SpawnedBy(entry)) if (ComesBy(by, seen)) return true;
            return false;
        }

        /// <summary>What spawns each entry (<see cref="Entry.TermLinks"/>, "spawns"), made once from the catalog.</summary>
        private Dictionary<Entry, List<Entry>> _spawnedBy;

        private IReadOnlyList<Entry> SpawnedBy(Entry entry)
        {
            if (_spawnedBy == null)
            {
                _spawnedBy = new Dictionary<Entry, List<Entry>>();
                foreach (var by in _catalog.All)
                {
                    foreach (var spawned in by.TermLinks("spawns"))
                    {
                        if (spawned == null) continue;
                        if (!_spawnedBy.TryGetValue(spawned, out var those)) _spawnedBy[spawned] = those = new List<Entry>();
                        those.Add(by);
                    }
                }
            }
            return _spawnedBy.TryGetValue(entry, out var spawners) ? (IReadOnlyList<Entry>)spawners : Array.Empty<Entry>();
        }

        /// <summary>
        /// The creatures a kill was once counted for that it does not stand for now (<see cref="Killed"/>),
        /// known and killed in the book only by that: nothing in the records tells of them now, and the character never met them in play, which keeps the way it came to know one
        /// or pairs it with the biome it stood in.
        /// </summary>
        private List<Entry> Unkilled(CompendiumRecords records, CompendiumBook book, IEnumerable<Entry> known)
        {
            var told = new HashSet<Entry>(known);
            var biomes = _catalog.All.Where(e => e.Kind == Kind.Biome).ToList();
            var stray = new List<Entry>();
            foreach (var token in records.Killed)
            {
                if (token == null || !_byToken.TryGetValue(token, out var entries)) continue;
                foreach (var entry in entries)
                {
                    if (entry.Kind != Kind.Creature || told.Contains(entry) || stray.Contains(entry)) continue;
                    if (!book.Proved(entry.Key, EntryKeys.Did(Deed.Killed)) || book.Came(entry.Key) != null) continue;
                    if (biomes.Any(b => book.Witnessed(entry.Key, b.Key))) continue;
                    stray.Add(entry);
                }
            }
            return stray;
        }

        /// <summary>The creatures an item falls from that live in a biome the character entered.</summary>
        private static IEnumerable<Entry> CreaturesDropping(Entry item, HashSet<string> entered) =>
            item.TermLinks("from").Where(e => e.Kind == Kind.Creature && e.Biomes.Any(entered.Contains));

        /// <summary>The entries of a kind going by any of these names.</summary>
        private IEnumerable<Entry> Named(IEnumerable<string> tokens, Kind kind)
        {
            foreach (var token in tokens)
            {
                if (token == null || !_byToken.TryGetValue(token, out var entries)) continue;
                foreach (var entry in entries) if (entry.Kind == kind) yield return entry;
            }
        }

        /// <summary>The entries of a kind made from these prefabs.</summary>
        private IEnumerable<Entry> Prefabs(IEnumerable<string> names, Kind kind) =>
            names.Select(_catalog.Find).Where(e => e != null && e.Kind == kind);

        private static int Raise(CompendiumBook book, IEnumerable<Entry> entries, KnownLevel level) =>
            entries.Count(e => book.Raise(e.Key, level));
    }
}
