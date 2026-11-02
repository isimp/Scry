namespace Scry
{
    /// <summary>
    /// Whether a box opened by its button stands open: a click on the button opens it and another
    /// puts it away, it stays while the pointer goes elsewhere, and a click outside it puts it away.
    /// What opens it for a player, as a notice's chip, opens it the same way; a button of that kind
    /// standing outside the box toggles it from where it stood as its press began.
    /// </summary>
    internal sealed class ClickBox
    {
        /// <summary>Whether it stood open as the last press began, before that press put it away.</summary>
        private bool _openAtPress;

        public bool Open { get; private set; }

        /// <summary>Its button clicked: open, or put away again.</summary>
        public void Pressed() => Open = !Open;

        /// <summary>A mouse button pressed down, inside the box and its button or outside them: outside, put away.</summary>
        public void DownAt(bool inside)
        {
            _openAtPress = Open;
            if (!inside) Open = false;
        }

        /// <summary>A button outside the box clicked to open or put it away, as it stood when the press began.</summary>
        public void ToggledFromOutside() => Open = !_openAtPress;

        public void Show() => Open = true;

        public void Close() => Open = false;
    }
}
