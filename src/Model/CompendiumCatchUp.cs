using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Writes into what a character knows (<see cref="CompendiumBook"/>) what the game's own
    /// records of it tell (<see cref="CompendiumRecords"/>), so a character played before does
    /// not start from nothing. Read by the loose rules: a creature killed, an item picked up,
    /// crafted or eaten, a piece unlocked, used or placed and a biome entered are known, as are a
    /// trophy held and the creatures it falls from, every resource giving an item so known, and
    /// every status effect something so known gives (a bed's Rested, a biome's weather), paired
    /// with its givers among them; an item only had or offered is seen, and one whose recipe is
    /// unlocked is heard of. A
    /// record counts only for the kinds it is about, and for every entry going by its name
    /// (<see cref="Entry.Tokens"/>). For the strict rules it pairs what the records show went
    /// together: a recipe unlocked, an item crafted or a piece placed with its ingredients and
    /// station, a trophy with the creatures it falls from, a thing picked or picked up with the
    /// resources giving it, a food eaten with the status effects it gives.
    /// </summary>
    internal sealed class CompendiumCatchUp
    {
        private readonly EntryCatalog _catalog;
        private readonly Dictionary<string, List<Entry>> _byToken = new Dictionary<string, List<Entry>>(StringComparer.Ordinal);

        public CompendiumCatchUp(EntryCatalog catalog)
        {
            _catalog = catalog;
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
            var items = Named(records.PickedUp, Kind.Item).Concat(Named(records.Crafted, Kind.Item)).Concat(Named(records.Eaten, Kind.Item)).ToList();
            items.AddRange(Prefabs(records.Trophies.Concat(records.Picked), Kind.Item));

            var known = new List<Entry>(items);
            known.AddRange(Named(records.Killed, Kind.Creature));
            foreach (var trophy in Prefabs(records.Trophies, Kind.Item)) known.AddRange(trophy.TermLinks("from").Where(e => e.Kind == Kind.Creature));
            foreach (var item in items) known.AddRange(item.TermLinks("from").Where(e => e.Kind == Kind.Resource));
            known.AddRange(Named(records.Recipes.Concat(records.Stations).Concat(records.Placed), Kind.Piece));
            known.AddRange(Named(records.Biomes.SelectMany(area => area.Split(' ')), Kind.Biome));
            var givers = Givers(known);
            known.AddRange(givers.Keys);

            var raised = Raise(book, known, KnownLevel.Known, quietly)
                + Raise(book, Named(records.Materials, Kind.Item), KnownLevel.Seen, quietly)
                + Raise(book, Named(records.Recipes, Kind.Item), KnownLevel.HeardOf, quietly);
            Pair(records, book);
            foreach (var given in givers)
            {
                foreach (var giver in given.Value) book.Witness(given.Key.Key, giver);
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
        private void Pair(CompendiumRecords records, CompendiumBook book)
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
                foreach (var resource in item.TermLinks("from")) if (resource.Kind == Kind.Resource) book.Witness(item.Key, resource.Key);
            }
            foreach (var food in Named(records.Eaten, Kind.Item))
            {
                foreach (var link in food.Links) if (link.Group == LinkWords.StatusEffects) book.Witness(food.Key, link.Target);
            }
        }

        /// <summary>Learns of an entry met in play, by its key, as news; true when that is more than was known. What the catalog has no entry for is passed over.</summary>
        public bool Met(string key, KnownLevel level, CompendiumBook book) => _catalog.Find(key) != null && book.Raise(key, level);

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
