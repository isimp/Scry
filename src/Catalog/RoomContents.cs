using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>What a dungeon's room holds by what was read of it (<see cref="PlaceContents"/>): its parts and the creatures its spawn points place, by their prefabs' names; nothing for a room not read yet, or anything else.</summary>
    internal static class RoomContents
    {
        /// <summary>A room's parts and creatures by their prefabs' names, for the compendium (<see cref="CompendiumView.RoomsHold"/>).</summary>
        public static readonly Func<Entry, IEnumerable<string>> Of = Named;

        private static IEnumerable<string> Named(Entry room)
        {
            var contents = room?.Source is PlaceSource place && place.IsRoom ? place.Contents : null;
            if (contents == null) yield break;
            foreach (var part in contents.Parts) if (!string.IsNullOrEmpty(part.Prefab)) yield return part.Prefab;
            foreach (var creature in contents.Creatures) if (!string.IsNullOrEmpty(creature.Prefab)) yield return creature.Prefab;
        }
    }
}
