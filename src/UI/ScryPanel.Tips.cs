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

        /// <summary>Asks for a tooltip at the mouse, shown once the mouse has rested on the same thing for a moment.</summary>
        private static void AskTip(string key, string text)
        {
            if (Event.current.type != EventType.Repaint || _drag != Drag.None || string.IsNullOrEmpty(text)) return;
            _askedTipKey = key;
            _askedTipText = text;
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
