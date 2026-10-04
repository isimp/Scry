namespace Scry
{
    /// <summary>What an item is used for, in words: the title of each kind of use and place (<see cref="UseBook"/>), and each thing it goes into.</summary>
    internal static class UseWords
    {
        /// <summary>A use's title by its kind and the place's shown name (null for none), with the upgrade station's name where it is known.</summary>
        public static string Title(UseKind kind, string place, string upgradeStation)
        {
            switch (kind)
            {
                case UseKind.Crafts: return place != null ? $"Used to make at {place}" : "Used to make by hand";
                case UseKind.UpgradesPastTop: return "Takes these past their top quality, at " + ItemWords.UpgradeStation(upgradeStation);
                case UseKind.Builds: return place != null ? $"Used to build near {place}" : "Used to build";
                case UseKind.TurnsInto: return place != null ? $"{place} turns it into" : "Turned into";
                case UseKind.Fuels: return "Burnt as fuel by";
                default: return "Eaten by";
            }
        }

        /// <summary>What it goes into, said to need it only for upgrades where the recipe takes none of it at first.</summary>
        public static string Target(string name, bool upgradesOnly) => upgradesOnly ? name + " (upgrades)" : name;
    }
}
