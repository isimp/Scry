using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The biomes a world spawn names, split by whether they are its home. A spawn asking for a
    /// weather spawns only while that weather is on (<c>SpawnData.m_requiredEnvironments</c>): a
    /// biome whose own weathers include one of them is home; a biome where only a world event
    /// brings that weather (an invasion's) sees it only during the event. A spawn waiting for a
    /// world event to be on where it would spawn (<c>m_requiredPersistentEvent</c>, Fimbulvinter's
    /// Jotun warriors) is at home nowhere. A spawn asking for neither is at home wherever it names.
    /// </summary>
    internal static class SpawnBiomes
    {
        public static (List<string> Home, List<string> Events) Split(IEnumerable<string> biomes, IEnumerable<string> required, Func<string, ICollection<string>> weathersOf,
            string persistentEvent = null)
        {
            var asked = (required ?? Enumerable.Empty<string>()).Where(w => !string.IsNullOrEmpty(w)).ToList();
            var home = new List<string>();
            var events = new List<string>();
            foreach (var biome in biomes)
            {
                var own = weathersOf(biome);
                if (string.IsNullOrEmpty(persistentEvent) && (asked.Count == 0 || asked.Any(w => own != null && own.Contains(w)))) home.Add(biome);
                else events.Add(biome);
            }
            return (home, events);
        }

        /// <summary>
        /// A creature's home among the biomes its spawns name: where it spawns with no world key
        /// asked for, the biomes its keyed spawns add coming later (Charred warriors to the other
        /// biomes once Fader falls); where every spawn waits for a key, those biomes are its home
        /// (the Jotun warriors' Deep North patrols).
        /// </summary>
        /// <param name="unkeyed">The home biomes of its spawns that ask for no key.</param>
        /// <param name="keyed">The home biomes of its spawns that wait for one.</param>
        public static (List<string> Home, List<string> Later) Later(IEnumerable<string> unkeyed, IEnumerable<string> keyed)
        {
            var home = unkeyed.Distinct().ToList();
            if (home.Count == 0) return (keyed.Distinct().ToList(), new List<string>());
            return (home, keyed.Distinct().Where(b => !home.Contains(b)).ToList());
        }
    }
}
