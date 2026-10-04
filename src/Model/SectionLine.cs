using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The line of section names under the details' title, for going round a long page. It
    /// shows where there is more than one section to go to; a click brings a section's heading
    /// to the top of the details, as far as they scroll; and the section being read is lit: the
    /// one a click went to while the details stay where it put them, else the last whose heading
    /// has come within the top third of what shows, or the last of all at the end of the scroll.
    /// </summary>
    internal static class SectionLine
    {
        /// <summary>Whether the line shows, for a page of so many sections.</summary>
        public static bool Shows(int sections) => sections >= 2;

        /// <summary>Where the details scroll to for a section whose heading stands at a height: a gap over the heading, within the scroll's range.</summary>
        public static float JumpTo(float heading, float gap, float maxScroll) => Math.Min(Math.Max(heading - gap, 0f), Math.Max(maxScroll, 0f));

        /// <summary>
        /// The section being read, by its place among the headings, or -1 for a page with none.
        /// </summary>
        /// <param name="headings">Where each section's heading stands on the page, top first.</param>
        /// <param name="scroll">How far the page is scrolled.</param>
        /// <param name="shown">How much of the page shows at once.</param>
        /// <param name="maxScroll">How far the page scrolls at most.</param>
        /// <param name="jumped">The section a click went to last, or -1.</param>
        /// <param name="jumpedTo">Where that click scrolled the page to.</param>
        public static int InView(IReadOnlyList<float> headings, float scroll, float shown, float maxScroll, int jumped, float jumpedTo)
        {
            if (headings.Count == 0) return -1;
            if (jumped >= 0 && jumped < headings.Count && Math.Abs(scroll - jumpedTo) < 1f) return jumped;
            if (maxScroll > 0f && scroll >= maxScroll - 1f) return headings.Count - 1;
            var line = scroll + shown / 3f;
            var lit = 0;
            for (var i = 1; i < headings.Count; i++)
            {
                if (headings[i] <= line) lit = i;
            }
            return lit;
        }
    }
}
