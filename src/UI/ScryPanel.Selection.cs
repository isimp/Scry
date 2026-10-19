using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>The selected entry beside the list: its sections laid out, folded and scrolled, and its title.</summary>
    internal static partial class ScryPanel
    {
        private static float _sideHeight;
        private static float _stageBaseH = 300f;

        /// <summary>The stage's height at the most as the page was last laid out, the header under it kept in the panel; a drag of its edge stops there.</summary>
        private static float _stageMost = float.MaxValue;

        /// <summary>How much of the page the stage leaves under the page's header at the least, before scaling: a row of what follows.</summary>
        private const float PageKept = 40f;

        /// <summary>How tall a page not opened yet is under its stage, before scaling: its name and the lines saying how to know it.</summary>
        private const float ClosedPageHeight = 120f;

        /// <summary>Whether the page's header was last drawn whole inside the panel, for the self-test.</summary>
        [Diagnostic]
        public static bool HeaderInsideForTest { get; private set; }

        /// <summary>Where the details are scrolled to.</summary>
        private static Vector2 _sideScroll;

        private static void DetailsFromTop() => _sideScroll = Vector2.zero;

        /// <summary>What of the scrolled side can be seen, in its own terms, so rows of chips out of sight are only counted, not drawn.</summary>
        private static Rect _sideVisible;

        private static bool OutOfSight(Rect rect) => OutOfSight(rect, _sideVisible);

        /// <summary>Whether a row lies wholly above or below what of its scrolled view can be seen.</summary>
        private static bool OutOfSight(Rect rect, Rect visible) => rect.yMax < visible.yMin || rect.yMin > visible.yMax;

        /// <summary>How many chips of a list show before the rest are left to a "more" chip.</summary>
        private const int FirstChips = 18;

        /// <summary>Long lists opened to show all, by key; all fold again when another entry is shown.</summary>
        private static readonly HashSet<string> OpenLists = new HashSet<string>();

        /// <summary>How many of a list to show (<see cref="Shortlist"/>).</summary>
        private static int ShownOf(string key, int total, int first = FirstChips) => Shortlist.Shown(total, first, OpenLists.Contains(key));

        /// <summary>
        /// The chip at the end of a long list, placed as its own chips are: "N more" shows the
        /// rest, "Show fewer" folds it again. Every long list in the panel ends with it.
        /// </summary>
        private static void MoreChip(string key, int total, int first, float width, ref ChipFlow flow) => MoreChip(key, total, first, width, ref flow, _sideVisible);

        private static void MoreChip(string key, int total, int first, float width, ref ChipFlow flow, Rect visible)
        {
            if (!Shortlist.Long(total, first)) return;
            var open = OpenLists.Contains(key);
            var text = open ? "Show fewer" : PanelWords.More(Shortlist.Hidden(total, first, false));
            var w = Mathf.Min(width, Skin.Width(Skin.Chip, text) + U(16f));
            var at = flow.Place(w);
            var rect = new Rect(at.X, at.Y, w, flow.RowHeight);
            if (!OutOfSight(rect, visible) && GUI.Button(rect, text, Skin.Fitted(Skin.Chip, rect)))
            {
                if (open) OpenLists.Remove(key);
                else OpenLists.Add(key);
            }
        }

        private static bool AnyOpen()
        {
            foreach (var key in Foldable) if (!Folded.Contains(key)) return true;
            return false;
        }

        /// <summary>The icon at the section line's end that folds every section away, or opens every one where none is open; always in the same place whatever is selected.</summary>
        private static void FoldAllIcon(Rect rect, bool anyOpen)
        {
            var hover = rect.Contains(Event.current.mousePosition);
            if (GUI.Button(rect, GUIContent.none, Skin.IconButton)) FoldAll(anyOpen);
            Skin.Icon(new Rect(rect.x + U(3f), rect.y + U(2f), rect.width - U(6f), rect.height - U(4f)), anyOpen ? Skin.FoldAllMark : Skin.OpenAllMark, hover ? Skin.Text : Skin.Dim);
            if (hover) AskTip("fold-all", PanelWords.FoldAllTip(anyOpen));
            if (Event.current.type == EventType.Repaint) ToolbarPartsForTest.Add(("fold", rect));
        }

        /// <summary>A square button's icon, inset from its edges.</summary>
        private static Rect Inset(Rect rect) => new Rect(rect.x + U(4f), rect.y + U(4f), rect.width - U(8f), rect.height - U(8f));

        /// <summary>
        /// The toolbar under the name: the entry's actions as one joined group from the left, the
        /// slider icons as one at the first line's right end, the actions wrapping as a group
        /// under them where the line has no room (<see cref="PageToolbar"/>).
        /// </summary>
        private static float Toolbar(Explorer explorer, Entry entry, float width, float y, PageToolbar.Laid laid, bool sized, bool withStage)
        {
            if (laid.Lines == 0) return y;
            CountDrawn(PanelPart.Toolbar);
            DrawActions(entry, withStage, new Vector2(0f, y));
            // The faint caption after the buttons, cut short of the icons on their line.
            if (_actionCaption != null && laid.CaptionLine >= 0)
            {
                var end = laid.CaptionLine == 0 && laid.IconsX < width ? laid.IconsX - U(8f) : width;
                var at = new Rect(laid.CaptionX, y + laid.CaptionLine * (ToolbarRow + ToolbarLineGap), Mathf.Max(0f, end - laid.CaptionX), ToolbarRow);
                GUI.Label(at, Skin.Fit(Skin.FaintLabel, _actionCaption, at.width), Skin.FaintLabel);
                if (Event.current.type == EventType.Repaint) ToolbarPartsForTest.Add(("caption", at));
            }
            SlideIcons(entry, explorer.Modifiers, laid.IconsX, y, sized);
            return y + ToolbarHeight(laid.Lines);
        }

        /// <summary>How tall the toolbar is for so many lines, with the gap under it; nothing for none.</summary>
        private static float ToolbarHeight(int lines) => lines > 0 ? lines * ToolbarRow + (lines - 1) * ToolbarLineGap + U(10f) : 0f;

        /// <summary>The entry the side was last shown for; another starts at its top.</summary>
        private static Entry _sideFor;

        private static void Side(Explorer explorer, Rect rect, bool withStage)
        {
            var entry = explorer.Selected;
            if (!ReferenceEquals(entry, _sideFor))
            {
                _sideFor = entry;
                _sideScroll = Vector2.zero;
                OpenLists.Clear();
                ForgetSectionLine();
            }

            // What a world's catalog points at is gone once the world is: nothing to show then.
            if (entry != null && entry.Source is UnityEngine.Object source && source == null)
            {
                explorer.Select(null);
                entry = null;
            }
            if (entry == null)
            {
                Skin.Box(rect, Skin.Panel);
                var middle = new Rect(rect.x + U(30f), rect.y + rect.height / 2f - U(40f), rect.width - U(60f), U(80f));
                GUI.Label(new Rect(middle.x, middle.y, middle.width, U(30f)), "Pick something from the list", Skin.Center);
                GUI.Label(new Rect(middle.x, middle.y + U(32f), middle.width, U(44f)),
                    "Type to search, use the arrow keys to move, and Enter to play or show it.", Skin.CenterDim);
                return;
            }

            // With the compendium on, a page opens once the character knows its entry.
            var level = explorer.LevelOf(entry);
            if (level != KnownLevel.Known)
            {
                ClosedPage(explorer, entry, level, rect, withStage);
                return;
            }

            var top = rect.y;

            // The header stays in sight while what is below it scrolls, so a long section scrolled
            // to never leaves the selection unnamed: the name, the toolbar under it (what it does,
            // its sliders) and a line of the sections going to each, fold all at its end. What the
            // toolbar holds is measured first, as it decides how tall the header is, and the stage
            // over it gives way so the header and a little of the page stay inside the panel.
            var width = rect.width - U(14f);
            MeasureActions(entry, withStage);
            var sized = Adjustable(entry, withStage, out var staged) && staged;
            var caption = _actionCaption != null ? Skin.Width(Skin.FaintLabel, _actionCaption) + U(2f) : 0f;
            var laid = PageToolbar.Lay(width, ActionWidths, SlideIconCount(entry, sized), ToolbarRow, U(1f), U(8f), ActionPlaces, caption);
            var lineH = SectionLineHeight(width);
            var headerH = TitleHeight(explorer, entry) + ToolbarHeight(laid.Lines) + lineH;
            if (withStage) top += SideStage(explorer, entry, rect, headerH + U(PageKept));
            // Cut at the panel's end where even the stage's least leaves too little room.
            var titleArea = new Rect(rect.x, top, width, Mathf.Max(0f, Mathf.Min(headerH, rect.yMax - top)));
            if (Event.current.type == EventType.Repaint) HeaderInsideForTest = top + headerH <= rect.yMax + 0.5f;
            var shown = Mathf.Max(0f, rect.yMax - titleArea.yMax);
            var anyOpen = AnyOpen();
            if (Event.current.type == EventType.Repaint) ToolbarPartsForTest.Clear();
            GUI.BeginGroup(titleArea);
            var titleH = Section("side title", 0f, at => Title(explorer, entry, titleArea.width, at));
            titleH = Section("side toolbar", titleH, at => Toolbar(explorer, entry, titleArea.width, at, laid, sized, withStage));
            titleH = Section("side sections", titleH, at => SectionLineRow(titleArea.width, at, shown, Mathf.Max(0f, _sideHeight - shown), anyOpen));
            GUI.EndGroup();
            top += titleH;

            var below = new Rect(rect.x, top, rect.width, Mathf.Max(0f, rect.yMax - top));
            var content = new Rect(0f, 0f, below.width - U(14f), Mathf.Max(_sideHeight, below.height));
            _sideScroll = GUI.BeginScrollView(below, _sideScroll, content, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);
            _sideVisible = new Rect(0f, _sideScroll.y, content.width, below.height);

            var cw = content.width;
            var y = 0f;
            StartMarking();
            // Without a stage a sound says what it is here; a status effect's card would only repeat In the game.
            if (!withStage && entry.Kind == Kind.Sound) y = Section("side card", y, at => CompactCard(entry, cw, at));
            y = Section("side actions", y, at => ActionNotes(entry, cw, at, withStage));
            if (Looks.IsWorn(entry)) y = Section("side wearing", y, at => Wearing(explorer, cw, at));
            if (entry.Kind == Kind.Sound)
            {
                // With a stage the timeline is on the sound's card.
                if (!withStage) y = Section("side timeline", y, at => Timeline(cw, at));
                y = Section("side variants", y, at => Variants(entry, cw, at));
            }

            // How it plays comes right under what can be done with it, then what it is in the game:
            // an item's stats and recipe are what most look for, and come before a creature's
            // hundreds of clips.
            y = Section("side adjust", y, at => Adjust(explorer, entry, cw, at, withStage));
            y = Section("side facts", y, at => FactsSection(explorer, entry, cw, at));
            y = Section("side tooltip", y, at => TooltipSection(explorer, entry, cw, at));
            y = Section("side runes", y, at => RunesSection(explorer, entry, cw, at));
            y = Section("side readme", y, at => ReadmeSection(entry, cw, at));
            y = Section("side animations", y, at => Animations(explorer, entry, cw, at, withStage));
            y = Section("side effects", y, at => Effects(explorer, entry, cw, at, withStage));
            y = Section("side plays in", y, at => PlaysInSection(explorer, entry, cw, at));
            y = Section("side links", y, at => LinksSection(explorer, entry, cw, at));
            // The game's commands that spawn or give a thing are no part of what a character knows.
            if (explorer.ShowsCommands) y = Section("side command", y, at => Command(explorer, entry, cw, at));
            y = Section("side details", y, at => Details(explorer, entry, cw, at));
            EndMarking();
            // The height as this draw found it, so the scroll range is right from the next event on,
            // and a scroll left past the end by content that shrank comes back to the end.
            _sideHeight = y + U(8f);
            GUI.EndScrollView();
            var maxScroll = Mathf.Max(0f, _sideHeight - below.height);
            if (ArriveAtSection(maxScroll) is float section) _sideScroll.y = section;
            _sideScroll.y = Mathf.Clamp(_sideScroll.y, 0f, maxScroll);
        }

        /// <summary>
        /// The stage at the top of the side; how much of the side it takes, leaving <paramref name="keep"/>
        /// under it and its edge however far it is dragged (<see cref="SideSplit"/>). A sound or a status
        /// effect with nothing to show on a person gets a card only as tall as what it holds, so
        /// its details start higher; a model gets the stage.
        /// </summary>
        private static float SideStage(Explorer explorer, Entry entry, Rect rect, float keep)
        {
            var card = CardHeight(entry);
            _stageBaseH = Mathf.Round(Mathf.Min(rect.width * 0.60f, rect.height * 0.50f));
            _stageMost = Mathf.Max(U(120f), SideSplit.StageHeight(float.MaxValue, rect.height, U(120f), keep + U(12f)));
            var stageH = card > 0f ? card : Mathf.Round(SideSplit.StageHeight(_stageBaseH * _stageScale, rect.height, U(120f), keep + U(12f)));
            Section("side stage", 0f, _ => { StageArea(explorer, entry, new Rect(rect.x, rect.y, rect.width, stageH)); return 0f; });
            if (card <= 0f) StageHandle(new Rect(rect.x, rect.y + stageH, rect.width, U(12f)));
            return stageH + U(12f);
        }

        /// <summary>
        /// A page the character has not opened yet: what it is named (something never met only by its
        /// kind), how much is known of it and what would open it. What was seen stands on the
        /// stage; nothing else does.
        /// </summary>
        private static void ClosedPage(Explorer explorer, Entry entry, KnownLevel level, Rect rect, bool withStage)
        {
            CountDrawn(PanelPart.ClosedPage);
            var top = rect.y;
            if (withStage && level == KnownLevel.Seen && CardHeight(entry) <= 0f) top += SideStage(explorer, entry, rect, U(ClosedPageHeight));
            Skin.Box(new Rect(rect.x, top, rect.width, rect.yMax - top), Skin.Panel);
            var x = rect.x + U(16f);
            var width = rect.width - U(32f);
            var name = level == KnownLevel.Unknown ? CompendiumWords.Unknown(entry.Kind) : entry.ShownName;
            FitLabel(new Rect(x, top + U(12f), width, U(30f)), name, Skin.Big, 13f);
            GUI.Label(new Rect(x, top + U(46f), width, U(22f)), CompendiumWords.Closed(level), Skin.DimLabel);
            GUI.Label(new Rect(x, top + U(70f), width, U(22f)), CompendiumWords.HowToKnow(entry.Kind), Skin.FaintLabel);
            // Under the strict rules, coming to know it opens its page, but not all of it at once.
            if (explorer.CompendiumOn && explorer.Compendium.Rule == CompendiumRule.Strict) GUI.Label(new Rect(x, top + U(92f), width, U(22f)), CompendiumWords.ClosedStrict, Skin.FaintLabel);
        }

        /// <summary>
        /// Draws one section of the side, timed. A section that fails is left out, told once in the
        /// log, and the others still draw: a mod's odd prefab costs one section, not the panel.
        /// No section opens a group or scroll view of its own, so leaving one halfway is safe.
        /// </summary>
        private static float Section(string part, float y, System.Func<float, float> draw)
        {
            var started = Timing.Start();
            try
            {
                if (Guard.Run(Feature.DetailsShown, part, () => draw(y), out var after)) return after;
                GUI.enabled = true;
                GUI.color = Color.white;
                return y;
            }
            finally
            {
                Timing.Add(part, started);
            }
        }

        /// <summary>How tall the name's rows are: the name, the line under it, and the floor's line where the floor shows it.</summary>
        private static float TitleHeight(Explorer explorer, Entry entry) => U(34f) + U(26f) + (explorer.ByFloor(entry) ? U(24f) : 0f) + U(8f);

        /// <summary>Where the header's parts were last drawn, by what each is: the name, copy, star, each action and slider icon, the section line and fold all; for the self-test.</summary>
        [Diagnostic]
        public static List<(string Part, Rect At)> ToolbarPartsForTest { get; } = new List<(string, Rect)>();

        private static float Title(Explorer explorer, Entry entry, float width, float y)
        {
            var primary = entry.ShownName;
            var repaint = Event.current.type == EventType.Repaint;

            // The name, then copying it and keeping it a favourite, square buttons at the row's end.
            var size = ToolbarRow;
            var starRect = new Rect(width - size, y + U(3f), size, size);
            var copyRect = new Rect(starRect.x - U(6f) - size, starRect.y, size, size);
            var nameRect = new Rect(0f, y, copyRect.x - U(10f), U(30f));
            var fits = FitLabel(nameRect, primary, Skin.Big, 13f);
            // With the compendium on the prefab's name stays off the line under the name, as the list leaves it out; it shows on the name's hover.
            var prefabOff = explorer.CompendiumOn && entry.Name != primary;
            if (nameRect.Contains(Event.current.mousePosition) && (!fits || prefabOff))
                AskTip("title", Naming.Lines(fits ? null : primary, prefabOff ? DetailWords.PrefabName(entry.Name) : null));

            if (copyRect.Contains(Event.current.mousePosition)) AskTip("copy-name", ActionWords.CopyName(entry.Name));
            if (GUI.Button(copyRect, GUIContent.none, Skin.IconButton))
            {
                GUIUtility.systemCopyBuffer = entry.Name;
                Say(PanelWords.Copied(entry.Name));
            }
            Skin.Icon(Inset(copyRect), Skin.CopyMark, copyRect.Contains(Event.current.mousePosition) ? Skin.Text : Skin.Dim);

            var favourite = explorer.Favourites.Contains(entry);
            if (GUI.Button(starRect, GUIContent.none, favourite ? Skin.On : Skin.IconButton)) explorer.ToggleFavourite(entry);
            Skin.Icon(Inset(starRect), favourite ? Skin.Star : Skin.StarHollow, favourite ? Skin.Accent : starRect.Contains(Event.current.mousePosition) ? Skin.Text : Skin.Dim);
            if (starRect.Contains(Event.current.mousePosition)) AskTip("star", DetailWords.StarTip(favourite));
            if (repaint)
            {
                ToolbarPartsForTest.Add(("name", nameRect));
                ToolbarPartsForTest.Add(("copy", copyRect));
                ToolbarPartsForTest.Add(("star", starRect));
            }
            y += U(34f);

            // Kind in colour, then the prefab name (when the game shows another, and the compendium is off) and where it comes from.
            var x = _compact ? KindBadge(entry, new Vector2(0f, y)) + U(10f) : 0f;
            var origin = DetailWords.From(entry);
            var sub = entry.Name == primary || prefabOff ? origin : DetailWords.Sub(entry.Name, origin);
            {
                var subRect = new Rect(x, y, width - x, U(22f));
                // Where a mod added it, where it comes from is a link to the mod's own page, or else
                // shows everything it added, drawn as a link; the prefab's name before it is not.
                var linked = entry.Origin == Origin.Mod && entry.ModName.Length > 0 && entry.Kind != Kind.Mod;
                var lead = entry.Name == primary || prefabOff ? "" : DetailWords.SubLead(entry.Name);
                var leadW = lead.Length > 0 ? Skin.Width(Skin.DimLabel, lead) : 0f;
                if (linked && leadW + Skin.Width(Skin.DimLabel, origin) <= subRect.width)
                {
                    if (lead.Length > 0) GUI.Label(new Rect(subRect.x, subRect.y, leadW, subRect.height), lead, Skin.DimLabel);
                    var link = new Rect(subRect.x + leadW, subRect.y, Skin.Width(Skin.DimLabel, origin) + U(2f), subRect.height);
                    LinkLabel(link, origin, Skin.DimLabel, Skin.KindColor(Kind.Mod));
                    ModLink(explorer, entry, link);
                }
                else
                {
                    if (!FitLabel(subRect, sub, Skin.DimLabel, 10f) && subRect.Contains(Event.current.mousePosition)) AskTip("sub", sub);
                    if (linked) ModLink(explorer, entry, subRect);
                }
                y += U(26f);
            }

            // Shown as known only by the compendium's floor: said under the title, with the list's mark.
            if (explorer.ByFloor(entry))
            {
                CountDrawn(PanelPart.FloorLine);
                var mark = U(16f);
                Skin.Icon(new Rect(0f, y + (U(22f) - mark) / 2f, mark, mark), Skin.Steps, Skin.Faint);
                var line = new Rect(mark + U(6f), y, width - mark - U(6f), U(22f));
                var said = CompendiumWords.ByFloor(explorer.Compendium.Floor);
                if (!FitLabel(line, said, Skin.DimLabel, 10f) && line.Contains(Event.current.mousePosition)) AskTip("floor-line", said);
                y += U(24f);
            }

            return y + U(8f);
        }

        /// <summary>Where a mod added the entry: a click on the line saying so goes to the mod's own page, or else shows everything it added.</summary>
        private static void ModLink(Explorer explorer, Entry entry, Rect rect)
        {
            var page = EntryKeys.For(Kind.Mod, entry.ModName);
            var known = Goes(explorer, page);
            var clue = UnsureWords.IsSureClue(entry.ModClue) ? null : UnsureWords.ModClue(entry.ModClue);
            if (rect.Contains(Event.current.mousePosition)) AskTip("mod" + clue, DetailWords.ModTip(clue, entry.ModName, known));
            if (!GUI.Button(rect, GUIContent.none, GUIStyle.none)) return;
            if (known) Go(explorer, page);
            else SearchFor(explorer, SearchHelp.Term("mod", entry.ModName));
        }

        /// <summary>
        /// Draws a label, shrinking its text step by step until it fits, down to a smallest size.
        /// Returns false when even that was too wide and the text is cut.
        /// </summary>
        private static bool FitLabel(Rect rect, string text, GUIStyle style, float smallest)
        {
            var original = style.fontSize;
            var floor = Mathf.Max(1, Mathf.RoundToInt(smallest * _s));
            var fits = Skin.Width(style, text) <= rect.width;
            while (!fits && style.fontSize > floor)
            {
                style.fontSize -= 1;
                fits = Skin.Width(style, text) <= rect.width;
            }
            GUI.Label(rect, text, style);
            style.fontSize = original;
            return fits;
        }

        /// <summary>Lets go of the entry the details were last drawn for.</summary>
        private static void ForgetSelection()
        {
            _sideFor = null;
            ForgetSectionLine();
            ForgetFactPlan();
            ForgetYou();
            ForgetSkillCard();
        }
    }
}
