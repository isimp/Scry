using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The biomes a world spawn names, split by whether they are its home. A spawn asking for a
    /// weather spawns only while that weather is on (<c>SpawnData.m_requiredEnvironments</c>): a
    /// biome whose own weathers include one of them is home; a biome where only a world event
    /// brings that weather (Fimbulvinter's weathers, an invasion's) sees it only during the event.
    /// A spawn asking for no weather is at home wherever it names.
    /// </summary>
    public static class SpawnBiomes
    {
        public static (List<string> Home, List<string> Events) Split(IEnumerable<string> biomes, IEnumerable<string> required, Func<string, ICollection<string>> weathersOf)
        {
            var asked = (required ?? Enumerable.Empty<string>()).Where(w => !string.IsNullOrEmpty(w)).ToList();
            var home = new List<string>();
            var events = new List<string>();
            foreach (var biome in biomes)
            {
                var own = weathersOf(biome);
                if (asked.Count == 0 || asked.Any(w => own != null && own.Contains(w))) home.Add(biome);
                else events.Add(biome);
            }
            return (home, events);
        }
    }
}
