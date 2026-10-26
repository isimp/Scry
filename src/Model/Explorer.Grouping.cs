using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>The ways a kind's tab groups its list (<see cref="ListGrouping"/>), each kind keeping its own.</summary>
    internal sealed partial class Explorer
    {
        private readonly Dictionary<Kind, GroupLens> _lenses = new Dictionary<Kind, GroupLens>();

        /// <summary>The ways the tab shown can group its list; its own alone outside a kind's tab.</summary>
        public IReadOnlyList<GroupLens> Lenses => KindFilter is Kind kind ? ListGrouping.Lenses(kind) : ListGrouping.Lenses(Kind.Other);

        /// <summary>The way the tab shown groups its list, each kind its own; set, the list is grouped again.</summary>
        public GroupLens Lens
        {
            get => KindFilter is Kind kind && _lenses.TryGetValue(kind, out var lens) ? lens : GroupLens.Own;
            set
            {
                if (!(KindFilter is Kind kind) || Lens == value || !ListGrouping.Lenses(kind).Contains(value)) return;
                _lenses[kind] = value;
                Refresh();
            }
        }

        /// <summary>
        /// The group an entry is listed under in the way the tab groups: its own, its biome or what
        /// taming allows. By biome its own biomes, else those of what gives it, and under the
        /// strict rules only those it was met in.
        /// </summary>
        public ListGroup ListGroupOf(Entry entry)
        {
            // Its own group is read as it is now, as leftovers join their owners'.
            if (Lens == GroupLens.Own) return GroupNow(entry);
            // The list asks every frame: kept while the way, the tab and what is known stay.
            var stamp = (Lens, KindFilter, KnowStamp);
            if (!_groupedAs.Equals(stamp))
            {
                _groupedAs = stamp;
                _grouped.Clear();
            }
            if (_grouped.TryGetValue(entry, out var kept)) return kept;
            return _grouped[entry] = GroupNow(entry);
        }

        private readonly Dictionary<Entry, ListGroup> _grouped = new Dictionary<Entry, ListGroup>();
        private (GroupLens, Kind?, int) _groupedAs = (GroupLens.Own, null, -1);

        private ListGroup GroupNow(Entry entry)
        {
            switch (Lens)
            {
                case GroupLens.Taming: return ListGrouping.Taming(entry);
                case GroupLens.Biome:
                    var strict = CompendiumOn && _view.Rule == CompendiumRule.Strict;
                    var biomes = entry.Biomes.Length > 0 ? entry.Biomes : entry.GiverBiomes;
                    if (strict) biomes = biomes.Where(b => _view.Book.Witnessed(entry.Key, EntryKeys.For(Kind.Biome, b), Tie.Where)).ToArray();
                    return ListGrouping.Biome(biomes, BiomeShown, strict ? ListGrouping.NotMetInABiome : ListGrouping.NoBiome);
                default: return new ListGroup { Name = entry.Group, Order = entry.GroupOrder };
            }
        }

        /// <summary>A biome by the name its page goes by, else its own words.</summary>
        private string BiomeShown(string biome) => Find(EntryKeys.For(Kind.Biome, biome))?.ShownName ?? Naming.FieldLabel(biome);

        /// <summary>
        /// The results by their groups in the way the tab groups, the groups in their order and
        /// then by name, within a group by rank (<see cref="Entry.GroupRank"/>) and then in the
        /// order the search gave them. Sorted as numbers: a group's place, a rank and each
        /// result's place in one, so a tab of thousands is sorted with no call per comparison.
        /// </summary>
        private List<Entry> ByGroup(List<Entry> results)
        {
            var groups = results.Select(ListGroupOf).ToArray();
            var orders = groups.Select(g => g.Order).Distinct().OrderBy(o => o).ToList();
            var names = groups.Select(g => g.Name ?? "").Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
            var orderRank = new Dictionary<int, int>();
            for (var i = 0; i < orders.Count; i++) orderRank[orders[i]] = i;
            var nameRank = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < names.Count; i++) nameRank[names[i]] = i;

            var keys = new long[results.Count];
            var entries = results.ToArray();
            for (var i = 0; i < entries.Length; i++)
            {
                var rank = Math.Max(0, Math.Min(entries[i].GroupRank, 63));
                keys[i] = ((long)orderRank[groups[i].Order] << 48) | ((long)nameRank[groups[i].Name ?? ""] << 27) | ((long)rank << 21) | (long)i;
            }
            Array.Sort(keys, entries);
            return new List<Entry>(entries);
        }
    }
}
