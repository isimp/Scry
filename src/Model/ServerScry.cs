namespace Scry
{
    /// <summary>Whether the world's server runs Scry, as its hello tells (<see cref="ServerScries"/>).</summary>
    internal enum ServerScry
    {
        /// <summary>This game runs the world: it is its own server.</summary>
        Here,

        /// <summary>Joined a moment ago, its hello not heard yet.</summary>
        Waiting,

        /// <summary>Its Scry said hello, of the same messages.</summary>
        Scry,

        /// <summary>No hello within the wait: it runs no Scry.</summary>
        None,

        /// <summary>Its Scry is of other messages, to be updated: as none until then.</summary>
        Outdated,
    }

    /// <summary>
    /// The world's server's Scry, known from its hello a moment after joining: heard, its Scry is
    /// asked; not heard within <see cref="HelloWait"/>, it runs none, and nothing is asked or
    /// waited for; where this game runs the world, it is its own server.
    /// </summary>
    internal static class ServerScries
    {
        /// <summary>How long after joining the server's hello is waited for, in seconds, as a player's is.</summary>
        public const double HelloWait = ScryPeers.HelloWait;

        /// <summary>The server's Scry: by whether this game runs the world, its hello was heard, of other messages, and how long since joining.</summary>
        public static ServerScry Of(bool runsWorld, bool heard, bool outdated, double sinceJoined) =>
            runsWorld ? ServerScry.Here
            : outdated ? ServerScry.Outdated
            : heard ? ServerScry.Scry
            : sinceJoined >= HelloWait ? ServerScry.None
            : ServerScry.Waiting;

        /// <summary>Whether the server answers Scry's asks: its Scry heard, or this game itself.</summary>
        public static bool Answers(ServerScry server) => server == ServerScry.Here || server == ServerScry.Scry;

        /// <summary>Whether what only the server's Scry answers is given up on for the session.</summary>
        public static bool GivesUp(ServerScry server) => server == ServerScry.None || server == ServerScry.Outdated;
    }
}
