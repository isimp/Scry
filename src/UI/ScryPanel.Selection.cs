using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The selected entry beside the list: its sections laid out, folded and scrolled, and its title.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>What of the scrolled side can be seen, in its own terms, so rows of chips out of sight are only counted, not drawn.</summary>
        private static Rect _sideVisible;

        private static bool OutOfSight(Rect rect) => rect.yMax < _sideVisible.yMin || rect.yMin > _sideVisible.yMax;

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
        private static void MoreChip(string key, int total, int first, float width, float rowH, float gap, ref float x, ref float y)
        {
            if (!Shortlist.Long(total, first)) return;
            var open = OpenLists.Contains(key);
            var text = open ? "Show fewer" : $"{Numbers.Count(Shortlist.Hidden(total, first, false))} more";
            var w = Mathf.Min(width, Skin.Width(Skin.Chip, text) + U(16f));
            if (x + w > width && x > 0f)
            {
                x = 0f;
                y += rowH + gap;
            }
            var rect = new Rect(x, y, w, rowH);
            if (!OutOfSight(rect) && GUI.Button(rect, text, Skin.Chip))
            {
                if (open) OpenLists.Remove(key);
                else OpenLists.Add(key);
            }
            x += w + gap;
        }

        /// <summary>Room kept at the end of the name's second line for the link that folds every section.</summary>
        private static float _foldAllW;

        private static bool AnyOpen()
        {
            foreach (var key in Foldable) if (!Folded.Contains(key)) return true;
            return false;
        }

        /// <summary>Folds or opens every section, always in the same place whatever is selected.</summary>
        private static void FoldAllLink(Rect link, string text, bool anyOpen)
        {
            LinkLabel(link, text, Skin.FaintLabel, Skin.Faint);
            if (link.Contains(Event.current.mousePosition)) AskTip("fold-all", anyOpen ? "Fold every section away" : "Open every section");
            if (GUI.Button(link, GUIContent.none, GUIStyle.none)) FoldAll(anyOpen);
        }

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

            var top = rect.y;
            if (withStage)
            {
                // A sound or a status effect with nothing to show on a person gets a card only as
                // tall as what it holds, so its details start higher; a model gets the stage.
                var card = CardHeight(entry);
                _stageBaseH = Mathf.Round(Mathf.Min(rect.width * 0.60f, rect.height * 0.50f));
                var stageH = card > 0f ? card : Mathf.Round(Mathf.Clamp(_stageBaseH * _stageScale, U(120f), rect.height * 0.85f));
                Section("side stage", 0f, _ => { StageArea(explorer, entry, new Rect(rect.x, rect.y, rect.width, stageH)); return 0f; });
                if (card <= 0f) StageHandle(new Rect(rect.x, rect.y + stageH, rect.width, U(12f)));
                top += stageH + U(12f);
            }

            // The name stays in sight while what is below it scrolls, so a long section scrolled
            // to never leaves the selection unnamed.
            var titleArea = new Rect(rect.x, top, rect.width - U(14f), U(68f));
            var anyOpen = AnyOpen();
            var foldText = anyOpen ? "fold all" : "open all";
            _foldAllW = Skin.Width(Skin.FaintLabel, foldText) + U(16f);
            GUI.BeginGroup(titleArea);
            var titleH = Section("side title", 0f, at => Title(explorer, entry, titleArea.width, at, withStage));
            FoldAllLink(new Rect(titleArea.width - _foldAllW + U(12f), U(34f), _foldAllW - U(12f), U(22f)), foldText, anyOpen);
            GUI.EndGroup();
            top += titleH;

            var below = new Rect(rect.x, top, rect.width, rect.yMax - top);
            var content = new Rect(0f, 0f, below.width - U(14f), Mathf.Max(_sideHeight, below.height));
            _sideScroll = GUI.BeginScrollView(below, _sideScroll, content, false, false, GUIStyle.none, Skin.Gui.verticalScrollbar);
            _sideVisible = new Rect(0f, _sideScroll.y, content.width, below.height);

            var cw = content.width;
            var y = 0f;
            // Without a stage a sound says what it is here; a status effect's card would only repeat In the game.
            if (!withStage && entry.Kind == Kind.Sound) y = Section("side card", y, at => CompactCard(entry, cw, at));
            y = Section("side actions", y, at => Actions(entry, cw, at, withStage));
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
            y = Section("side runes", y, at => RunesSection(entry, cw, at));
            y = Section("side readme", y, at => ReadmeSection(entry, cw, at));
            y = Section("side animations", y, at => Animations(explorer, entry, cw, at, withStage));
            y = Section("side effects", y, at => Effects(explorer, entry, cw, at, withStage));
            y = Section("side plays in", y, at => PlaysInSection(explorer, entry, cw, at));
            y = Section("side links", y, at => LinksSection(explorer, entry, cw, at));
            y = Section("side command", y, at => Command(explorer, entry, cw, at));
            y = Section("side details", y, at => Details(explorer, entry, cw, at));
            // The height as this draw found it, so the scroll range is right from the next event on,
            // and a scroll left past the end by content that shrank comes back to the end.
            _sideHeight = y + U(8f);
            GUI.EndScrollView();
            _sideScroll.y = Mathf.Clamp(_sideScroll.y, 0f, Mathf.Max(0f, _sideHeight - below.height));
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
                return draw(y);
            }
            catch (ExitGUIException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Faults.Tell("the panel's " + part, ex);
                GUI.enabled = true;
                GUI.color = Color.white;
                return y;
            }
            finally
            {
                Timing.Add(part, started);
            }
        }

        private static float Title(Explorer explorer, Entry entry, float width, float y, bool withStage)
        {
            var primary = string.IsNullOrEmpty(entry.DisplayName) ? entry.Name : entry.DisplayName;
            var copyW = U(104f);

            // Volume and, while the stage shows a model, size: small icons opening their sliders.
            var sized = Adjustable(entry, withStage, out var staged) && staged;
            var iconsW = SlideIcons(entry, explorer.Modifiers, width - copyW - U(44f), y + U(4f), sized) + U(8f);
            var nameRect = new Rect(0f, y, width - copyW - U(46f) - iconsW, U(30f));
            var fits = FitLabel(nameRect, primary, Skin.Big, 13f);
            if (!fits && nameRect.Contains(Event.current.mousePosition)) AskTip("title", primary);

            var favourite = explorer.Favourites.Contains(entry);
            var starRect = new Rect(width - U(28f), y + U(4f), U(22f), U(22f));
            Skin.Icon(starRect, favourite ? Skin.Star : Skin.StarHollow, favourite ? Skin.Accent : Skin.Dim);
            if (GUI.Button(starRect, GUIContent.none, GUIStyle.none)) explorer.ToggleFavourite(entry);
            if (starRect.Contains(Event.current.mousePosition)) AskTip("star", favourite ? "Remove from favourites" : "Add to favourites");

            if (GUI.Button(new Rect(width - copyW - U(38f), y + U(2f), copyW, U(26f)), "Copy name", Skin.Button))
            {
                GUIUtility.systemCopyBuffer = entry.Name;
                Session.Say($"Copied \"{entry.Name}\".");
            }
            y += U(34f);

            // Kind in colour, then the prefab name (when the game shows another) and where it comes from.
            var x = _compact ? KindBadge(entry, new Vector2(0f, y)) + U(10f) : 0f;
            var origin = OriginText(entry);
            var sub = entry.Name == primary ? origin : entry.Name + (origin.Length > 0 ? "   ·   " + origin : "");
            // The second line is kept even when empty: the link to fold every section sits at its end.
            {
                var subRect = new Rect(x, y, width - x - _foldAllW, U(22f));
                if (!FitLabel(subRect, sub, Skin.DimLabel, 10f) && subRect.Contains(Event.current.mousePosition)) AskTip("sub", sub);
                // Where a mod added it, the line goes to the mod's own page, or else shows everything it added.
                if (entry.Origin == Origin.Mod && entry.ModName.Length > 0 && entry.Kind != Kind.Mod)
                {
                    var page = EntryKeys.For(Kind.Mod, entry.ModName);
                    var known = InCatalog(explorer, page);
                    var guess = UnsureWords.IsSureClue(entry.ModClue) ? "" : UnsureWords.ModClue(entry.ModClue) + "\n";
                    if (subRect.Contains(Event.current.mousePosition)) AskTip("mod" + guess, guess + (known ? "Go to " + entry.ModName + ": what it adds and changes" : "Show everything " + entry.ModName + " added"));
                    if (GUI.Button(subRect, GUIContent.none, GUIStyle.none))
                    {
                        if (known) Go(explorer, page);
                        else SearchFor(explorer, "mod:" + entry.ModName.Replace(" ", "").ToLowerInvariant());
                    }
                }
                y += U(26f);
            }

            return y + U(8f);
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
    }
}
