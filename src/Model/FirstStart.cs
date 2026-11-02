namespace Scry
{
    /// <summary>
    /// What a new player meets: the key opening Scry told once, a few seconds into a world, where
    /// the game tells its own messages; and the first time Scry opens, a short setup, every row
    /// set already, put off by Later until Scry next opens, put away by Done. A character never
    /// asked about its compendium is offered it again.
    /// </summary>
    internal static class FirstStart
    {
        /// <summary>How long a character stands in a world before the key is told: past the loading's own messages.</summary>
        public const float HintAfter = 5f;

        public static bool HintDue(bool hinted, bool inWorld, float playerFor) => !hinted && inWorld && playerFor >= HintAfter;

        public static string Hint(string key) => "Scry is ready: press " + key + " to open it, or type /scry in the chat";

        public static bool SetupDue(bool setUp, bool putOff, bool characterAsked) => !putOff && (!setUp || !characterAsked);
    }
}
