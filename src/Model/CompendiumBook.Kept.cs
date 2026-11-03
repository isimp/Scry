using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The kinds the character keeps on Valheim's map, standing every time it opens as Show on map
    /// shows them: each by the key its spots are kept by, several kinds of one place shown at once
    /// as one (<see cref="MapShowing.AllKindsKey"/>); every kind of dungeon kept from the start,
    /// as one, until taken off.
    /// </summary>
    internal sealed partial class CompendiumBook
    {
        /// <summary>The key every kind of dungeon is kept by, as one.</summary>
        public const string Dungeons = "dungeons";

        private readonly HashSet<string> _kept = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Whether every kind of dungeon was taken off the map, kept from the start otherwise.</summary>
        private bool _dungeonsUnkept;

        private List<string> _keptList;

        /// <summary>Counts up whenever what is kept changes, for the map and the list to be worked out again.</summary>
        public int KeptVersion { get; private set; }

        /// <summary>Whether a kind is kept on the map.</summary>
        public bool Keeps(string kind) => kind == Dungeons ? !_dungeonsUnkept : kind != null && _kept.Contains(kind);

        /// <summary>What is kept, the dungeons first, then the rest by their keys.</summary>
        public IReadOnlyList<string> Kept
        {
            get
            {
                if (_keptList != null) return _keptList;
                _keptList = new List<string>();
                if (!_dungeonsUnkept) _keptList.Add(Dungeons);
                _keptList.AddRange(_kept.OrderBy(kind => kind, StringComparer.Ordinal));
                return _keptList;
            }
        }

        /// <summary>Keeps a kind on the map, or takes it off: a choice of the player's.</summary>
        public void Keep(string kind, bool on)
        {
            if (string.IsNullOrEmpty(kind) || Keeps(kind) == on) return;
            if (kind == Dungeons) _dungeonsUnkept = !on;
            else if (on) _kept.Add(kind);
            else _kept.Remove(kind);
            KeptChanged();
        }

        /// <summary>Takes every kind off the map, the dungeons too.</summary>
        public void TakeAllOff()
        {
            if (_kept.Count == 0 && _dungeonsUnkept) return;
            _kept.Clear();
            _dungeonsUnkept = true;
            KeptChanged();
        }

        private void KeptChanged()
        {
            _keptList = null;
            KeptVersion++;
            Changed = true;
        }
    }
}
