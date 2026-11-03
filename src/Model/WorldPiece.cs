namespace Scry
{
    /// <summary>
    /// Which building pieces a hit teaches, as a hit teaches a rock or a tree. The world places
    /// pieces of its own: the Dvergr's crates, lanterns, beams and walls, the goblins' fences, the
    /// black marble of the Mistlands' ruins. Those stand from no player (<c>Piece.GetCreator</c>
    /// is 0, as it is for one with no <c>Piece</c> at all), so hitting one is how a player comes
    /// to know it. A piece a player built is known by its recipe, and hitting a friend's wall
    /// says nothing of how to build it.
    /// </summary>
    internal static class WorldPiece
    {
        /// <summary>Whether hitting a piece makes it known, by who built it (0 for none): one the world placed, never one a player built.</summary>
        public static bool LearnedByHit(long creator) => creator == 0L;
    }
}
