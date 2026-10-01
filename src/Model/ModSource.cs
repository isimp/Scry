namespace Scry
{
    /// <summary>A mod loaded, as its entry holds it: its name, version and id as it declares them, and the folder it was loaded from.</summary>
    public sealed class ModSource
    {
        public string Name = "", Version = "", Guid = "", Folder = "";

        /// <summary>From its package's manifest.json, when a mod manager installed it: what it is, who made it, and its website.</summary>
        public string Description = "", Author = "", Website = "";

        /// <summary>Its package's readme and icon, each "" when it has none.</summary>
        public string ReadmePath = "", IconPath = "";

        /// <summary>The mods it needs, works with or will not run with, and those needing or working with it.</summary>
        public ModRelations Relations = new ModRelations();
    }

    /// <summary>How a mod's page words what it is.</summary>
    public static class ModWords
    {
        /// <summary>Its version and id, for its card.</summary>
        public static string Card(ModSource mod)
        {
            if (string.IsNullOrEmpty(mod.Version)) return "Id " + mod.Guid;
            return $"Version {mod.Version}, id {mod.Guid}";
        }

        /// <summary>A website worth a link: one on the web, not a file or anything else a click could open.</summary>
        public static bool IsWebsite(string url) =>
            !string.IsNullOrEmpty(url) && (url.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://", System.StringComparison.OrdinalIgnoreCase))
            && url.IndexOfAny(new[] { ' ', '"', '\n', '\r' }) < 0;
    }
}
