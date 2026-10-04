using System;

namespace Scry
{
    /// <summary>How long a short note stays: a moment to notice it, and time to read its words, never so long it lingers.</summary>
    internal static class NoteTime
    {
        private static readonly char[] Spaces = { ' ' };

        /// <summary>The seconds a note stays: 2.5, and a quarter second for each word, at most 10.</summary>
        public static float Seconds(string text)
        {
            var words = string.IsNullOrWhiteSpace(text) ? 0 : text.Split(Spaces, StringSplitOptions.RemoveEmptyEntries).Length;
            return Math.Min(10f, 2.5f + 0.25f * words);
        }
    }
}
