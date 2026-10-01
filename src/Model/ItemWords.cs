namespace Scry
{
    /// <summary>
    /// Answers an item's page gives even when they are "no": whether it goes through portals,
    /// how far gear can be upgraded, that it never wears out, and where it is repaired
    /// (<c>InventoryGui.CanRepair</c>: at the station its recipe names, or its repair station,
    /// from the recipe's level) or that it cannot be.
    /// </summary>
    public static class ItemWords
    {
        public const string NoWear = "does not wear out";

        public static string Portals(bool teleportable) => teleportable ? "can go through" : "cannot go through";

        public static string Quality(int maxQuality) => maxQuality > 1 ? $"up to {maxQuality}" : "1 only: it cannot be upgraded";

        public static string Repair(bool canBeRepaired, string station, int level)
        {
            if (!canBeRepaired) return "cannot be repaired";
            if (string.IsNullOrEmpty(station)) return "cannot be repaired: no recipe names a station for it";
            return level > 1 ? $"{station} level {level}" : station;
        }
    }
}
