namespace Scry
{
    /// <summary>How a container's size is told: its slots, in rows as its inventory shows them (<c>Container.m_width</c> a row, <c>m_height</c> rows).</summary>
    public static class ContainerWords
    {
        /// <summary>"10, in 2 rows of 5"; null for a container with no slots.</summary>
        public static string Slots(int width, int height)
        {
            if (width <= 0 || height <= 0) return null;
            var slots = width * height;
            if (slots == 1) return "1";
            return height == 1 ? $"{slots}, in one row" : $"{slots}, in {height} rows of {width}";
        }
    }
}
