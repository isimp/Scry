namespace Scry
{
    /// <summary>The explorer's peek: a page shown whole for now though the compendium is on.</summary>
    internal sealed partial class Explorer
    {
        private bool _peeking;

        /// <summary>The page peeked at alone (<see cref="PeekPage"/>); none while every page is, or none.</summary>
        private Entry _peekedPage;

        /// <summary>
        /// Whether a page shows whole for now though the compendium is on, its links leading
        /// anywhere, the list and its counts staying as it knows them, for checking something or
        /// helping a friend: the page shown alone (<see cref="PeekedPage"/>), or every page until it
        /// is stopped, a whole name in quotes then finding what the character has not met. Setting
        /// it peeks at every page, or stops either; what was looked at meanwhile and not seen is no
        /// step back after. Never with the compendium off.
        /// </summary>
        public bool Peeking
        {
            get => _peeking;
            set
            {
                var peek = value && CompendiumSet;
                var alone = _peekedPage;
                _peekedPage = null;
                if (peek == _peeking && alone == null) return;
                _peeking = peek;
                Retell();
            }
        }

        /// <summary>Whether every page shows whole until the peek is stopped, not the page shown alone.</summary>
        public bool PeekingEvery => _peeking && _peekedPage == null;

        /// <summary>The page shown whole alone until another entry is gone to; none.</summary>
        public Entry PeekedPage => _peeking ? _peekedPage : null;

        /// <summary>
        /// Peeks at the page shown alone, as the eye does: it shows whole until another entry is
        /// gone to, by the list, a link or a step back. With no page shown, every page; with the
        /// compendium off, none.
        /// </summary>
        public void PeekPage()
        {
            if (!CompendiumSet) return;
            if (_selected == null)
            {
                Peeking = true;
                return;
            }
            var was = _peeking;
            _peekedPage = _selected;
            _peeking = true;
            if (!was) Retell();
        }

        /// <summary>
        /// Whether going to an entry ends a peek at the page shown alone: it does, but for one the
        /// character has not seen, which only that page's links reach, peeked at in turn.
        /// </summary>
        private bool PeekEndsGoingTo(Entry entry)
        {
            if (_peekedPage == null) return false;
            if (entry != null && _view.Of(entry) < KnownLevel.Seen)
            {
                _peekedPage = entry;
                return false;
            }
            return true;
        }
    }
}
