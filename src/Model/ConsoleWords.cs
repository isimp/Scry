namespace Scry
{
    /// <summary>What the console says back to /scry, each reply led by Scry's name so it is told apart from the game's own lines.</summary>
    internal static class ConsoleWords
    {
        /// <summary>The reply to /scry clear.</summary>
        public const string Cleared = "previews cleared.";

        /// <summary>A reply, led by Scry's name.</summary>
        public static string Reply(string said) => "Scry: " + said;

        /// <summary>The reply to /scry monitor: whether the resource monitor is now on.</summary>
        public static string Monitor(bool on) => Reply("the resource monitor is " + (on ? "on." : "off."));
    }
}
