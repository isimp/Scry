using System;

namespace Scry
{
    /// <summary>
    /// What a place's contents are told with that its own bundle does not hold
    /// (<see cref="PlaceContents.Tell"/>): the game's words in the language it is in, a creature's
    /// name as the game shows it, the music the game gives a weather, and whether the world
    /// levels its creatures up at all. A place is read into what its bundle holds, and told with
    /// these whenever it is shown, so what is kept of it from one session to the next reads
    /// right in any language and any world.
    /// </summary>
    internal sealed class PlaceTerms
    {
        /// <summary>The game's words for a key, in the language it is in; empty where it has none.</summary>
        public Func<string, string> Words = key => "";

        /// <summary>A creature's name as the game shows it, by its prefab; empty for none.</summary>
        public Func<string, string> CreatureName = prefab => "";

        /// <summary>The music the game plays in a weather, by its name (<c>EnvSetup.m_musicDay</c> and the like); empty for none.</summary>
        public Func<string, string> WeatherMusic = environment => "";

        /// <summary>Whether the world rolls its creatures' stars at all (<c>SpawnSystem.GetLevelUpChance</c> above nothing).</summary>
        public bool StarsRoll = true;
    }
}
