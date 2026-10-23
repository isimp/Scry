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
        /// <summary>How flat a surface must be to count as floor: about 18 degrees at the steepest.</summary>
        private const float Flat = 0.95f;

        private static readonly List<Collider> Colliders = new List<Collider>();

        /// <summary>How many rays a reading casts between its pauses (<see cref="Reading"/>): each a fraction of a millisecond.</summary>
        private const int RaysASlice = 32;

        /// <summary>How many surfaces one ray goes through at the most; it goes on <see cref="FloorRecord.Past"/> past each.</summary>
        private const int MostSurfaces = 48;

        private static int _solidLayers;

        /// <summary>The layers the game finds a spawned creature's floor on (<c>ZoneSystem.m_solidRayMask</c>).</summary>
        private static int SolidLayers => _solidLayers != 0 ? _solidLayers : _solidLayers = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");

        /// <summary>How often a copy's floors were told from what was kept, and found by its rays.</summary>
        public static int FromKept { get; private set; }

        public static int Probed { get; private set; }

        /// <summary>
        /// For the self-test: while set, a copy whose floors the record tells is probed too, and
        /// given with the record, what its rays found, the parts there, its spawn points' keys,
        /// every part each ray met by its place in the grid's list, and the copy's root, to look at
        /// a part that differs as it stands.
        /// </summary>
        [Diagnostic]
        public static Action<FloorRecord, FloorRecord, HashSet<int>, List<string>, Dictionary<int, List<(float Height, int Part)>>, Transform> Checked { get; set; }

        /// <summary>
        /// Notes which of a sleeping copy's colliders are on none of the layers the game finds a
        /// spawned creature's floor on (<c>ZoneSystem.m_solidRayMask</c>): known by their own
        /// layers, as the stage's layer is all the copy has once awake.
        /// </summary>
        public static void NoteNotSolid(GameObject copy, HashSet<Collider> into)
        {
            copy.GetComponentsInChildren(true, Colliders);
            foreach (var collider in Colliders)
            {
                if (collider != null && (SolidLayers & (1 << collider.gameObject.layer)) == 0) into.Add(collider);
            }
            Colliders.Clear();
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
        /// name of what each landed on in <paramref name="names"/>, when given, and where each of
        /// its spawn points' creature stands, by what it was rolled to (<paramref name="roll"/>,
        /// noted as it was made); the ground the rays were cast over, 0 for a copy with
        /// nothing to land on. From what was kept of the place where that tells the copy's roll,
        /// else by rays cast over the copy, woken for them by <paramref name="wake"/> and put back
        /// by <paramref name="sleep"/> where it sleeps; its colliders go either way. Only ground
        /// walked to from <paramref name="start"/> lands (<see cref="FloorWalk"/>), every flat
        /// surface where none is given; the highest its rays met is told to <paramref name="top"/>.
        /// A copy not standing upright is read as it stands, every flat surface, nothing told.
        /// </summary>
        public static float Read(GameObject copy, Transform space, int layer, List<FloorHit> hits, int patch, string prefab, bool room, PlaceRoll roll,
            List<string> names = null, Action wake = null, Action sleep = null, FloorWalk.Start start = null, Action<float> top = null)
        {
            var ground = 0f;
            using (var steps = Reading(copy, space, layer, hits, patch, prefab, room, roll, names, wake, sleep, start, top).GetEnumerator())
            {
                while (steps.MoveNext()) if (steps.Current.HasValue) ground = steps.Current.Value;
            }
            return ground;
        }

        /// <summary>
        /// The copy's floors read as <see cref="Read"/> reads them, one ray at a time: none between
        /// rays, then the ground the rays were cast over, so a place too big to read in one frame
        /// is read a few milliseconds a frame. The copy must stand as it is until the reading is
        /// done; its colliders go once it is, or once the reading is let go of unfinished.
        /// </summary>
        public static IEnumerable<float?> Reading(GameObject copy, Transform space, int layer, List<FloorHit> hits, int patch, string prefab, bool room, PlaceRoll roll,
            List<string> names = null, Action wake = null, Action sleep = null, FloorWalk.Start start = null, Action<float> top = null)
        {
            if (copy == null)
            {
                yield return 0f;
                yield break;
            }
            var root = copy.transform;
            try
            {
                // The parts are gathered so many at a time, a big place's thousands over frames,
                // none between them, as the rays are cast.
                var parts = new List<RayPart>();
                var transforms = new List<Transform>();
                var timed = Timing.Start();
                foreach (var _ in PartsOf(copy, roll, parts, transforms))
                {
                    Timing.Add("floor probe parts", timed);
                    yield return null;
                    timed = Timing.Start();
                }
                var present = new HashSet<int>();
                for (var i = 0; i < transforms.Count; i++) if (There(transforms[i], root)) present.Add(i);
                var placed = roll.Spawns.Where(s => s.At != null && s.At.IsChildOf(root)).ToList();
                var keys = placed.Select(s => KeyOf(s.At, root)).ToList();
                var upright = root.up.y > 0.9999f;
                var stamp = PlaceRecords.StampOf(prefab, room);
                var kept = upright ? FloorRecords.Find(prefab, room, stamp) : null;
                if (kept != null && !kept.Fits(parts)) kept = null;
                var covered = kept != null && kept.Covers(present, keys);
                Timing.Add("floor probe parts", timed);

                if (covered && Checked == null)
                {
                    FromKept++;
                    timed = Timing.Start();
                    Tell(kept, present, root, space, patch, hits, names, placed, keys, start, top);
                    Timing.Add("floor probe kept", timed);
                    yield return kept.Ground;
                    yield break;
                }

                wake?.Invoke();
                try
                {
                    timed = Timing.Start();
                    // Made and moved this frame: physics learns where its colliders are before they are measured.
                    Physics.SyncTransforms();
                    Timing.Add("floor probe sync", timed);
                    if (!upright)
                    {
                        yield return ByStageGrid(copy, space, layer, hits, patch, names, placed, roll.NotSolid);
                        yield break;
                    }

                    timed = Timing.Start();
                    var record = kept ?? New(prefab, room, stamp, root, transforms, parts);
                    if (record == null)
                    {
                        yield return 0f;
                        yield break;
                    }
                    var met = covered && Checked != null ? new Dictionary<int, List<(float Height, int Part)>>() : null;
                    ProbeSeen seen = null;
                    foreach (var cast in Cast(record, root, layer, transforms, present, placed, keys, met))
                    {
                        if (cast != null)
                        {
                            seen = cast;
                            continue;
                        }
                        // Between rays: the time so far counted, and counted again from the next.
                        Timing.Add("floor probe rays", timed);
                        yield return null;
                        timed = Timing.Start();
                    }
                    Probed++;
                    Timing.Add("floor probe rays", timed);
                    if (covered)
                    {
                        Checked?.Invoke(kept, FloorRecord.New(prefab, room, stamp, kept.MinX, kept.MaxX, kept.MinZ, kept.MaxZ, kept.Top, kept.Bottom, parts).With(seen), present, keys, met, root);
                        Tell(kept, present, root, space, patch, hits, names, placed, keys, start, top);
                        yield return kept.Ground;
                        yield break;
                    }
                    var grown = record.With(seen);
                    FloorRecords.Put(grown);
                    Tell(grown, present, root, space, patch, hits, names, placed, keys, start, top);
                    yield return grown.Ground;
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
                    if (collider != null) UnityEngine.Object.Destroy(collider);
                }
                Colliders.Clear();
                Timing.Add("floor probe colliders", gone);
            }
        }

        /// <summary>How many of a copy's colliders are gathered as parts at a time, between which a reading may wait for the next frame.</summary>
        private const int PartsAtOnce = 256;

        /// <summary>
        /// Gathers the parts of a copy a ray can meet into <paramref name="parts"/>, their objects
        /// into <paramref name="transforms"/>, in the order its record keeps them: each object
        /// holding a solid collider that is there, or that a roll can bring (every object above it
        /// switched on, or one of those its rolls switch), named, and solid where the game's floor
        /// rays for what it drops meet it. So many colliders at a time (<see cref="PartsAtOnce"/>),
        /// none between them; the copy's colliders taken first into a list of the reading's own, as
        /// another reading may run between.
        /// </summary>
        private static IEnumerable<object> PartsOf(GameObject copy, PlaceRoll roll, List<RayPart> parts, List<Transform> transforms)
        {
            var root = copy.transform;
            var seen = new HashSet<Transform>();
            var colliders = copy.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (i > 0 && i % PartsAtOnce == 0) yield return null;
                var collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger) continue;
                var part = collider.transform;
                if (!seen.Add(part)) continue;
                var possible = true;
                for (var t = part; t != null && t != root; t = t.parent)
                {
                    if (!t.gameObject.activeSelf && !roll.Switched.Contains(t))
                    {
                        possible = false;
                        break;
                    }
                }
                if (!possible) continue;
                transforms.Add(part);
                parts.Add(new RayPart { Name = PartName(part), Solid = !roll.NotSolid.Contains(collider) });
            }
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
        /// a part each meets or passes just under one, in the copy's own frame; into <paramref name="met"/>, when given,
        /// every part each of the record's rays met. The rays pass through other copies on the
        /// stage (<see cref="OwnDown"/>), so what is kept of a kind of place is the same whatever
        /// stood beside the copy it was probed on. None after each slice of rays
        /// (<see cref="RaysASlice"/>), what was seen last, so a reading can pause between slices.
        /// </summary>
        private static IEnumerable<ProbeSeen> Cast(FloorRecord record, Transform root, int layer, List<Transform> transforms, HashSet<int> present, List<Stage.SpawnHere> placed, List<string> keys,
            Dictionary<int, List<(float Height, int Part)>> met = null)
        {
            var seen = new ProbeSeen();
            seen.Present.UnionWith(present);
            var index = new Dictionary<Transform, int>();
            for (var i = 0; i < transforms.Count; i++) index[transforms[i]] = i;
            var mask = layer >= 0 ? 1 << layer : Physics.DefaultRaycastLayers;

            System.Func<Collider, int> partOf = c => index.TryGetValue(c.transform, out var part) ? part : -1;
            List<RaySurface> Surfaces(Vector3 from, float length, List<(float Height, int Part)> all = null)
            {
                var surfaces = new List<RaySurface>();
                OwnDown(from, length, mask, partOf, RayHits);
                foreach (var hit in RayHits)
                {
                    var part = index[hit.collider.transform];
                    var height = root.InverseTransformPoint(hit.point).y;
                    all?.Add((height, part));
                    surfaces.Add(new RaySurface { Height = height, Flat = hit.normal.y >= Flat, Part = part });
                }
                return surfaces;
            }

            var grid = record.Grid;
            for (var ray = 0; ray < grid.Count; ray++)
            {
                var all = met != null ? met[ray] = new List<(float Height, int Part)>() : null;
                var surfaces = Surfaces(root.TransformPoint(new Vector3(grid[ray].X, record.Top, grid[ray].Z)), record.Top - record.Bottom, all);
                if (surfaces.Count > 0) seen.Rays[ray] = surfaces;
                if (ray % RaysASlice == RaysASlice - 1) yield return null;
            }
            for (var i = 0; i < placed.Count; i++) seen.Spawns[keys[i]] = Surfaces(placed[i].At.position + Vector3.up, FloorBelow);
            yield return seen;
        }

        /// <summary>
        /// Tells the copy's floors and where its creatures stand from a record, for the parts
        /// there: its ground walked to from <paramref name="start"/>, each with how far over it what
        /// hides it from above is, and the highest its rays met, in the space given.
        /// </summary>
        private static void Tell(FloorRecord record, HashSet<int> present, Transform root, Transform space, int patch, List<FloorHit> hits, List<string> names, List<Stage.SpawnHere> placed, List<string> keys,
            FloorWalk.Start start, Action<float> top)
        {
            var cell = record.Cell;
            float Height(float x, float y, float z) => space.InverseTransformPoint(root.TransformPoint(new Vector3(x, y, z))).y;
            foreach (var landing in start != null ? FloorWalk.Landings(record, present, start) : record.Landings(present))
            {
                var height = Height(landing.X, landing.Height, landing.Z);
                var over = landing.Over > 0f ? Height(landing.X, landing.Height + landing.Over, landing.Z) - height : 0f;
                hits.Add(new FloorHit { Patch = patch, I = landing.I, J = landing.J, Height = height, Area = cell, Over = over, Open = landing.Open });
                names?.Add(record.Parts[landing.Part].Name);
            }
            var highest = record.Highest(present);
            if (top != null && !float.IsNaN(highest)) top(Height((record.MinX + record.MaxX) / 2f, highest, (record.MinZ + record.MaxZ) / 2f));
            for (var i = 0; i < placed.Count; i++)
            {
                if (!record.GroundUnder(keys[i], present, out var ground)) continue;
                placed[i].Drop = root.InverseTransformPoint(placed[i].At.position).y - ground;
                placed[i].Grounded = true;
            }
        }

        /// <summary>
        /// Each surface a ray straight down from <paramref name="from"/> meets within
        /// <paramref name="length"/>, top down: it goes on past each, as one collider (a cave
        /// room's rock) can be met more than once.
        /// </summary>
        private static IEnumerable<RaycastHit> Down(Vector3 from, float length, int mask)
        {
            var top = from.y;
            var bottom = from.y - length;
            for (var step = 0; step < MostSurfaces && top > bottom; step++)
            {
                if (!Physics.Raycast(new Vector3(from.x, top, from.z), Vector3.down, out var hit, top - bottom, mask, QueryTriggerInteraction.Ignore)) yield break;
                top = hit.point.y - FloorRecord.Past;
                yield return hit;
            }
        }

        /// <summary>Every hit of one step of <see cref="OwnDown"/>; more than this, and that step goes one hit at a time.</summary>
        private static readonly RaycastHit[] StepHits = new RaycastHit[128];

        /// <summary>One step's hits by how far along each is and its part, which of them are kept, and all a ray kept, for <see cref="OwnDown"/>.</summary>
        private static readonly List<(float Distance, int Part)> StepParts = new List<(float, int)>();
        private static readonly List<int> StepKept = new List<int>();
        private static readonly List<RaycastHit> RayHits = new List<RaycastHit>();

        /// <summary>
        /// Into <paramref name="into"/>, each surface of a part of the copy (<paramref name="partOf"/>,
        /// -1 for none) a ray straight down from <paramref name="from"/> meets within
        /// <paramref name="length"/>, top down, as <see cref="Down"/> meets them but passing
        /// through every other collider: each step takes the nearest of all it hits that is the
        /// copy's own, so another room's floor beside or over the copy's, even at the same height,
        /// neither ends the ray nor hides what is under it. With each it takes what of other parts
        /// the step past it passes (<see cref="FloorRecord.StepKeeps"/>).
        /// </summary>
        private static void OwnDown(Vector3 from, float length, int mask, System.Func<Collider, int> partOf, List<RaycastHit> into)
        {
            into.Clear();
            var top = from.y;
            var bottom = from.y - length;
            for (var step = 0; step < MostSurfaces && top > bottom; step++)
            {
                var origin = new Vector3(from.x, top, from.z);
                var count = Physics.RaycastNonAlloc(origin, Vector3.down, StepHits, top - bottom, mask, QueryTriggerInteraction.Ignore);
                RaycastHit? nearest = null;
                if (count < StepHits.Length)
                {
                    StepParts.Clear();
                    for (var i = 0; i < count; i++) StepParts.Add((StepHits[i].distance, StepHits[i].collider != null ? partOf(StepHits[i].collider) : -1));
                    FloorRecord.StepKeeps(StepParts, StepKept);
                    if (StepKept.Count > 0) nearest = StepHits[StepKept[0]];
                    foreach (var kept in StepKept) into.Add(StepHits[kept]);
                }
                else
                {
                    // Too many to be sure of the nearest: one hit at a time, past the others.
                    foreach (var hit in Down(origin, top - bottom, mask))
                    {
                        if (hit.collider == null || partOf(hit.collider) < 0) continue;
                        nearest = hit;
                        into.Add(hit);
                        break;
                    }
                }
                if (nearest == null) return;
                top = nearest.Value.point.y - FloorRecord.Past;
            }
        }

        /// <summary>
        /// A copy not standing upright, probed as it stands on the stage's own grid and nothing
        /// kept: rays over the world box of its colliders, and where each spawn point's creature
        /// stands found by the game's floor rays.
        /// </summary>
        private static float ByStageGrid(GameObject copy, Transform space, int layer, List<FloorHit> hits, int patch, List<string> names, List<Stage.SpawnHere> placed, HashSet<Collider> notSolid)
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
            DropSpawns(root, mask, placed, notSolid);
            var grid = FloorFinder.Grid(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z);
            var cell = FloorFinder.CellArea(bounds.min.x, bounds.max.x, bounds.min.z, bounds.max.z, grid.Count);
            foreach (var (i, j, x, z) in grid)
            {
                var highest = float.NaN;
                foreach (var hit in Down(new Vector3(x, top, z), length, mask))
                {
                    if (hit.collider == null || !hit.collider.transform.IsChildOf(root)) continue;
                    if (float.IsNaN(highest)) highest = hit.point.y;
                    if (hit.normal.y < Flat) continue;
                    hits.Add(new FloorHit
                    {
                        Patch = patch, I = i, J = j, Height = space.InverseTransformPoint(hit.point).y, Area = cell,
                        Open = FloorRecord.IsOpen(highest, hit.point.y),
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
        private static void DropSpawns(Transform root, int mask, List<Stage.SpawnHere> spawns, HashSet<Collider> notSolid)
        {
            foreach (var spawn in spawns)
            {
                var point = spawn.At.position;
                foreach (var hit in Down(point + Vector3.up, FloorBelow, mask))
                {
                    if (hit.collider == null || !hit.collider.transform.IsChildOf(root) || notSolid.Contains(hit.collider)) continue;
                    spawn.Drop = point.y - hit.point.y;
                    spawn.Grounded = true;
                    break;
                }
            }
        }
    }
}
