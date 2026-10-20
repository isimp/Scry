using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What the list and the controls above it say: its tabs, its filters, its groups and why it is empty.</summary>
    internal static class ListWords
    {
        /// <summary>The line a faint row's tip adds: why it is faint.</summary>
        public const string Silent = "Nothing to see or hear: it has no model, particles, light or sound";

        /// <summary>The tip of what brings the folded list back.</summary>
        public const string BringBack = "Bring the list back";

        /// <summary>Which of several of one kind going by one name an entry is, beside it in the list: "2 of 3".</summary>
        public static string OneOf(int which, int of) => $"{Numbers.Count(which)} of {Numbers.Count(of)}";

        /// <summary>A kind's tab: its label and, in the colour given, how many it holds.</summary>
        public static string Tab(string label, int count, string colour) => $"{label}  <color=#{colour}>{Numbers.Count(count)}</color>";

        /// <summary>The compact view's pill of the kind picked: its tab, and a mark that opens the others or closes them.</summary>
        public static string KindPill(string tab, bool open) => tab + (open ? "  ▴" : "  ▾");

        /// <summary>The kind pill's tip.</summary>
        public const string KindPillTip = "Pick the kind the list shows";

        /// <summary>The compact view's chip of where the list is taken from (all, the game's own, mods'), and a mark that opens the choice or closes it.</summary>
        public static string OriginChip(string name, bool open) => "From " + name + (open ? "  ▴" : "  ▾");

        /// <summary>The origin switch's tip, and its chip's in the compact view.</summary>
        public const string OriginTip = "Everything, only the game's own, or only what mods added";

        /// <summary>The favourites filter's tip.</summary>
        public static string FavouritesTip(bool on) => on ? "Showing only favourites" : "Show only favourites";

        /// <summary>The recent filter's tip.</summary>
        public static string RecentTip(bool on) => on ? "Showing what you looked at last, newest first" : "Show what you looked at last, newest first";

        /// <summary>The list button's tip, by whether the list is folded away.</summary>
        public static string ListButtonTip(bool hidden) => hidden ? BringBack : "Fold the list away, leaving the room to the details";

        /// <summary>The button in the gap the folded list leaves: an arrow where the gap is narrow and upright.</summary>
        public static string ShowList(bool upright) => upright ? "\u203A" : "Show the list  \u25BE";

        /// <summary>A group's heading in the list, with the mark of whether it is folded.</summary>
        public static string Group(string heading, bool folded) => (folded ? "\u25B8 " : "\u25BE ") + heading;

        /// <summary>The key a day's group of the learned list is folded by, apart from any kind's group.</summary>
        public static string DayGroup(int day) => "day:" + Stored.Count(day);

        /// <summary>A group heading's tip.</summary>
        public static string GroupTip(bool folded) => folded ? "Show this group" : "Fold this group away";

        /// <summary>Why the list is empty: no favourites yet, nothing looked at yet, or nothing matching.</summary>
        public static string Nothing(bool noFavourites, bool nothingRecent) =>
            noFavourites ? "No favourites yet. Star something to keep it here."
            : nothingRecent ? "Nothing looked at yet. What you select is kept here."
            : "Nothing matches.";

        /// <summary>The note above every kind's matches, where the picked tab has none.</summary>
        public static string NothingIn(string kind, int all) => $"Nothing in {kind}, showing all {Numbers.Count(all)}";

        /// <summary>The note over a list shown for a search whose slips were mended (<see cref="SearchSlip"/>): what was typed, and what is shown instead.</summary>
        public static string Mended(IReadOnlyList<(string Typed, string Used)> mended) =>
            $"No match for {string.Join(", ", mended.Select(m => m.Typed))}; showing {string.Join(", ", mended.Select(m => m.Used))}";
    }
}
