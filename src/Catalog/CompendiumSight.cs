using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character sees, for the compendium, by one rule for everything in the world: a
    /// thing is seen once it is in the camera's view, nothing solid between, within the reach its
    /// size gives (<see cref="Sighting.Reach"/>, 30 m for one of a person's size or smaller), and
    /// it is paired with the biome and the place it stands in. Creatures are looked at once a
    /// second; everything else the game keeps in the world (trees, logs, rocks, deposits,
    /// buildings, pickables, nests, items on the ground) is kept as it wakes and looked at in
    /// rounds, a share each frame and all once a second (<see cref="Rounds{T}"/>), and let go once
    /// seen or once nothing more is to be learned of it there. One that stands still rests while
    /// out of reach, out of view or hidden by more than a move of the character or the eye could
    /// change, passed over without a look until either moves (<see cref="Viewpoint"/>), so standing
    /// still costs only what can move. A place is seen with its centre in
    /// view within its own reach and <see cref="CreatureReach"/> beyond it. A container opened
    /// shows what it holds, seen, and the container itself is known.
    /// </summary>
    internal static class CompendiumSight
    {
        static CompendiumSight() => WorldCaches.Register(nameof(CompendiumSight), () =>
        {
            Things.Clear();
            Shapes.Clear();
        });

        /// <summary>How far beyond its own reach a place in view is seen, as far as a creature of a person's size.</summary>
        private const float CreatureReach = Sighting.PersonReach;

        /// <summary>Half the angle of the view taken as seen, about the width the game's camera shows.</summary>
        private const float HalfView = 50f;

        /// <summary>How high above where a thing stands it is looked at, at the least, so the line to a small one does not graze the ground.</summary>
        private const float LeastHeight = 0.5f;

        private static AccessTools.FieldRef<List<Location>> _places;
        private static AccessTools.FieldRef<InventoryGui, Container> _container;
        private static int _solid;

        /// <summary>Everything the game keeps in the world, as it wakes, to be looked at in rounds of a second.</summary>
        private static readonly Rounds<ZNetView> Things = new Rounds<ZNetView>(1f);

        /// <summary>Where the character and the eye were when the things resting last woke.</summary>
        private static readonly Viewpoint Eye = new Viewpoint();

        /// <summary>
        /// What each kind of thing is, by its prefab: its key, whether it is a creature (looked at
        /// on its own), whether it shows anything, how far it is seen, how high its middle is,
        /// whether it can move, and whether the catalog looked with lists it.
        /// </summary>
        private static readonly Dictionary<int, Shape> Shapes = new Dictionary<int, Shape>();

        private sealed class Shape
        {
            public string Key;
            public bool Creature;
            public bool Shows;
            public float Reach;
            public float Height;
            public bool Moves;
            public CompendiumCatchUp ListedBy;
            public bool Listed;
        }

        /// <summary>How many things are kept to look at now, those resting among them.</summary>
        public static int ThingCount => Things.Count;

        /// <summary>How many of the things kept rest now, one by one; for the self-test.</summary>
        public static int ThingsResting => Things.Resting;

        /// <summary>How many things were seen, or had nothing more to tell, and were let go, in this world.</summary>
        public static int ThingsLetGo => Things.LetGo;

        /// <summary>How many things were dropped, gone or not to be looked at (a creature, one showing nothing, one the catalog lacks), in this world.</summary>
        public static int ThingsDropped => Things.Dropped;

        /// <summary>Whether a thing is kept to look at now.</summary>
        public static bool Keeps(ZNetView thing) => Things.Contains(thing);

        /// <summary>A thing the game keeps in the world woke: kept to be looked at.</summary>
        public static void Woke(ZNetView thing)
        {
            if (thing != null && thing.GetZDO() != null) Things.Add(thing);
        }

        /// <summary>Looks around the character once a second: the creatures in view, the places, and an open container.</summary>
        public static void Look(Player player, CompendiumCatchUp catchUp, CompendiumBook book)
        {
            Bind();
            var view = GameCamera.instance != null ? GameCamera.instance.transform : player.transform;
            foreach (var creature in Character.GetAllCharacters())
            {
                if (creature == null || creature.IsPlayer()) continue;
                var centre = creature.GetCenterPoint();
                if (Vector3.Distance(player.transform.position, centre) <= Sighting.Reach(creature.GetHeight()) && InView(view, centre) && Clear(view.position, centre, null))
                {
                    var key = Utils.GetPrefabName(creature.gameObject);
                    // Its level less one is its stars, as the game shows them over it.
                    catchUp.Met(key, KnownLevel.Seen, book, stars: creature.GetLevel() - 1);
                    CompendiumKeeper.MetIn(key, creature.transform.position);
                }
            }
            foreach (var place in _places())
            {
                if (place == null) continue;
                var centre = place.transform.position;
                if (Vector3.Distance(player.transform.position, centre) <= place.m_exteriorRadius + CreatureReach && InView(view, centre) && InSight(view.position, centre, place.m_exteriorRadius))
                {
                    catchUp.Met(EntryKeys.For(Kind.Location, Utils.GetPrefabName(place.gameObject)), KnownLevel.Seen, book);
                }
            }
            var gui = InventoryGui.instance;
            var container = gui != null && gui.IsContainerOpen() ? _container(gui) : null;
            if (container == null) return;
            var chest = Utils.GetPrefabName(container.gameObject);
            catchUp.Met(chest, KnownLevel.Known, book, CameBy.Used);
            CompendiumKeeper.MetIn(chest, container.transform.position);
            var chestPlace = Location.GetLocation(container.transform.position);
            foreach (var held in container.GetInventory().GetAllItems())
            {
                if (held?.m_dropPrefab == null) continue;
                catchUp.Met(held.m_dropPrefab.name, KnownLevel.Seen, book);
                // Its loot was found in it, and in the place it stands in.
                CompendiumKeeper.Saw(chest, held.m_dropPrefab.name);
                if (chestPlace != null) CompendiumKeeper.Saw(held.m_dropPrefab.name, EntryKeys.For(Kind.Location, Utils.GetPrefabName(chestPlace.gameObject)));
            }
        }

        /// <summary>
        /// Looks at the share of the things kept that a frame of so many seconds is due, within
        /// <see cref="FrameBudgetMs"/> of the frame, the things resting woken first where the
        /// character or the eye moved; called each frame.
        /// </summary>
        public static void Sweep(Player player, CompendiumCatchUp catchUp, CompendiumBook book, float seconds)
        {
            if (!Ready(player, catchUp, book, letGo: true)) return;
            if (Eye.Moved(V(_from), V(_eye), V(_forward))) Things.Wake();
            _sweepStarted = Stopwatch.GetTimestamp();
            Things.Look(seconds, LookAtThing, Spent);
        }

        private static Vec3 V(Vector3 v) => new Vec3(v.x, v.y, v.z);

        /// <summary>
        /// The most of a frame looking at things may take, as after a world or a stretch of it
        /// loads, when thousands are new; the rest wait for the frames after, a round then
        /// taking longer than a second.
        /// </summary>
        private const double FrameBudgetMs = 0.2;

        private static long _sweepStarted;
        private static readonly Func<bool> Spent = () => (Stopwatch.GetTimestamp() - _sweepStarted) * 1000.0 / Stopwatch.Frequency >= FrameBudgetMs;

        /// <summary>
        /// Looks at every thing kept once, into a book of its own, letting none go; how the
        /// self-test times a whole round, every thing in view looked through.
        /// </summary>
        public static void LookAtAll(Player player, CompendiumCatchUp catchUp, CompendiumBook book)
        {
            if (!Ready(player, catchUp, book, letGo: false)) return;
            Things.LookAtAll(LookAtThing);
        }

        // What a round looks with, set before each: the one delegate made once, so no frame makes one.
        private static readonly Func<ZNetView, Looked> LookAtThing = LookAt;
        private static CompendiumCatchUp _catchUp;
        private static CompendiumBook _book;
        private static bool _letGo;
        private static Vector3 _from;
        private static Vector3 _eye;
        private static Vector3 _forward;

        private static bool Ready(Player player, CompendiumCatchUp catchUp, CompendiumBook book, bool letGo)
        {
            if (player == null || catchUp == null || book == null) return false;
            Bind();
            var view = GameCamera.instance != null ? GameCamera.instance.transform : player.transform;
            _catchUp = catchUp;
            _book = book;
            _letGo = letGo;
            _from = player.transform.position;
            _eye = view.position;
            _forward = view.forward;
            return true;
        }

        /// <summary>
        /// One thing, by the rule: dropped when gone, a creature, showing nothing or not in the
        /// catalog; kept while out of reach; let go when seen before and paired with the biome and
        /// place it stands in; kept while out of view or hidden; seen and paired, then let go.
        /// One out of reach costs only where it stands, as most do most of the time. One kept that
        /// stands still rests (<see cref="Unchanged"/>).
        /// </summary>
        private static Looked LookAt(ZNetView thing)
        {
            var zdo = thing != null ? thing.GetZDO() : null;
            if (zdo == null) return Looked.Dropped;
            var shape = ShapeOf(zdo.GetPrefab(), thing);
            if (shape == null) return Looked.Dropped;
            if (!ReferenceEquals(shape.ListedBy, _catchUp))
            {
                shape.ListedBy = _catchUp;
                shape.Listed = _catchUp.Lists(shape.Key);
            }
            if (shape.Creature || !shape.Shows || !shape.Listed) return Looked.Dropped;
            var at = thing.transform.position;
            var middle = at + Vector3.up * shape.Height;
            var away = (middle - _from).sqrMagnitude;
            if (away > shape.Reach * shape.Reach)
            {
                var beyond = shape.Reach + Viewpoint.Step;
                return Unchanged(shape, away > beyond * beyond);
            }
            if (_letGo && _book.Level(shape.Key) >= KnownLevel.Seen && CompendiumKeeper.PairedIn(shape.Key, at)) return Looked.LetGo;
            var toward = middle - _eye;
            var angle = Vector3.Angle(_forward, toward);
            if (angle > HalfView)
            {
                // The eye's step turns the line to a near thing more than the view's own turn does.
                var near = toward.magnitude;
                return Unchanged(shape, near > Viewpoint.Step && angle > HalfView + Viewpoint.Turn + Mathf.Asin(Viewpoint.Step / near) * Mathf.Rad2Deg);
            }
            if (!Clear(_eye, middle, thing.transform)) return Unchanged(shape, true);
            _catchUp.Met(shape.Key, KnownLevel.Seen, _book);
            CompendiumKeeper.MetIn(shape.Key, at);
            // It stays where it is: seen and paired once, it is let go, so a farm of crops or a
            // forest in view costs nothing the next second.
            return _letGo ? Looked.LetGo : Looked.Kept;
        }

        /// <summary>
        /// A thing kept, not seen this time: resting where it stands still and no move short of
        /// one the viewpoint tells (<see cref="Viewpoint.Step"/>, <see cref="Viewpoint.Turn"/>) could
        /// bring it into reach or view, else kept to be looked at next round. One hidden rests
        /// too: what hides it stands still as well, as the ground, rock and buildings mostly do.
        /// </summary>
        private static Looked Unchanged(Shape shape, bool beyondAMove) => beyondAMove && !shape.Moves ? Looked.Rests : Looked.Kept;

        /// <summary>A kind of thing's shape, worked out from the first of it looked at; none for a prefab the game does not know.</summary>
        private static Shape ShapeOf(int prefab, ZNetView thing)
        {
            if (Shapes.TryGetValue(prefab, out var shape)) return shape;
            var source = GamePrefabs.Hashed(prefab);
            if (source == null) return null;
            shape = new Shape { Key = source.name, Creature = thing.GetComponent<Character>() != null, Moves = Moves(thing) };
            if (Measure(thing.transform, out var bounds))
            {
                var size = bounds.size;
                shape.Shows = true;
                shape.Reach = Sighting.Reach(Mathf.Max(size.y, Mathf.Max(size.x, size.z)));
                shape.Height = Mathf.Max(LeastHeight, bounds.center.y - thing.transform.position.y);
            }
            Shapes[prefab] = shape;
            return shape;
        }

        private static readonly List<Rigidbody> Bodies = new List<Rigidbody>();

        /// <summary>
        /// Whether a kind of thing can move: the game syncs a moving thing's place
        /// (<c>ZSyncTransform</c>: items on the ground, ships, carts, logs), physics moves a body
        /// in it (<c>Rigidbody</c>, not kinematic: fish, ragdolls), or it is a bird flying its
        /// rounds (<c>RandomFlyingBird</c>); else it stands where it is, as trees, rocks,
        /// buildings and pickables do.
        /// </summary>
        private static bool Moves(ZNetView thing)
        {
            if (thing.GetComponent<ZSyncTransform>() != null || thing.GetComponent<RandomFlyingBird>() != null) return true;
            thing.GetComponentsInChildren(true, Bodies);
            try
            {
                foreach (var body in Bodies) if (body != null && !body.isKinematic) return true;
                return false;
            }
            finally
            {
                Bodies.Clear();
            }
        }

        private static readonly List<MeshFilter> Meshes = new List<MeshFilter>();
        private static readonly List<SkinnedMeshRenderer> Skins = new List<SkinnedMeshRenderer>();

        /// <summary>
        /// What a thing's meshes take up in the world, those hidden now among them (a bush picked
        /// bare, a building's worn look), as each mesh's own box stands where it is; false when it
        /// has none, as a spawn point or a zone's keeper.
        /// </summary>
        private static bool Measure(Transform thing, out Bounds bounds)
        {
            bounds = default;
            var any = false;
            thing.GetComponentsInChildren(true, Meshes);
            thing.GetComponentsInChildren(true, Skins);
            try
            {
                foreach (var mesh in Meshes) if (mesh.sharedMesh != null) Grow(ref bounds, ref any, mesh.sharedMesh.bounds, mesh.transform);
                foreach (var skin in Skins) if (skin.sharedMesh != null) Grow(ref bounds, ref any, skin.sharedMesh.bounds, skin.transform);
            }
            finally
            {
                Meshes.Clear();
                Skins.Clear();
            }
            return any;
        }

        /// <summary>Grows bounds by the eight corners of a mesh's own box, where they stand in the world.</summary>
        private static void Grow(ref Bounds bounds, ref bool any, Bounds own, Transform at)
        {
            var toWorld = at.localToWorldMatrix;
            var c = own.center;
            var e = own.extents;
            for (var corner = 0; corner < 8; corner++)
            {
                var point = toWorld.MultiplyPoint3x4(c + new Vector3((corner & 1) == 0 ? -e.x : e.x, (corner & 2) == 0 ? -e.y : e.y, (corner & 4) == 0 ? -e.z : e.z));
                if (!IsNumber(point)) continue;
                if (any) bounds.Encapsulate(point);
                else bounds = new Bounds(point, Vector3.zero);
                any = true;
            }
        }

        /// <summary>Whether a point is made of numbers, as a mod's broken mesh may not be.</summary>
        private static bool IsNumber(Vector3 point) => !float.IsNaN(point.x + point.y + point.z) && !float.IsInfinity(point.x + point.y + point.z);

        private static bool InView(Transform view, Vector3 point) => Vector3.Angle(view.forward, point - view.position) <= HalfView;

        /// <summary>Whether any of the points a place is looked for at (<see cref="Sighting.PlacePoints"/>) is in sight from the eye (<see cref="Sighting.PlaceInSight"/>).</summary>
        private static bool InSight(Vector3 eye, Vector3 centre, float radius)
        {
            foreach (var (x, y, z) in Sighting.PlacePoints(radius))
            {
                var blocked = Physics.Linecast(eye, centre + new Vector3(x, y, z), out var hit, _solid);
                if (Sighting.PlaceInSight(blocked, hit.point.x - centre.x, hit.point.z - centre.z, radius)) return true;
            }
            return false;
        }

        /// <summary>
        /// Whether nothing solid (ground, rock, a building) stands between the eye and a point; what
        /// the line meets of the thing looked at itself does not hide it.
        /// </summary>
        private static bool Clear(Vector3 eye, Vector3 point, Transform thing) =>
            !Physics.Linecast(eye, point, out var hit, _solid) || (thing != null && hit.collider != null && hit.collider.transform.IsChildOf(thing));

        private static void Bind()
        {
            if (_container != null) return;
            _places = AccessTools.StaticFieldRefAccess<List<Location>>(AccessTools.Field(typeof(Location), "s_allLocations"));
            _solid = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            _container = AccessTools.FieldRefAccess<InventoryGui, Container>("m_currentContainer");
        }
    }
}
