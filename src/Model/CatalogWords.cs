namespace Scry
{
    /// <summary>How far reading the catalog has got, in words, as the panel's card shows it while the catalog is read.</summary>
    internal static class CatalogWords
    {
        public static string Reading(string what) => "Reading " + what;

        /// <summary>A step and how many it has done: "Linking entries: 1,200".</summary>
        public static string Progress(string step, int done) => $"{step}: {Numbers.Count(done)}";

        /// <summary>A step, how many it has done and of how many: "Reading prefabs: 3 of 4,000".</summary>
        public static string Progress(string step, int done, int of) => $"{step}: {Numbers.Count(done)} of {Numbers.Count(of)}";

        /// <summary>A step, how many it has done and of about how many, where the whole is only known roughly.</summary>
        public static string ProgressAbout(string step, int done, int about) => $"{step}: {Numbers.Count(done)} of about {Numbers.Count(about)}";
    }
}
