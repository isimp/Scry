namespace Scry
{
    /// <summary>
    /// Where the pointer goes back to after looking around with the right mouse button. While the
    /// camera turns the game holds the cursor locked, which Unity keeps in the middle of the
    /// window, and there it would stay once let go; it is put back where the look began instead.
    /// Not while the panel is closed, when the game holds the cursor for play.
    /// </summary>
    internal sealed class PointerReturn
    {
        private bool _looking;
        private float _fromX, _fromY;
        private bool _due;

        /// <summary>Each frame: whether the right button looks around now, whether the panel is open, and where the pointer is.</summary>
        public void Frame(bool looking, bool open, float x, float y)
        {
            if (!open)
            {
                _looking = _due = false;
                return;
            }
            if (looking && !_looking)
            {
                _fromX = x;
                _fromY = y;
                _due = false;
            }
            else if (!looking && _looking) _due = true;
            _looking = looking;
        }

        /// <summary>Where the pointer is to be put back, once: false when it is not to be moved.</summary>
        public bool TakeDue(out float x, out float y)
        {
            x = _fromX;
            y = _fromY;
            if (!_due) return false;
            _due = false;
            return true;
        }
    }
}
