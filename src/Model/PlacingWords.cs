namespace Scry
{
    /// <summary>
    /// What an item's page tells of placing it, where a build tool places it as a piece of itself
    /// (<c>ItemDrop.MakePiece</c>), as the Serving Tray places food: what places it and what that
    /// uses up, what a placed one does, and what it stands as.
    /// </summary>
    internal static class PlacingWords
    {
        /// <summary>The topic's heading on an item's page.</summary>
        public const string Title = "Placing it";

        /// <summary>The tool that places it, and how many of the item placing one uses up; none said where it uses up something else.</summary>
        public static string With(string tool, int usesUp) =>
            usesUp <= 0 ? tool : usesUp == 1 ? $"{tool}, using up one" : $"{tool}, using up {Numbers.Count(usesUp)}";

        /// <summary>
        /// What a placed one does (<c>ItemDrop.Interact</c>): a food or drink is eaten or drunk from
        /// where it stands with use and picked up with the alternate use, anything else picked up
        /// with use; placed, it never despawns, as a dropped item does (<c>ItemDrop.SlowUpdate</c>).
        /// </summary>
        public static string Once(bool consumable, bool drink) =>
            consumable
                ? $"{(drink ? "drunk" : "eaten")} from where it stands with use, picked up with the alternate use, and never despawns"
                : "picked up again with use, and never despawns";

        /// <summary>What a placed one stands as: a piece of its material (<c>WearNTear.m_materialType</c>, by name) with its health.</summary>
        public static string StandsAs(string material, float health)
        {
            var word = Material(material);
            var article = "aeiou".IndexOf(word.Length > 0 ? word[0] : 'x') >= 0 ? "an" : "a";
            return $"{article} {word} piece with {Numbers.Amount(health)} health";
        }

        private static string Material(string material)
        {
            switch (material)
            {
                case "Wood": return "wooden";
                default: return (material ?? "").ToLowerInvariant();
            }
        }
    }
}
