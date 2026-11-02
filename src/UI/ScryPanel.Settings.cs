using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The cards a player sets Scry and the compendium from: the settings, opened by the gear in
    /// the title row over the panel's whole body (<see cref="SettingsCard"/>); in the list's place,
    /// the compendium's guide, opened by the ? in its box, and the question asked once of a
    /// character whose compendium is off, whether to keep one.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static Vector2 _guideScroll, _welcomeScroll;
        private static float _guideHeight, _welcomeHeight;

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

        /// <summary>
        /// Whether the character played is to be asked whether to keep a compendium now (<see
        /// cref="CompendiumBook.WelcomeDue"/>): nothing else in the list's place, and no self-test
        /// shown, as it sets the rules itself.
        /// </summary>
        private static bool WelcomeDue() => (_askWelcome || CompendiumKeeper.Book?.WelcomeDue == true && !SelfTestHost.StripShown) && _card == ListCard.None;

        private static bool _askWelcome;

        /// <summary>Asks whether to keep a compendium whatever the book says, or no longer, for the self-test to see the question drawn; it answers nothing.</summary>
        internal static void AskWelcomeForTest(bool ask) => _askWelcome = ask;

        /// <summary>The question asked once of a character whose compendium is off: Loose, Strict, or not now, each saying what it means; an answer keeps it asked.</summary>
        private static void WelcomeCard(Rect rect)
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return;
            CountDrawn(PanelPart.WelcomeCard);
            var body = BeginCard(rect, ref _welcomeScroll, _welcomeHeight, SettingsWords.WelcomeTitle(Player.m_localPlayer.OrNull()?.GetPlayerName()));
            var y = body.Y;
            var h = Skin.Wrap.CalcHeight(new GUIContent(SettingsWords.WelcomeWords), body.Width);
            GUI.Label(new Rect(body.X, y, body.Width, h), SettingsWords.WelcomeWords, Skin.Wrap);
            y += h + U(14f);
            var answered = -1;
            for (var i = 0; i < SettingsWords.WelcomeAnswers.Length; i++)
            {
                var (name, means) = SettingsWords.WelcomeAnswers[i];
                var box = new Rect(body.X, y, body.Width, U(52f));
                if (GUI.Button(box, GUIContent.none, Skin.Button)) answered = i;
                GUI.Label(new Rect(box.x + U(12f), box.y + U(6f), box.width - U(24f), U(20f)), name, Skin.Label);
                GUI.Label(new Rect(box.x + U(12f), box.y + U(26f), box.width - U(24f), U(20f)), means, Skin.DimLabel);
                y += box.height + U(8f);
            }
            if (Event.current.type == EventType.Repaint) _welcomeHeight = y;
            var closed = EndCard(rect, out _);
            if (answered < 0 && !closed) return;
            if (answered == 0) Choose(CompendiumRule.Loose);
            if (answered == 1) Choose(CompendiumRule.Strict);
            book.Welcome();
            CompendiumKeeper.Save(now: true);
        }
    }
}
