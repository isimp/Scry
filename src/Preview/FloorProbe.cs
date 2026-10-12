using System;
using System.Collections.Generic;
using System.Linq;
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
    /// What the rays met is kept for each kind of place (<see cref="FloorRecord"/>,
    /// <see cref="FloorRecords"/>), on a grid in its own frame, each surface with the part it is
    /// on: a roll whose parts were all seen is told from the record with nothing woken, and one
    /// bringing a part not yet seen is probed and what it sees added. A copy that does not stand
    /// upright is probed on a grid of the stage's own and nothing kept.
    /// </summary>
    internal static class FloorProbe
    {
        /// <summary>Leaving a world forgets the copies noted (<see cref="WorldCaches"/>).</summary>
        static FloorProbe() => WorldCaches.Register(nameof(FloorProbe), ForgetNotSolid);

        /// <summary>How flat a surface must be to count as floor: about 18 degrees at the steepest.</summary>
        private const float Flat = 0.95f;

        private static readonly List<Collider> Colliders = new List<Collider>();

        /// <summary>How many surfaces one ray goes through at the most, and how far past each it goes on.</summary>
        private const int MostSurfaces = 48;
        private const float Past = 0.02f;

        /// <summary>
        /// The colliders of copies with spawn points that are on none of the layers the game finds
        /// a spawned creature's floor on (<c>ZoneSystem.m_solidRayMask</c>), noted while the copy
        /// sleeps with its own layers, as the stage's layer is all it has once awake.
        /// </summary>
        private static readonly HashSet<Collider> NotSolid = new HashSet<Collider>();

        /// <summary>The objects each copy's rolls switch on or off (<see cref="PlaceCopy"/>), noted as it is rolled: the parts a roll can bring.</summary>
        private static readonly Dictionary<GameObject, HashSet<Transform>> Switches = new Dictionary<GameObject, HashSet<Transform>>();

        private static int _solidLayers;

        /// <summary>The layers the game finds a spawned creature's floor on (<c>ZoneSystem.m_solidRayMask</c>).</summary>
        private static int SolidLayers => _solidLayers != 0 ? _solidLayers : _solidLayers = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");

        /// <summary>How often a copy's floors were told from what was kept, and found by its rays.</summary>
        public static int FromKept { get; private set; }

        public static int Probed { get; private set; }

        /// <summary>
        /// While set, a copy whose floors the record tells is probed too, and the two compared, for
        /// the self-test: how many were compared, how many differed, and the last that did.
        /// </summary>
        [Diagnostic]
        public static bool Verify { get; set; }

        [Diagnostic]
        public static int Verified { get; private set; }

        [Diagnostic]
        public static int Differed { get; private set; }

        [Diagnostic]
        public static string LastDiffered { get; private set; } = "";

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

        /// <summary>Notes the objects a copy's rolls switched on or off.</summary>
        public static void NoteSwitches(GameObject copy, HashSet<Transform> switches) => Switches[copy] = switches;

        /// <summary>Forgets the colliders and copies noted, as the stage is cleared.</summary>
        public static void ForgetNotSolid()
        {
            NotSolid.Clear();
            Switches.Clear();
        }

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
        /// The copy's floors, as patch <paramref name="patch"/> of <paramref name="hits"/>, with the
        /// name of what each landed on in <paramref name="names"/>, when given, and where each
        /// spawn point's creature stands; the ground the rays were cast over, 0 for a copy with
        /// nothing to land on. From what was kept of the place where that tells the copy's roll,
        /// else by rays cast over the copy, woken for them by <paramref name="wake"/> and put back
        /// by <paramref name="sleep"/> where it sleeps; its colliders go either way.
        /// </summary>
        public static float Read(GameObject copy, Transform space, int layer, List<FloorHit> hits, int patch, string prefab, bool room,
            List<string> names = null, List<Stage.SpawnHere> spawns = null, Action wake = null, Action sleep = null)
        {
            if (copy == null) return 0f;
            var root = copy.transform;
            try
            {
                var timed = Timing.Start();
                var parts = PartsOf(copy, out var transforms);
                var present = new HashSet<int>();
                for (var i = 0; i < transforms.Count; i++) if (There(transforms[i], root)) present.Add(i);
                var placed = (spawns ?? new List<Stage.SpawnHere>()).Where(s => s.At != null && s.At.IsChildOf(root)).ToList();
                var keys = placed.Select(s => KeyOf(s.At, root)).ToList();
                var upright = root.up.y > 0.9999f;
                var stamp = Locations.StampOf(prefab, room);
                var kept = upright ? FloorRecords.Find(prefab, room, stamp) : null;
                if (kept != null && !kept.Fits(parts)) kept = null;
                var covered = kept != null && kept.Covers(present, keys);
                Timing.Add("floor probe parts", timed);

                if (covered && !Verify)
                {
                    FromKept++;
                    timed = Timing.Start();
                    Tell(kept, present, root, space, patch, hits, names, placed, keys);
                    Timing.Add("floor probe kept", timed);
                    return kept.Ground;
                }

                wake?.Invoke();
                try
                {
                    timed = Timing.Start();
                    // Made and moved this frame: physics learns where its colliders are before they are measured.
                    Physics.SyncTransforms();
                    Timing.Add("floor probe sync", timed);
                    if (!upright) return ByStageGrid(copy, space, layer, hits, patch, names, placed);

                    timed = Timing.Start();
                    var record = kept ?? New(prefab, room, stamp, root, transforms, parts);
                    if (record == null) return 0f;
                    var seen = Cast(record, root, layer, transforms, present, placed, keys);
                    Probed++;
                    Timing.Add("floor probe rays", timed);
                    if (covered)
                    {
                        Compare(kept, FloorRecord.New(prefab, room, stamp, kept.MinX, kept.MaxX, kept.MinZ, kept.MaxZ, kept.Top, kept.Bottom, parts).With(seen), present, keys);
                        Tell(kept, present, root, space, patch, hits, names, placed, keys);
                        return kept.Ground;
                    }
                    var grown = record.With(seen);
                    FloorRecords.Put(grown);
                    Tell(grown, present, root, space, patch, hits, names, placed, keys);
                    return grown.Ground;
                }
                finally
                {
                    sleep?.Invoke();
                }
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
                    UnityEngine.Object.Destroy(collider);
                }
                Colliders.Clear();
                Switches.Remove(copy);
                Timing.Add("floor probe colliders", gone);
            }
        }

        /// <summary>
        /// The parts of a copy a ray can meet, in the order its record keeps them: each object
        /// holding a solid collider that is there, or that a roll can bring (every object above it
        /// switched on, or one of those its rolls switch), named, and solid where the game's floor
        /// rays for what it drops meet it.
        /// </summary>
        private static List<RayPart> PartsOf(GameObject copy, out List<Transform> transforms)
        {
            var root = copy.transform;
            var switches = Switches.TryGetValue(copy, out var noted) ? noted : new HashSet<Transform>();
            var parts = new List<RayPart>();
            transforms = new List<Transform>();
            var seen = new HashSet<Transform>();
            copy.GetComponentsInChildren(true, Colliders);
            foreach (var collider in Colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                var part = collider.transform;
                if (!seen.Add(part)) continue;
                var possible = true;
                for (var t = part; t != null && t != root; t = t.parent)
                {
                    if (!t.gameObject.activeSelf && !switches.Contains(t))
                    {
                        possible = false;
                        break;
                    }
                }
                if (!possible) continue;
                transforms.Add(part);
                parts.Add(new RayPart { Name = PartName(part), Solid = !NotSolid.Contains(collider) });
            }
            Colliders.Clear();
            return parts;
        }

        /// <summary>Whether a part is there in this roll: it and every object above it switched on.</summary>
        private static bool There(Transform part, Transform root)
        {
            for (var t = part; t != null && t != root; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
            }
            return true;
        }

        /// <summary>A spawn point by its place in the copy: each step down from the root, which child it is.</summary>
        private static string KeyOf(Transform point, Transform root)
        {
            var steps = new List<string>();
            for (var t = point; t != null && t != root; t = t.parent) steps.Add(Stored.Count(t.GetSiblingIndex()));
            steps.Reverse();
            return string.Join(".", steps);
        }

        /// <summary>
        /// A record of nothing seen yet, its grid laid over every part a roll can bring, in the
        /// copy's own frame, and its rays cast from a metre over the highest to a metre under the
        /// lowest; none where the copy has no part to land on.
        /// </summary>
        private static FloorRecord New(string prefab, bool room, string stamp, Transform root, List<Transform> transforms, List<RayPart> parts)
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var any = false;
            foreach (var part in transforms)
            {
                part.GetComponents(Colliders);
                foreach (var collider in Colliders)
                {
                    if (collider == null || !collider.enabled || collider.isTrigger) continue;
                    foreach (var corner in Corners(collider))
                    {
                        var at = root.InverseTransformPoint(corner);
                        min = Vector3.Min(min, at);
                        max = Vector3.Max(max, at);
                        any = true;
                    }
                }
            }
            Colliders.Clear();
            return any ? FloorRecord.New(prefab, room, stamp, min.x, max.x, min.z, max.z, max.y + 1f, min.y - 1f, parts) : null;
        }

        /// <summary>The corners of the box a collider fills, in the world, whether or not it is awake.</summary>
        private static IEnumerable<Vector3> Corners(Collider collider)
        {
            Vector3 center, extents;
            switch (collider)
            {
                case BoxCollider box:
                    center = box.center;
                    extents = box.size / 2f;
                    break;
                case SphereCollider sphere:
                    center = sphere.center;
                    extents = Vector3.one * sphere.radius;
                    break;
                case CapsuleCollider capsule:
                    center = capsule.center;
                    extents = Vector3.one * capsule.radius;
                    extents[capsule.direction] = Mathf.Max(capsule.radius, capsule.height / 2f);
                    break;
                case MeshCollider mesh when mesh.sharedMesh != null:
                    center = mesh.sharedMesh.bounds.center;
                    extents = mesh.sharedMesh.bounds.extents;
                    break;
                default:
                    // Anything else by the box the physics gives it, known only while it is awake.
                    var bounds = collider.bounds;
                    if (bounds.size == Vector3.zero) yield break;
                    for (var c = 0; c < 8; c++) yield return new Vector3(c % 2 == 0 ? bounds.min.x : bounds.max.x, c / 2 % 2 == 0 ? bounds.min.y : bounds.max.y, c / 4 == 0 ? bounds.min.z : bounds.max.z);
                    yield break;
            }
            var owner = collider.transform;
            for (var c = 0; c < 8; c++)
            {
                var local = center + Vector3.Scale(extents, new Vector3(c % 2 == 0 ? -1f : 1f, c / 2 % 2 == 0 ? -1f : 1f, c / 4 == 0 ? -1f : 1f));
                yield return owner.TransformPoint(local);
            }
        }

        /// <summary>
        /// Casts the record's rays over the copy, and one down from a metre above each spawn point
        /// the game drops its creature from (<c>ZoneSystem.FindFloor</c>), noting every surface of
        /// a part each meets, in the copy's own frame.
        /// </summary>
        private static ProbeSeen Cast(FloorRecord record, Transform root, int layer, List<Transform> transforms, HashSet<int> present, List<Stage.SpawnHere> placed, List<string> keys)
        {
            var seen = new ProbeSeen();
            seen.Present.UnionWith(present);
            var index = new Dictionary<Transform, int>();
            for (var i = 0; i < transforms.Count; i++) index[transforms[i]] = i;
            var mask = layer >= 0 ? 1 << layer : Physics.DefaultRaycastLayers;

            List<RaySurface> Down(Vector3 from, float length)
            {
                var surfaces = new List<RaySurface>();
                var top = from.y;
                var bottom = from.y - length;
                for (var step = 0; step < MostSurfaces && top > bottom; step++)
                {
                    if (!Physics.Raycast(new Vector3(from.x, top, from.z), Vector3.down, out var hit, top - bottom, mask, QueryTriggerInteraction.Ignore)) break;
                    top = hit.point.y - Past;
                    if (hit.collider == null || !index.TryGetValue(hit.collider.transform, out var part)) continue;
                    surfaces.Add(new RaySurface { Height = root.InverseTransformPoint(hit.point).y, Flat = hit.normal.y >= Flat, Part = part });
                }
                return surfaces;
            }

            var grid = record.Grid;
            for (var ray = 0; ray < grid.Count; ray++)
            {
                var surfaces = Down(root.TransformPoint(new Vector3(grid[ray].X, record.Top, grid[ray].Z)), record.Top - record.Bottom);
                if (surfaces.Count > 0) seen.Rays[ray] = surfaces;
            }
            for (var i = 0; i < placed.Count; i++) seen.Spawns[keys[i]] = Down(placed[i].At.position + Vector3.up, FloorBelow);
            return seen;
        }

        /// <summary>Tells the copy's floors and where its creatures stand from a record, for the parts there.</summary>
        private static void Tell(FloorRecord record, HashSet<int> present, Transform root, Transform space, int patch, List<FloorHit> hits, List<string> names, List<Stage.SpawnHere> placed, List<string> keys)
        {
            var cell = record.Cell;
            foreach (var landing in record.Landings(present))
            {
                var at = root.TransformPoint(new Vector3(landing.X, landing.Height, landing.Z));
                hits.Add(new FloorHit { Patch = patch, I = landing.I, J = landing.J, Height = space.InverseTransformPoint(at).y, Area = cell, Open = landing.Open });
                names?.Add(record.Parts[landing.Part].Name);
            }
            for (var i = 0; i < placed.Count; i++)
            {
                if (!record.GroundUnder(keys[i], present, out var ground)) continue;
                placed[i].Drop = root.InverseTransformPoint(placed[i].At.position).y - ground;
                placed[i].Grounded = true;
            }
        }

        /// <summary>
        /// How far apart what was kept and a roll's own rays may put the same surface: a ray's hit
        /// is off by a millimetre or so where a dungeon's rooms stand, high over the world, and
        /// the copy stands elsewhere each time.
        /// </summary>
        private const float SameHeight = 0.01f;

        /// <summary>For the self-test: whether what a record tells of a roll is what that roll's own rays found, each surface at about the same height.</summary>
        [Diagnostic]
        private static void Compare(FloorRecord kept, FloorRecord fresh, HashSet<int> present, List<string> keys)
        {
            Verified++;
            string Told(FloorLanding l) => Stored.Count(l.I) + "," + Stored.Count(l.J) + " at " + Stored.Number(l.Height, 3) + (l.Open ? " open" : "");
            var onlyKept = new List<string>();
            var unmatched = fresh.Landings(present).ToList();
            foreach (var landing in kept.Landings(present))
            {
                var match = unmatched.FindIndex(l => l.I == landing.I && l.J == landing.J && l.Open == landing.Open && Math.Abs(l.Height - landing.Height) <= SameHeight);
                if (match >= 0) unmatched.RemoveAt(match);
                else onlyKept.Add(Told(landing));
            }
            var onlyRays = unmatched.Select(Told).ToList();
            foreach (var key in keys)
            {
                var was = kept.GroundUnder(key, present, out var wasGround);
                var now = fresh.GroundUnder(key, present, out var nowGround);
                if (was == now && (!was || Math.Abs(wasGround - nowGround) <= SameHeight)) continue;
                onlyKept.Add("spawn " + key + (was ? " on " + Stored.Number(wasGround, 3) : " on nothing"));
                onlyRays.Add("spawn " + key + (now ? " on " + Stored.Number(nowGround, 3) : " on nothing"));
            }
            if (onlyKept.Count == 0 && onlyRays.Count == 0) return;
            Differed++;
            LastDiffered = kept.Prefab + ": only kept " + Stored.Count(onlyKept.Count) + " (" + string.Join("; ", onlyKept.Take(3)) + "), only by rays " + Stored.Count(onlyRays.Count) + " (" + string.Join("; ", onlyRays.Take(3)) + ")";
        }

        /// <summary>
        /// A copy not standing upright, probed as it stands on the stage's own grid and nothing
        /// kept: rays over the world box of its colliders, and where each spawn point's creature
        /// stands found by the game's floor rays.
        /// </summary>
        private static float ByStageGrid(GameObject copy, Transform space, int layer, List<FloorHit> hits, int patch, List<string> names, List<Stage.SpawnHere> placed)
        {
            var timed = Timing.Start();
            copy.GetComponentsInChildren(false, Colliders);
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
            Colliders.Clear();
            if (around == null) return 0f;

            var bounds = around.Value;
            var top = bounds.max.y + 1f;
            var length = bounds.size.y + 2f;
            var mask = layer >= 0 ? 1 << layer : Physics.DefaultRaycastLayers;
            var root = copy.transform;
            DropSpawns(root, mask, placed);
            var grid = FloorFinder.Grid(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z);
            var cell = FloorFinder.CellArea(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, grid.Count);
            var bottom = top - length;
            foreach (var (i, j, x, z) in grid)
            {
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
                        Open = highest - hit.point.y < FloorRecord.Covered,
                    });
                    names?.Add(PartName(hit.collider.transform));
                }
            }
            Probed++;
            Timing.Add("floor probe rays", timed);
            return grid.Count * cell;
        }

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
    }
}
