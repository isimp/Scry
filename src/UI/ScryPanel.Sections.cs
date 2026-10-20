using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The side's sections, headings and rows, and the tips shown while the mouse rests on something.</summary>
    internal static partial class ScryPanel
    {
        // ----- Sections and rows -----

        /// <summary>The sections folded shut, by key. Details start shut; the rest start open.</summary>
        private static readonly HashSet<string> Folded = new HashSet<string> { "details" };

        private static bool IsFolded(string key) => key != null && Folded.Contains(key);

        /// <summary>Folds a section shut or opens it, remembered with where the panel is.</summary>
        private static void SetFolded(string key, bool folded)
        {
            if (folded == IsFolded(key)) return;
            if (folded) Folded.Add(key);
            else Folded.Remove(key);
            SaveRects();
        }

        /// <summary>Folds these sections shut and opens every other, as remembered.</summary>
        private static void FoldOnly(IEnumerable<string> keys)
        {
            Folded.Clear();
            foreach (var key in keys) Folded.Add(key);
        }

        /// <summary>Every section that folds, by key.</summary>
        private static readonly string[] Foldable = { "kept", "variants", "adjust", "animations", "effects", "playsin", "links", "facts", "tooltip", "runes", "command", "details" };

        private static void FoldAll(bool fold)
        {
            if (fold) foreach (var key in Foldable) Folded.Add(key);
            else Folded.Clear();
            SaveRects();
        }

        /// <summary>
        /// A section's heading and rule, with how many the section holds where
        /// <paramref name="count"/> is 0 or more. With a key, the heading folds the section shut or
        /// opens it when clicked, and the choice is remembered; the caller skips its body while
        /// folded; and the line under the title names it (<see cref="MarkSection"/>).
        /// </summary>
        private static float SectionHeading(string text, float width, float y, Action reset, string key = null, int count = -1)
        {
            var folded = IsFolded(key);
            var shown = key == null ? text : PanelWords.SectionTitle(MarkSection(key, text, count, y), folded);
            var textW = Skin.Width(Skin.Heading, shown);
            var head = new Rect(0f, y, textW + U(4f), U(20f));
            if (key == null)
            {
                GUI.Label(head, shown, Skin.Heading);
            }
            else
            {
                LinkLabel(head, shown, Skin.Heading, Skin.Heading.normal.textColor);
                if (head.Contains(Event.current.mousePosition)) AskTip("fold:" + key, PanelWords.SectionTip(folded));
                if (GUI.Button(head, GUIContent.none, GUIStyle.none))
                {
                    if (Event.current.shift) FoldAll(!folded);
                    else if (folded) Folded.Remove(key);
                    else Folded.Add(key);
                    SaveRects();
                }
            }
            var lineEnd = reset != null ? width - U(74f) : width;

            Skin.Fill(new Rect(textW + U(12f), y + U(10f), Mathf.Max(0f, lineEnd - textW - U(12f)), U(1f)), Skin.Outline);
            var resetChip = new Rect(width - U(64f), y - U(2f), U(64f), U(24f));
            if (reset != null && GUI.Button(resetChip, "Reset", Skin.Fitted(Skin.Chip, resetChip))) reset();
            return y + U(folded ? 36f : 30f);
        }

        /// <summary>A labelled row's label, in the column every labelled row (sliders, segments, toggles) shares.</summary>
        private static void RowLabel(string label, float labelW, float y, float rowH) => FitLabel(new Rect(0f, y, labelW - U(6f), rowH), label, Skin.DimLabel, 10f);

        /// <summary>A slider's bar, as thin as every bar in the panel, across a row and in its middle.</summary>
        private static Rect BarIn(float x, float y, float width, float rowH) => new Rect(x, y + (rowH - U(14f)) / 2f, width, U(14f));

        /// <summary>
        /// A timeline's bar, which a clip's time can be dragged along to any point, as a sound's and
        /// an animation's have. Returns the time dragged to, or null where it was not dragged.
        /// </summary>
        private static float? TimeBar(float x, float y, float width, float rowH, float time, float length)
        {
            var changed = GUI.changed;
            GUI.changed = false;
            var picked = GUI.HorizontalSlider(BarIn(x, y, width, rowH), time, 0f, Mathf.Max(length, 0.01f));
            var dragged = GUI.changed;
            GUI.changed = changed || dragged;
            return dragged ? picked : (float?)null;
        }

        private static float SliderRow(string label, string value, float current, float min, float max, float width, float labelW, ref float y)
        {
            var rowH = U(26f);
            RowLabel(label, labelW, y, rowH);
            var valueW = U(64f);
            var result = GUI.HorizontalSlider(BarIn(labelW, y, width - labelW - valueW - U(10f), rowH), current, min, max);
            GUI.Label(new Rect(width - valueW, y, valueW, rowH), value, Skin.Label);
            y += rowH + U(8f);
            return result;
        }

        /// <summary>A labelled row of segments, one of them on; returns the one clicked, or -1.</summary>
        private static int Segments(string label, List<string> names, int selected, float width, float labelW, ref float y) => SegmentRow(label, names, selected, null, width, labelW, ref y);

        /// <summary>Like <see cref="Segments"/>, but any number can be on; returns the one clicked, or -1.</summary>
        private static int Toggles(string label, List<string> names, Func<int, bool> on, float width, float labelW, ref float y) => SegmentRow(label, names, -1, on, width, labelW, ref y);

        /// <summary>
        /// The segments after their label, wrapping back to the label's edge; lit where
        /// <paramref name="on"/> says, or the one <paramref name="selected"/> without it.
        /// </summary>
        private static int SegmentRow(string label, List<string> names, int selected, Func<int, bool> on, float width, float labelW, ref float y)
        {
            var rowH = U(28f);
            RowLabel(label, labelW, y, rowH);
            var clicked = -1;
            // Fitting on one line, the choices stand joined as one group, a hair's seam between them;
            // else each wraps as a segment of its own.
            var widths = new float[names.Count];
            var whole = 0f;
            for (var i = 0; i < names.Count; i++)
            {
                widths[i] = Skin.Width(Skin.SegmentOn, names[i]) + U(10f);
                whole += widths[i] + 1f;
            }
            if (labelW + whole <= width)
            {
                var x = labelW;
                for (var i = 0; i < names.Count; i++)
                {
                    var lit = on != null ? on(i) : i == selected;
                    if (GUI.Button(new Rect(x, y, widths[i], rowH), names[i], Skin.Choice(i, names.Count, lit))) clicked = i;
                    x += widths[i] + 1f;
                }
                y += rowH + U(8f);
                return clicked;
            }
            var flow = new ChipFlow(labelW, width, y, rowH, U(4f), U(4f));
            for (var i = 0; i < names.Count; i++)
            {
                var lit = on != null ? on(i) : i == selected;
                var style = lit ? Skin.SegmentOn : Skin.Segment;
                var w = Skin.Width(style, names[i]) + U(10f);
                var at = flow.Place(w);
                if (GUI.Button(new Rect(at.X, at.Y, w, flow.RowHeight), names[i], style)) clicked = i;
            }
            y = flow.RowBottom + U(8f);
            return clicked;
        }

        // ----- Tooltips -----

        /// <summary>The panel's foot line for each view and setting, once made.</summary>
        private static readonly string[] FootHints = new string[8];

        /// <summary>The foot's hint for the view and settings now (<see cref="PanelWords.FootHint"/>), made once for each.</summary>
        private static string FootHint()
        {
            var walk = Settings.WalkWhileOpen;
            var look = Settings.LookWithRightMouse;
            var index = (_compact ? 4 : 0) + (walk ? 2 : 0) + (look ? 1 : 0);
            return FootHints[index] ?? (FootHints[index] = PanelWords.FootHint(!_compact, walk, look));
        }

        // ----- What is off -----

        private static int _offCount = -1;
        private static List<Feature> _offFeatures = new List<Feature>();
        private static string _offLine;

        /// <summary>How much was off when the notice was put away; it comes back when more goes off.</summary>
        private static int _offPutAway;
        private static Vector2 _offScroll;

        /// <summary>What is off, read again whenever how much is off changes, not every frame.</summary>
        private static void ReadOff()
        {
            var count = Compatibility.OffCount;
            if (count == _offCount) return;
            _offCount = count;
            _offFeatures = count > 0 ? Compatibility.FeaturesOff() : new List<Feature>();
            _offLine = OffWords.Line(_offFeatures);
        }

        /// <summary>How tall the notice under the header is: none while nothing is off or it was put away.</summary>
        private static float OffNoticeHeight()
        {
            ReadOff();
            return _offFeatures.Count > _offPutAway ? U(32f) : 0f;
        }

        /// <summary>
        /// A strip under the header while part of Scry is off, so nothing fails quietly: what is
        /// off in a line, sliding when it is longer than the strip, details in the list's place,
        /// and a cross that puts it away until more goes off; the compact view's step at its end
        /// where given. What was clicked.
        /// </summary>
        private static StripClick OffNotice(Rect rect, string step = null)
        {
            if (_offLine == null) return StripClick.None;
            var clicked = NoticeStrip(rect, Skin.Warn, _offLine, Skin.WarnLabel, true, "Details", _card == ListCard.Off, true, crossTip: "Put this away until more of Scry goes off", step: step);
            switch (clicked)
            {
                case StripClick.Chip:
                    ToggleCard(ListCard.Off);
                    break;
                case StripClick.Cross:
                    _offPutAway = _offFeatures.Count;
                    CloseCard(ListCard.Off);
                    break;
            }
            return clicked;
        }

        // ----- The notice of what the world's server sets -----

        private static WorldRules _rulesFor;
        private static string _rulesLine;

        /// <summary>The notice's line for the rules given, made again only when they change.</summary>
        private static string RulesLine(WorldRules rules)
        {
            if (ReferenceEquals(rules, _rulesFor)) return _rulesLine;
            _rulesFor = rules;
            return _rulesLine = WorldRulesWords.Notice(rules);
        }

        /// <summary>The notice of what the world's server sets as it shows now; none while it is not up. For the self-test.</summary>
        internal static string RulesNoticeLine => RulesNoticeHeight() > 0f ? _rulesLine : null;

        /// <summary>How tall the notice of what the world's server sets is: none where it sets nothing, or where the notice as it reads now was told in this world.</summary>
        private static float RulesNoticeHeight()
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return 0f;
            var line = RulesLine(book.World);
            return line != null && !book.ToldRules(CompendiumMap.World, line) ? U(32f) : 0f;
        }

        /// <summary>
        /// A strip under the header told once on joining a world whose server sets any of the
        /// compendium's rules, as the box's locks alone are easy to miss: what it sets in a line,
        /// Show the rules opening the compendium's guide, and a cross putting it away; either keeps
        /// it told in this world until the server's rules change. The compact view's step at its
        /// end where given. What was clicked.
        /// </summary>
        private static StripClick RulesNotice(Rect rect, string step = null)
        {
            CountDrawn(PanelPart.RulesNotice);
            var clicked = NoticeStrip(rect, Skin.Accent, _rulesLine, Skin.Label, false, WorldRulesWords.ShowRules, GuideShown, true, crossTip: WorldRulesWords.PutAway, step: step);
            if (clicked == StripClick.Chip) ShowGuide(true);
            if (clicked == StripClick.Chip || clicked == StripClick.Cross) TellRules();
            return clicked;
        }

        /// <summary>Keeps the notice of what the world's server sets told in this world: its chip and cross, and the self-test.</summary>
        internal static void TellRules()
        {
            var book = CompendiumKeeper.Book;
            if (book == null || _rulesLine == null) return;
            book.TellRules(CompendiumMap.World, _rulesLine);
            CompendiumKeeper.Save();
        }

        /// <summary>What the character learned since the panel last told of it (<see cref="LearnedNotice"/>).</summary>
        private static readonly LearnedNotice LearnedNews = new LearnedNotice();

        private static List<Entry> _newsFor;
        private static string _newsLine;

        /// <summary>The notice's line as last drawn, for the self-test.</summary>
        internal static string NewsLine => _newsLine;

        /// <summary>Lets go of the book and entries the notice of what was learned holds, as on leaving a world.</summary>
        private static void ForgetNews()
        {
            LearnedNews.Forget();
            _newsFor = null;
        }

        /// <summary>The compendium's strip under the header: a warning while peeking, else what was newly learned; none otherwise.</summary>
        private static float CompendiumStripHeight(Explorer explorer) => explorer.Peeking || NewsUp(explorer) ? U(32f) : 0f;

        /// <summary>Whether the notice tells what was newly learned: news there is, and the notice tells it (<see cref="Settings.NewsNotice"/>), else the Learned tab alone does.</summary>
        private static bool NewsUp(Explorer explorer) => Settings.NewsNotice && LearnedNews.New(explorer).Count > 0;

        /// <summary>
        /// Warns that everything shows while peeking, with a chip that stops it; else names what
        /// the character learned since last told, with a chip listing what it learned lately and
        /// a cross putting the notice away until it learns more; the compact view's step at its
        /// end where given. What was clicked.
        /// </summary>
        private static StripClick CompendiumStrip(Explorer explorer, Rect rect, string step = null)
        {
            if (explorer.Peeking)
            {
                CountDrawn(PanelPart.PeekStrip);
                var peeked = NoticeStrip(rect, Skin.Warn, CompendiumWords.PeekingLine(_compact), Skin.WarnLabel, true, CompendiumWords.StopPeeking, false, false, lineTip: CompendiumWords.PeekingLineTip(_compact), step: step);
                PeekLineFitsForTest = _stripLineFits;
                if (peeked == StripClick.Chip) Peek(explorer, false);
                return peeked;
            }

            var news = LearnedNews.New(explorer);
            if (!ReferenceEquals(news, _newsFor))
            {
                _newsFor = news;
                // What the explored map brought is counted, as it may be hundreds of places at once;
                // the rest is told by what happened, by day (JournalWords.News).
                _newsLine = JournalWords.News(news.Where(e => !explorer.FromMap(e)).Select(explorer.NewsLineOf).ToList(),
                    news.Count(e => explorer.FromMap(e) && e.Kind != Kind.Biome), news.Count(e => explorer.FromMap(e) && e.Kind == Kind.Biome));
            }
            CountDrawn(PanelPart.LearnedStrip);
            var clicked = NoticeStrip(rect, Skin.Accent, _newsLine, Skin.Label, false, CompendiumWords.ShowLearned, explorer.LearnedOnly, true,
                chipTip: CompendiumWords.LearnedTip(false), crossTip: CompendiumWords.PutNewsAway, step: step);
            switch (clicked)
            {
                case StripClick.Chip:
                    LearnedNews.Told();
                    ShowLearned(explorer, true);
                    break;
                case StripClick.Cross:
                    LearnedNews.Told();
                    break;
            }
            return clicked;
        }

        // ----- Who plays with Scry -----

        /// <summary>When the notice naming the players whose games lack Scry shows (<see cref="GapNotice"/>).</summary>
        private static GapNotice _gaps = new GapNotice();

        private static int _withVersion = -1;
        private static string _withLine;
        private static string _withRow;

        /// <summary>The faint line under the row: what a game without Scry means, and what the server's lacking leaves out where it lacks it.</summary>
        private static string _withHintSaid = PlayingWithWords.RowHint;

        /// <summary>The compendium box's row of the server and players played with, and its faint line, as last read; for the self-test.</summary>
        [Diagnostic]
        public static (string Row, string Hint) PlayingWithForTest
        {
            get
            {
                ReadPlayingWith();
                return (_withRow, _withHintSaid);
            }
        }
        private static List<string> _lacking = new List<string>();

        /// <summary>The notice's line and the box's row as last told, for the self-test.</summary>
        internal static string PlayingWithLine => _withLine;

        internal static string PlayingWithRow => _withRow;

        /// <summary>A notice of the self-test's own in place of the player's, so what it closes is left as it was; the one it replaced.</summary>
        internal static GapNotice GapNoticeForTest(GapNotice notice)
        {
            var was = _gaps;
            _gaps = notice ?? new GapNotice();
            return was;
        }

        /// <summary>What is told of the players played with, read again whenever they change (<see cref="EventLink.PlayingWithVersion"/>), not every frame.</summary>
        private static void ReadPlayingWith()
        {
            if (EventLink.PlayingWithVersion == _withVersion) return;
            _withVersion = EventLink.PlayingWithVersion;
            var players = EventLink.PlayingWith;
            _withLine = PlayingWithWords.Notice(players);
            _withRow = PlayingWithWords.Row(players, EventLink.Server);
            _withHintSaid = PlayingWithWords.Hint(EventLink.Server);
            _lacking = PlayingWithWords.Lacking(players);
        }

        /// <summary>How tall the notice naming players without Scry is: none with the compendium off, every player's game with Scry, or the notice put away for them.</summary>
        private static float PlayingWithNoticeHeight()
        {
            var book = CompendiumKeeper.Book;
            if (book == null || book.Rule == CompendiumRule.Off) return 0f;
            ReadPlayingWith();
            return _withLine != null && _gaps.Shows(_lacking) ? U(32f) : 0f;
        }

        /// <summary>
        /// A strip under the header naming the players whose games lack Scry, as what runs in
        /// their games counts nothing for the character: a chip opening the compendium's box,
        /// which lists everyone played with, and a cross putting it away until another such
        /// player joins; the compact view's step at its end where given. What was clicked.
        /// </summary>
        private static StripClick PlayingWithNotice(Rect rect, string step = null)
        {
            CountDrawn(PanelPart.PlayingWithNotice);
            var clicked = NoticeStrip(rect, Skin.Accent, _withLine, Skin.Label, false, PlayingWithWords.WhoHasScry, _bookPinned, true, crossTip: PlayingWithWords.PutAway, step: step);
            switch (clicked)
            {
                case StripClick.Chip:
                    ShowWhoHasScry(!_bookPinned);
                    break;
                case StripClick.Cross:
                    PutPlayingWithAway();
                    break;
            }
            return clicked;
        }

        // ----- The compact view's one notice strip -----

        /// <summary>Which notice the compact view's one strip shows (<see cref="NoticeLine"/>).</summary>
        private static readonly NoticeLine Notices = new NoticeLine();

        /// <summary>The notices up now, made again each time they are asked for in this one list.</summary>
        private static readonly List<Notice> NoticesUp = new List<Notice>();

        /// <summary>The step's words as last made, kept while where the notice stands is the same.</summary>
        private static (int Which, int Of, string Said) _noticeStep;

        /// <summary>The notice the strip last showed, where it stood among those up and how many were; for the self-test.</summary>
        [Diagnostic]
        public static (Notice? Shown, int Which, int Of) NoticeLineForTest { get; private set; }

        /// <summary>Steps the compact view's strip on to the next notice, as its step does; for the self-test.</summary>
        [Diagnostic]
        public static void StepNoticeForTest()
        {
            if (_explorer != null) Notices.Step(NoticesUp.Count > 0 ? NoticesUp : NoticesUpNow(_explorer));
        }

        /// <summary>The notices up now, in the order of how pressing each is (<see cref="Notice"/>): peeking keeps the news back, as the full view's strip does.</summary>
        private static List<Notice> NoticesUpNow(Explorer explorer)
        {
            NoticesUp.Clear();
            if (OffNoticeHeight() > 0f) NoticesUp.Add(Notice.Off);
            if (RulesNoticeHeight() > 0f) NoticesUp.Add(Notice.Rules);
            if (explorer.Peeking) NoticesUp.Add(Notice.Peeking);
            if (PlayingWithNoticeHeight() > 0f) NoticesUp.Add(Notice.PlayingWith);
            if (!explorer.Peeking && NewsUp(explorer)) NoticesUp.Add(Notice.Learned);
            return NoticesUp;
        }

        /// <summary>How tall the compact view's notices are: one strip while any is up.</summary>
        private static float NoticeLineHeight(Explorer explorer) => NoticesUpNow(explorer).Count > 0 ? U(32f) : 0f;

        /// <summary>
        /// The compact view's one notice strip: the notice shown drawn as its own strip is in the
        /// full view, with where it stands among those up and a step on to the next at its end
        /// while more than one is up.
        /// </summary>
        private static void NoticeLineStrip(Explorer explorer, Rect rect)
        {
            var up = NoticesUpNow(explorer);
            var shown = Notices.Shown(up);
            var (which, of) = Notices.Place(up);
            NoticeLineForTest = (shown, which, of);
            if (shown == null) return;
            if (of > 1 && (_noticeStep.Which != which || _noticeStep.Of != of)) _noticeStep = (which, of, PanelWords.NoticeStep(which, of));
            var step = of > 1 ? _noticeStep.Said : null;
            StripClick clicked;
            switch (shown.Value)
            {
                case Notice.Off:
                    clicked = OffNotice(rect, step);
                    break;
                case Notice.Rules:
                    clicked = RulesNotice(rect, step);
                    break;
                case Notice.PlayingWith:
                    clicked = PlayingWithNotice(rect, step);
                    break;
                default:
                    clicked = CompendiumStrip(explorer, rect, step);
                    break;
            }
            if (clicked == StripClick.Step) Notices.Step(up);
            CountDrawn(PanelPart.NoticeLine);
        }

        /// <summary>Opens the compendium's box with whom the character plays with, in the full view, or lets it close: the notice's chip, and the self-test.</summary>
        internal static void ShowWhoHasScry(bool show)
        {
            if (show) Compact = false;
            PinCompendiumBox(show);
        }

        /// <summary>Puts the notice away until another player without Scry joins: its cross, and the self-test.</summary>
        internal static void PutPlayingWithAway()
        {
            _gaps.Close(_lacking);
            PinCompendiumBox(false);
        }

        /// <summary>Starts or stops showing everything though the compendium is on (<see cref="Explorer.Peeking"/>), keeping the list where it was.</summary>
        private static void Peek(Explorer explorer, bool peek)
        {
            explorer.Peeking = peek;
            ListFiltered();
            RevealSelected();
        }

        /// <summary>Lists what the character learned lately, newest first, or the list as before.</summary>
        private static void ShowLearned(Explorer explorer, bool on)
        {
            explorer.LearnedOnly = on;
            ListFiltered();
            RevealSelected();
        }

        internal enum StripClick
        {
            None,
            Chip,
            Cross,

            /// <summary>The compact view's step on to the next notice.</summary>
            Step,
        }

        /// <summary>Whether the last strip's line fitted it, or slid; and whether the peeking strip's did, for the self-test.</summary>
        private static bool _stripLineFits;

        [Diagnostic]
        public static bool PeekLineFitsForTest { get; private set; }

        /// <summary>
        /// A notice under the header (the self-test's, what is off): tinted in its tone with a bar
        /// at its left, a mark where it warns, its line sliding where it is longer than the strip
        /// and the rest of what it says in the line's tip where it has one, a chip, a cross that
        /// puts it away where it can be, and where given the compact view's step on to the next
        /// notice at its end, in the line's colour and lit under the mouse. Returns what was clicked.
        /// </summary>
        internal static StripClick NoticeStrip(Rect rect, Color tone, string line, GUIStyle lineStyle, bool mark, string chip, bool chipOn, bool cross, string chipTip = null, string crossTip = null, string lineTip = null, string step = null)
        {
            Skin.Fill(rect, Skin.Alpha(tone, 0.13f));
            Skin.Fill(new Rect(rect.x, rect.y, U(3f), rect.height), tone);
            var left = rect.x + U(14f);
            if (mark)
            {
                var size = U(18f);
                var markRect = new Rect(rect.x + U(12f), rect.y + (rect.height - size) / 2f, size, size);
                Skin.Icon(markRect, Skin.Circle, tone);
                GUI.Label(markRect, "!", Skin.Mark);
                left = markRect.xMax + U(10f);
            }

            var chipH = U(24f);
            var stepW = step != null ? Skin.Width(lineStyle, step) + U(12f) : 0f;
            var stepAt = new Rect(rect.xMax - U(4f) - stepW, rect.y + (rect.height - chipH) / 2f, stepW, chipH);
            var right = step != null ? stepAt.x - U(2f) : rect.xMax;
            var crossW = U(24f);
            var away = new Rect(right - crossW - U(4f), rect.y + (rect.height - crossW) / 2f, crossW, crossW);
            var chipW = Skin.Width(Skin.Chip, chip) + U(12f);
            var button = new Rect((cross ? away.x - U(6f) : right - U(4f)) - chipW, rect.y + (rect.height - chipH) / 2f, chipW, chipH);
            var lineRect = new Rect(left, rect.y, button.x - left - U(8f), rect.height);
            _stripLineFits = Skin.Width(lineStyle, line) <= lineRect.width;
            Ticker(lineRect, line, lineStyle);
            if (lineTip != null && lineRect.Contains(Event.current.mousePosition)) AskTip("strip-line:" + chip, lineTip);

            var clicked = StripClick.None;
            if (GUI.Button(button, chip, Skin.Fitted(chipOn ? Skin.ChipOn : Skin.Chip, button))) clicked = StripClick.Chip;
            if (chipTip != null && button.Contains(Event.current.mousePosition)) AskTip("strip:" + chip, chipTip);
            if (step != null)
            {
                if (stepAt.Contains(Event.current.mousePosition))
                {
                    if (Event.current.type == EventType.Repaint) Skin.Fill(stepAt, Skin.Alpha(Skin.Text, 0.08f));
                    AskTip("strip-step", PanelWords.NoticeStepTip);
                }
                GUI.Label(new Rect(stepAt.x + U(6f), stepAt.y, stepAt.width - U(12f), stepAt.height), step, lineStyle);
                if (GUI.Button(stepAt, GUIContent.none, GUIStyle.none)) clicked = StripClick.Step;
            }
            if (!cross) return clicked;
            if (GUI.Button(away, "\u00D7", Skin.Close)) clicked = StripClick.Cross;
            if (crossTip != null && away.Contains(Event.current.mousePosition)) AskTip("strip-away:" + chip, crossTip);
            return clicked;
        }

        /// <summary>The notice's details in the list's place: why, what to do, and each part that is off.</summary>
        private static void OffCard(Rect rect)
        {
            var width = CardWidth(rect);
            var body = OffWords.Details();
            var bodyH = Skin.Height(Skin.DimWrap, body, width);
            var height = U(44f) + bodyH + U(12f) + _offFeatures.Count * U(26f) + U(12f);
            var card = BeginCard(rect, ref _offScroll, height, "What is off");
            var x = card.X;
            var y = card.Y;
            GUI.Label(new Rect(x, y, width, bodyH), body, Skin.DimWrap);
            y += bodyH + U(12f);
            foreach (var feature in _offFeatures)
            {
                Skin.Fill(new Rect(x, y + U(8f), U(6f), U(6f)), Skin.Warn);
                GUI.Label(new Rect(x + U(16f), y, width - U(16f), U(22f)), Naming.Capital(feature.Name), Skin.Label);
                y += U(26f);
            }
            if (EndCard(rect, out var close)) CloseCard(ListCard.Off);

            // For reporting: what is off, with the versions, ready to paste.
            if (CardButton(rect, close, "Copy for a report", out _))
            {
                if (!Guard.Run(Feature.Panel, "telling the game's version", GameVersion, out var game)) game = "of an unknown version";
                GUIUtility.systemCopyBuffer = OffWords.Report(About.Version, game, _offFeatures);
                Say("Copied what is off, with Scry's and the game's versions.");
            }
        }

        /// <summary>The game's version, in a method of its own so that an update renaming it fails only here.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static string GameVersion() => global::Version.GetVersionString();
    }
}
