using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The self-test's made-up events that the game running the world runs as its own, so events
    /// are seen to cross between games as a friend's game sends them: only things the self-test
    /// makes up, a few at a time, each sent only to the game that asked and only where the rules
    /// for every event chose it (<see cref="EventRecipients"/>), so no other player's book ever
    /// holds one.
    /// </summary>
    internal static class TestEvents
    {
        /// <summary>How the name of every thing the self-test makes up begins.</summary>
        public const string Prefix = "ScryMadeUp";

        /// <summary>At most so many are run at a time.</summary>
        public const int Most = 8;

        /// <summary>Whether the world's game runs an event of a thing: only one the self-test made up.</summary>
        public static bool Runs(string thing) => thing != null && thing.StartsWith(Prefix, StringComparison.Ordinal);

        /// <summary>How many of those asked for are run: none below zero, <see cref="Most"/> at most.</summary>
        public static int HowMany(int asked) => Math.Max(0, Math.Min(asked, Most));

        /// <summary>Whether an event taken reaches the character's book: a made-up one only while the self-test records into a book of its own, should one come late or reach another player.</summary>
        public static bool Reaches(string thing, bool recordingForTest) => recordingForTest || !Runs(thing);

        /// <summary>Whether a test event goes to the game that asked: only where the rules chose it among those it goes to.</summary>
        public static bool ToAsker(IReadOnlyList<long> chosen, long asker)
        {
            foreach (var session in chosen) if (session == asker) return true;
            return false;
        }
    }

    /// <summary>The events this game took lately, by their game and number, so the self-test can ask whether one sent to it came: the last <see cref="Kept"/> only.</summary>
    internal sealed class TakenLately
    {
        /// <summary>How many are kept.</summary>
        public const int Kept = 32;

        private readonly Queue<(long Game, int Number)> _order = new Queue<(long, int)>();
        private readonly HashSet<(long Game, int Number)> _taken = new HashSet<(long, int)>();

        /// <summary>An event taken; past <see cref="Kept"/>, the oldest is forgotten.</summary>
        public void Took(long game, int number)
        {
            if (!_taken.Add((game, number))) return;
            _order.Enqueue((game, number));
            if (_order.Count > Kept) _taken.Remove(_order.Dequeue());
        }

        /// <summary>Whether an event was taken lately.</summary>
        public bool Has(long game, int number) => _taken.Contains((game, number));
    }
}
