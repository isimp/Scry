namespace Scry
{
    /// <summary>
    /// A step back or forward counted once a press: the mouse's back and forward buttons reach the
    /// panel both as a key in the game's update and as an event in the panel's drawing, at times a
    /// frame apart, so the same way again within <see cref="SamePress"/> is the same press.
    /// </summary>
    internal sealed class StepPress
    {
        /// <summary>How long, in seconds, a step the same way is the same press arriving again: some frames, shorter than any second press.</summary>
        public const float SamePress = 0.12f;

        private bool _back;
        private float _at = float.NegativeInfinity;

        /// <summary>Whether a step this way now is a press of its own, kept as the last press if so.</summary>
        public bool Take(bool back, float at)
        {
            if (back == _back && at - _at < SamePress) return false;
            _back = back;
            _at = at;
            return true;
        }
    }
}
