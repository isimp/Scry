namespace Scry
{
    /// <summary>
    /// Work done once changes have stopped for a moment, rather than for each change: a camp's
    /// rooms come in one by one, each changing what the stage's ground holds, and its grass is
    /// scattered again once they have stopped coming. Due once for each settling.
    /// </summary>
    internal sealed class AfterQuiet
    {
        private readonly float _seconds;
        private float _changedAt;
        private bool _waiting;

        /// <summary>A wait of so many seconds after the last change.</summary>
        public AfterQuiet(float seconds) => _seconds = seconds;

        /// <summary>A change: the work is due once the wait has passed with no other.</summary>
        public void Changed(float now)
        {
            _changedAt = now;
            _waiting = true;
        }

        /// <summary>Whether the work is due now, the wait passed since the last change; true once, then false until the next change.</summary>
        public bool Due(float now)
        {
            if (!_waiting || now - _changedAt < _seconds) return false;
            _waiting = false;
            return true;
        }

        /// <summary>The work was done at once, so nothing waits for it.</summary>
        public void Done() => _waiting = false;
    }
}
