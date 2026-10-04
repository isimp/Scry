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

        /// <summary>Where on the screen a note said by a click shows, beside the pointer; null for one said otherwise, which shows in the foot.</summary>
        private static Vector2? _noteAt;

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

        /// <summary>A short note said by a key or by Scry itself, shown in the foot of the panel while it lasts.</summary>
        public static string Note => Time.unscaledTime < _noteUntil && _noteAt == null ? _note : null;

        /// <summary>
        /// Says a short note, long enough to be read (<see cref="NoteTime"/>): beside the pointer
        /// when a click said it, where the eye is; in the foot of the panel otherwise.
        /// </summary>
        public static void Say(string text)
        {
            _note = text;
            _noteUntil = Time.unscaledTime + NoteTime.Seconds(text);
            var e = Event.current;
            _noteAt = e != null && e.isMouse ? GUIUtility.GUIToScreenPoint(e.mousePosition) : (Vector2?)null;
        }

        /// <summary>The note a click said, drawn over everything beside where it was clicked while it lasts.</summary>
        private static void PointerNote()
        {
            if (Event.current.type != EventType.Repaint || _noteAt == null || Time.unscaledTime >= _noteUntil || string.IsNullOrEmpty(_note)) return;
            var at = _noteAt.Value;
            var width = Mathf.Min(U(380f), Skin.Width(Skin.Tip, _note) + U(2f));
            var height = Skin.Height(Skin.Tip, _note, width);
            var x = Mathf.Clamp(at.x + U(14f), U(4f), Screen.width - width - U(4f));
            var y = at.y - height - U(12f);
            if (y < U(4f)) y = at.y + U(22f);
            Skin.Tip.Draw(new Rect(x, y, width, height), new GUIContent(_note), false, false, false, false);
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
            FocusSearch(!_compact && Settings.FocusSearchOnOpen);
            RevealSelected();
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
            if (explorer != null) Guard.Run(Feature.Panel, "preparing the panel's lookups", Prepare, explorer);
            Guard.Run(Feature.LookAndWalkWhileOpen, "looking around and stepping back", LookAndStep);
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

        /// <summary>Lets go of the explorer of the world left.</summary>
        private static void ForgetOpen()
        {
            _explorer = null;
        }
    }
}
