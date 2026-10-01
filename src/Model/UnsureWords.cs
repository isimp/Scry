namespace Scry
{
    /// <summary>
    /// Scry is sure of what it reads from the game's data and of what it works out by the game's
    /// own rules (whose code the startup check watches). The rest is its best knowledge, marked
    /// on the page with <see cref="Mark"/> and a reason shown on hover: which mod added a thing
    /// when only clues say so, what a mod may change in code Scry cannot read, what was seen in
    /// play, and where it found nothing.
    /// </summary>
    public static class UnsureWords
    {
        /// <summary>What an unsure line's label starts with.</summary>
        public const string Mark = "~ ";

        /// <summary>The clue that is a mod's own word: Jotunn's registry, where a mod built on it registers what it adds.</summary>
        public const string RegistryClue = "Jotunn's registry";

        public const string Hooked = "A mod runs code of its own here, and only the mod knows what it does";
        public const string Seen = "What happened in your games, not the game's odds; a rare drop may not have come yet";
        public const string NothingFound = "Scry found nothing that gives it; a location, an event or a mod's own code still may";
        public const string NowhereFound = "Scry found nothing that places it; a location, an event, another creature or a mod's own code still may";
        public const string ModSkill = "The mod adding this skill names it nowhere Scry can read";

        public static string Marked(string label) => Mark + label;

        /// <summary>Whether the clue a mod was named by is the mod's own word; no clue (a mod's own page) is too.</summary>
        public static bool IsSureClue(string clue) => string.IsNullOrEmpty(clue) || clue == RegistryClue;

        /// <summary>Why a mod named by a clue is not sure, by the clue.</summary>
        public static string ModClue(string clue)
        {
            const string not = "; the mod does not say so itself";
            switch (clue)
            {
                case "its scripts": return "Scry matched it to this mod by the scripts it carries, which come from the mod's own assembly" + not;
                case "a bundle holding it by name": return "Scry matched it to this mod by an asset bundle the mod ships that holds something of its name" + not;
                case "the bundles holding what it uses": return "Scry matched it to this mod by its sounds or icons, found in asset bundles the mod ships" + not;
                default: return "Scry matched it to this mod by clues" + not;
            }
        }
    }
}
