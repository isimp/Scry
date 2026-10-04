namespace Scry
{
    /// <summary>A thing's looks by their place among its kind: a plant's grown forms, an item's styles.</summary>
    internal static class LookWords
    {
        /// <summary>A plant's grown form, numbered where it has more than one.</summary>
        public static string Grown(int index, int count) => count == 1 ? "Grown" : $"Grown {Numbers.Count(index + 1)}";

        public static string Style(int index) => $"Style {Numbers.Count(index + 1)}";
    }
}
