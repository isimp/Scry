using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The stars a creature can come with in the world loaded: every source the catalog read
    /// (<see cref="Knowledge.Stars"/>: world spawns, raids, spawners, nests, triggers, summons and
    /// breeding), and every place's spawn points by the levels each place sets for them, a
    /// dungeon's rooms by those of the places it is built into, but for the groups a place leaves
    /// out (<c>CreatureSpawner.Spawn</c>); only their least where the world lets no level rise
    /// (<see cref="PlaceSpawns.StarsRoll"/>). Worked out again as places are read.
    /// </summary>
    internal static class CreatureStars
    {
        static CreatureStars() => WorldCaches.Register(nameof(CreatureStars), Forget);

        private static StarSources _built;
        private static EntryCatalog _of;
        private static int _regroups = -1;
        private static bool _rise;

        public static void Forget()
        {
            _built = null;
            _of = null;
            _regroups = -1;
            _rise = false;
        }

        /// <summary>What the stars are worked out from now: the catalog's grouping, which places read change, and whether levels rise; a creature's page is told again as it changes.</summary>
        public static int Stamp => (WorldCatalog.Current?.Regroups ?? -1) * 2 + (PlaceSpawns.StarsRoll ? 1 : 0);

        /// <summary>The stars a creature's page shows, by its prefab, and whether they are unsure: a mod's creature no source names (<see cref="StarSources.Shown"/>).</summary>
        public static (int Stars, bool Unsure) Of(string prefab) => StarSources.Shown(MostLevel(prefab), Knowledge.ModName(prefab).Length > 0);

        /// <summary>The most level anything in the world can give a creature, by its prefab; none where nothing gives it any.</summary>
        public static int? MostLevel(string prefab) => string.IsNullOrEmpty(prefab) ? null : Built().MostLevel(prefab);

        private static StarSources Built()
        {
            var catalog = WorldCatalog.Current;
            var rise = PlaceSpawns.StarsRoll;
            if (_built != null && ReferenceEquals(catalog, _of) && (catalog == null || catalog.Regroups == _regroups) && rise == _rise) return _built;
            var stars = Knowledge.Stars.Copy();
            stars.LevelsRise = rise;
            if (catalog != null)
            {
                // Each location's own levels, by the label its rooms name it with.
                var byLabel = new Dictionary<string, List<PlaceLevels>>(System.StringComparer.Ordinal);
                foreach (var entry in catalog.All)
                {
                    if (!(entry.Source is PlaceSource place) || place.IsRoom || place.Contents == null) continue;
                    var label = Places.LabelOf(entry);
                    if (label == null) continue;
                    if (!byLabel.TryGetValue(label, out var kept)) byLabel[label] = kept = new List<PlaceLevels>();
                    kept.Add(place.Contents.Levels);
                }
                foreach (var entry in catalog.All)
                {
                    if (!(entry.Source is PlaceSource place) || place.Contents == null || place.Contents.SpawnLevels.Count == 0) continue;
                    var standsIn = place.IsRoom
                        ? entry.FoundIn.SelectMany(label => byLabel.TryGetValue(label, out var levels) ? levels : Enumerable.Empty<PlaceLevels>()).ToList()
                        : new List<PlaceLevels> { place.Contents.Levels };
                    stars.Place(place.Contents.SpawnLevels, standsIn);
                }
            }
            _built = stars;
            _of = catalog;
            _regroups = catalog?.Regroups ?? -1;
            _rise = rise;
            return stars;
        }
    }
}
