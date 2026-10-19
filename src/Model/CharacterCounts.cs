namespace Scry
{
    /// <summary>
    /// What the game counted of a thing for the character playing (<c>PlayerProfile.m_playerStats</c>):
    /// how often it killed it, ate it, picked it or built it, and how many of it it made and
    /// picked up.
    /// </summary>
    internal sealed class CharacterCounts
    {
        public int Killed;

        /// <summary>Of the kills, those the game counted as dealt all in melee, with arrows or bolts, by magic, or bare-handed; a kill of mixed hits is in none.</summary>
        public int Melee, Ranged, Magic, Unarmed;

        public int Built;
        public int Eaten;
        public int Picked;
        public int Made;
        public int PickedUp;
    }
}
