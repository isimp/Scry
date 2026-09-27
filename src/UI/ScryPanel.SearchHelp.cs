using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Help with the search (rules in <see cref="SearchHelp"/>): what could finish the word being
    /// typed, in a list under the box and as the rest of the best one after the text, taken by a
    /// click or with Tab (Shift+Tab back); and a row of chips under the kind tabs with the terms in
    /// the search, each taken out with a click, and the values the open tab has most of, each put
    /// in with a click. The arrow keys and Enter stay with the list.
    /// </summary>
    internal static partial class ScryPanel
    {
        private static TermIndex _terms;
        private static Explorer _termsFor;
        private static Locations.State _termsAt;

        /// <summary>Every value the search keys can take, read once per catalog and again once the locations are read.</summary>
        private static TermIndex TermsFor(Explorer explorer)
        {
            if (_terms == null || !ReferenceEquals(explorer, _termsFor) || _termsAt != Locations.Now)
            {
                var started = Timing.Start();
                _terms = new TermIndex(explorer.Catalog);
                _termsFor = explorer;
                _termsAt = Locations.Now;
                _suggestFor = null;
                _chipsFor = null;
                Timing.Add("search terms", started);
            }
            return _terms;
        }

        // ----- Suggestions while typing -----

        private static readonly TabCycle Cycle = new TabCycle();
        private static string _suggestFor;
        private static Kind? _suggestKind;
        private static List<Suggestion> _suggested = new List<Suggestion>();

        /// <summary>What could finish the word, kept while the word and the tab stay the same.</summary>
        private static List<Suggestion> SuggestFor(Explorer explorer, string word)
        {
            if (word != _suggestFor || explorer.KindFilter != _suggestKind || !ReferenceEquals(_termsFor, explorer) || _termsAt != Locations.Now)
            {
                var terms = TermsFor(explorer);
                _suggested = SearchHelp.Suggest(word, terms, explorer.KindFilter);
                _suggestFor = word;
                _suggestKind = explorer.KindFilter;
            }
            return _suggested;
        }

        /// <summary>The list under the search box, as last drawn: where it is, what it holds, and which one Tab takes.</summary>
        private static bool _dropShown;
        private static Rect _dropRect;
        private static IReadOnlyList<Suggestion> _dropList = new List<Suggestion>();
        private static int _dropMark;
        private static WordSpan _dropSpan;

        /// <summary>Where the caret goes once the search box has the keyboard again after a click took it.</summary>
        private static int _caretTo = -1;

        private static float DropRowH => U(28f);

        private static TextEditor SearchEditor()
        {
            if (GUI.GetNameOfFocusedControl() != SearchControl) return null;
            return GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
        }

        /// <summary>
        /// Puts new text in the search, the box's own copy included, with the caret where given.
        /// When the box has lost the keyboard to a click, it gets it back only if asked: a chip
        /// clicked with the mouse leaves the keys to walking.
        /// </summary>
        private static void SetSearch(Explorer explorer, string text, int caret, bool refocus = true)
        {
            explorer.Text = text;
            _listScroll = Vector2.zero;
            _reveal = true;
            _help = false;
            var editor = SearchEditor();
            if (editor != null)
            {
                editor.text = text;
                editor.cursorIndex = editor.selectIndex = caret;
            }
            else if (refocus)
            {
                // A click took the keyboard; the box gets it back, and the caret, on the next frames.
                _focusSearch = true;
                _caretTo = caret;
            }
        }

        /// <summary>Before the search box: Tab through the suggestions, and a click on one of them.</summary>
        private static void SearchKeysAndPicks(Explorer explorer)
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Tab || e.character == '\t') && GUI.GetNameOfFocusedControl() == SearchControl)
            {
                if (e.keyCode == KeyCode.Tab)
                {
                    var editor = SearchEditor();
                    var caret = editor != null ? editor.cursorIndex : (explorer.Text ?? "").Length;
                    var (text, at) = Cycle.Next(explorer.Text ?? "", caret, w => SuggestFor(explorer, w), e.shift);
                    if (text != explorer.Text) SetSearch(explorer, text, at);
                }
                e.Use();
                return;
            }

            if (_dropShown && e.type == EventType.MouseDown && e.button == 0 && _dropRect.Contains(e.mousePosition))
            {
                var row = Mathf.FloorToInt((e.mousePosition.y - _dropRect.y - U(4f)) / DropRowH);
                if (row >= 0 && row < _dropList.Count)
                {
                    var text = SearchHelp.Replace(explorer.Text ?? "", _dropSpan, _dropList[row].Insert, out var caret);
                    Cycle.Reset();
                    SetSearch(explorer, text, caret);
                }
                e.Use();
            }
        }

        /// <summary>
        /// After the search box: the caret put back after a click, and what could finish the word
        /// at the caret, with the rest of the best one drawn after the text when the caret is at
        /// the end. The list itself is drawn last, over what lies below the box.
        /// </summary>
        private static void SearchSuggestions(Explorer explorer, Rect box)
        {
            _dropShown = false;
            var editor = SearchEditor();
            if (editor == null) return;
            if (_caretTo >= 0)
            {
                editor.cursorIndex = editor.selectIndex = Mathf.Min(_caretTo, (explorer.Text ?? "").Length);
                _caretTo = -1;
            }

            var text = explorer.Text ?? "";
            var caret = Mathf.Clamp(editor.cursorIndex, 0, text.Length);

            // While Tab cycles, the list being cycled stays, the one in the search marked.
            if (Cycle.IsAt(text, caret))
            {
                _dropShown = true;
                _dropList = Cycle.Suggestions;
                _dropSpan = new WordSpan { Start = Cycle.Start, End = caret, Word = text.Substring(Cycle.Start, caret - Cycle.Start) };
                _dropMark = Cycle.Index;
                var cycleW = Mathf.Min(box.width, Mathf.Max(U(300f), box.width * 0.6f));
                _dropRect = new Rect(box.x, box.yMax + U(2f), cycleW, _dropList.Count * DropRowH + U(8f));
                return;
            }

            var span = SearchHelp.WordAt(text, caret);
            var typed = text.Substring(span.Start, caret - span.Start);
            if (typed.Length == 0 || editor.cursorIndex != editor.selectIndex) return;

            var suggested = SuggestFor(explorer, typed);
            if (suggested.Count == 0) return;
            if (suggested.Count == 1 && string.Equals(suggested[0].Insert, span.Word, StringComparison.OrdinalIgnoreCase)) return;

            // The rest of the best one, faint, right after the text.
            if (caret == text.Length && Event.current.type == EventType.Repaint)
            {
                var ghost = SearchHelp.Ghost(typed, suggested);
                if (ghost.Length > 0)
                {
                    var style = GhostStyle();
                    var field = Skin.Field;
                    var textW = field.CalcSize(new GUIContent(text)).x - field.padding.horizontal;
                    var x = box.x + field.padding.left + textW - editor.scrollOffset.x;
                    var ghostW = style.CalcSize(new GUIContent(ghost)).x;
                    if (x + ghostW < box.xMax - U(36f)) GUI.Label(new Rect(x, box.y, ghostW + U(2f), box.height), ghost, style);
                }
            }

            _dropShown = true;
            _dropList = suggested;
            _dropSpan = span;
            _dropMark = 0;
            var width = Mathf.Min(box.width, Mathf.Max(U(300f), box.width * 0.6f));
            _dropRect = new Rect(box.x, box.yMax + U(2f), width, suggested.Count * DropRowH + U(8f));
        }

        private static GUIStyle _ghostStyle;
        private static GUIStyle _ghostOf;

        /// <summary>The search box's text, faint, without its box or left padding.</summary>
        private static GUIStyle GhostStyle()
        {
            if (_ghostStyle == null || !ReferenceEquals(_ghostOf, Skin.Field))
            {
                _ghostOf = Skin.Field;
                _ghostStyle = new GUIStyle(Skin.Field);
                _ghostStyle.normal.background = null;
                _ghostStyle.hover.background = null;
                _ghostStyle.focused.background = null;
                _ghostStyle.normal.textColor = Skin.Faint;
                _ghostStyle.padding = new RectOffset(0, 0, Skin.Field.padding.top, Skin.Field.padding.bottom);
            }
            return _ghostStyle;
        }

        /// <summary>The list under the search box, drawn over the rest; its clicks are taken before anything below sees them.</summary>
        private static void DrawSuggestions()
        {
            if (!_dropShown || Event.current.type != EventType.Repaint) return;
            Skin.Box(_dropRect, Skin.Backdrop, Skin.Outline);
            var mouse = Event.current.mousePosition;
            for (var i = 0; i < _dropList.Count; i++)
            {
                var suggestion = _dropList[i];
                var row = new Rect(_dropRect.x + U(4f), _dropRect.y + U(4f) + i * DropRowH, _dropRect.width - U(8f), DropRowH);
                if (i == _dropMark || row.Contains(mouse)) Skin.Fill(row, i == _dropMark ? new Color(0.96f, 0.72f, 0.34f, 0.18f) : new Color(1f, 1f, 1f, 0.06f));

                var name = suggestion.Label;
                var nameW = Mathf.Min(row.width * 0.62f, Skin.Width(Skin.Label, name) + U(4f));
                GUI.Label(new Rect(row.x + U(8f), row.y, nameW, row.height), name, Skin.Label);
                var side = suggestion.IsKey ? suggestion.Note : suggestion.Count.ToString("N0");
                GUI.Label(new Rect(row.x + U(8f) + nameW + U(8f), row.y, row.width - nameW - U(24f), row.height), side, suggestion.IsKey ? Skin.DimLabel : RightDim());
            }
        }

        private static GUIStyle _rightDim;
        private static GUIStyle _rightDimOf;

        private static GUIStyle RightDim()
        {
            if (_rightDim == null || !ReferenceEquals(_rightDimOf, Skin.DimLabel))
            {
                _rightDimOf = Skin.DimLabel;
                _rightDim = new GUIStyle(Skin.DimLabel) { alignment = TextAnchor.MiddleRight };
            }
            return _rightDim;
        }

        // ----- Chips under the kind tabs -----

        private static List<Suggestion> _chips = new List<Suggestion>();
        private static List<string> _active = new List<string>();
        private static string _chipsFor;
        private static Kind? _chipsKind;
        private static bool _chipsOpen;

        /// <summary>
        /// The row of chips under the tabs: first the terms in the search, lit, each taken out with
        /// a click; then the values the open tab has most of, each put in with a click. One row,
        /// and a chip for the rest that opens up to six. Below <paramref name="top"/>; where the
        /// row ends, or <paramref name="top"/> when there is nothing to show.
        /// </summary>
        private static float TermChips(Explorer explorer, float top, float left, float width)
        {
            var rect = new Rect(left, top + U(8f), width, U(26f));
            var text = explorer.Text ?? "";
            if (text != _chipsFor || explorer.KindFilter != _chipsKind || !ReferenceEquals(_termsFor, explorer) || _termsAt != Locations.Now)
            {
                if (explorer.KindFilter != _chipsKind) _chipsOpen = false;
                var terms = TermsFor(explorer);
                _active = SearchHelp.ActiveTerms(text);
                _chips = SearchHelp.Chips(terms, explorer.KindFilter, text);
                _chipsFor = text;
                _chipsKind = explorer.KindFilter;
            }
            if (_active.Count == 0 && _chips.Count == 0) return top;

            var rowH = rect.height;
            var gap = U(6f);
            var x = rect.x;
            var y = rect.y;
            var maxRows = _chipsOpen ? 6 : 1;
            var rows = 1;
            var total = _active.Count + _chips.Count;
            var mouse = Event.current.mousePosition;

            bool Place(string label, out Rect at, float reserve)
            {
                var w = Mathf.Min(rect.width, Skin.Width(Skin.Chip, label) + U(16f));
                if (x + w > rect.xMax - reserve && x > rect.x)
                {
                    if (rows >= maxRows)
                    {
                        at = default;
                        return false;
                    }
                    rows++;
                    x = rect.x;
                    y += rowH + gap;
                }
                at = new Rect(x, y, w, rowH);
                x += w + gap;
                return true;
            }

            var moreW = Skin.Width(Skin.Chip, "999 more") + U(16f);
            var shown = 0;
            string change = null;
            for (var i = 0; i < total; i++)
            {
                var active = i < _active.Count;
                var label = active ? _active[i] + "  ×" : $"{_chips[i - _active.Count].Label}  {_chips[i - _active.Count].Count:N0}";
                var last = rows >= maxRows;
                if (!Place(label, out var at, last && i < total - 1 ? moreW : 0f)) break;
                shown++;
                if (GUI.Button(at, label, active ? Skin.ChipOn : Skin.Chip)) change = active ? _active[i] : _chips[i - _active.Count].Insert;
                if (at.Contains(mouse))
                {
                    if (active) AskTip("term:" + _active[i], "Take " + _active[i] + " out of the search");
                    else
                    {
                        var chip = _chips[i - _active.Count];
                        AskTip("chip:" + chip.Insert, $"{chip.Insert}: {chip.Note}");
                    }
                }
            }

            // The rest, or folding them again.
            if (shown < total || _chipsOpen)
            {
                var label = _chipsOpen ? "Show fewer" : $"{total - shown:N0} more";
                var w = Skin.Width(Skin.Chip, label) + U(16f);
                if (x + w > rect.xMax && x > rect.x)
                {
                    x = rect.x;
                    y += rowH + gap;
                }
                if (GUI.Button(new Rect(x, y, w, rowH), label, Skin.Chip)) _chipsOpen = !_chipsOpen;
            }

            if (change != null)
            {
                Cycle.Reset();
                var changed = SearchHelp.Toggle(text, change);
                SetSearch(explorer, changed, changed.Length, refocus: false);
            }
            return y + rowH;
        }
    }
}
