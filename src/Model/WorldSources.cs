using System;

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

        /// <summary>The places with a map icon, which the game shows on every map: traders, boss altars, the start.</summary>
        OnEveryMap,
    }

    /// <summary>How a source's places show, as the player chose (<see cref="CompendiumBook.Shows"/>).</summary>
    internal enum SourceLevel
    {
        /// <summary>Not at all.</summary>
        Off,

        /// <summary>Seen: listed, their pages closed.</summary>
        Seen,

        /// <summary>Known, bringing along what a known place does.</summary>
        Known,

        /// <summary>Known with every room of a dungeon, walked or not.</summary>
        WithInsides,
    }

    /// <summary>The sources a world's places show from, and what each allows.</summary>
    internal static class WorldSources
    {
        /// <summary>Every source, in the order the box lists them.</summary>
        public static readonly WorldSource[] All = (WorldSource[])Enum.GetValues(typeof(WorldSource));

        /// <summary>
        /// The level a source starts at and the least it is set to: the character's own ground and
        /// the places marked for it are written into its book as seen as they come, so they show
        /// at least that; the others start off.
        /// </summary>
        public static SourceLevel Least(WorldSource source) =>
            source == WorldSource.Explored || source == WorldSource.Marked ? SourceLevel.Seen : SourceLevel.Off;
    }
}
