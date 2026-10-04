using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What a link's chip and tip say: what it goes to, with its note where short, or the animation it plays there.</summary>
    internal static class LinkWords
    {
        /// <summary>A link that goes to a creature and plays one of its animations, or goes to it alone.</summary>
        public static string Animation(string shown, string animation) => animation.Length > 0 ? shown + " \u00B7 " + animation : shown;

        /// <summary>The tip of a link to a creature's animation.</summary>
        public static string AnimationTip(string shown, string animation) =>
            animation.Length > 0 ? $"Go to {shown} and play its {animation} animation" : PanelWords.GoTo(shown);

        /// <summary>A link's chip, with its note where it has one short enough.</summary>
        public static string Chip(string shown, IReadOnlyList<string> notes) =>
            notes.Count == 1 && notes[0].Length > 0 && notes[0].Length <= 28 ? shown + " \u00B7 " + notes[0] : shown;

        /// <summary>A link's tip: where it goes, and its notes, the first twelve.</summary>
        public static string Tip(string shown, IReadOnlyList<string> notes) =>
            PanelWords.GoTo(shown) + (notes.Count > 0 ? "\n" + string.Join("\n", notes.Take(12)) : "");
    }
}
