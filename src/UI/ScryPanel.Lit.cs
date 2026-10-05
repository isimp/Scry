using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>The parts of the list's names the search matched, lit behind the text (<see cref="SearchLit"/>).</summary>
    internal static partial class ScryPanel
    {
        /// <summary>The search the words below were read from; read again only when it changes.</summary>
        private static string _litFor;

        private static readonly List<string> LitWords = new List<string>();
        private static readonly List<string> LitNames = new List<string>();
        private static readonly List<(int Start, int Length)> LitSpans = new List<(int, int)>();

        /// <summary>One content for measuring every row's text, so lighting makes nothing new each frame.</summary>
        private static readonly GUIContent LitContent = new GUIContent();

        /// <summary>Reads the search's typed words and quoted names when it changed; whether there is anything to light.</summary>
        private static bool ReadLit(string text)
        {
            if (text != _litFor)
            {
                _litFor = text;
                LitWords.Clear();
                LitNames.Clear();
                var parsed = Scry.Search.Parse(text ?? "");
                LitWords.AddRange(parsed.Words);
                LitNames.AddRange(parsed.Names);
            }
            return LitWords.Count > 0 || LitNames.Count > 0;
        }

        /// <summary>Lights behind a label the parts of its text the search matched, where the style places them, within the label; on repaint only.</summary>
        private static void LightMatches(Rect rect, string text, GUIStyle style)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            SearchLit.Spans(text, LitWords, LitNames, LitSpans);
            if (LitSpans.Count == 0) return;
            LitContent.text = text;
            var height = style.lineHeight + U(2f);
            var top = rect.y + (rect.height - height) / 2f;
            foreach (var (start, length) in LitSpans)
            {
                var left = Mathf.Max(style.GetCursorPixelPosition(rect, LitContent, start).x, rect.x);
                var right = Mathf.Min(style.GetCursorPixelPosition(rect, LitContent, start + length).x, rect.xMax);
                if (right > left) Skin.Fill(new Rect(left, top, right - left, height), Skin.MatchLit);
            }
        }
    }
}
