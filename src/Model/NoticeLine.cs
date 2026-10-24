using System.Collections.Generic;

namespace Scry
{
    /// <summary>A notice under the panel's header, in the order of how pressing it is.</summary>
    internal enum Notice
    {
        /// <summary>Part of Scry is off until it is updated.</summary>
        Off,

        /// <summary>A lasting file could not be read and was set aside.</summary>
        SetAside,

        /// <summary>The world's server sets some of the compendium's rules, told once a world.</summary>
        Rules,

        /// <summary>Every page shows whole while peeking.</summary>
        Peeking,

        /// <summary>Players whose games lack Scry, as what runs in them counts nothing for the character.</summary>
        PlayingWith,

        /// <summary>What the character learned since last told.</summary>
        Learned,
    }

    /// <summary>
    /// The compact view's one notice strip: of the notices up, the most pressing shows first, and
    /// the one shown stays while it is up; once it goes the most pressing takes the strip, and one
    /// newly up that is more pressing than the one shown takes it at once, so a warning is never
    /// left behind a step. The strip's step goes to the next up, round to the first.
    /// </summary>
    internal sealed class NoticeLine
    {
        private Notice? _shown;

        /// <summary>The notices up when last asked, to tell which are newly up.</summary>
        private readonly HashSet<Notice> _wasUp = new HashSet<Notice>();

        /// <summary>The notice the strip shows of those up; none while none is.</summary>
        public Notice? Shown(IReadOnlyList<Notice> up)
        {
            Notice? arrived = null;
            for (var i = 0; i < up.Count; i++)
            {
                if (!_wasUp.Contains(up[i]) && (arrived == null || up[i] < arrived)) arrived = up[i];
            }
            _wasUp.Clear();
            for (var i = 0; i < up.Count; i++) _wasUp.Add(up[i]);
            if (up.Count == 0) return _shown = null;
            if (_shown == null || !_wasUp.Contains(_shown.Value)) _shown = MostPressing(up);
            else if (arrived < _shown) _shown = arrived;
            return _shown;
        }

        /// <summary>Where the notice shown stands among those up, from one, and how many are up; none while none is shown (<see cref="Shown"/> asked first).</summary>
        public (int Which, int Of) Place(IReadOnlyList<Notice> up)
        {
            if (_shown == null) return (0, 0);
            var before = 0;
            for (var i = 0; i < up.Count; i++) if (up[i] < _shown) before++;
            return (before + 1, up.Count);
        }

        /// <summary>Shows the next notice up after the one shown, round to the most pressing.</summary>
        public void Step(IReadOnlyList<Notice> up)
        {
            if (_shown == null || up.Count == 0) return;
            Notice? next = null;
            for (var i = 0; i < up.Count; i++) if (up[i] > _shown && (next == null || up[i] < next)) next = up[i];
            _shown = next ?? MostPressing(up);
        }

        private static Notice MostPressing(IReadOnlyList<Notice> up)
        {
            var most = up[0];
            for (var i = 1; i < up.Count; i++) if (up[i] < most) most = up[i];
            return most;
        }
    }
}
