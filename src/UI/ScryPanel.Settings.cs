using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The cards a player sets Scry and the compendium from, in the list's place: the settings,
    /// opened by the gear in the title row, each written to the config file as it is chosen; the
    /// compendium's guide, opened by the ? in its box; and the question asked once of a character
    /// whose compendium is off, whether to keep one.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static Vector2 _settingsScroll, _guideScroll, _welcomeScroll;
        private static float _settingsHeight, _guideHeight, _welcomeHeight;

        /// <summary>How wide the settings card's widest row needs to be to stand on one line, and how wide the card is, as last drawn; for the self-test.</summary>
        [Diagnostic]
        public static (float Needed, float Width) SettingsFitForTest { get; private set; }

        /// <summary>Whether the next key pressed becomes the one opening the panel.</summary>
        private static bool _keyWaiting;

        /// <summary>The panel's size dragged to while the slider is held, set once it is let go.</summary>
        private static float? _sizeHeld;

        /// <summary>Whether the settings card is shown, and showing or putting it away; the gear and the self-test.</summary>
        public static bool SettingsShown => _card == ListCard.Settings;

        public static void ShowSettings(bool shown)
        {
            if (shown) ShowCard(ListCard.Settings);
            else CloseCard(ListCard.Settings);
            _keyWaiting = false;
        }

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

        /// <summary>
        /// The settings card: the panel's size, the key opening it, walking and looking while it is
        /// open, where the keys go as it opens, sounds on selecting, the wait before a tip, how the
        /// stage is filmed and whether the notice tells what was learned; each chosen is written to
        /// the config file at once.
        /// </summary>
        private static void SettingsCard(Rect rect)
        {
            var e = Event.current;
            // A key pressed while waiting becomes the one opening the panel; Escape keeps the one there is.
            if (_keyWaiting && e.type == EventType.KeyDown && e.keyCode != KeyCode.None)
            {
                if (e.keyCode != KeyCode.Escape && Settings.OpensPanel(e.keyCode)) Settings.OpenKey = e.keyCode;
                _keyWaiting = false;
                e.Use();
            }
            CountDrawn(PanelPart.SettingsCard);
            var body = BeginCard(rect, ref _settingsScroll, _settingsHeight, SettingsWords.Title);
            GUI.BeginGroup(new Rect(body.X, 0f, body.Width, Mathf.Max(_settingsHeight, rect.height)));
            var width = body.Width;
            var labelW = LabelColumn.Width(SettingsLabels(), U(14f), width);
            var y = body.Y;
            var wrapped = _rowsWrapped;
            RowsStart();

            // The size follows the slider once it is let go, as the panel changing size under a
            // dragged slider moves the slider away from the mouse.
            var shown = _sizeHeld ?? Settings.UiScale;
            var top = y;
            var dragged = SettingsWords.SizeStep(SliderRow(SettingsWords.Size, SettingsWords.SizeName(shown), shown, SettingsWords.SmallestSize, SettingsWords.LargestSize, width, labelW, ref y));
            if (!Mathf.Approximately(dragged, shown)) _sizeHeld = dragged;
            if (_sizeHeld.HasValue && GUIUtility.hotControl == 0)
            {
                Settings.UiScale = _sizeHeld.Value;
                _sizeHeld = null;
            }
            RowTip(top, y, width, "settings-size", SettingsWords.SizeTip);

            // The key: its name sunken, as a value read, and the button that waits for another.
            var rowH = U(28f);
            RowLabel(SettingsWords.Key, labelW, y, rowH);
            var keyName = _keyWaiting ? SettingsWords.Waiting : Settings.OpenKey.ToString();
            var keyW = Mathf.Max(U(60f), Skin.Width(Skin.Value, keyName) + U(16f));
            GUI.Label(new Rect(labelW, y, keyW, rowH), keyName, Skin.Value);
            var pressRect = new Rect(labelW + keyW + U(6f), y, Skin.Width(Skin.Button, SettingsWords.PressAKey) + U(12f), rowH);
            if (GUI.Button(pressRect, SettingsWords.PressAKey, _keyWaiting ? Skin.On : Skin.Button)) _keyWaiting = !_keyWaiting;
            if (pressRect.Contains(e.mousePosition)) AskTip("settings-key", SettingsWords.PressTip);
            y += rowH + U(8f);

            top = y;
            var open = new List<string> { SettingsWords.Walk, SettingsWords.Look };
            var flipped = Toggles(SettingsWords.WhileOpen, open, i => i == 0 ? Settings.WalkWhileOpen : Settings.LookWithRightMouse, width, labelW, ref y);
            if (flipped == 0) Settings.WalkWhileOpen = !Settings.WalkWhileOpen;
            if (flipped == 1) Settings.LookWithRightMouse = !Settings.LookWithRightMouse;
            RowTip(top, y, width, "settings-open", SettingsWords.WhileOpenTip);

            top = y;
            var opening = Segments(SettingsWords.Opening, new List<string> { SettingsWords.SearchReady, SettingsWords.KeysWalk }, Settings.FocusSearchOnOpen ? 0 : 1, width, labelW, ref y);
            if (opening >= 0) Settings.FocusSearchOnOpen = opening == 0;
            RowTip(top, y, width, "settings-opening", SettingsWords.OpeningTip);

            var sounds = Segments(SettingsWords.Sounds, new List<string> { SettingsWords.PlayOnSelect, SettingsWords.OnlyOnEnter }, Settings.PlayOnSelect ? 0 : 1, width, labelW, ref y);
            if (sounds >= 0) Settings.PlayOnSelect = sounds == 0;

            var waits = new List<string>();
            foreach (var wait in SettingsWords.TipWaits) waits.Add(SettingsWords.TipWaitName(wait));
            var tip = Segments(SettingsWords.Tips, waits, SettingsWords.Nearest(SettingsWords.TipWaits, Settings.TooltipDelay), width, labelW, ref y);
            if (tip >= 0) Settings.TooltipDelay = SettingsWords.TipWaits[tip];

            top = y;
            var stage = Segments(SettingsWords.Stage, new List<string> { SettingsWords.Full, SettingsWords.Fast }, Settings.StageQuality == StageQuality.Fast ? 1 : 0, width, labelW, ref y);
            if (stage >= 0) Settings.StageQuality = stage == 1 ? StageQuality.Fast : StageQuality.Full;
            RowTip(top, y, width, "settings-stage", SettingsWords.StageTip);

            top = y;
            var news = Segments(SettingsWords.News, new List<string> { SettingsWords.Notice, SettingsWords.LearnedOnly }, Settings.NewsNotice ? 0 : 1, width, labelW, ref y);
            if (news >= 0) Settings.NewsNotice = news == 0;
            RowTip(top, y, width, "settings-news", SettingsWords.NewsTip);

            top = y;
            var names = Segments(SettingsWords.MarkNames, new List<string> { SettingsWords.WhenPointedAt, SettingsWords.Always }, Settings.MarkNamesAlways ? 1 : 0, width, labelW, ref y);
            if (names >= 0) Settings.MarkNamesAlways = names == 1;
            RowTip(top, y, width, "settings-mark-names", SettingsWords.MarkNamesTip);

            if (_rowsWrapped > wrapped) CountDrawn(PanelPart.SettingsRowWrapped);
            SettingsFitForTest = (_widestRow, width);
            var keptH = Skin.DimWrap.CalcHeight(new GUIContent(SettingsWords.Kept), width);
            GUI.Label(new Rect(0f, y + U(4f), width, keptH), SettingsWords.Kept, Skin.DimWrap);
            y += keptH + U(16f);
            GUI.EndGroup();
            if (e.type == EventType.Repaint) _settingsHeight = y;
            if (EndCard(rect, out _)) ShowSettings(false);
        }

        private static readonly string[] SettingsLabelNames = { SettingsWords.Size, SettingsWords.Key, SettingsWords.WhileOpen, SettingsWords.Opening, SettingsWords.Sounds, SettingsWords.Tips, SettingsWords.Stage, SettingsWords.News, SettingsWords.MarkNames };
        private static readonly float[] SettingsLabelWidths = new float[SettingsLabelNames.Length];

        /// <summary>How wide each of the settings card's labels is drawn, for their column to be as wide as the widest.</summary>
        private static float[] SettingsLabels()
        {
            for (var i = 0; i < SettingsLabelNames.Length; i++) SettingsLabelWidths[i] = Skin.Width(Skin.DimLabel, SettingsLabelNames[i]);
            return SettingsLabelWidths;
        }

        /// <summary>A row's tip, asked while the mouse is over the row from its top to where the next begins.</summary>
        private static void RowTip(float top, float y, float width, string key, string tip)
        {
            if (new Rect(0f, top, width, y - top).Contains(Event.current.mousePosition)) AskTip(key, tip);
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
