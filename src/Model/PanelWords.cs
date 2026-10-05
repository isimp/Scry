using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What the panel says in many places around what it shows: where a click goes, a section's
    /// heading with its count and whether it is folded, the two states of a toggle, and the few
    /// words a chip or a note is made of.
    /// </summary>
    internal static class PanelWords
    {
        /// <summary>A link's tip: where a click goes.</summary>
        public static string GoTo(string name) => "Go to " + name;

        /// <summary>A link's tip, saying under it when what it goes to plays now.</summary>
        public static string GoTo(string name, bool playingNow) => playingNow ? GoTo(name) + "\n(playing now)" : GoTo(name);

        /// <summary>A chip's text with the arrow of one that goes somewhere.</summary>
        public static string GoArrow(string text) => text + "  \u203A";

        /// <summary>A section's or group's heading with how many it holds: "EFFECTS  3".</summary>
        public static string Heading(string title, int count) => $"{title}  {Numbers.Count(count)}";

        /// <summary>A heading that folds, with the mark of whether it is folded.</summary>
        public static string SectionTitle(string heading, bool folded) => heading + (folded ? "  \u25B8" : "  \u25BE");

        /// <summary>A folding heading's tip: what a click does, and a shift-click.</summary>
        public static string SectionTip(bool folded) => (folded ? "Open this section" : "Fold this section away") + "\nShift-click: every section";

        /// <summary>A section's name on the line under the details' title, from its heading, with how many it holds where the heading tells it: "Animations 12".</summary>
        public static string SectionLink(string heading, int count) => count >= 0 ? SectionName(heading) + " " + Numbers.Count(count) : SectionName(heading);

        /// <summary>The tip of a section's name on that line, from its heading: where a click goes, and that it opens a folded section.</summary>
        public static string SectionLinkTip(string heading, bool folded) => folded ? "Open " + SectionName(heading) + " and go to it" : GoTo(SectionName(heading));

        private static string SectionName(string heading) => Naming.Capital(heading.ToLowerInvariant());

        /// <summary>The tip of an example in the search's help.</summary>
        public static string TryInSearch(string example) => $"Search for {example}";

        /// <summary>The link that folds or opens every section, by whether any is open.</summary>
        public static string FoldAll(bool anyOpen) => anyOpen ? "fold all" : "open all";

        /// <summary>The tip of the link that folds or opens every section.</summary>
        public static string FoldAllTip(bool anyOpen) => anyOpen ? "Fold every section away" : "Open every section";

        /// <summary>A setting's state.</summary>
        public static string OnOff(bool on) => on ? "On" : "Off";

        /// <summary>The button that pauses what plays, or resumes it while paused.</summary>
        public static string Pause(bool paused) => paused ? "Resume" : "Pause";

        /// <summary>The button that changes the panel's view, by the view it changes to.</summary>
        public static string ViewButton(bool compact) => compact ? "Full view" : "Compact";

        /// <summary>What a click on Copy says it copied.</summary>
        public static string Copied(string text) => $"Copied \"{text}\".";

        /// <summary>The note over the chips of what plays in a part: "In club:".</summary>
        public static string In(string part) => "In " + part + ":";

        /// <summary>The chip that shows the rest of a long list.</summary>
        public static string More(int hidden) => $"{Numbers.Count(hidden)} more";

        /// <summary>How long something lasts.</summary>
        public static string Lasts(double seconds) => "Lasts " + Numbers.Duration(seconds);

        /// <summary>The button that plays an effect list, by its label.</summary>
        public static string PlayList(string label) => "Play " + label.ToLowerInvariant();

        /// <summary>A note of work going on, with one to three dots that change three times a second.</summary>
        public static string Waiting(string what, float seconds) => what + new string('.', 1 + (int)(seconds * 3f) % 3);

        /// <summary>The keys the full view's hint adds, which the compact view has no room for.</summary>
        private static readonly string[] FullViewKeys = { "Enter plays", "Ctrl+F searches", "Esc leaves a box" };

        /// <summary>
        /// The foot's hint: the keys that are not obvious, short, and only those the view and the
        /// settings allow; the rest is under "?".
        /// </summary>
        public static string FootHint(bool full, bool walk, bool look)
        {
            var parts = new List<string>();
            if (full) parts.AddRange(FullViewKeys);
            if (walk) parts.Add("keys walk when not typing");
            if (look) parts.Add("right-drag outside to look");
            if (parts.Count == 0) parts.Add("Enter plays");
            parts[0] = Naming.Capital(parts[0]);
            return string.Join("  \u00B7  ", parts);
        }
    }
}
