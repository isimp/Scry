using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The kinds the character keeps on Valheim's map, standing every time it opens as Show on map
    /// shows them: each by the key its spots are kept by, several kinds of one place shown at once
    /// as one (<see cref="MapShowing.AllKindsKey"/>); every kind of dungeon kept from the start,
    /// as one, until taken off. Each kept kind stands in a mark of its own (<see cref="KeptMarks"/>),
    /// the lowest free as it is kept, kept with it while it is.
    /// </summary>
    internal sealed partial class CompendiumBook
    {
        /// <summary>The key every kind of dungeon is kept by, as one.</summary>
        public const string Dungeons = "dungeons";

        /// <summary>The kinds kept, each with its mark.</summary>
        private readonly Dictionary<string, int> _kept = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Whether every kind of dungeon was taken off the map, kept from the start otherwise.</summary>
        private bool _dungeonsUnkept;

        private List<string> _keptList;

        /// <summary>Counts up whenever what is kept changes, for the map and the list to be worked out again.</summary>
        public int KeptVersion { get; private set; }

        /// <summary>Whether a kind is kept on the map.</summary>
        public bool Keeps(string kind) => kind == Dungeons ? !_dungeonsUnkept : kind != null && _kept.ContainsKey(kind);

        /// <summary>The mark a kept kind stands in, the dungeons' for every kind of dungeon; none for one not kept.</summary>
        public int? MarkOf(string kind) => kind == Dungeons ? KeptMarks.Dungeons : kind != null && _kept.TryGetValue(kind, out var mark) ? mark : (int?)null;

        /// <summary>
        /// The mark a line kept by its own key stands in, for its picker; none for every kind of
        /// dungeon, which keeps its amber discs, nor for a kind kept only with others as one line.
        /// </summary>
        public int? PickableMark(string kind) => kind != null && _kept.TryGetValue(kind, out var mark) ? mark : (int?)null;

        /// <summary>The lowest mark no kept kind stands in, for one shown and not kept.</summary>
        public int FreeMark() => KeptMarks.Lowest(_kept.Values);

        /// <summary>What is kept, the dungeons first, then the rest by their keys.</summary>
        public IReadOnlyList<string> Kept
        {
            get
            {
                if (_keptList != null) return _keptList;
                _keptList = new List<string>();
                if (!_dungeonsUnkept) _keptList.Add(Dungeons);
                _keptList.AddRange(_kept.Keys.OrderBy(kind => kind, StringComparer.Ordinal));
                return _keptList;
            }
        }

        /// <summary>
        /// Keeps a kind on the map, or takes it off: a choice of the player's. Several kinds kept as
        /// one (<see cref="MapShowing.AllKindsKey"/>) take each one's own line off, so nothing stands
        /// twice, and stand in the lowest mark those stood in, so they keep their look.
        /// </summary>
        public void Keep(string kind, bool on)
        {
            if (string.IsNullOrEmpty(kind) || Keeps(kind) == on) return;
            if (kind == Dungeons) _dungeonsUnkept = !on;
            else if (on)
            {
                var replaced = kind.IndexOf('+') >= 0 ? kind.Split('+').Where(_kept.ContainsKey).ToList() : new List<string>();
                var marks = replaced.Select(k => _kept[k]).ToList();
                foreach (var own in replaced) _kept.Remove(own);
                // A picture is only ever picked, so it is the look the player gave them.
                var pictured = marks.Where(m => m >= KeptMarks.Most).ToList();
                _kept[kind] = pictured.Count > 0 ? pictured.Min() : marks.Count > 0 ? marks.Min() : FreeMark();
            }
            else _kept.Remove(kind);
            KeptChanged();
        }

        /// <summary>
        /// Gives a kept kind the mark the player picked (<see cref="KeptMarks"/>); another kept may
        /// stand in it too. Whether it changed: never for every kind of dungeon, a kind not kept,
        /// a mark this Scry does not offer, or the mark it has. Every kind of dungeon is kept without a
        /// mark of its own, so it is never picked.
        /// </summary>
        public bool Pick(string kind, int mark)
        {
            if (kind == null || !KeptMarks.Offered(mark) || !_kept.TryGetValue(kind, out var had) || had == mark) return false;
            _kept[kind] = mark;
            KeptChanged();
            return true;
        }

        /// <summary>Takes every kind off the map, the dungeons too.</summary>
        public void TakeAllOff()
        {
            if (_kept.Count == 0 && _dungeonsUnkept) return;
            _kept.Clear();
            _dungeonsUnkept = true;
            KeptChanged();
        }

        /// <summary>
        /// Once a book is read: a kind kept without a mark, as an older book keeps it, or in one
        /// this Scry does not offer, given the lowest free, in the order of their keys. Kinds
        /// sharing a mark keep it, as the player may pick one look for several.
        /// </summary>
        private void MarkKept()
        {
            var held = new List<int>();
            var unmarked = new List<string>();
            foreach (var kind in _kept.Keys.OrderBy(k => k, StringComparer.Ordinal))
            {
                if (KeptMarks.Offered(_kept[kind])) held.Add(_kept[kind]);
                else unmarked.Add(kind);
            }
            foreach (var kind in unmarked)
            {
                var mark = KeptMarks.Lowest(held);
                _kept[kind] = mark;
                held.Add(mark);
            }
        }

        private void KeptChanged()
        {
            _keptList = null;
            KeptVersion++;
            Changed = true;
        }
    }
}
