using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Finds where a place's floors are (<see cref="FloorFinder"/>): its copy is made with its
    /// colliders kept, rays are cast straight down over it onto them on a grid, and where they
    /// land on what is flat is noted, as heights in the space given, with each ray's place on the
    /// grid, the ground it stands for and whether anything of the place is above it. Each ray
    /// goes down through every surface it meets, not only the first of each collider as one cast
    /// finds them: a cave room's rock is a single collider, the top of its rock and its floor
    /// both. A roof (its pieces lean 26 or 45 degrees) or a ramp is not flat. The colliders are
    /// taken away after, so the copy is like any other.
    /// </summary>
    internal static class FloorProbe
    {
        /// <summary>How flat a surface must be to count as floor: about 18 degrees at the steepest.</summary>
        private const float Flat = 0.95f;

        private static readonly List<Collider> Colliders = new List<Collider>();

        /// <summary>How many surfaces one ray goes through at the most, and how far past each it goes on.</summary>
        private const int MostSurfaces = 48;
        private const float Past = 0.02f;

        /// <summary>How far under the topmost thing on a ray a surface must be for something to be above it.</summary>
        private const float Covered = 0.5f;

        /// <summary>
        /// The colliders of copies with spawn points that are on none of the layers the game finds
        /// a spawned creature's floor on (<c>ZoneSystem.m_solidRayMask</c>), noted while the copy
        /// sleeps with its own layers, as the stage's layer is all it has once awake.
        /// </summary>
        private static readonly HashSet<Collider> NotSolid = new HashSet<Collider>();

        private static int _solidLayers;

        /// <summary>The layers the game finds a spawned creature's floor on (<c>ZoneSystem.m_solidRayMask</c>).</summary>
        private static int SolidLayers => _solidLayers != 0 ? _solidLayers : _solidLayers = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");

        /// <summary>Notes which of a sleeping copy's colliders the game's floor rays pass through.</summary>
        public static void NoteNotSolid(GameObject copy)
        {
            copy.GetComponentsInChildren(true, Colliders);
            foreach (var collider in Colliders)
            {
                if (collider != null && (SolidLayers & (1 << collider.gameObject.layer)) == 0) NotSolid.Add(collider);
            }
            Colliders.Clear();
        }

        /// <summary>Forgets the colliders noted, as the stage is cleared.</summary>
        public static void ForgetNotSolid() => NotSolid.Clear();

        /// <summary>What a ray landed on, for the self-test to tell: its collider's object and the one it is under, as many are called "default".</summary>
        [Diagnostic]
        private static string PartName(Transform part)
        {
            string Plain(Transform t) => t.gameObject.name.Replace("(Clone)", "").Trim();
            return part.parent != null ? Plain(part.parent) + "/" + Plain(part) : Plain(part);
        }

        /// <summary>How far down the game looks for a spawned creature's floor, from a metre above its point (<c>ZoneSystem.FindFloor</c>).</summary>
        private const float FloorBelow = 1000f;

        /// <summary>
        /// Where each spawn point on the copy puts its creature, as the game spawns it
        /// (<c>CreatureSpawner.Spawn</c>, <c>ZoneSystem.FindFloor</c>): on the first solid thing
        /// straight down from a metre above the point, which can be a little above it; where
        /// nothing is, at the point. Set as how far it drops, so it holds wherever the copy goes.
        /// </summary>
        private static void DropSpawns(Transform root, int mask, List<Stage.SpawnHere> spawns)
        {
            foreach (var spawn in spawns)
            {
                if (spawn.At == null || !spawn.At.IsChildOf(root)) continue;
                var point = spawn.At.position;
                var from = point.y + 1f;
                var bottom = from - FloorBelow;
                for (var step = 0; step < MostSurfaces && from > bottom; step++)
                {
                    if (!Physics.Raycast(new Vector3(point.x, from, point.z), Vector3.down, out var hit, from - bottom, mask, QueryTriggerInteraction.Ignore)) break;
                    from = hit.point.y - Past;
                    if (hit.collider == null || !hit.collider.transform.IsChildOf(root) || NotSolid.Contains(hit.collider)) continue;
                    spawn.Drop = point.y - hit.point.y;
                    spawn.Grounded = true;
                    break;
                }
            }
        }

        /// <summary>
        /// Casts the rays over the copy and adds where they land to <paramref name="hits"/>, as
        /// patch <paramref name="patch"/>, and to <paramref name="names"/>, when given, the name of
        /// what each landed on; the ground the rays were cast over, 0 for a copy with nothing to
        /// land on.
        /// </summary>
        public static float Read(GameObject copy, Transform space, int layer, List<FloorHit> hits, int patch, List<string> names = null, List<Stage.SpawnHere> spawns = null)
        {
            if (copy == null) return 0f;
            copy.GetComponentsInChildren(false, Colliders);
            var rays = 0;
            try
            {
                // Made and moved this frame: physics learns where its colliders are before they are measured.
                var timed = Timing.Start();
                Physics.SyncTransforms();
                Timing.Add("floor probe sync", timed);
                timed = Timing.Start();
                Bounds? around = null;
                foreach (var collider in Colliders)
                {
                    if (collider == null || !collider.enabled || collider.isTrigger) continue;
                    if (around == null) around = collider.bounds;
                    else
                    {
                        var grown = around.Value;
                        grown.Encapsulate(collider.bounds);
                        around = grown;
                    }
                }
                if (around == null) return 0f;

                var bounds = around.Value;
                var top = bounds.max.y + 1f;
                var length = bounds.size.y + 2f;
                var mask = layer >= 0 ? 1 << layer : Physics.DefaultRaycastLayers;
                var root = copy.transform;
                if (spawns != null) DropSpawns(root, mask, spawns);
                var grid = FloorFinder.Grid(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z);
                var cell = FloorFinder.CellArea(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, grid.Count);
                var bottom = top - length;
                foreach (var (i, j, x, z) in grid)
                {
                    rays++;
                    var from = top;
                    var highest = float.NaN;
                    for (var step = 0; step < MostSurfaces && from > bottom; step++)
                    {
                        if (!Physics.Raycast(new Vector3(x, from, z), Vector3.down, out var hit, from - bottom, mask, QueryTriggerInteraction.Ignore)) break;
                        from = hit.point.y - Past;
                        if (hit.collider == null || !hit.collider.transform.IsChildOf(root)) continue;
                        if (float.IsNaN(highest)) highest = hit.point.y;
                        if (hit.normal.y < Flat) continue;
                        hits.Add(new FloorHit
                        {
                            Patch = patch, I = i, J = j, Height = space.InverseTransformPoint(hit.point).y, Area = cell,
                            Open = highest - hit.point.y < Covered,
                        });
                        names?.Add(PartName(hit.collider.transform));
                    }
                }
                Timing.Add("floor probe rays", timed);
                return rays * cell;
            }
            finally
            {
                // Nothing else of the copy should meet anything; its colliders go once read.
                var gone = Timing.Start();
                copy.GetComponentsInChildren(true, Colliders);
                foreach (var collider in Colliders)
                {
                    if (collider == null) continue;
                    NotSolid.Remove(collider);
                    Object.Destroy(collider);
                }
                Colliders.Clear();
                Timing.Add("floor probe colliders", gone);
            }
        }
    }
}
