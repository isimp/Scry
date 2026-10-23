using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>Where a world's places come to show besides the character meeting them (<see cref="CompendiumBook.FoundIn"/>).</summary>
    internal enum WorldSource
    {
        /// <summary>The ground the character explored itself, by its explored map.</summary>
        Explored,

        /// <summary>Ground from maps shared at a cartography table.</summary>
        SharedMaps,

        /// <summary>Places a Vegvisir or a rune stone marked on the character's map.</summary>
        Marked,

        /// <summary>The places with a map icon, which the game shows on every map: traders, the start, the places Hildir sends you to.</summary>
        OnEveryMap,

        /// <summary>Every kind of place the world holds, by the server's census of it (<see cref="PlaceCounts"/>).</summary>
        WholeWorld,
    }

    /// <summary>How a source's places show, as the player chose (<see cref="CompendiumBook.Shows"/>).</summary>
    internal enum SourceLevel
    {
        /// <summary>Not at all.</summary>
        Off,

        /// <summary>Heard of, with how many there are: the census's alone (<see cref="WorldSource.WholeWorld"/>).</summary>
        Counted,

        /// <summary>Seen: listed, their pages closed.</summary>
        Seen,

        /// <summary>Known, bringing along what a known place does.</summary>
        Known,

        /// <summary>Known, with what anyone on this server walked inside its dungeons (<see cref="ServerWalks"/>).</summary>
        WithInsides,
    }

    /// <summary>The sources a world's places show from, and what each allows.</summary>
    internal static class WorldSources
    {
        /// <summary>Every source, in the order the box lists them.</summary>
        public static readonly WorldSource[] All = (WorldSource[])Enum.GetValues(typeof(WorldSource));

        /// <summary>The level a source starts at: the character's own ground and the places marked for it seen, the others off.</summary>
        public static SourceLevel Starts(WorldSource source) =>
            source == WorldSource.Explored || source == WorldSource.Marked ? SourceLevel.Seen : SourceLevel.Off;

        private static readonly SourceLevel[] Every = (SourceLevel[])Enum.GetValues(typeof(SourceLevel));
        private static readonly SourceLevel[] OffOrShown = Array.FindAll(Every, level => level != SourceLevel.Counted);
        private static readonly SourceLevel[] OffOrCounted = { SourceLevel.Off, SourceLevel.Counted };

        /// <summary>Whether a source is ground rather than single places: the ground explored and ground from shared maps, whose dungeons can be marked on Valheim's big map (<see cref="DungeonMarks"/>).</summary>
        public static bool HasGround(WorldSource source) => source == WorldSource.Explored || source == WorldSource.SharedMaps;

        /// <summary>
        /// The levels the box offers a source, Off first: no source writes the character's book, so
        /// each can be turned off; the whole world only off or counted, no other counted.
        /// </summary>
        public static SourceLevel[] Offered(WorldSource source) => source == WorldSource.WholeWorld ? OffOrCounted : OffOrShown;

        /// <summary>Whether the box offers a source a level.</summary>
        public static bool Allows(WorldSource source, SourceLevel level) => Array.IndexOf(Offered(source), level) >= 0;

        /// <summary>Whether a place a source found has insides to show (<see cref="SourceLevel.WithInsides"/>), by the kinds of place that are dungeons; With insides is dimmed where none has.</summary>
        public static bool HasInsides(IEnumerable<string> found, Func<string, bool> isDungeon)
        {
            foreach (var kind in found) if (isDungeon(kind)) return true;
            return false;
        }
    }
}
