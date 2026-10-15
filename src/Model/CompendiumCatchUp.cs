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
        private readonly Dictionary<string, List<Entry>> _byToken = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);

        /// <param name="catalog">The entries the records are told in.</param>
        /// <param name="toolsOf">The tools that build a piece, by its prefab; none where not told.</param>
        /// <param name="soldByTrader">Whether a trader sells an item, by its prefab; where not told, any might be.</param>
        /// <param name="biomesShown">Each biome's name as the game shows it in the player's language, by the biome's entry name.</param>
        public CompendiumCatchUp(EntryCatalog catalog, Func<string, IReadOnlyList<string>> toolsOf = null, Func<string, bool> soldByTrader = null,
            IReadOnlyDictionary<string, string> biomesShown = null)
        {
            _catalog = catalog;
            _toolsOf = toolsOf;
            _soldByTrader = soldByTrader;
            _biomesShown = biomesShown;
            foreach (var entry in catalog.All)
            {
                foreach (var token in entry.Tokens)
                {
                    if (string.IsNullOrEmpty(token)) continue;
                    if (!_byToken.TryGetValue(token, out var list)) _byToken[token] = list = new List<Entry>();
                    list.Add(entry);
                }
            }
        }

        /// <summary>
        /// Raises in the book what the records tell; how many entries it learned more of. Quietly
        /// when what the records hold was known before, as on first reading them after a
        /// character loads, so none of it counts among the latest learned.
        /// </summary>
        public int Fill(CompendiumRecords records, CompendiumBook book, bool quietly)
        {
            // An unlocked recipe's item is known: the crafting menu shows it whole, its look, stats and what it takes.
            var items = Named(records.PickedUp, Kind.Item).Concat(Named(records.Crafted, Kind.Item)).Concat(Named(records.Eaten, Kind.Item)).Concat(Named(records.Recipes, Kind.Item)).ToList();
            items.AddRange(Prefabs(records.Trophies.Concat(records.Picked), Kind.Item));

            // Each record raises what it tells by its own way, the first way to tell a thing kept with it.
            var known = new List<(Entry Entry, CameBy Way)>();
            void Add(IEnumerable<Entry> entries, CameBy way) => known.AddRange(entries.Select(e => (e, way)));
            Add(Named(records.Killed, Kind.Creature), CameBy.Killed);
            Add(Named(records.Crafted, Kind.Item), CameBy.Made);
            Add(Named(records.Eaten, Kind.Item), CameBy.Eaten);
            Add(Named(records.Recipes, Kind.Item), CameBy.Recipe);
            Add(Named(records.PickedUp, Kind.Item), CameBy.Held);
            Add(Prefabs(records.Picked, Kind.Item), CameBy.Picked);
            Add(Prefabs(records.Trophies, Kind.Item), CameBy.Held);
            foreach (var trophy in Prefabs(records.Trophies, Kind.Item)) Add(trophy.TermLinks("from").Where(e => e.Kind == Kind.Creature), CameBy.Trophy);
            // What gives an item: only where it grows in a biome the character entered, as Wood
            // picked up in the Meadows says nothing of the Ashlands' logs.
            var entered = Entered(records);
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
            var raised = known.Count(k => book.Raise(k.Entry.Key, KnownLevel.Known, quietly, k.Way))
                + Raise(book, materials.Except(held), KnownLevel.Seen, quietly);
            Pair(records, book, entered);
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
                foreach (var creature in trophy.TermLinks("from")) if (creature.Kind == Kind.Creature) book.Witness(trophy.Key, creature.Key);
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
        public bool Met(string key, KnownLevel level, CompendiumBook book, CameBy? way = null) => _catalog.Find(key) != null && book.Raise(key, level, way: way);

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
        /// at its entrance it is seen. True when it was learned more of.
        /// </summary>
        public bool StoodIn(string place, bool hasInside, bool inside, CompendiumBook book) =>
            Met(place, !hasInside || inside ? KnownLevel.Known : KnownLevel.Seen, book, hasInside ? CameBy.WentInside : CameBy.StoodIn);

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

        /// <summary>
        /// The biomes the character entered, from the areas the game keeps (<c>Player.m_knownBiome</c>):
        /// each is the biome's name as the game shows it in the player's language, an area's prefix
        /// before it where there is one ("Dunkler Sumpf"), so a biome counts where its shown name
        /// stands in an area as whole words; an area kept by its name token counts too.
        /// </summary>
        private List<Entry> BiomesEntered(CompendiumRecords records)
        {
            var entered = Named(records.Biomes.SelectMany(area => area.Split(' ')), Kind.Biome).ToList();
            if (_biomesShown == null) return entered;
            foreach (var biome in _catalog.All)
            {
                if (biome.Kind != Kind.Biome || entered.Contains(biome) || !_biomesShown.TryGetValue(biome.Name, out var shown) || string.IsNullOrEmpty(shown)) continue;
                if (records.Biomes.Any(area => (" " + area + " ").IndexOf(" " + shown + " ", StringComparison.OrdinalIgnoreCase) >= 0)) entered.Add(biome);
            }
            return entered;
        }

        /// <summary>The resources giving an item that grow in a biome the character entered; one growing nowhere the world places it is left to be met.</summary>
        private static IEnumerable<Entry> ResourcesGiving(Entry item, HashSet<string> entered) =>
            item.TermLinks("from").Where(e => e.Kind == Kind.Resource && e.Biomes.Any(entered.Contains));

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

        private static int Raise(CompendiumBook book, IEnumerable<Entry> entries, KnownLevel level, bool quietly) =>
            entries.Count(e => book.Raise(e.Key, level, quietly));
    }
}
