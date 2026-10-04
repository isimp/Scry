namespace Scry
{
    /// <summary>What the list and the controls above it say: its tabs, its filters, its groups and why it is empty.</summary>
    internal static class ListWords
    {
        /// <summary>The line a faint row's tip adds: why it is faint.</summary>
        public const string Silent = "Nothing to see or hear: it has no model, particles, light or sound";

        /// <summary>The tip of what brings the folded list back.</summary>
        public const string BringBack = "Bring the list back";

        /// <summary>A kind's tab: its label and, in the colour given, how many it holds.</summary>
        public static string Tab(string label, int count, string colour) => $"{label}  <color=#{colour}>{Numbers.Count(count)}</color>";

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

        /// <summary>A group heading's tip.</summary>
        public static string GroupTip(bool folded) => folded ? "Show this group" : "Fold this group away";

        /// <summary>Why the list is empty: no favourites yet, nothing looked at yet, or nothing matching.</summary>
        public static string Nothing(bool noFavourites, bool nothingRecent) =>
            noFavourites ? "No favourites yet. Star something to keep it here."
            : nothingRecent ? "Nothing looked at yet. What you select is kept here."
            : "Nothing matches.";

        /// <summary>The note above every kind's matches, where the picked tab has none.</summary>
        public static string NothingIn(string kind, int all) => $"Nothing in {kind}, showing all {Numbers.Count(all)}";
    }
}
