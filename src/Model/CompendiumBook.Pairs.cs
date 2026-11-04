using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>The book's pairs and proofs: what the character saw go together, and what it saw done, kept apart.</summary>
    internal sealed partial class CompendiumBook
    {
        /// <summary>
        /// What was seen go together, either way round, with the ways it was seen together as
        /// bits of <see cref="Tie"/> (<see cref="SawWith"/>); every bit set where no way was named
        /// (<see cref="Witness"/>, or a pair kept before ways were), which answers for any way.
        /// </summary>
        private readonly Dictionary<(string, string), int> _pairs = new Dictionary<(string, string), int>();

        /// <summary>A pair seen together with no way named: every way.</summary>
        private const int AnyWay = -1;

        private static int Bit(Tie tie) => 1 << (int)tie;

        /// <summary>What was seen done to a thing, by the thing and the proof (<see cref="Prove"/>): a deed, a damage type told, a weak spot found, an attack taken.</summary>
        private readonly HashSet<(string, string)> _proofs = new HashSet<(string, string)>();

        /// <summary>
        /// Whether this book was kept before attacks taken had a key of their own, when one was the
        /// creature paired with its weapon's prefab: such a pair still counts as that proof, as
        /// nothing can tell it from any other pair. Kept with the book, as those pairs stay.
        /// </summary>
        private bool _attacksAsPairs;

        /// <summary>The book's format as saved since proofs had lines of their own (a <c>format</c> line); a book without one was kept before.</summary>
        public const int Format = 2;

        /// <summary>Whether the text read said its format.</summary>
        private bool _formatRead;

        /// <summary>
        /// Notes that the character saw something done to a thing, by its proof: a deed
        /// (<see cref="EntryKeys.Did"/>), a damage type a hit told (<see cref="EntryKeys.Damage"/>),
        /// a weak spot found (<see cref="EntryKeys.WeakSpot"/>), an attack taken
        /// (<see cref="EntryKeys.Attack"/>). Kept apart from what was seen go together. True when it was not proved before.
        /// </summary>
        public bool Prove(string key, string proof)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(proof) || !_proofs.Add((key, proof))) return false;
            PairsVersion++;
            Changed = true;
            return true;
        }

        /// <summary>Whether the character saw something done to a thing (<see cref="Prove"/>); an attack taken, in a book kept before attacks had a key, also as the creature paired with its weapon.</summary>
        public bool Proved(string key, string proof)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(proof)) return false;
            if (_proofs.Contains((key, proof))) return true;
            if (TwinsOf != null)
            {
                foreach (var twin in TwinsOf(key)) if (_proofs.Contains((twin, proof))) return true;
            }
            var attack = EntryKeys.IsAttack(proof, out var item);
            if (_attacksAsPairs && attack && _pairs.ContainsKey(Pair(key, item))) return true;
            // An attack seen of one kind of a family is each kind's that makes it: they go by one
            // name and look alike, so a Dvergr mage's ice bolt is its ice kind's. Its death's blast
            // is no item, and may differ from kind to kind.
            if (!attack || KindsOf == null || EntryKeys.IsDeathAttack(item)) return false;
            foreach (var kind in KindsOf(key))
            {
                if (_proofs.Contains((kind, proof)) || (_attacksAsPairs && _pairs.ContainsKey(Pair(kind, item)))) return true;
            }
            return false;
        }

        /// <summary>
        /// The other kinds of a creature's family, by key (<see cref="Families.KindsOf"/>): an
        /// attack seen of one is each one's (<see cref="Proved"/>). Not kept; none where not told,
        /// and none for a key of no family.
        /// </summary>
        public Func<string, IReadOnlyList<string>> KindsOf { get; set; }

        private static readonly string CaughtProof = EntryKeys.Did(Deed.Caught);

        /// <summary>
        /// A catch has always paired the fish with the bait it took (<see cref="Tie.Eats"/>), so a
        /// book kept before catches were noted as such holds them only so: each fish paired that way
        /// with a bait it bites, by their keys, was caught. A tame creature eating a fish pairs them
        /// the same way, which is no catch. How many catches it proved anew.
        /// </summary>
        public int ProveCaughtByBaits(Func<string, string, bool> bitesOn)
        {
            if (bitesOn == null) return 0;
            var caught = new List<string>();
            foreach (var pair in _pairs)
            {
                if (pair.Value == AnyWay || (pair.Value & Bit(Tie.Eats)) == 0) continue;
                if (bitesOn(pair.Key.Item1, pair.Key.Item2)) caught.Add(pair.Key.Item1);
                if (bitesOn(pair.Key.Item2, pair.Key.Item1)) caught.Add(pair.Key.Item2);
            }
            var proved = 0;
            foreach (var fish in caught) if (Prove(fish, CaughtProof)) proved++;
            return proved;
        }

        /// <summary>How many proofs the character holds.</summary>
        [ForTest]
        public int Proofs => _proofs.Count;

        /// <summary>What was seen of each creature up close, by its key: the stars it had and what it carried, as tokens (<see cref="Sighted"/>).</summary>
        private readonly Dictionary<string, HashSet<string>> _sightings = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        private const string StarsSeenToken = "stars:";
        private const string WearsToken = "wears:";

        /// <summary>
        /// The character saw a creature up close, as the game shows one: with so many
        /// <paramref name="stars"/> (none is a sighting too) and carrying <paramref name="items"/>,
        /// by their prefabs. Kept apart from its level and its pairs: what the stage may show of it
        /// under the strict rules. True when any of it was new.
        /// </summary>
        public bool Sighted(string key, int stars, IEnumerable<string> items)
        {
            if (string.IsNullOrEmpty(key)) return false;
            var added = false;
            if (stars >= 0) added |= Sight(key, StarsSeenToken + Stored.Count(stars));
            foreach (var item in items ?? Array.Empty<string>()) if (!string.IsNullOrEmpty(item)) added |= Sight(key, WearsToken + item);
            if (!added) return false;
            PairsVersion++;
            Changed = true;
            return true;
        }

        private bool Sight(string key, string token)
        {
            if (!_sightings.TryGetValue(key, out var seen)) _sightings[key] = seen = new HashSet<string>(StringComparer.Ordinal);
            return seen.Add(token);
        }

        /// <summary>The stars a creature was seen with up close, each once; none for one never seen so.</summary>
        public IEnumerable<int> StarsSeen(string key)
        {
            if (string.IsNullOrEmpty(key) || !_sightings.TryGetValue(key, out var seen)) yield break;
            foreach (var token in seen)
            {
                if (token.StartsWith(StarsSeenToken, StringComparison.Ordinal) && Stored.TryCount(token.Substring(StarsSeenToken.Length), out var stars)) yield return stars;
            }
        }

        /// <summary>The items a creature was seen carrying up close, by their prefabs, each once; none for one never seen so.</summary>
        public IEnumerable<string> WornSeen(string key)
        {
            if (string.IsNullOrEmpty(key) || !_sightings.TryGetValue(key, out var seen)) yield break;
            foreach (var token in seen) if (token.StartsWith(WearsToken, StringComparison.Ordinal)) yield return token.Substring(WearsToken.Length);
        }

        /// <summary>The items a creature was seen attack with, by their prefabs (<see cref="Proved"/> with <see cref="EntryKeys.Attack"/>), each once; none for one never seen so. Its death, an attack where it hurts (<see cref="EntryKeys.DeathAttack"/>), is no item.</summary>
        public IEnumerable<string> AttackedWith(string key)
        {
            if (string.IsNullOrEmpty(key)) yield break;
            foreach (var (proved, proof) in _proofs) if (proved == key && EntryKeys.IsAttack(proof, out var item) && !EntryKeys.IsDeathAttack(item)) yield return item;
        }

        /// <summary>Every sighting as its lines are kept, for the book's text.</summary>
        private IEnumerable<(string Key, string Token)> SightingLines()
        {
            foreach (var pair in _sightings) foreach (var token in pair.Value) yield return (pair.Key, token);
        }

        /// <summary>A sighting's line read back: a stars or wears token of a creature's key.</summary>
        private void ReadSighting(string key, string token)
        {
            if (token.StartsWith(StarsSeenToken, StringComparison.Ordinal))
            {
                if (Stored.TryCount(token.Substring(StarsSeenToken.Length), out _)) Sight(key, token);
            }
            else if (token.StartsWith(WearsToken, StringComparison.Ordinal) && token.Length > WearsToken.Length) Sight(key, token);
        }

        /// <summary>
        /// Notes that the character saw two entries go together, either way round: a drop fall
        /// from a creature, a creature in a biome or a place, an item made at a station, a chest's
        /// loot, a creature's attack hit it. True when it was not noted before. What the strict
        /// rules show of how a known entry connects.
        /// </summary>
        public bool Witness(string a, string b) => Saw(a, b, AnyWay);

        /// <summary>
        /// Notes that the character saw two entries go together one way (<see cref="Tie"/>): a drop
        /// fall from a creature, an item given by a tree, held in a chest, met in a biome. True when
        /// the pair or the way was not noted before. A row with a way of its own asks by it
        /// (<see cref="Witnessed(string, string, Tie)"/>).
        /// </summary>
        public bool SawWith(string a, string b, Tie tie) => Saw(a, b, Bit(tie));

        private bool Saw(string a, string b, int ways)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b) return false;
            var pair = Pair(a, b);
            _pairs.TryGetValue(pair, out var had);
            if ((had | ways) == had && _pairs.ContainsKey(pair)) return false;
            _pairs[pair] = had | ways;
            PairsVersion++;
            Changed = true;
            return true;
        }

        /// <summary>Whether the character saw two entries go together, either way round, any way.</summary>
        public bool Witnessed(string a, string b) => !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && (_pairs.ContainsKey(Pair(a, b)) || TwinPairs(a, b).Any(p => _pairs.ContainsKey(p)));

        /// <summary>Whether the character saw two entries go together one way, or with no way named.</summary>
        public bool Witnessed(string a, string b, Tie tie) =>
            !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && (HasWay(Pair(a, b), tie) || TwinPairs(a, b).Any(p => HasWay(p, tie)));

        private bool HasWay((string, string) pair, Tie tie) => _pairs.TryGetValue(pair, out var ways) && (ways & Bit(tie)) != 0;

        /// <summary>
        /// Each thing's twins, by key (<see cref="Twins"/>): what was found out or seen together of
        /// one holds for each, read whenever a page asks, so finds from before count too. Not kept; none where
        /// not told.
        /// </summary>
        public Func<string, IReadOnlyList<string>> TwinsOf { get; set; }

        /// <summary>The pairs a pair of two things stands for through either's twins.</summary>
        private IEnumerable<(string, string)> TwinPairs(string a, string b)
        {
            if (TwinsOf == null) yield break;
            foreach (var twin in TwinsOf(a)) yield return Pair(twin, b);
            foreach (var twin in TwinsOf(b)) yield return Pair(a, twin);
        }

        /// <summary>The ways a pair was seen together as a line keeps them, "drops,gives"; none for any way.</summary>
        private static string WaysKept(int ways)
        {
            if (ways == AnyWay) return null;
            var named = new List<string>();
            foreach (Tie tie in Enum.GetValues(typeof(Tie))) if ((ways & Bit(tie)) != 0) named.Add(TieWords.Kept(tie));
            return string.Join(",", named);
        }

        /// <summary>The ways a line keeps read back: any way where none is named, or none this Scry knows.</summary>
        private static int WaysRead(string kept)
        {
            if (string.IsNullOrEmpty(kept)) return AnyWay;
            var ways = 0;
            foreach (var word in kept.Split(',')) if (TieWords.Read(word) is Tie tie) ways |= Bit(tie);
            return ways == 0 ? AnyWay : ways;
        }

        /// <summary>A pair read back from its own line: kept any way where none was named before.</summary>
        private void ReadPair(string a, string b)
        {
            var pair = Pair(a, b);
            if (!_pairs.ContainsKey(pair)) _pairs[pair] = AnyWay;
        }

        /// <summary>The ways of a pair read back from their line, which names them where the pair's own line does not.</summary>
        private void ReadWays(string a, string b, string kept)
        {
            var pair = Pair(a, b);
            _pairs.TryGetValue(pair, out var had);
            _pairs[pair] = (had == AnyWay ? 0 : had) | WaysRead(kept);
        }

        /// <summary>How many pairs the character saw go together.</summary>
        [ForTest]
        public int Pairs => _pairs.Count;

        private static (string, string) Pair(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

        /// <summary>Whether a key is a proof of something seen done rather than a thing: a deed, a damage type, a weak spot, an attack taken.</summary>
        private static bool IsProof(string key) => EntryKeys.IsDeed(key, out _) || EntryKeys.IsDamage(key, out _) || EntryKeys.IsWeakSpot(key) || EntryKeys.IsAttack(key, out _);
    }
}
