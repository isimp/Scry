namespace Scry
{
    /// <summary>
    /// Answers an item's page gives even when they are "no": whether it goes through portals,
    /// how far gear can be upgraded, that it never wears out, and where it is repaired
    /// (<c>InventoryGui.CanRepair</c>: at the station its recipe names, or its repair station,
    /// from the recipe's level) or that it cannot be.
    /// </summary>
    internal static class ItemWords
    {
        public const string NoWear = "does not wear out";

        public static string Portals(bool teleportable) => teleportable ? "can go through" : "cannot go through";

        public static string Quality(int maxQuality) => maxQuality > 1 ? $"up to {Numbers.Count(maxQuality)}" : "1 only: it cannot be upgraded";

        public static string Repair(bool canBeRepaired, string station, int level)
        {
            if (!canBeRepaired) return "cannot be repaired";
            if (string.IsNullOrEmpty(station)) return "cannot be repaired: no recipe names a station for it";
            return level > 1 ? $"{station} level {Numbers.Count(level)}" : station;
        }

        /// <summary>The title over an item's upgrade kits: the station that takes it past its top quality.</summary>
        public static string PastTop(string upgradeStation) => "Past its top quality, at " + UpgradeStation(upgradeStation);

        /// <summary>The upgrade station by the name the game shows, or said plainly where no prefab is one.</summary>
        public static string UpgradeStation(string shown) => shown ?? "an upgrade station";
    }
}
