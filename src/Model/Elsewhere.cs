namespace Scry
{
    /// <summary>
    /// What the character did that ran in another player's game, so its game counted nothing of
    /// it: with other players near, one player's game runs each thing around them, the first to
    /// come near it until they move away, and only that game sees what falls, breaks or is picked.
    /// </summary>
    internal enum Elsewhere
    {
        /// <summary>A kill the character took part in: the game that ran it tells each player who hit the creature.</summary>
        Kill,

        /// <summary>A thing the character hit that broke in that game: gone soon after the character's last hit on it.</summary>
        Break,

        /// <summary>A pick the character made: it starts in the picker's game and runs in that one.</summary>
        Pick,
    }

    /// <summary>What only the game running a thing sees, so another player's game may run it in the character's place.</summary>
    internal static class OtherGames
    {
        /// <summary>Whether a deed is seen only by the game running the thing: a birth, growing up, a break, a starred one's fall, a spawn, a crop grown, a station's work finished.</summary>
        public static bool MayRun(Deed deed)
        {
            switch (deed)
            {
                case Deed.Born:
                case Deed.GrewUp:
                case Deed.Broke:
                case Deed.StarredFell:
                case Deed.Spawned:
                case Deed.Grew:
                case Deed.Finished:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The same by what shows a number kept back (<see cref="EntryKeys.Did"/>); never anything but a deed.</summary>
        public static bool MayRun(string proof) => EntryKeys.IsDeed(proof, out var deed) && MayRun(deed);

        /// <summary>Whether a count is kept only by the game running the thing: what falls and what is given, not what a chest holds or the weather.</summary>
        public static bool MayRun(Tally tally) => tally == Tally.Fell || tally == Tally.Gave;
    }

    /// <summary>How what ran in another player's game is told (<see cref="Elsewhere"/>).</summary>
    internal static class ElsewhereWords
    {
        /// <summary>What ends a hint or a count another player's game may run in the character's place, once the character played with others.</summary>
        public const string Suffix = ", where your game runs it";

        /// <summary>The tip of each such hint and count: why.</summary>
        public const string Tip = "With other players near, one player's game runs each thing around you: the first to come near it, until they move away. "
            + "Only that game sees it fall, break, be picked, spawn, be born, grow up or eat, so yours counts only what it runs.";

        /// <summary>The faint line under what a creature eats, some of it not yet seen, once the character played with others.</summary>
        public const string Eating = "What it eats shows as you see it eat" + Suffix;

        /// <summary>The faint line under a count, telling how many more of the character's own ran in another player's game; none for none.</summary>
        public static string Line(Elsewhere what, int count)
        {
            if (count <= 0) return null;
            var more = Numbers.Count(count) + " more ";
            var one = count == 1;
            switch (what)
            {
                case Elsewhere.Kill: return more + "of your kills ran in another player's game, so " + (one ? "its" : "their") + " drops were not counted";
                case Elsewhere.Break: return more + "you hit broke in another player's game, so what " + (one ? "it" : "they") + " gave was not counted";
                default: return more + "of your picks ran in another player's game, so what " + (one ? "it" : "they") + " gave was not counted";
            }
        }

        /// <summary>What follows the character's kills of a creature, drawn faint: how many of them ran in another player's game; none for none.</summary>
        public static string Killed(int count) => count > 0 ? " · " + Numbers.Count(count) + " in another player's game" : null;
    }
}
