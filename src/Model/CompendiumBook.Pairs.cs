using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>The book's pairs and proofs: what the character saw go together, and what it saw done, kept apart.</summary>
    internal sealed partial class CompendiumBook
    {
        /// <summary>What was seen go together, either way round (<see cref="Witness"/>).</summary>
        private readonly HashSet<(string, string)> _pairs = new HashSet<(string, string)>();

        /// <summary>What was seen done to a thing, by the thing and the proof (<see cref="Prove"/>): a deed, a damage type told, a weak spot found, an attack taken.</summary>
        private readonly HashSet<(string, string)> _proofs = new HashSet<(string, string)>();

        /// <summary>
        /// Whether this book was kept before attacks taken had a key of their own, when one was the
        /// creature paired with its weapon's prefab: such a pair still counts as that proof, as
        /// nothing can tell it from any other pair. Kept with the book, as those pairs stay.
        /// </summary>
        private bool _attacksAsPairs;

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
            return _attacksAsPairs && EntryKeys.IsAttack(proof, out var item) && _pairs.Contains(Pair(key, item));
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
        public bool Witness(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b) || a == b || !_pairs.Add(Pair(a, b))) return false;
            PairsVersion++;
            Changed = true;
            return true;
        }

        /// <summary>Whether the character saw two entries go together, either way round.</summary>
        public bool Witnessed(string a, string b) => !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && _pairs.Contains(Pair(a, b));

        /// <summary>How many pairs the character saw go together.</summary>
        [ForTest]
        public int Pairs => _pairs.Count;

        private static (string, string) Pair(string a, string b) => string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);

        /// <summary>Whether a key is a proof of something seen done rather than a thing: a deed, a damage type, a weak spot, an attack taken.</summary>
        private static bool IsProof(string key) => EntryKeys.IsDeed(key, out _) || EntryKeys.IsDamage(key, out _) || EntryKeys.IsWeakSpot(key) || EntryKeys.IsAttack(key, out _);
    }
}
