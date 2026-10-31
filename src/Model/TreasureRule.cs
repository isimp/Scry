namespace Scry
{
    /// <summary>
    /// What a treasure lying in the world gives when picked, as the game chooses it
    /// (<c>PickableItem.SetupRandomPrefab</c>): its own item where it names one, else one of its
    /// random items, a random list beside an own item never drawn from.
    /// </summary>
    internal static class TreasureRule
    {
        /// <summary>Whether a treasure gives one of its random items rather than its own.</summary>
        public static bool GivesRandom(bool hasItem, int randomItems) => !hasItem && randomItems > 0;
    }
}
