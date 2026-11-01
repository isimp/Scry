using System;

namespace Scry
{
    /// <summary>
    /// The message the game shows the player closest to a creature it tamed, "&lt;its name&gt;
    /// $hud_tamedone" as it tames it: the creature it names, by its name token.
    /// </summary>
    internal static class TameMessage
    {
        private const string Done = " $hud_tamedone";

        /// <summary>The name token of the creature a message says was tamed; none for any other message.</summary>
        public static string Tamed(string text) =>
            text != null && text.Length > Done.Length && text.EndsWith(Done, StringComparison.Ordinal) ? text.Substring(0, text.Length - Done.Length) : null;
    }
}
