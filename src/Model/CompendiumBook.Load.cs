using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>How a saved book is read back: each kind of line, and what a book kept by an earlier version has put right as it is read.</summary>
    internal sealed partial class CompendiumBook
    {
        /// <summary>The names of the met list read so far while the book loads, so each is listed once; none where the book holds no met list (<see cref="MetFromLately"/>).</summary>
        private HashSet<string> _metRead;

        /// <summary>
        /// For a book kept before what was met was kept apart from what was found out: the met list
        /// in the order of the learned list, on the day of its news where that was a meeting, with
        /// no day where it was something found out.
        /// </summary>
        private void MetFromLately()
        {
            foreach (var key in _lately)
            {
                _metLately.Add(key);
                var proof = FoundOutOf(key);
                if ((proof == null || proof == EntryKeys.OnMap) && _newsDay.TryGetValue(key, out var day)) _metDay[key] = day;
            }
        }

        /// <summary>The names of the learned list read so far while the book loads, so each is listed once; none otherwise.</summary>
        private HashSet<string> _latelyRead;

        /// <summary>
        /// How each kind of a saved book's lines is read, by its first word: each takes the book,
        /// the line's parts and its name (its second part, unescaped), and passes over a line it
        /// cannot read. A first word no reader takes is passed over.
        /// </summary>
        private static readonly Dictionary<string, Action<CompendiumBook, string[], string>> LineReaders = new Dictionary<string, Action<CompendiumBook, string[], string>>(StringComparer.Ordinal)
        {
            ["opened"] = (book, parts, name) =>
            {
                if (parts.Length == 2 && !ChestByNumber(name)) book._opened.Add(name);
            },
            ["found"] = (book, parts, name) =>
            {
                if (parts.Length == 3 && Stored.TryUnescaped(parts[2], out var proof) && proof.Length > 0) book._found[name] = proof;
            },
            ["know"] = (book, parts, name) =>
            {
                if (parts.Length == 3 && Stored.TryCount(parts[2], out var level) && level > (int)KnownLevel.Unknown && level <= (int)KnownLevel.Known) book._levels[name] = (KnownLevel)level;
            },
            ["lately"] = (book, parts, name) =>
            {
                if ((parts.Length != 2 && parts.Length != 3) || !book._latelyRead.Add(name)) return;
                book._lately.Add(name);
                if (parts.Length == 3 && Stored.TryCount(parts[2], out var newsDay)) book._newsDay[name] = newsDay;
            },
            ["met"] = (book, parts, name) =>
            {
                if (book._metRead == null) book._metRead = new HashSet<string>(StringComparer.Ordinal);
                if ((parts.Length != 2 && parts.Length != 3) || !book._metRead.Add(name)) return;
                book._metLately.Add(name);
                if (parts.Length == 3 && Stored.TryCount(parts[2], out var metDay)) book._metDay[name] = metDay;
            },
            ["mapped"] = (book, parts, name) =>
            {
                if (parts.Length == 2) book._mapped.Add(name);
            },
            ["read"] = (book, parts, name) =>
            {
                if (parts.Length == 2) book._read.Add(name);
            },
            ["place"] = (book, parts, name) =>
            {
                if (parts.Length != 4 || !Enum.TryParse(parts[2], out WorldSource source) || !Enum.IsDefined(typeof(WorldSource), source)
                    || !Stored.TryUnescaped(parts[3], out var kind) || kind.Length == 0) return;
                if (!book._foundIn.TryGetValue((name, source), out var found)) book._foundIn[(name, source)] = found = new HashSet<string>(StringComparer.Ordinal);
                found.Add(kind);
            },
            ["shows"] = (book, parts, name) =>
            {
                if (parts.Length == 3 && Enum.TryParse(name, out WorldSource shown) && Enum.IsDefined(typeof(WorldSource), shown)
                    && Enum.TryParse(parts[2], out SourceLevel level) && Enum.IsDefined(typeof(SourceLevel), level) && WorldSources.Allows(shown, level)) book._shows[shown] = level;
            },
            ["marks"] = (book, parts, name) =>
            {
                if (parts.Length == 2 && Enum.TryParse(name, out WorldSource marked) && Enum.IsDefined(typeof(WorldSource), marked) && WorldSources.HasGround(marked)) book._marks.Add(marked);
            },
            ["welcomed"] = (book, parts, name) =>
            {
                if (parts.Length == 2) book._welcomed = true;
            },
            ["mapapart"] = (book, parts, name) =>
            {
                if (parts.Length == 2) book._mapApart = true;
            },
            ["ruled"] = (book, parts, name) =>
            {
                if (parts.Length == 3 && Stored.TryUnescaped(parts[2], out var ruled) && ruled.Length > 0) book._rulesTold[name] = ruled;
            },
            ["rule"] = (book, parts, name) =>
            {
                if (parts.Length == 2 && (name == nameof(CompendiumRule.Loose) || name == nameof(CompendiumRule.Strict)))
                    book._rule = name == nameof(CompendiumRule.Loose) ? CompendiumRule.Loose : CompendiumRule.Strict;
            },
            ["floor"] = (book, parts, name) =>
            {
                if (parts.Length == 2 && Stored.TryCount(parts[1], out var floor)) book._floor = Math.Max(0, Math.Min(Tiers.Last, floor));
            },
            ["saw"] = (book, parts, name) =>
            {
                if (parts.Length != 3 || !Stored.TryUnescaped(parts[2], out var other) || other.Length == 0 || other == name) return;
                // A book kept before held deeds, damage types and weak spots as pairs, either way round.
                if (IsProof(other)) book._proofs.Add((name, other));
                else if (IsProof(name)) book._proofs.Add((other, name));
                else book._pairs.Add(Pair(name, other));
            },
            ["did"] = (book, parts, name) =>
            {
                if (parts.Length == 3 && Stored.TryUnescaped(parts[2], out var proof) && proof.Length > 0) book._proofs.Add((name, proof));
            },
            ["attacksaspairs"] = (book, parts, name) =>
            {
                if (parts.Length == 2) book._attacksAsPairs = true;
            },
            ["sight"] = (book, parts, name) =>
            {
                if (parts.Length == 3 && Stored.TryUnescaped(parts[2], out var token) && token.Length > 0) book.ReadSighting(name, token);
            },
            ["came"] = (book, parts, name) =>
            {
                if (parts.Length >= 5 && Enum.TryParse(parts[2], out CameBy way) && Enum.IsDefined(typeof(CameBy), way) && Read(parts, 3, out var cameAt)) book._came[name] = (way, cameAt);
            },
            ["seen"] = (book, parts, name) =>
            {
                if (parts.Length == 8 && Stored.TryCount(parts[2], out var stars) && Read(parts, 3, out var seenAt)) book._seen[name] = (seenAt, stars);
            },
            ["died"] = (book, parts, name) =>
            {
                if (parts.Length == 4 && Stored.TryCount(parts[2], out var deaths) && deaths > 0 && Stored.TryCount(parts[3], out var lastDay)) book._deaths[name] = (deaths, lastDay);
            },
        };

        /// <summary>
        /// Drops the news of pairs a book kept before held (<see cref="EntryKeys.With"/>), which took
        /// a thing to the top of Learned each time it first stood somewhere: the thing goes back to
        /// where it was met in the list, on the day it was met, and one never met, known before
        /// meetings were kept, leaves the list.
        /// </summary>
        private void DropPairNews()
        {
            var dropped = _found.Where(p => EntryKeys.IsWith(p.Value, out _)).Select(p => p.Key).ToList();
            if (dropped.Count == 0) return;
            foreach (var key in dropped)
            {
                _found.Remove(key);
                _lately.Remove(key);
                _learnedAt.Remove(key);
                _newsDay.Remove(key);
            }
            var metAt = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < _metLately.Count; i++) metAt[_metLately[i]] = i;
            foreach (var key in dropped)
            {
                if (!metAt.TryGetValue(key, out var met)) continue;
                // Back among what was met before it: before the first listed thing met after it.
                var at = _lately.Count;
                for (var i = 0; i < _lately.Count; i++)
                {
                    if (metAt.TryGetValue(_lately[i], out var other) && other > met)
                    {
                        at = i;
                        break;
                    }
                }
                _lately.Insert(at, key);
                if (_metDay.TryGetValue(key, out var day)) _newsDay[key] = day;
            }
            Changed = true;
        }

        /// <summary>Whether the places this book once took off the explored map were given back (<see cref="GiveBackMapPlaces"/>); a new book has none to give.</summary>
        private bool _mapApart = true;

        /// <summary>
        /// Gives back what a book kept off the explored map while the map wrote its places as seen:
        /// each place only ever seen off it, with no sighting, leaves the book and its Learned list,
        /// and every world is read again, so they come back as found in the character's ground
        /// (<see cref="MapCatchUp.Apply"/>). Biomes and places seen, known or met another way stay.
        /// </summary>
        private void GiveBackMapPlaces()
        {
            _mapApart = true;
            var given = _levels.Where(p => p.Value == KnownLevel.Seen && p.Key.StartsWith(EntryKeys.Location, StringComparison.Ordinal)
                                           && !_seen.ContainsKey(p.Key) && FoundOutOf(p.Key) == EntryKeys.OnMap).Select(p => p.Key).ToList();
            foreach (var key in given)
            {
                _levels.Remove(key);
                _lately.Remove(key);
                _learnedAt.Remove(key);
                _newsDay.Remove(key);
                _found.Remove(key);
                ForgetMet(key);
            }
            _mapped.Clear();
            Version++;
            Changed = true;
        }
    }
}
