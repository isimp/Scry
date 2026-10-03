using System;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Whether the panel is open, and what it is handed. The session opens and closes it
    /// (<see cref="Opened"/>, <see cref="Closed"/>) and each frame hands it the world's explorer
    /// and how far the catalog's reading has got (<see cref="Update"/>); the panel asks to be
    /// closed (<see cref="CloseAsked"/>), and keeps what the game's input needs to know of it
    /// (<see cref="TextInputBlock"/> and the patches beside it): whether it holds the keys,
    /// whether the camera is being turned to look around, whether the character may walk.
    /// </summary>
    internal static partial class ScryPanel
    {
        public static bool IsOpen { get; private set; }

        /// <summary>The world's explorer, or null before its catalog is read.</summary>
        private static Explorer _explorer;

        /// <summary>How far the catalog's reading has got while there is no explorer yet.</summary>
        private static string _reading;

        private static int _closedFrame = -10;
        private static string _note;
        private static float _noteUntil;

        /// <summary>Told when the panel asks to be closed, by its cross or Escape; the session closes it.</summary>
        public static event Action CloseAsked;

        private static void AskClose() => CloseAsked?.Invoke();

        /// <summary>
        /// Whether the game should keep its hands off input. Stays true for the frame after
        /// closing, so the Escape that closed the panel does not also open the game's menu.
        /// </summary>
        public static bool BlocksInput => IsOpen || Time.frameCount <= _closedFrame + 1;

        /// <summary>
        /// Whether the right mouse button is held to look around while the panel is open. Only the
        /// camera is freed; moving, blocking and attacking stay off.
        /// </summary>
        public static bool Looking { get; private set; }

        /// <summary>
        /// Whether the character can walk while the panel is open: whenever none of its text boxes
        /// has the keyboard, so typing a search never moves anyone.
        /// </summary>
        public static bool Walking => IsOpen && !Typing && Settings.WalkWhileOpen;

        /// <summary>A short line shown at the foot of the panel for a few seconds.</summary>
        public static string Note => Time.unscaledTime < _noteUntil ? _note : null;

        public static void Say(string text)
        {
            _note = text;
            _noteUntil = Time.unscaledTime + 4f;
        }

        /// <summary>The panel opens, with the world's explorer and how far the catalog's reading has got.</summary>
        public static void Opened(Explorer explorer, string reading)
        {
            IsOpen = true;
            _explorer = explorer;
            _reading = reading;
            Skin.LookForFontsAgain();

            // In the compact view the keys walk until the search is clicked, so it is not focused on
            // opening; in the full view as the player sets it.
            _focusSearch = !_compact && Settings.FocusSearchOnOpen;
            _reveal = true;
        }

        public static void Closed()
        {
            IsOpen = false;
            Looking = false;
            _closedFrame = Time.frameCount;
        }

        /// <summary>
        /// Each frame: the world's explorer and how far the catalog's reading has got, the panel's
        /// lookups kept ready, and the mouse's right button to look around and its own back and
        /// forward buttons, each part on its own.
        /// </summary>
        public static void Update(Explorer explorer, string reading)
        {
            _explorer = explorer;
            _reading = reading;
            if (explorer != null) Guard.Run("preparing the panel's lookups", Prepare, explorer);
            Guard.Run("looking around and stepping back", LookAndStep);
        }

        private static void LookAndStep()
        {
            // Looking starts only from a press outside the panel, so a right click on it stays a click.
            if (!IsOpen || !Input.GetMouseButton(1)) Looking = false;
            else if (Input.GetMouseButtonDown(1) && Settings.LookWithRightMouse && !Covers(Input.mousePosition)) Looking = true;

            // The mouse's own back and forward buttons step through jumps, as in a browser.
            if (IsOpen && _explorer != null && Input.GetKeyDown(KeyCode.Mouse3)) Step(_explorer, true);
            if (IsOpen && _explorer != null && Input.GetKeyDown(KeyCode.Mouse4)) Step(_explorer, false);
        }
    }
}
