using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The cards a player sets Scry and the compendium from: the settings, opened by the gear in
    /// the title row over the panel's whole body (<see cref="SettingsCard"/>); in the list's place,
    /// the compendium's guide, opened by the ? in its box; and the first start's setup over the
    /// body (<see cref="SetupCard"/>).
    /// </summary>
    internal static partial class ScryPanel
    {
        private static Vector2 _guideScroll;
        private static float _guideHeight;

        /// <summary>Whether the compendium's guide is shown, and showing or putting it away; the box's ? and the self-test.</summary>
        public static bool GuideShown => _card == ListCard.Guide;

        public static void ShowGuide(bool shown)
        {
            if (shown) ShowCard(ListCard.Guide);
            else CloseCard(ListCard.Guide);
        }

        /// <summary>The gear in the title row, lit while the settings card is shown.</summary>
        private static void SettingsGear(Rect rect)
        {
            var shown = SettingsShown;
            if (GUI.Button(rect, GUIContent.none, shown ? Skin.On : Skin.IconButton)) ShowSettings(!shown);
            var inset = U(6f);
            Skin.Icon(new Rect(rect.x + inset, rect.y + inset, rect.width - inset * 2f, rect.height - inset * 2f), Skin.Gear, shown ? Skin.Accent : Skin.Dim);
            if (rect.Contains(Event.current.mousePosition)) AskTip("settings", SettingsWords.GearTip);
            CountDrawn(PanelPart.SettingsGear);
        }

        /// <summary>The compendium's guide: its rules, Known up to, peeking, Learned and You, what a world shows, the pins and playing together.</summary>
        private static void GuideCard(Rect rect)
        {
            CountDrawn(PanelPart.GuideCard);
            var body = BeginCard(rect, ref _guideScroll, _guideHeight, SettingsWords.GuideTitle);
            var y = body.Y;
            foreach (var (caption, words) in SettingsWords.Guide)
            {
                GUI.Label(new Rect(body.X, y, body.Width, U(22f)), caption, Skin.WrapBold);
                y += U(24f);
                var h = Skin.Wrap.CalcHeight(new GUIContent(words), body.Width);
                GUI.Label(new Rect(body.X, y, body.Width, h), words, Skin.Wrap);
                y += h + U(12f);
            }
            if (Event.current.type == EventType.Repaint) _guideHeight = y;
            if (EndCard(rect, out _)) ShowGuide(false);
        }
    }
}
