using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The line of section names under the details' title (<see cref="SectionLine"/>). Each
    /// heading the details draw notes where it stands, and the line is laid out from where the
    /// last whole draw found them, as the title is drawn before the sections under it.
    /// </summary>
    internal static partial class ScryPanel
    {
        private struct SectionMark
        {
            public string Key;
            public string Heading;
            public string Link;
            public float Top;
        }

        /// <summary>The headings found so far by the draw of the details going on.</summary>
        private static readonly List<SectionMark> Marking = new List<SectionMark>();

        /// <summary>The headings the last whole draw of the details found, which the line names.</summary>
        private static readonly List<SectionMark> Marks = new List<SectionMark>();

        private static readonly List<float> MarkTops = new List<float>();

        /// <summary>Whether the details' sections are being drawn, so their headings are noted.</summary>
        private static bool _marking;

        /// <summary>The section a click went to, scrolled to once the draw has found its heading.</summary>
        private static string _goingTo;

        /// <summary>The section a click last went to and where the details were scrolled for it, lit while they stay there.</summary>
        private static string _wentTo;

        private static float _wentToScroll;

        /// <summary>Each heading's text with its count and its name on the line, made when the heading or its count changes rather than for every event.</summary>
        private static readonly Dictionary<string, (string Heading, int Count, string Shown, string Link)> HeadingTexts = new Dictionary<string, (string, int, string, string)>();

        /// <summary>A heading as it shows, with how many its section holds where it is told (<paramref name="count"/> 0 or more); noted for the line while the details' sections are drawn.</summary>
        private static string MarkSection(string key, string heading, int count, float y)
        {
            if (!HeadingTexts.TryGetValue(key, out var texts) || texts.Heading != heading || texts.Count != count)
            {
                texts = (heading, count, count >= 0 ? PanelWords.Heading(heading, count) : heading, PanelWords.SectionLink(heading, count));
                HeadingTexts[key] = texts;
            }
            if (_marking) Marking.Add(new SectionMark { Key = key, Heading = heading, Link = texts.Link, Top = y });
            return texts.Shown;
        }

        /// <summary>Starts noting the headings of the details' sections, as they are drawn.</summary>
        private static void StartMarking()
        {
            Marking.Clear();
            _marking = true;
        }

        /// <summary>Stops noting headings; what this draw found is what the line names from now on.</summary>
        private static void EndMarking()
        {
            _marking = false;
            Marks.Clear();
            Marks.AddRange(Marking);
        }

        /// <summary>
        /// Goes to a section of the details: opens it where it is folded, and brings its heading
        /// to the top once the draw has found it. What a click on its name on the line does.
        /// </summary>
        public static void GoToSection(string key)
        {
            if (IsFolded(key)) SetFolded(key, false);
            _goingTo = key;
        }

        /// <summary>Where to scroll the details to for the section a click went to, once its heading is found; null when no click waits.</summary>
        private static float? ArriveAtSection(float maxScroll)
        {
            if (_goingTo == null) return null;
            var key = _goingTo;
            _goingTo = null;
            foreach (var mark in Marks)
            {
                if (mark.Key != key) continue;
                _wentTo = key;
                _wentToScroll = SectionLine.JumpTo(mark.Top, U(4f), maxScroll);
                return _wentToScroll;
            }
            return null;
        }

        /// <summary>Lets go of the sections found, for another entry or world.</summary>
        private static void ForgetSectionLine()
        {
            Marks.Clear();
            _goingTo = null;
            _wentTo = null;
        }

        /// <summary>The sections on the line, by key, with their names as it shows them, for the self-test.</summary>
        public static IEnumerable<(string Key, string Link)> SectionLinks()
        {
            if (!SectionLine.Shows(Marks.Count)) yield break;
            foreach (var mark in Marks) yield return (mark.Key, mark.Link);
        }

        /// <summary>Whether a section of the details is open, for the self-test.</summary>
        public static bool SectionOpen(string key) => !IsFolded(key);

        /// <summary>The section lit on the line, by its key, or null; for the self-test.</summary>
        public static string SectionLit { get; private set; }

        private static ChipFlow LineFlow(float width, float y) => new ChipFlow(0f, width, y, U(20f), U(14f), U(2f));

        private static float LinkWidth(SectionMark mark) => Skin.Width(Skin.Small, mark.Link) + U(2f);

        /// <summary>How tall the line is across a width: nothing where it does not show.</summary>
        private static float SectionLineHeight(float width)
        {
            if (!SectionLine.Shows(Marks.Count)) return 0f;
            var flow = LineFlow(width, 0f);
            foreach (var mark in Marks) flow.Place(LinkWidth(mark));
            return flow.Below + U(6f);
        }

        /// <summary>
        /// Draws the line at a height, the section being read lit and underlined; a click on a
        /// name goes to its section (<see cref="GoToSection"/>).
        /// </summary>
        /// <param name="width">How wide the line may run.</param>
        /// <param name="y">The line's top.</param>
        /// <param name="shown">How much of the details shows at once.</param>
        /// <param name="maxScroll">How far the details scroll at most.</param>
        private static float SectionLineRow(float width, float y, float shown, float maxScroll)
        {
            if (!SectionLine.Shows(Marks.Count))
            {
                SectionLit = null;
                return y;
            }
            MarkTops.Clear();
            var wentTo = -1;
            for (var i = 0; i < Marks.Count; i++)
            {
                MarkTops.Add(Marks[i].Top);
                if (Marks[i].Key == _wentTo) wentTo = i;
            }
            var lit = SectionLine.InView(MarkTops, _sideScroll.y, shown, maxScroll, wentTo, _wentToScroll);
            SectionLit = lit >= 0 ? Marks[lit].Key : null;

            var flow = LineFlow(width, y);
            for (var i = 0; i < Marks.Count; i++)
            {
                var mark = Marks[i];
                var w = LinkWidth(mark);
                var at = flow.Place(w);
                var rect = new Rect(at.X, at.Y, w, flow.RowHeight);
                LinkLabel(rect, mark.Link, Skin.Small, i == lit ? Skin.Accent : Skin.Dim);
                if (i == lit) Skin.Fill(new Rect(rect.x, rect.yMax - U(2f), rect.width - U(2f), U(2f)), Skin.Accent);
                if (rect.Contains(Event.current.mousePosition)) AskTip("section:" + mark.Key, PanelWords.SectionLinkTip(mark.Heading, IsFolded(mark.Key)));
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) GoToSection(mark.Key);
            }
            CountDrawn(PanelPart.SectionLine);
            return flow.Below + U(6f);
        }
    }
}
