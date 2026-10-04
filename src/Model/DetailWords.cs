namespace Scry
{
    /// <summary>What the details side says of the thing selected: where it comes from, its prefab, what it is made of, and its console command.</summary>
    internal static class DetailWords
    {
        /// <summary>Where a thing comes from, under its name: the game, a mod by name, or a mod Scry matched it to by clues (marked unsure).</summary>
        public static string From(Entry entry)
        {
            if (entry.Kind == Kind.Mod) return "a mod loaded";
            if (entry.Origin == Origin.Vanilla) return "from the game";
            if (entry.Origin != Origin.Mod) return "";
            if (entry.ModName.Length == 0) return "added by a mod";
            return UnsureWords.IsSureClue(entry.ModClue) ? "added by " + entry.ModName : UnsureWords.Marked("added by " + entry.ModName);
        }

        /// <summary>What stands between the prefab's name and where it comes from, on the line under the name.</summary>
        public const string SubSeparator = "   \u00B7   ";

        /// <summary>The line under the name: the prefab's name where the game shows another, and where it comes from.</summary>
        public static string Sub(string name, string from) => from.Length > 0 ? SubLead(name) + from : name;

        /// <summary>The line under the name up to where it comes from: the prefab's name and the separator.</summary>
        public static string SubLead(string name) => name + SubSeparator;

        /// <summary>The tip of the line that goes to a thing's mod: how Scry matched it where unsure, then where a click goes.</summary>
        public static string ModTip(string clue, string mod, bool known) =>
            Naming.Lines(clue, known ? PanelWords.GoTo(mod) + ": what it adds and changes" : "Show everything " + mod + " added");

        /// <summary>The star's tip.</summary>
        public static string StarTip(bool favourite) => favourite ? "Remove from favourites" : "Add to favourites";

        /// <summary>The details card's line for the prefab's name.</summary>
        public static string PrefabName(string name) => "Prefab name: " + name;

        /// <summary>The details card's line for where it comes from.</summary>
        public static string OriginLine(Entry entry) =>
            "Origin: " + (entry.Origin == Origin.Vanilla ? "the game" : entry.Origin == Origin.Mod ? (entry.ModName.Length > 0 ? entry.ModName : "a mod, not named") : "unknown");

        /// <summary>The details card's line for how many looks its stars give it.</summary>
        public static string StarLooks(int looks) => $"Star looks: {Numbers.Count(looks)}";

        /// <summary>The details card's line for its components.</summary>
        public static string MadeOf(string components) => "Made of: " + components;

        /// <summary>An ingredient's or a drop's chip: its amount, where it has one, and its name.</summary>
        public static string Amounted(string amount, string name) => string.IsNullOrEmpty(amount) ? name : $"{amount}  {name}";

        /// <summary>The console command's heading: one that gives an item, or one that spawns.</summary>
        public static string CommandHeading(bool give) => give ? "GIVE COMMAND" : "SPAWN COMMAND";

        /// <summary>What copying the console command says.</summary>
        public static string CopiedCommand(string command) => PanelWords.Copied(command) + " Paste it into the console (F5).";
    }
}
