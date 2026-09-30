namespace Scry
{
    /// <summary>A mod loaded, as its entry holds it: its name, version and id as it declares them, and the folder it was loaded from.</summary>
    public sealed class ModSource
    {
        public string Name = "", Version = "", Guid = "", Folder = "";
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
    }
}
