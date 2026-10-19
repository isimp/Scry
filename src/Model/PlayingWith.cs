using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>Whether another player's game runs Scry (<see cref="ScryPeers"/>).</summary>
    internal enum PeerScry
    {
        /// <summary>Scry of the same messages: its events reach this character.</summary>
        Scry,

        /// <summary>No Scry: nothing of what runs in it reaches this character.</summary>
        None,

        /// <summary>Scry of other messages, to be updated: as none until then.</summary>
        Outdated,
    }

    /// <summary>How the players played with are told: the notice naming those whose games lack Scry, and the compendium box's row.</summary>
    internal static class PlayingWithWords
    {
        /// <summary>The compendium box's caption for the players played with.</summary>
        public const string RowCaption = "Playing with";

        /// <summary>The notice's button, opening the compendium's box with the row.</summary>
        public const string WhoHasScry = "Who has Scry";

        /// <summary>The faint line under the row.</summary>
        public const string RowHint = "What runs in a game without Scry counts nothing for you.";

        /// <summary>The tip of the notice's cross.</summary>
        public const string PutAway = "Put this away until another player without Scry joins";

        /// <summary>
        /// The notice naming the players whose games run no Scry, or Scry of other messages, and
        /// that what runs in their games counts nothing for this character; none where every
        /// player's game has Scry.
        /// </summary>
        public static string Notice(IReadOnlyList<string> without, IReadOnlyList<string> outdated)
        {
            var parts = new List<string>();
            if (without.Count > 0) parts.Add(Naming.Joined(without) + (without.Count == 1 ? " plays without Scry" : " play without Scry"));
            if (outdated.Count > 0)
            {
                var owners = new List<string>();
                foreach (var name in outdated) owners.Add(name + "'s");
                parts.Add(Naming.Joined(owners) + (outdated.Count == 1 ? " Scry needs updating" : " Scry need updating"));
            }
            if (parts.Count == 0) return null;
            var one = without.Count + outdated.Count == 1;
            var whose = one ? (without.Count == 1 ? without[0] : outdated[0]) + "'s game" : "their games";
            return Naming.Joined(parts) + ": what runs in " + whose + " counts nothing for you.";
        }

        /// <summary>The notice for the players played with (<see cref="ScryPeers.PlayingWith"/>); none where every one's game has Scry.</summary>
        public static string Notice(IReadOnlyList<(string Name, PeerScry Scry)> players)
        {
            var without = new List<string>();
            var outdated = new List<string>();
            foreach (var (name, scry) in players)
            {
                if (scry == PeerScry.None) without.Add(name);
                else if (scry == PeerScry.Outdated) outdated.Add(name);
            }
            return Notice(without, outdated);
        }

        /// <summary>The names of the players played with whose games lack Scry or need it updated, as the notice is closed for (<see cref="GapNotice"/>).</summary>
        public static List<string> Lacking(IReadOnlyList<(string Name, PeerScry Scry)> players)
        {
            var names = new List<string>();
            foreach (var (name, scry) in players) if (scry != PeerScry.Scry) names.Add(name);
            return names;
        }

        /// <summary>Whether a player's game has Scry, as the row says it.</summary>
        private static string Said(PeerScry scry)
        {
            switch (scry)
            {
                case PeerScry.Scry: return "Scry";
                case PeerScry.None: return "no Scry";
                default: return "needs updating";
            }
        }

        /// <summary>The players played with and whether each one's game has Scry, "Ana · no Scry, Cara · Scry"; none with no one else in the world.</summary>
        public static string Row(IReadOnlyList<(string Name, PeerScry Scry)> players)
        {
            if (players.Count == 0) return null;
            var each = new List<string>();
            foreach (var (name, scry) in players) each.Add(name + " · " + Said(scry));
            return string.Join(", ", each);
        }
    }

    /// <summary>When the notice naming players without Scry shows: while one is in the world, until closed, and again once another joins.</summary>
    internal sealed class GapNotice
    {
        /// <summary>The players the notice was closed for.</summary>
        private readonly HashSet<string> _closedFor = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Whether it shows for the players whose games lack Scry: while any was not among those it was closed for.</summary>
        public bool Shows(IReadOnlyCollection<string> lacking)
        {
            foreach (var name in lacking) if (!_closedFor.Contains(name)) return true;
            return false;
        }

        /// <summary>Closed for the players named now.</summary>
        public void Close(IReadOnlyCollection<string> lacking) => _closedFor.UnionWith(lacking);

        /// <summary>On leaving a world.</summary>
        public void Forget() => _closedFor.Clear();
    }
}
