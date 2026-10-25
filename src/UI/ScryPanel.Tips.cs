using UnityEngine;

namespace Scry
{
    /// <summary>The panel's tooltips: a part under the mouse asks for one as it repaints, and it shows once the mouse has rested on the same thing for a moment.</summary>
    internal static partial class ScryPanel
    {
        // A tooltip asked for during this repaint, and the one showing.
        private static string _askedTipKey;
        private static string _askedTipText;
        private static Vector2 _askedTipAt;
        private static string _tipKey;
        private static float _tipSince;
        private static float TipDelay => Settings.TooltipDelay;

        /// <summary>A repaint asks for its tooltip afresh: none until a part under the mouse asks for one.</summary>
        private static void AskTipsAfresh()
        {
            if (Event.current.type == EventType.Repaint) _askedTipKey = null;
        }

        /// <summary>How many tips were asked for this event, so a row drawn faint knows whether what it drew asked for one of its own.</summary>
        private static int TipAskedCount;

        /// <summary>
        /// Asks for a tooltip at the mouse, shown once the mouse has rested on the same thing for a
        /// moment. A tip draws plain text, so what the game's or a mod's words mark up is left out
        /// (<see cref="Naming.Plain"/>), never shown as its tags.
        /// </summary>
        private static void AskTip(string key, string text)
        {
            if (Event.current.type != EventType.Repaint || _drag != Drag.None || string.IsNullOrEmpty(text)) return;
            TipAskedCount++;
            _askedTipKey = key;
            _askedTipText = Naming.Plain(text);
            _askedTipAt = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
        }

        private static void Tooltip()
        {
            if (Event.current.type != EventType.Repaint) return;

            if (_askedTipKey == null)
            {
                _tipKey = null;
                return;
            }

            if (_askedTipKey != _tipKey)
            {
                _tipKey = _askedTipKey;
                _tipSince = Time.unscaledTime;
            }
            if (Time.unscaledTime - _tipSince < TipDelay) return;

            var content = new GUIContent(_askedTipText);
            var maxW = U(420f);
            var width = Mathf.Min(maxW, Skin.Width(Skin.Tip, _askedTipText) + U(2f));
            var height = Skin.Height(Skin.Tip, _askedTipText, width);
            var x = Mathf.Min(_askedTipAt.x + U(16f), Screen.width - width - U(4f));
            var y = _askedTipAt.y + U(20f);
            if (y + height > Screen.height - U(4f)) y = _askedTipAt.y - height - U(8f);
            Skin.Tip.Draw(new Rect(x, y, width, height), content, false, false, false, false);
        }
    }
}
