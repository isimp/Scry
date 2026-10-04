namespace Scry
{
    /// <summary>What the list of what is out in the world says: the button that clears it, each line, and how many did not fit.</summary>
    internal static class OutWords
    {
        /// <summary>The button that takes everything out of the world, with how many lines are out where more than one.</summary>
        public static string ClearWorld(int lines) => lines <= 1 ? "Clear world" : $"Clear world  {Numbers.Count(lines)}";

        /// <summary>A line: what is out, and how many where more than one.</summary>
        public static string Row(string name, int count) => count > 1 ? name + "  " + Numbers.Times(count) : name;

        /// <summary>The last line, for those that did not fit.</summary>
        public static string More(int more) => $"and {Numbers.Count(more)} more; Clear world takes them all";
    }
}
