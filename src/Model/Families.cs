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

        /// <summary>The name it goes by: the game's own for resources named alike ("Petrified Bone"), else its head's, a place's without the number it ends with ("Tar pit").</summary>
        public string Name => Called ?? (Numbered ? Families.WithoutNumber(Head.ShownName) : Head.ShownName);

        /// <summary>The name the game shows its resources by, where they are one by it; none for any other family.</summary>
        internal string Called;

        /// <summary>Whether its kinds are told by the numbers their names end with, a place's.</summary>
        internal bool Numbered;

        /// <summary>The kind it goes by first.</summary>
        public Entry Head => Kinds[0];

        /// <summary>The kind a copy is of; none for a kind.</summary>
        public Entry CopyOf(Entry member) => member != null && _copyOf.TryGetValue(member, out var original) ? original : null;

        private readonly Dictionary<Entry, (Entry Base, int Look)> _wornAs = new Dictionary<Entry, (Entry, int)>();

        /// <summary>
        /// The kind another kind is no more than a look of, and that look (<see cref="Families"/>):
        /// a kind the world places nowhere, given one of the sets the other rolls, which is met only
        /// as the other in that set. None for any other member.
        /// </summary>
        public (Entry Base, int Look) WornAs(Entry member) => member != null && _wornAs.TryGetValue(member, out var worn) ? worn : (null, 0);

        /// <summary>Its kinds the page tells apart: every one but those no more than another's look (<see cref="WornAs"/>).</summary>
        public IEnumerable<Entry> KindsShown => Kinds.Where(k => !_wornAs.ContainsKey(k));

        internal void AddLook(Entry kind, Entry wornBy, int look) => _wornAs[kind] = (wornBy, look);

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
        /// a copy and in the family's order among equals; none where it met none. A kind no more
        /// than another's look (<see cref="WornAs"/>) only where the character met no other member.
        /// With no compendium (<paramref name="level"/> none), the head.
        /// </summary>
        public Entry Lead(Func<Entry, KnownLevel> level)
        {
            if (level == null) return Head;
            return Lead(level, false) ?? Lead(level, true);
        }

        private Entry Lead(Func<Entry, KnownLevel> level, bool looks)
        {
            Entry best = null;
            var bestLevel = KnownLevel.Unknown;
            var bestIsCopy = true;
            foreach (var member in Members)
            {
                if (_wornAs.ContainsKey(member) != looks) continue;
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

        /// <summary>The members the page switches among: those the character met, seen or known, every one with no compendium; none that is no more than another's look (<see cref="WornAs"/>), which that one's look row shows.</summary>
        public List<Entry> Met(Func<Entry, KnownLevel> level) =>
            Members.Where(m => !_wornAs.ContainsKey(m) && (level == null || level(m) >= KnownLevel.Seen)).ToList();
    }

    /// <summary>Finds the families among the catalog's entries (<see cref="Family"/>), each member told of its own.</summary>
    internal static class Families
    {
        /// <summary>
        /// The other kinds of each creature's family, by key, as the catalog's families stand
        /// (<see cref="Of"/>): what the book holds of one kind's attacks holds for each
        /// (<see cref="CompendiumBook.KindsOf"/>). Read once into a map that is only read after, so
        /// any thread may ask; none for a key of no family.
        /// </summary>
        public static Func<string, IReadOnlyList<string>> KindsOf(EntryCatalog catalog)
        {
            var kinds = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var entry in catalog?.All ?? (IEnumerable<Entry>)Array.Empty<Entry>())
            {
                var family = entry.Family;
                if (entry.Kind != Kind.Creature || family == null || family.Kinds.Count < 2 || !family.Kinds.Contains(entry)) continue;
                kinds[entry.Key] = family.Kinds.Where(k => k != entry).Select(k => k.Key).ToArray();
            }
            return key => key != null && kinds.TryGetValue(key, out var others) ? others : Array.Empty<string>();
        }

        /// <summary>The families the entries make; every entry's own family set, none where it is in none.</summary>
        public static List<Family> Of(IReadOnlyList<Entry> entries)
        {
            var found = new List<Family>();
            foreach (var entry in entries) entry.Family = null;
            var byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in entries) byKey[entry.Key] = entry;
            // Passed along, never kept: the search finds families on a worker thread, and two at once would name each other's copies.
            Func<string, string> named = key => byKey.TryGetValue(key, out var e) ? e.ShownName : null;
            found.AddRange(ItemFamilies(entries.Where(e => e.Kind == Kind.Item), named));
            found.AddRange(CreatureFamilies(entries.Where(e => e.Kind == Kind.Creature), named));
            found.AddRange(PlaceFamilies(entries.Where(e => e.Kind == Kind.Location && !e.IsRoom)));
            found.AddRange(ResourceFamilies(entries.Where(e => e.Kind == Kind.Resource).ToList()));
            foreach (var family in found) family.Seal();
            return found;
        }

        private static string TokenOf(Entry entry) => entry.Tokens.Length > 0 ? entry.Tokens[0] : null;

        /// <summary>An item made by a recipe and its copies of its name, made by none and named after it, for creatures to wear (FW_, SP_).</summary>
        /// <param name="items">The items to look among.</param>
        /// <param name="named">An entry's shown name by its key, among those the families are found in.</param>
        private static IEnumerable<Family> ItemFamilies(IEnumerable<Entry> items, Func<string, string> named)
        {
            foreach (var alike in items.Where(e => TokenOf(e) != null).GroupBy(TokenOf))
            {
                var made = alike.Where(e => e.Stations.Length > 0).ToList();
                if (made.Count != 1) continue;
                var original = made[0];
                var copies = alike.Where(e => e != original && e.Stations.Length == 0 && e.Name.Length > original.Name.Length
                    && e.Name.IndexOf(original.Name, StringComparison.Ordinal) >= 0).OrderBy(e => e.Name, StringComparer.Ordinal).ToList();
                if (copies.Count == 0) continue;
                var family = new Family();
                family.AddKind(original, original.ShownName);
                foreach (var copy in copies) family.AddCopy(copy, original, CopyLabel(copy, original, named), CopyWords(copy, original));
                yield return family;
            }
        }

        /// <summary>A copy's words: who carries it (<see cref="LinkWords.CarriedBy"/>), else the words its prefab adds to its original's, as they are written.</summary>
        private static string CopyLabel(Entry copy, Entry original, Func<string, string> named)
        {
            var carriers = copy.Links.Where(l => l.Group == LinkWords.CarriedBy).Select(l => named(l.Target)).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();
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
        /// <param name="creatures">The creatures to look among.</param>
        /// <param name="named">An entry's shown name by its key, among those the families are found in.</param>
        private static IEnumerable<Family> CreatureFamilies(IEnumerable<Entry> creatures, Func<string, string> named)
        {
            foreach (var alike in creatures.Where(e => TokenOf(e) != null).GroupBy(TokenOf))
            {
                var members = alike.OrderBy(e => e.Name.Length).ThenBy(e => e.Name, StringComparer.Ordinal).ToList();
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
                foreach (var (copy, original) in copies) family.AddCopy(copy, original, CopyLabel(copy, original, named), CopyWords(copy, original));
                FoldLooks(family);
                yield return family;
            }
        }

        /// <summary>
        /// Tells the family which of its kinds are no more than another's look (<see cref="Family.WornAs"/>):
        /// one the world places nowhere, wearing all it has as one set of a kind the world places
        /// and that rolls several, as the Dvergr mage's fire, ice and support kinds are the mage in
        /// each of its robes.
        /// </summary>
        private static void FoldLooks(Family family)
        {
            foreach (var kind in family.Kinds)
            {
                if (Placed(kind)) continue;
                var worn = SoleGear(kind);
                if (worn == null) continue;
                foreach (var wearer in family.Kinds)
                {
                    if (wearer == kind || wearer.GearSets.Length < 2 || !Placed(wearer)) continue;
                    var look = Array.FindIndex(wearer.GearSets, set => worn.SetEquals(set.Concat(wearer.GivenGear)));
                    if (look < 0) continue;
                    family.AddLook(kind, wearer, look + 1);
                    break;
                }
            }
        }

        /// <summary>Whether the world places a creature where Scry sees: a biome spawns it, a place holds it, or something spawns it.</summary>
        private static bool Placed(Entry creature) =>
            creature.Biomes.Length > 0 || creature.FoundIn.Length > 0 || creature.Links.Any(l => l.Group == LinkWords.SpawnedBy);

        /// <summary>All a creature wears when it rolls no set or a single one: that set with what it is always given; none where it rolls several, or wears nothing.</summary>
        private static HashSet<string> SoleGear(Entry creature)
        {
            if (creature.GearSets.Length > 1) return null;
            var worn = new HashSet<string>(creature.GivenGear, StringComparer.Ordinal);
            if (creature.GearSets.Length == 1) worn.UnionWith(creature.GearSets[0]);
            return worn.Count > 0 ? worn : null;
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
                var stem = told ? 0 : SharedStem(members.Select(m => m.Name).ToList());
                for (var i = 0; i < members.Count; i++) family.AddKind(members[i], told ? numbers[i].TrimStart('0') is var n && n.Length > 0 ? n : "0" : AfterStem(members[i].Name, stem));
                yield return family;
            }
        }

        /// <summary>
        /// Resources the game names alike when looked at (<see cref="Entry.HoverName"/>) whose spots
        /// the map keeps as their own, two or more: one row by that name, as Petrified Bone's ribs and
        /// skull, each kind told by the number its prefab ends with or the words after the start the
        /// names share, as a place's kinds are; each one's broken form, whose spots the map keeps as
        /// it, its copy. Trees, logs and what the map marks not are left as they are.
        /// </summary>
        private static IEnumerable<Family> ResourceFamilies(IReadOnlyList<Entry> resources)
        {
            var wholes = resources.Where(e => !string.IsNullOrEmpty(e.HoverName) && e.SpotKind == e.Name);
            foreach (var alike in wholes.GroupBy(e => e.HoverName, StringComparer.Ordinal))
            {
                var kinds = alike.OrderBy(e => e.Name, NumberOrder.Instance).ToList();
                if (kinds.Count < 2) continue;
                var family = new Family { Called = kinds[0].HoverShownName ?? kinds[0].ShownName };
                var labels = KindLabels(kinds.Select(k => k.Name).ToList());
                for (var i = 0; i < kinds.Count; i++)
                {
                    family.AddKind(kinds[i], labels[i]);
                    foreach (var broken in resources.Where(e => e != kinds[i] && e.SpotKind == kinds[i].Name).OrderBy(e => e.Name, StringComparer.Ordinal))
                        family.AddCopy(broken, kinds[i], labels[i] + ", broken", "broken");
                }
                yield return family;
            }
        }

        /// <summary>
        /// The words telling resources named alike apart: the numbers their prefabs end with, one with
        /// none beside numbered ones the first ("mudpile", "mudpile2"); else the words after the start
        /// their names share ("Ribs" and "Skull" of giant_ribs and giant_skull), or the whole name where none follow.
        /// </summary>
        private static List<string> KindLabels(IReadOnlyList<string> names)
        {
            var numbers = names.Select(Number).ToList();
            var bare = numbers.Count(n => n.Length == 0);
            var numbered = numbers.Where(n => n.Length > 0).ToList();
            if (bare <= 1 && numbered.Distinct().Count() == numbered.Count && !(bare == 1 && numbered.Contains("1")))
                return numbers.Select(n => n.Length > 0 ? n.TrimStart('0') is var t && t.Length > 0 ? t : "0" : "1").ToList();
            var stem = SharedStem(names);
            return names.Select(n => Naming.Capital(Naming.Readable(n.Substring(stem).Trim('_') is var rest && rest.Length > 0 ? rest : n))).ToList();
        }

        /// <summary>How long the start all the names share is, but for the digits it ends with, which tell them apart: "TarPit" of TarPit1 and TarPit1_1.</summary>
        private static int SharedStem(IReadOnlyList<string> names)
        {
            var length = names.Min(n => n.Length);
            for (var i = 0; i < length; i++)
            {
                if (names.All(n => n[i] == names[0][i])) continue;
                length = i;
                break;
            }
            while (length > 0 && char.IsDigit(names[0][length - 1])) length--;
            return length;
        }

        /// <summary>What follows a name's shared stem, its underscores as dashes: "1-1" of TarPit1_1; the name itself where nothing follows.</summary>
        private static string AfterStem(string name, int stem)
        {
            var rest = name.Substring(stem).Trim('_').Replace('_', '-');
            return rest.Length > 0 ? rest : name;
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
