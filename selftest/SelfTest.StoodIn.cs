using System.Collections;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for places built as a dungeon whose rooms stand in the world.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Hildir's Sealed Tower, its rooms standing in the world, is a place stood in, no dungeon
        /// lying inside: no entrance of it is marked as a dungeon's, and a visit kept of it is marked
        /// as you were there. Every other kind built as a dungeon lies inside.
        /// </summary>
        private static IEnumerator PlacesStoodIn(Probe p)
        {
            var tower = LocationNamed("Hildir_plainsfortress");
            if (tower == null)
            {
                p.Skip("there is no Sealed Tower in this world's catalog");
                yield break;
            }
            p.Check(!DungeonKinds.Is(tower.Name) && DungeonKinds.InWorld(tower.Name), "Hildir's Sealed Tower, its rooms standing in the world, is a place stood in, no dungeon lying inside",
                $"lies inside {DungeonKinds.Is(tower.Name)}, in the world {DungeonKinds.InWorld(tower.Name)}");
            var inWorld = X.Catalog.Where(e => PlaceOf(e) != null && !PlaceOf(e).IsRoom && DungeonKinds.InWorld(e.Name)).Select(e => e.Name).ToList();
            p.Note($"kinds built as a dungeon in the world: {string.Join(", ", inWorld)}; lying inside: {Numbers.Count(DungeonKinds.All.Count)}");
            var visits = DungeonWalks.Maps.OfWorld(CompendiumMap.World).Where(m => m.Map.Rooms.Count == 0).ToList();
            p.Note($"places stood in kept: {(visits.Count == 0 ? "none" : string.Join(", ", visits.Select(v => $"{v.Map.Location} days {Numbers.Count(v.Map.FirstDay)} to {Numbers.Count(v.Map.LastDay)}")))}");
            yield break;
        }
    }
}
