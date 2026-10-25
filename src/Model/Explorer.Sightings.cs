using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What the stage and a page only seen show of a creature by what was seen of it up close: its stars, what it carried, where it stood.</summary>
    internal sealed partial class Explorer
    {
        /// <summary>
        /// Whether the page shown keeps back what only something seen done tells (a deed,
        /// <see cref="EntryKeys.Did"/>, or a weak spot a hit found, <see cref="EntryKeys.WeakSpot"/>):
        /// under the strict rules, on a known page above the milestone floor, until it was seen.
        /// </summary>
        public bool KeepsBack(string proof) => LeavingOut && proof != null && !_view.Book.Proved(_selected.Key, proof) && !AlsoShownBy(proof);

        /// <summary>The game prints the health of the creature ridden (<c>EnemyHud</c>, its health text for the mount alone): riding one proves its health as a kill does.</summary>
        private bool AlsoShownBy(string proof) => proof == EntryKeys.Did(Deed.Killed) && _view.Book.Proved(_selected.Key, EntryKeys.Did(Deed.Ridden));

        /// <summary>The hover of a creature's drops title under the strict rules (<see cref="TallyWords.KillsSeenTip"/>): the kills whose loot was seen to fall against the game's count; none otherwise.</summary>
        public string KillsSeenTip(string of, int counted) => LeavingOut ? TallyWords.KillsSeenTip(_view.Book.Tallied(Tally.Fell).Kills(of), counted) : null;

        /// <summary>
        /// Whether a creature's page shown keeps back the clips of one of its attacks, by the item
        /// the attack is made with: as its damage (<see cref="HidesAttack"/>), under the strict
        /// rules, on a known page above the milestone floor, until the attack hit the character or
        /// was seen begun up close (<see cref="EntryKeys.Attack"/>); such a clip is listed as a smudge
        /// and does not play.
        /// </summary>
        public bool KeepsBackAttackClip(string item) => item != null && _selected?.Kind == Kind.Creature && KeepsBack(EntryKeys.Attack(item));

        /// <summary>
        /// An item's amount in a row counted from what the character saw (<see cref="Tally"/>) of
        /// a creature or resource: under the strict rules, on a known page above the milestone
        /// floor, how many came and how often, a smudge where it was never counted; none otherwise,
        /// when the game's own amount shows. Kept by item while what is known and the selection
        /// stay, as a page's chips ask every frame.
        /// </summary>
        public string StrictAmount(Tally tally, string of, string item)
        {
            if (tally == Tally.None || !LeavingOut) return null;
            if (_strictAmountsOf != (KnowStamp, SelectionVersion))
            {
                _strictAmounts.Clear();
                _strictAmountsOf = (KnowStamp, SelectionVersion);
            }
            var key = (tally, of, item);
            if (_strictAmounts.TryGet(key, out var amount)) return amount;
            var counted = _view.Book.Tallied(tally);
            var drop = counted.Of(of).FirstOrDefault(d => d.Item == item);
            amount = drop == null ? CompendiumWords.Hidden : tally == Tally.Weather ? TallyWords.Seen(drop, counted.Kills(of)) : TallyWords.Chip(drop, counted.Kills(of));
            _strictAmounts.Set(key, amount);
            return amount;
        }

        /// <summary>The strict amounts by tally, source and item, and what is known and selected they were worked out at.</summary>
        private readonly RecentCache<(Tally Tally, string Of, string Item), string> _strictAmounts = new RecentCache<(Tally, string, string), string>(2048);
        private (int Know, int Selection) _strictAmountsOf = (-1, -1);

        /// <summary>The world's seen reach, within which stars, gear and attacks are seen up close; 0 where the server keeps none.</summary>
        public float SeenReach => _view?.Book?.World.SeenReach ?? WorldRules.SeenReachSetting.Default;

        /// <summary>
        /// Whether the stage may show a creature, by its key, carrying an item, by its prefab: with
        /// the compendium off, peeking, under the loose rules or on a page shown whole, any; under
        /// the strict rules one it was seen carrying up close (<see cref="CompendiumBook.WornSeen"/>)
        /// or seen attack with, the attack taken or begun up close (<see cref="CompendiumBook.Proved"/>),
        /// so the clip of an attack that plays shows the weapon it is made with.
        /// </summary>
        public bool WearsShown(string creature, string item)
        {
            if (!StageFollowsSightings(creature)) return true;
            foreach (var worn in _view.Book.WornSeen(creature)) if (worn == item) return true;
            return _view.Book.Proved(creature, EntryKeys.Attack(item));
        }

        /// <summary>
        /// The levels the stage offers a creature at, 1 being no stars: every one up to its most
        /// with the compendium off, peeking, under the loose rules or on a page shown whole; under
        /// the strict rules no stars and each count of stars it was seen with up close.
        /// Kept as one list while what is known, the selection and peeking stay, as the Adjust rows ask every frame.
        /// </summary>
        public List<int> StarsOffered(Entry entry)
        {
            if (entry == null) return new List<int>();
            if (_starsOffered.TryGetValue(entry.Key, out var kept) && kept.Know == KnowStamp && kept.Selection == SelectionVersion && kept.Peeking == Peeking) return kept.Offered;
            if (_starsOffered.Count > 256) _starsOffered.Clear();
            var offered = StarsOfferedNow(entry);
            _starsOffered[entry.Key] = (KnowStamp, SelectionVersion, Peeking, offered);
            return offered;
        }

        /// <summary>The stars offered by entry, with what they were worked out at (<see cref="StarsOffered"/>).</summary>
        private readonly Dictionary<string, (int Know, int Selection, bool Peeking, List<int> Offered)> _starsOffered = new Dictionary<string, (int, int, bool, List<int>)>(StringComparer.Ordinal);

        /// <summary>The levels offered, worked out afresh (<see cref="StarsOffered"/>).</summary>
        private List<int> StarsOfferedNow(Entry entry)
        {
            var most = 1 + Math.Max(0, entry?.ExtraLevels ?? 0);
            var offered = new List<int>();
            if (entry == null) return offered;
            if (!StageFollowsSightings(entry.Key))
            {
                for (var level = 1; level <= most; level++) offered.Add(level);
                return offered;
            }
            offered.Add(1);
            foreach (var stars in _view.Book.StarsSeen(entry.Key))
            {
                var level = stars + 1;
                if (level > 1 && level <= most && !offered.Contains(level)) offered.Add(level);
            }
            offered.Sort();
            return offered;
        }

        /// <summary>
        /// The level a creature rolled on a place's example or in a raid's crowd stands at: the
        /// level rolled where the stage shows every star, else the highest it offers no higher
        /// than the one rolled (<see cref="StarsOffered"/>), no stars at the least.
        /// </summary>
        public int StarLevelFor(string creature, int rolled)
        {
            if (!StageFollowsSightings(creature)) return Math.Max(1, rolled);
            var level = 1;
            foreach (var stars in _view.Book.StarsSeen(creature)) if (stars + 1 <= rolled) level = Math.Max(level, stars + 1);
            return level;
        }

        /// <summary>Whether the stage shows of a creature only what was seen of it up close: the strict rules, not peeking, on a page not shown whole by the player's choice.</summary>
        private bool StageFollowsSightings(string creature)
        {
            if (!CompendiumOn || _view.Rule != CompendiumRule.Strict || string.IsNullOrEmpty(creature)) return false;
            var entry = Find(creature);
            return entry == null || !_view.Whole(entry);
        }

        /// <summary>
        /// What sight gave of an entry, for its page while it is only seen, each line where there is
        /// one: the stars it was seen with up close, what it was seen carrying or attack with, and
        /// the biomes and places it was seen in; none with the compendium off.
        /// </summary>
        public List<(string Label, string Value)> SeenLines(Entry entry)
        {
            var lines = new List<(string, string)>();
            if (!CompendiumOn || entry == null) return lines;
            var book = _view.Book;
            var stars = book.StarsSeen(entry.Key).Distinct().OrderBy(s => s).Select(CombatWords.StarsSeen).ToList();
            if (stars.Count > 0) lines.Add((CompendiumWords.SeenWithLabel, string.Join(", ", stars)));
            var worn = book.WornSeen(entry.Key).Concat(book.AttackedWith(entry.Key)).Select(item => Find(item)?.ShownName ?? Naming.Readable(item)).Distinct().OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            if (worn.Count > 0) lines.Add((CompendiumWords.SeenCarryingLabel, string.Join(", ", worn)));
            var where = new List<string>();
            foreach (var other in _catalog)
            {
                if ((other.Kind == Kind.Biome || other.Kind == Kind.Location) && book.Witnessed(entry.Key, other.Key)) where.Add(other.ShownName);
            }
            if (where.Count > 0) lines.Add((CompendiumWords.SeenInLabel, string.Join(", ", where)));
            return lines;
        }

        /// <summary>
        /// A drop's chance at so many stars in the creature's stars table under the strict rules,
        /// on a known page above the milestone floor: the character's own count, how many of the
        /// kills of one with that many stars dropped it (<see cref="TallyWords.Share"/>), a smudge while
        /// none with that many stars was seen to fall; none otherwise, when the game's chance shows.
        /// </summary>
        public string StrictStarShare(string creature, string item, int stars)
        {
            if (!LeavingOut) return null;
            // Opened as the stars table's cells of that many stars are: once one with them was seen to fall (EntryKeys.StarsFell).
            if (!_view.Book.Proved(creature, EntryKeys.StarsFell(stars))) return CompendiumWords.Hidden;
            var kills = _view.Book.StarredKills(creature, stars);
            if (kills == 0) return CompendiumWords.Hidden;
            var times = 0;
            foreach (var drop in _view.Book.StarredDrops(creature, stars)) if (drop.Item == item) times = drop.Times;
            return TallyWords.Share(times, kills);
        }
    }
}
