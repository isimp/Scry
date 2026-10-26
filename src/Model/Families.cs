using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Several prefabs that are one thing to a player, by the game's own naming: its kinds, which
    /// differ in what they are (a place's numbered kinds, a creature's kinds with other attacks),
    /// and the copies of each kind, the same thing placed otherwise (an item's copies for creatures
    /// to wear, a creature placed asleep or without its chest). The list shows it by one row, the
    /// page switches among the members the character met.
    /// </summary>
    internal sealed class Family
    {
        private readonly Dictionary<Entry, Entry> _copyOf = new Dictionary<Entry, Entry>();
        private readonly Dictionary<Entry, string> _labels = new Dictionary<Entry, string>();
        private readonly Dictionary<Entry, string> _plain = new Dictionary<Entry, string>();

        /// <summary>Its kinds, the head first, in the order they are named.</summary>
        public readonly List<Entry> Kinds = new List<Entry>();

        /// <summary>Every member, each kind followed by its copies.</summary>
        public readonly List<Entry> Members = new List<Entry>();

        /// <summary>The name it goes by: its head's, a place's without the number it ends with ("Tar pit").</summary>
        public string Name => Numbered ? Families.WithoutNumber(Head.ShownName) : Head.ShownName;

        /// <summary>Whether its kinds are told by the numbers their names end with, a place's.</summary>
        internal bool Numbered;

        /// <summary>The kind it goes by first.</summary>
        public Entry Head => Kinds[0];

        /// <summary>The kind a copy is of; none for a kind.</summary>
        public Entry CopyOf(Entry member) => member != null && _copyOf.TryGetValue(member, out var original) ? original : null;

        /// <summary>A member's words on the page's switch.</summary>
        public string Label(Entry member) => member != null && _labels.TryGetValue(member, out var label) ? label : member?.ShownName ?? "";

        /// <summary>A member's words naming no other thing: a copy's by the words its prefab adds, where its <see cref="Label"/> names who carries it; else its label.</summary>
        public string PlainLabel(Entry member) => member != null && _plain.TryGetValue(member, out var plain) ? plain : Label(member);

        internal void AddKind(Entry kind, string label)
        {
            Kinds.Add(kind);
            _labels[kind] = label;
        }

        internal void AddCopy(Entry copy, Entry original, string label, string plain)
        {
            _copyOf[copy] = original;
            _labels[copy] = label;
            _plain[copy] = plain;
        }

        /// <summary>Puts the members in order once all are in: each kind, then its copies.</summary>
        internal void Seal()
        {
            Members.Clear();
            foreach (var kind in Kinds)
            {
                Members.Add(kind);
                foreach (var pair in _copyOf) if (pair.Value == kind) Members.Add(pair.Key);
            }
            foreach (var member in Members) member.Family = this;
        }

        /// <summary>
        /// The member the list shows the family by: the one the character knows best, a kind before
        /// a copy and in the family's order among equals; none where it met none. With no
        /// compendium (<paramref name="level"/> none), the head.
        /// </summary>
        public Entry Lead(Func<Entry, KnownLevel> level)
        {
            if (level == null) return Head;
            Entry best = null;
            var bestLevel = KnownLevel.Unknown;
            var bestIsCopy = true;
            foreach (var member in Members)
            {
                var known = level(member);
                if (known < KnownLevel.Seen) continue;
                var isCopy = _copyOf.ContainsKey(member);
                if (best == null || known > bestLevel || (known == bestLevel && bestIsCopy && !isCopy))
                {
                    best = member;
                    bestLevel = known;
                    bestIsCopy = isCopy;
                }
            }
            return best;
        }

        /// <summary>The members the page switches among: those the character met, seen or known; every one with no compendium.</summary>
        public List<Entry> Met(Func<Entry, KnownLevel> level) =>
            level == null ? new List<Entry>(Members) : Members.Where(m => level(m) >= KnownLevel.Seen).ToList();
    }

    /// <summary>Finds the families among the catalog's entries (<see cref="Family"/>), each member told of its own.</summary>
    internal static class Families
    {
        /// <summary>The families the entries make; every entry's own family set, none where it is in none.</summary>
        public static List<Family> Of(IReadOnlyList<Entry> entries)
        {
            var found = new List<Family>();
            foreach (var entry in entries) entry.Family = null;
            var byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in entries) byKey[entry.Key] = entry;
            _named = key => byKey.TryGetValue(key, out var e) ? e.ShownName : null;
            found.AddRange(ItemFamilies(entries.Where(e => e.Kind == Kind.Item)));
            found.AddRange(CreatureFamilies(entries.Where(e => e.Kind == Kind.Creature)));
            found.AddRange(PlaceFamilies(entries.Where(e => e.Kind == Kind.Location && !e.IsRoom)));
            foreach (var family in found) family.Seal();
            return found;
        }

        /// <summary>An entry's shown name by its key, among those the families are found in.</summary>
        private static Func<string, string> _named = key => null;

        private static string TokenOf(Entry entry) => entry.Tokens.Length > 0 ? entry.Tokens[0] : null;

        /// <summary>An item made by a recipe and its copies of its name, made by none and named after it, for creatures to wear (FW_, SP_).</summary>
        private static IEnumerable<Family> ItemFamilies(IEnumerable<Entry> items)
        {
            foreach (var named in items.Where(e => TokenOf(e) != null).GroupBy(TokenOf))
            {
                var made = named.Where(e => e.Stations.Length > 0).ToList();
                if (made.Count != 1) continue;
                var original = made[0];
                var copies = named.Where(e => e != original && e.Stations.Length == 0 && e.Name.Length > original.Name.Length
                    && e.Name.IndexOf(original.Name, StringComparison.Ordinal) >= 0).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
                if (copies.Count == 0) continue;
                var family = new Family();
                family.AddKind(original, original.ShownName);
                foreach (var copy in copies) family.AddCopy(copy, original, CopyLabel(copy, original), CopyWords(copy, original));
                yield return family;
            }
        }

        /// <summary>A copy's words: who carries it (<see cref="LinkWords.CarriedBy"/>), else the words its prefab adds to its original's, as they are written.</summary>
        private static string CopyLabel(Entry copy, Entry original)
        {
            var carriers = copy.Links.Where(l => l.Group == LinkWords.CarriedBy).Select(l => _named(l.Target)).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
            if (carriers.Count > 0) return Naming.Joined(carriers);
            if (copy.TagNamesCarrier && !string.IsNullOrEmpty(copy.Tag)) return copy.Tag;
            return CopyWords(copy, original);
        }

        /// <summary>The words a copy's prefab adds to its original's, as they are written: "SP", "Sleeping".</summary>
        private static string CopyWords(Entry copy, Entry original)
        {
            var added = copy.Name.Replace(original.Name, " ").Trim(' ', '_');
            if (added.Length == 0) return copy.ShownName;
            return added.All(c => !char.IsLower(c)) ? added : Naming.Capital(Naming.Readable(added));
        }

        /// <summary>
        /// A creature's kinds, of its name and other attacks, and the copies of each, of its name
        /// and the same attacks, named after it with words of its own (placed asleep, without a chest).
        /// </summary>
        private static IEnumerable<Family> CreatureFamilies(IEnumerable<Entry> creatures)
        {
            foreach (var named in creatures.Where(e => TokenOf(e) != null).GroupBy(TokenOf))
            {
                var members = named.OrderBy(e => e.Name.Length).ThenBy(e => e.Name, StringComparer.Ordinal).ToList();
                if (members.Count < 2) continue;
                var family = new Family();
                var copies = new List<(Entry Copy, Entry Original)>();
                foreach (var member in members)
                {
                    // The longest kind it is named after and attacks as: its original.
                    var original = family.Kinds.Where(k => member.Name.StartsWith(k.Name + "_", StringComparison.Ordinal) && SameAttacks(k, member))
                        .OrderByDescending(k => k.Name.Length).FirstOrDefault();
                    if (original != null) copies.Add((member, original));
                    else family.AddKind(member, null);
                }
                var apart = family.Kinds.Count > 1 ? Naming.Apart(family.Kinds.Select(k => k.Name).ToList()) : null;
                var kinds = family.Kinds.ToList();
                family.Kinds.Clear();
                for (var i = 0; i < kinds.Count; i++)
                {
                    var words = apart != null ? apart[i] : "";
                    family.AddKind(kinds[i], words.Length > 0 ? Naming.Capital(words) : kinds[i].ShownName);
                }
                foreach (var (copy, original) in copies) family.AddCopy(copy, original, CopyLabel(copy, original), CopyWords(copy, original));
                yield return family;
            }
        }

        private static bool SameAttacks(Entry a, Entry b)
        {
            var one = new HashSet<string>(a.Attacks.Select(x => x.Item), StringComparer.Ordinal);
            return one.SetEquals(b.Attacks.Select(x => x.Item));
        }

        /// <summary>A place's kinds: the places going by one name but the number it ends with, in the order of their numbers.</summary>
        private static IEnumerable<Family> PlaceFamilies(IEnumerable<Entry> places)
        {
            foreach (var named in places.GroupBy(p => WithoutNumber(p.ShownName), StringComparer.OrdinalIgnoreCase))
            {
                var members = named.OrderBy(p => p.Name, NumberOrder.Instance).ToList();
                if (members.Count < 2) continue;
                var family = new Family { Numbered = true };
                var numbers = members.Select(m => Number(m.Name)).ToList();
                var told = numbers.All(n => n.Length > 0) && numbers.Distinct().Count() == numbers.Count;
                for (var i = 0; i < members.Count; i++) family.AddKind(members[i], told ? numbers[i].TrimStart('0') is var n && n.Length > 0 ? n : "0" : members[i].Name);
                yield return family;
            }
        }

        /// <summary>A name without the number it ends with: "Tar pit 1" as "Tar pit".</summary>
        internal static string WithoutNumber(string name)
        {
            var end = name.Length;
            while (end > 0 && char.IsDigit(name[end - 1])) end--;
            return name.Substring(0, end).TrimEnd();
        }

        /// <summary>The number a prefab's name ends with: "TarPit2" as "2"; nothing where it ends with none.</summary>
        private static string Number(string name)
        {
            var end = name.Length;
            while (end > 0 && char.IsDigit(name[end - 1])) end--;
            return name.Substring(end);
        }

        /// <summary>Names in order, the numbers they end with by their value: "TarPit2" before "TarPit10".</summary>
        private sealed class NumberOrder : IComparer<string>
        {
            public static readonly NumberOrder Instance = new NumberOrder();

            public int Compare(string a, string b)
            {
                var stemA = a.Substring(0, a.Length - Number(a).Length);
                var stemB = b.Substring(0, b.Length - Number(b).Length);
                var byStem = string.CompareOrdinal(stemA, stemB);
                if (byStem != 0) return byStem;
                Stored.TryCount(Number(a), out var na);
                Stored.TryCount(Number(b), out var nb);
                return na != nb ? na.CompareTo(nb) : string.CompareOrdinal(a, b);
            }
        }
    }
}
