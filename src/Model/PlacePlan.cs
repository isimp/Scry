using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What to do with a world's places on entering it, given what was kept of them
    /// (<see cref="KeptPlaces"/>): a place kept with the stamp it has now, whose names the catalog
    /// holds as many of as when it was read, holds the same and is shown as kept; one kept with
    /// another stamp (its bundle rewritten by an update, mods changed, or read by another build
    /// of Scry), or naming a prefab the game no longer has or has gained since, is shown as kept
    /// until it is read again; one never kept, or whose stamp is not known, is read.
    /// </summary>
    internal sealed class PlacePlan
    {
        /// <summary>What is kept of the world's places, in the order the world lists them.</summary>
        public readonly List<PlaceRecord> Shown = new List<PlaceRecord>();

        /// <summary>The world's places to read, by their place in its list.</summary>
        public readonly List<int> ToRead = new List<int>();

        /// <summary>How many places hold the same, may hold otherwise, and were never kept.</summary>
        public int Fresh, Stale, Missing;

        public static PlacePlan Of(KeptPlaces kept, IReadOnlyList<(string Prefab, bool Room, string Stamp)> world, Func<string, bool> inCatalog)
        {
            var plan = new PlacePlan();
            for (var i = 0; i < world.Count; i++)
            {
                var place = world[i];
                var record = kept.Find(place.Prefab, place.Room);
                if (record == null)
                {
                    plan.Missing++;
                    plan.ToRead.Add(i);
                    continue;
                }
                plan.Shown.Add(record);
                if (place.Stamp.Length > 0 && record.Stamp == place.Stamp && record.Names.Count(inCatalog) == record.Known)
                {
                    plan.Fresh++;
                    continue;
                }
                plan.Stale++;
                plan.ToRead.Add(i);
            }
            return plan;
        }
    }
}
