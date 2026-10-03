using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Help with the search (rules in <see cref="SearchHelp"/>): what could finish the word being
    /// typed, in a list under the box and as the rest of the best one after the text, taken by a
    /// click, with Tab (Shift+Tab back), or with Enter while a word is being typed. The arrow
    /// keys stay with the list.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The search box's help: its index of terms, and what it offers while typing.</summary>
        private static readonly SearchAssist Assist = new SearchAssist();

        /// <summary>
        /// The search box's help as an object of its own: the index of what each search key can
        /// take, read on a worker thread, the suggestions for the word at the caret, Tab's cycling,
        /// and the list under the box. The panel hands it the box and its input in their order.
        /// </summary>
        private sealed class SearchAssist
        {
            /// <summary>Whether the terms for this catalog, with the locations as they are read, are in.</summary>
            public bool HasTermsFor(Explorer explorer) => _terms != null && ReferenceEquals(_termsFor, explorer) && _termsAt == Locations.Now;

            /// <summary>Ends Tab's cycling.</summary>
            public void ResetCycle() => _cycle.Reset();

            /// <summary>Lets go of the index, which holds the whole catalog, and of what was offered.</summary>
            public void Forget()
            {
                _terms = null;
                _termsFor = null;
                _termsJob = null;
                _jobFor = null;
                _suggestFor = null;
                _suggested = new List<Suggestion>();
                _dropShown = false;
                _dropList = new List<Suggestion>();
                _caretTo = -1;
                _cycle.Reset();
            }

            private TermIndex _terms;
            private Explorer _termsFor;
            private Locations.State _termsAt;

            private System.Threading.Tasks.Task<TermIndex> _termsJob;
            private Explorer _jobFor;
            private Locations.State _jobAt;
            private static readonly TermIndex NoTerms = new TermIndex(new Entry[0]);

            /// <summary>
            /// Every value the search keys can take, read once per catalog and again once the
            /// locations are read. Reading thousands of entries took most of a frame (80 ms and more),
            /// so it is done on a worker thread; until it is done the terms of before serve, or none
            /// right after the catalog is read. The catalog's entries are only read there.
            /// </summary>
            public TermIndex TermsFor(Explorer explorer)
            {
                if (_terms != null && ReferenceEquals(explorer, _termsFor) && _termsAt == Locations.Now) return _terms;

                if (_termsJob == null || !ReferenceEquals(_jobFor, explorer) || _jobAt != Locations.Now)
                {
                    var catalog = explorer.Catalog;
                    _jobFor = explorer;
                    _jobAt = Locations.Now;
                    _termsJob = System.Threading.Tasks.Task.Run(() => new TermIndex(catalog));
                }

                if (_termsJob.IsCompleted)
                {
                    var started = Timing.Start();
                    if (_termsJob.Status == System.Threading.Tasks.TaskStatus.RanToCompletion)
                    {
                        _terms = _termsJob.Result;
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"Scry could not read the search terms on a worker thread, and reads them now: {_termsJob.Exception?.GetBaseException().Message}");
                        _terms = new TermIndex(explorer.Catalog);
                    }
                    _termsFor = _jobFor;
                    _termsAt = _jobAt;
                    _termsJob = null;
                    _suggestFor = null;
                    Timing.Add("search terms", started);
                    return _terms;
                }
                return _terms != null && ReferenceEquals(_termsFor, explorer) ? _terms : NoTerms;
            }

            // ----- Suggestions while typing -----

            private readonly TabCycle _cycle = new TabCycle();
            private string _suggestFor;
            private List<Suggestion> _suggested = new List<Suggestion>();

            /// <summary>What could finish the word, kept while the word stays the same.</summary>
            private List<Suggestion> SuggestFor(Explorer explorer, string word)
            {
                if (word != _suggestFor || !ReferenceEquals(_termsFor, explorer) || _termsAt != Locations.Now)
                {
                    var terms = TermsFor(explorer);
                    _suggested = SearchHelp.Suggest(word, terms);
                    _suggestFor = word;
                }
                return _suggested;
            }

            /// <summary>The list under the search box, as last drawn: where it is, what it holds, and which one Tab takes.</summary>
            private bool _dropShown;
            private Rect _dropRect;
            private IReadOnlyList<Suggestion> _dropList = new List<Suggestion>();
            private int _dropMark;
            private WordSpan _dropSpan;

            /// <summary>Whether Enter takes the marked suggestion (<see cref="SearchHelp.EnterTakesSuggestion"/>) instead of playing the selection.</summary>
            private bool _dropTakesEnter;

            /// <summary>Where the caret goes once the search box has the keyboard again after a click took it.</summary>
            private int _caretTo = -1;

            private static float DropRowH => U(28f);

            private static TextEditor SearchEditor()
            {
                if (GUI.GetNameOfFocusedControl() != SearchControl) return null;
                return GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            }

            /// <summary>
            /// Puts new text in the search, the box's own copy included, with the caret where given;
            /// when a click took the keyboard, the box gets it back.
            /// </summary>
            private void SetSearch(Explorer explorer, string text, int caret)
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
                else
                {
                    // A click took the keyboard; the box gets it back, and the caret, on the next frames.
                    _focusSearch = true;
                    _caretTo = caret;
                }
            }

            /// <summary>
            /// After the search box, Tab through the suggestions. Not before it: the box's name, by
            /// which its focus is known, is only given again as it is drawn in each pass, and a Tab it
            /// does not use (the box leaves Tab alone) moves Unity's keyboard focus to the next control.
            /// </summary>
            public void Tab(Explorer explorer)
            {
                var e = Event.current;
                if (e.type != EventType.KeyDown || (e.keyCode != KeyCode.Tab && e.character != '\t')) return;
                var editor = SearchEditor();
                if (editor == null) return;
                if (e.keyCode == KeyCode.Tab)
                {
                    var (text, at) = _cycle.Next(explorer.Text ?? "", editor.cursorIndex, w => SuggestFor(explorer, w), e.shift);
                    if (text != explorer.Text) SetSearch(explorer, text, at);
                }
                e.Use();
            }

            /// <summary>
            /// Enter takes the marked suggestion while one is offered for a word being typed, and ends
            /// Tab's cycling, keeping what it put in; true when it did. Asked by <see cref="Keys"/>
            /// before the search box is drawn, since the box uses up any key that types no character,
            /// Enter among them. Its name is not known yet in that pass, so the box's editor is found
            /// by the keyboard's control, which the box had at the end of the last one.
            /// </summary>
            public bool TakeEnter(Explorer explorer)
            {
                if (!SearchFocused || !_dropShown || !_dropTakesEnter || GUIUtility.keyboardControl == 0) return false;
                var editor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
                if (editor == null) return false;
                var text = explorer.Text ?? "";
                if (_cycle.IsAt(text, editor.cursorIndex))
                {
                    _cycle.Reset();
                    return true;
                }
                if (_dropMark < 0 || _dropMark >= _dropList.Count) return false;
                var taken = SearchHelp.Replace(text, _dropSpan, _dropList[_dropMark].Insert, out var caret);
                explorer.Text = taken;
                _listScroll = Vector2.zero;
                _reveal = true;
                _help = false;
                editor.text = taken;
                editor.cursorIndex = editor.selectIndex = caret;
                return true;
            }

            /// <summary>Before the search box and everything under the list of suggestions: a click on one of them.</summary>
            public void Picks(Explorer explorer)
            {
                var e = Event.current;
                if (_dropShown && e.type == EventType.MouseDown && e.button == 0 && _dropRect.Contains(e.mousePosition))
                {
                    var row = Mathf.FloorToInt((e.mousePosition.y - _dropRect.y - U(4f)) / DropRowH);
                    if (row >= 0 && row < _dropList.Count)
                    {
                        var text = SearchHelp.Replace(explorer.Text ?? "", _dropSpan, _dropList[row].Insert, out var caret);
                        _cycle.Reset();
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
            public void Suggestions(Explorer explorer, Rect box)
            {
                _dropShown = false;
                _dropTakesEnter = false;
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
                if (_cycle.IsAt(text, caret))
                {
                    _dropShown = true;
                    _dropList = _cycle.Suggestions;
                    _dropSpan = new WordSpan { Start = _cycle.Start, End = caret, Word = text.Substring(_cycle.Start, caret - _cycle.Start) };
                    _dropMark = _cycle.Index;
                    _dropTakesEnter = SearchHelp.EnterTakesSuggestion("", true, _dropList.Count);
                    var cycleW = Mathf.Min(box.width, Mathf.Max(U(300f), box.width * 0.6f));
                    _dropRect = new Rect(box.x, box.yMax + U(2f), cycleW, _dropList.Count * DropRowH + U(8f));
                    return;
                }

                // Nothing typed yet, in an empty box or after a space, shows every key; the start of
                // a word already there shows nothing.
                var span = SearchHelp.WordAt(text, caret);
                var typed = text.Substring(span.Start, caret - span.Start);
                if ((typed.Length == 0 && span.Word.Length > 0) || editor.cursorIndex != editor.selectIndex) return;

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
                _dropTakesEnter = SearchHelp.EnterTakesSuggestion(typed, false, suggested.Count);
                var width = Mathf.Min(box.width, Mathf.Max(U(300f), box.width * 0.6f));
                _dropRect = new Rect(box.x, box.yMax + U(2f), width, suggested.Count * DropRowH + U(8f));
            }

            private GUIStyle _ghostStyle;
            private GUIStyle _ghostOf;

            /// <summary>The search box's text, faint, without its box or left padding.</summary>
            private GUIStyle GhostStyle()
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
            public void Draw()
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
                    var side = suggestion.IsKey ? suggestion.Note : Numbers.Count(suggestion.Count);
                    GUI.Label(new Rect(row.x + U(8f) + nameW + U(8f), row.y, row.width - nameW - U(24f), row.height), side, suggestion.IsKey ? Skin.DimLabel : RightDim());
                }
            }

            private GUIStyle _rightDim;
            private GUIStyle _rightDimOf;

            private GUIStyle RightDim()
            {
                if (_rightDim == null || !ReferenceEquals(_rightDimOf, Skin.DimLabel))
                {
                    _rightDimOf = Skin.DimLabel;
                    _rightDim = new GUIStyle(Skin.DimLabel) { alignment = TextAnchor.MiddleRight };
                }
                return _rightDim;
            }
        }
    }
}
