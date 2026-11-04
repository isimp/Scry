using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What the character sees, for the compendium. A creature is seen once it is in the camera's
    /// view, nothing solid between, as far as its size makes it out (<see cref="Sighting.CreatureReach"/>,
    /// 30 m for one of a person's size or smaller), its stars, gear and attacks begun only up close,
    /// within the world's seen reach; any other thing within the world's seen reach of the eye by
    /// its nearest part (<see cref="Sighting.ThingReach"/>, <see cref="Sighting.NearestSq"/>), as
    /// the game names a thing where the eye's ray meets it, looked at on its upright at the eye's
    /// height; and each is paired with the biome and the place it stands in. Creatures are looked
    /// at once a second; everything else the game keeps in the world (trees, logs, rocks,
    /// deposits, buildings, pickables, nests, items on the ground) is kept as it wakes and looked
    /// at in rounds, a share each frame and all once a second (<see cref="Rounds{T}"/>), and let go
    /// once seen or once nothing more is to be learned of it there. One that stands still rests
    /// while out of reach, out of view or hidden by more than a move of the character or the eye
    /// could change, passed over without a look until either moves (<see cref="Viewpoint"/>), so
    /// standing still costs only what can move. A place is seen with its centre in view within its
    /// radius and a person's reach beyond, or as far as its height makes it out
    /// (<see cref="Sighting.PlaceReach"/>). A container opened shows what it holds, seen, and the
    /// container itself is known.
    /// </summary>
    internal static partial class CompendiumSight
    {
        static CompendiumSight() => WorldCaches.Register(nameof(CompendiumSight), () =>
        {
            Things.Clear();
            Shapes.Clear();
            // What the last round looked with, the book among it, let go of with the world.
            _catchUp = null;
            _book = null;
            _letGo = false;
            _from = _eye = _forward = Vector3.zero;
            _sweepStarted = 0;
            ForgetSpotsWorld();
            Bodies.Clear();
            CarriedNow.Clear();
            Meshes.Clear();
            Skins.Clear();
            Triggers.Clear();
            ForgetSwings();
            ForgetHeights();
        });

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
            public bool Regrows;
            public CompendiumCatchUp ListedBy;
            public bool Listed;

            /// <summary>Its box over and round where it stands: from its lowest to its highest part, and how far it reaches across the ground, for how near its nearest part is (<see cref="Sighting.NearestSq"/>).</summary>
            public float Bottom;
            public float Top;
            public float Radius;

            /// <summary>Its widest side, for how far it is seen for the map, and the kind it is kept by as a spot there (<see cref="SpotKinds"/>); none for one not worth finding again.</summary>
            public float Size;
            public string Spot;

            /// <summary>Whether its kind grows in gardens too, so one on tilled ground is no spot (<see cref="SpotKinds.GrowsInGardens"/>).</summary>
            public bool Garden;
        }

        /// <summary>How many things are kept to look at now, those resting among them.</summary>
        [ForTest]
        public static int ThingCount => Things.Count;

        /// <summary>How many of the things kept rest now, one by one; for the self-test.</summary>
        [ForTest]
        public static int ThingsResting => Things.Resting;

        /// <summary>How many things were seen, or had nothing more to tell, and were let go, in this world.</summary>
        [ForTest]
        public static int ThingsLetGo => Things.LetGo;

        /// <summary>How many things were dropped, gone or not to be looked at (a creature, one showing nothing, one the catalog lacks), in this world.</summary>
        [ForTest]
        public static int ThingsDropped => Things.Dropped;

        /// <summary>Whether a thing is kept to look at now.</summary>
        [ForTest]
        public static bool Keeps(ZNetView thing) => Things.Contains(thing);

        /// <summary>A thing the game keeps in the world woke: kept to be looked at, and, while attacks wait to be told, looked at as what one of them made (<see cref="SwingsPending.Woke"/>).</summary>
        public static void Woke(ZNetView thing)
        {
            if (thing == null || thing.GetZDO() == null) return;
            if (Swings.Count > 0) SwingMade(thing);
            Things.Add(thing);
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
                var away = Vector3.Distance(player.transform.position, centre);
                // Seen as far as its size makes it out; up close, within the world's seen reach, where
                // the game names a creature and marks its stars, its stars and gear are seen too.
                var upClose = away <= book.World.SeenReach;
                if (away <= Sighting.CreatureReach(creature.GetHeight(), book.World.SeenReach) && InView(view, centre) && Clear(view.position, centre, null))
                {
                    var key = Utils.GetPrefabName(creature.gameObject);
                    // Its level less one is its stars, as the game shows them over it, up close alone.
                    catchUp.Met(key, KnownLevel.Seen, book, stars: upClose ? creature.GetLevel() - 1 : -1);
                    CompendiumKeeper.MetIn(key, creature.transform.position);
                    // A creature the running raid brought, as the game marks the raid's own on every
                    // game (MonsterAI.IsEventCreature), was seen in the raid; one of its kind the
                    // world spawned meanwhile was not.
                    if (CompendiumKeeper.RaidBrings(key) && creature.GetBaseAI() is MonsterAI raider && raider.IsEventCreature())
                    {
                        CompendiumKeeper.Saw(CompendiumKeeper.RaidNow, key);
                        // One of its raiders up close shows what the raid brings of its kind, and how the raid is rolled.
                        if (upClose) CompendiumKeeper.RaiderSeen(key);
                    }
                    // A boss seen beside its altar goes with it, whoever called it.
                    if (creature.IsBoss()) CompendiumKeeper.BossSeen(creature);
                    // Up close in a place, one its spawn points place shows the stars they give.
                    if (upClose) CompendiumKeeper.SeenInPlace(key, creature.transform.position);
                    // Near enough for the game to name it and mark its stars, it is seen up close: its stars and what it carries are kept.
                    if (upClose && catchUp.Lists(key)) CompendiumKeeper.Sighted(key, creature.GetLevel() - 1, Carried(creature));
                }
            }
            foreach (var place in _places())
            {
                if (place == null) continue;
                var centre = place.transform.position;
                // Within its radius and a person's reach beyond, or as far as its height makes it out.
                var height = PlaceHeight(place);
                if (Vector3.Distance(player.transform.position, centre) <= Sighting.PlaceReach(place.m_exteriorRadius, height) && InView(view, centre) && InSight(view.position, centre, place.m_exteriorRadius, height))
                {
                    var key = EntryKeys.For(Kind.Location, Utils.GetPrefabName(place.gameObject));
                    catchUp.Met(key, KnownLevel.Seen, book);
                    // Seen in its biome, as a creature or a thing is where it stands.
                    CompendiumKeeper.MetInBiome(key, centre);
                    // Its spot kept for the map, where the world lets the map keep any.
                    if (Settings.SeenOnMapReach > 0f) SeenSpotsKeeper.Saw(key, centre.x, centre.z);
                }
            }
            var gui = InventoryGui.instance;
            var container = gui != null && gui.IsContainerOpen() ? _container(gui) : null;
            if (container == null) return;
            var chest = Utils.GetPrefabName(container.gameObject);
            catchUp.Met(chest, KnownLevel.Known, book, CameBy.Used);
            CompendiumKeeper.MetIn(chest, container.transform.position);
            // What a chest holds is seen in it. What the world put there is paired with the chest and
            // its place as the chest is first opened and counted (CompendiumKeeper.Opened), not by
            // whatever lies in it later, which someone may have put there.
            foreach (var held in container.GetInventory().GetAllItems())
            {
                if (held?.m_dropPrefab != null) catchUp.Met(held.m_dropPrefab.name, KnownLevel.Seen, book);
            }
        }

        /// <summary>Each creature prefab's attack items by the triggers that start them (<see cref="AttackTriggers"/>), worked out once each.</summary>
        private static readonly Dictionary<string, Dictionary<string, string[]>> Triggers = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.Ordinal);

        /// <summary>
        /// A creature's attack begun, as its animation's trigger reaches every player's game
        /// (<c>ZSyncAnimation.RPC_SetTrigger</c>): seen up close, within the world's seen reach (a
        /// boss's as much farther as its health bar shows, any as far as its creature uses it
        /// from, <see cref="Sighting.AttackReach"/>), in
        /// view with nothing solid between, the attack is found out, as taking its hit finds it
        /// out (<see cref="CompendiumKeeper.HitBy"/>). A trigger several of its weapons share is
        /// told by the weapon in its hand: at once on the game running the creature, else once the
        /// creature's data caught up or by what the attack put into the world beside it
        /// (<see cref="SwingsPending"/>). A player's own swings, a trigger that is no attack's, and
        /// a creature whose book is not open give nothing.
        /// </summary>
        public static void SwingSeen(Character creature, string trigger)
        {
            var player = Player.m_localPlayer;
            var book = CompendiumKeeper.Book;
            if (creature == null || player == null || book == null || creature.IsPlayer() || string.IsNullOrEmpty(trigger)) return;
            var key = Utils.GetPrefabName(creature.gameObject);
            var candidates = AttackTriggers.Candidates(TriggersOf(key), trigger);
            if (candidates.Count == 0) return;
            var centre = creature.GetCenterPoint();
            var away = Vector3.Distance(player.transform.position, centre);
            var boss = creature.IsBoss();
            var farthest = 0f;
            foreach (var each in candidates) farthest = Mathf.Max(farthest, AttackReachOf(each, boss, book));
            if (away > farthest) return;
            Bind();
            var view = GameCamera.instance != null ? GameCamera.instance.transform : player.transform;
            if (!InView(view, centre) || !Clear(view.position, centre, creature.transform)) return;
            if (candidates.Count == 1)
            {
                SwingTold(key, candidates[0], away, boss);
                return;
            }
            var net = creature.GetComponent<ZNetView>().OrNull();
            var hand = CompendiumKeeper.WeaponOf(creature as Humanoid).OrNull()?.name;
            // The game running it attacks with the very weapon in its hand; another game waits for its data.
            if (net == null || !net.IsValid() || net.IsOwner()) SwingTold(key, AttackTriggers.Resolve(candidates, hand), away, boss);
            else BeginSwing(creature, key, candidates, away);
        }

        /// <summary>How far an attack item's attack is seen begun (<see cref="Sighting.AttackReach"/>), by its own AI range.</summary>
        private static float AttackReachOf(string item, bool boss, CompendiumBook book)
        {
            var hud = EnemyHud.instance;
            var range = GamePrefabs.Named(item).OrNull()?.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared?.m_aiAttackRange ?? 0f;
            return Sighting.AttackReach(boss, book.World.SeenReach, hud != null ? hud.m_maxShowDistance : 0f, hud != null ? hud.m_maxShowDistanceBoss : 0f, range);
        }

        /// <summary>An attack told: found out, where it was begun within its own reach.</summary>
        private static void SwingTold(string creature, string item, float away, bool boss)
        {
            var book = CompendiumKeeper.Book;
            if (item == null || book == null || away > AttackReachOf(item, boss, book)) return;
            CompendiumKeeper.FoundOut(creature, EntryKeys.Attack(item));
        }

        /// <summary>
        /// A creature prefab's weapons by trigger, from the attacks of what it carries, kept once
        /// worked out. Only weapons take part, as the game attacks with nothing else
        /// (<c>Humanoid.EquipBestWeapon</c> skips an item for which <c>IsWeapon</c> is false).
        /// </summary>
        private static Dictionary<string, string[]> TriggersOf(string prefabName)
        {
            if (Triggers.TryGetValue(prefabName, out var map)) return map;
            var attacks = new List<(string, string, int, bool)>();
            var prefab = GamePrefabs.Named(prefabName);
            if (prefab != null)
            {
                foreach (var item in Relations.CarriedItems(prefab))
                {
                    var data = item.GetComponent<ItemDrop>().OrNull()?.m_itemData;
                    var shared = data?.m_shared;
                    if (shared == null) continue;
                    var weapon = data.IsWeapon();
                    if (shared.m_attack != null) attacks.Add((item.name, shared.m_attack.m_attackAnimation, shared.m_attack.m_attackChainLevels, weapon));
                    if (shared.m_secondaryAttack != null) attacks.Add((item.name, shared.m_secondaryAttack.m_attackAnimation, shared.m_secondaryAttack.m_attackChainLevels, weapon));
                }
            }
            return Triggers[prefabName] = AttackTriggers.Map(attacks);
        }

        /// <summary>
        /// The slots of what a creature wears that every client sees, each kept as the item's hash
        /// in the creature's data and drawn from it (<c>VisEquipment.UpdateEquipmentVisuals</c>):
        /// the hands, chest, legs, head, shoulders, utility and trinket. A Dvergr mage's staff is
        /// its utility item.
        /// </summary>
        private static readonly int[] WornSlots = { ZDOVars.s_rightItem, ZDOVars.s_leftItem, ZDOVars.s_chestItem, ZDOVars.s_legItem, ZDOVars.s_helmetItem, ZDOVars.s_shoulderItem, ZDOVars.s_utilityItem, ZDOVars.s_trinketItem };

        private static readonly List<string> CarriedNow = new List<string>();

        /// <summary>
        /// What a creature carries, by the prefabs of the items in its hands and on its body, as
        /// the game shows them on it to every player (<c>VisEquipment</c>); none for one with
        /// nothing on, or whose data is not loaded. The list is reused: read it before the next look.
        /// </summary>
        private static List<string> Carried(Character creature)
        {
            var zdo = creature.GetComponent<ZNetView>().OrNull()?.GetZDO();
            return CarriedIn(zdo == null ? (Func<int, int>)null : hash => zdo.GetInt(hash));
        }

        /// <summary>What a creature's data, read slot by slot, says it carries (<see cref="Carried"/>), for the self-test to hand in a creature's slots.</summary>
        [ForTest]
        public static List<string> CarriedForTest(Func<int, int> slot) => new List<string>(CarriedIn(slot));

        /// <summary>The items a creature's data names in its worn slots, read by <paramref name="slot"/>; none without data.</summary>
        private static List<string> CarriedIn(Func<int, int> slot)
        {
            CarriedNow.Clear();
            if (slot == null) return CarriedNow;
            foreach (var worn in WornSlots)
            {
                var hash = slot(worn);
                if (hash == 0) continue;
                var item = GamePrefabs.Hashed(hash);
                if (item != null && !CarriedNow.Contains(item.name)) CarriedNow.Add(item.name);
            }
            return CarriedNow;
        }

        /// <summary>
        /// Looks at the share of the things kept that a frame of so many seconds is due, within
        /// <see cref="FrameBudgetMs"/> of the frame, the things resting woken first where the
        /// character or the eye moved; called each frame.
        /// </summary>
        public static void Sweep(Player player, CompendiumCatchUp catchUp, CompendiumBook book, float seconds)
        {
            if (!Ready(player, catchUp, book, letGo: true)) return;
            if (Swings.Count > 0) TickSwings();
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
        [ForTest]
        public static void LookAtAll(Player player, CompendiumCatchUp catchUp, CompendiumBook book)
        {
            if (!Ready(player, catchUp, book, letGo: false)) return;
            Things.LookAtAll(LookAtThing);
        }

        // What a round looks with, set before each: the one delegate made once, so no frame makes one.
        private static readonly Func<ZNetView, Looked> LookAtThing = LookAt;
        private static readonly string GrewBack = EntryKeys.Did(Deed.GrewBack);
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
            // Within its size's reach and the world's seen reach of the eye, by its nearest part, as
            // the game names a thing where the eye's ray meets it, not by its middle, which a tree
            // carries far overhead; one worth finding again whose spot is not kept yet, within the
            // map's reach too, by its middle.
            var foot = V(at);
            var nearest = Sighting.NearestSq(V(_eye), foot, shape.Bottom, shape.Top, shape.Radius);
            var reach = Sighting.ThingReach(shape.Reach, _book.World.SeenReach);
            var spotReach = SpotsWanted(shape) ? SeenOnMap.Reach(shape.Size, Settings.SeenOnMapReach) : 0f;
            if (nearest > reach * reach && away > spotReach * spotReach)
            {
                var beyond = reach + Viewpoint.Step;
                var beyondSpot = spotReach + Viewpoint.Step;
                return Unchanged(shape, nearest > beyond * beyond && away > beyondSpot * beyondSpot);
            }
            var spot = spotReach > 0f ? SpotToKeep(shape, at) : null;
            // A pickable whose growing back is not yet found out is kept, as the usual way to meet
            // it is coming back to one bearing again (DeedWatch.SeenBearingAgain).
            var regrowth = shape.Regrows && !_book.Proved(shape.Key, GrewBack);
            // Seen and paired before: let go, or, while its spot is to be kept, only the map's line looked along.
            var known = _letGo && !regrowth && _book.Level(shape.Key) >= KnownLevel.Seen && CompendiumKeeper.PairedIn(shape.Key, at);
            if (known && spot == null) return Looked.LetGo;
            // Looked at on its upright at the eye's height, within its own height.
            var aimed = Sighting.Aim(V(_eye), foot, shape.Bottom, shape.Top);
            var aim = new Vector3(aimed.X, aimed.Y, aimed.Z);
            var toward = aim - _eye;
            var angle = Vector3.Angle(_forward, toward);
            if (angle > HalfView)
            {
                // The eye's step turns the line to a near thing more than the view's own turn does.
                var near = toward.magnitude;
                return Unchanged(shape, near > Viewpoint.Step && angle > HalfView + Viewpoint.Turn + Mathf.Asin(Viewpoint.Step / near) * Mathf.Rad2Deg);
            }
            // The map keeps its spot where its own line reaches it, leaves stopping it as they stop a creature's eye.
            var spotted = spot != null && away <= spotReach * spotReach && SpotInSight(_eye, middle, thing.transform) && SeenSpotsKeeper.Saw(spot, at.x, at.z);
            if (known) return spotted ? Looked.LetGo : SpotUnseen(shape, away, spotReach);
            if (nearest > reach * reach || !Clear(_eye, aim, thing.transform)) return Unchanged(shape, true);
            _catchUp.Met(shape.Key, KnownLevel.Seen, _book);
            CompendiumKeeper.MetIn(shape.Key, at);
            if (regrowth) DeedWatch.SeenBearingAgain(thing.GetComponent<Pickable>());
            // It stays where it is: seen and paired once, and its spot kept where it is worth finding
            // again, it is let go, so a farm of crops or a forest in view costs nothing the next second.
            if (!_letGo || regrowth) return Looked.Kept;
            return spot == null || spotted ? Looked.LetGo : SpotUnseen(shape, away, spotReach);
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
            var pickable = thing.GetComponent<Pickable>();
            shape = new Shape { Key = source.name, Creature = thing.GetComponent<Character>() != null, Moves = Moves(thing), Regrows = pickable != null && pickable.m_respawnTimeMinutes > 0f };
            if (Measure(thing.transform, out var bounds))
            {
                var size = bounds.size;
                shape.Shows = true;
                shape.Size = Mathf.Max(size.y, Mathf.Max(size.x, size.z));
                shape.Reach = Sighting.Reach(shape.Size);
                shape.Spot = SpotKinds.Of(prefab);
                shape.Garden = shape.Spot != null && SpotKinds.GrowsInGardens(prefab);
                var foot = thing.transform.position;
                shape.Height = Mathf.Max(LeastHeight, bounds.center.y - foot.y);
                shape.Bottom = bounds.min.y - foot.y;
                shape.Top = Mathf.Max(shape.Bottom + LeastHeight, bounds.max.y - foot.y);
                shape.Radius = Mathf.Max(Mathf.Abs(bounds.center.x - foot.x) + bounds.extents.x, Mathf.Abs(bounds.center.z - foot.z) + bounds.extents.z);
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
        private static bool InSight(Vector3 eye, Vector3 centre, float radius, float height)
        {
            foreach (var (x, y, z) in Sighting.PlacePoints(radius, height))
            {
                var blocked = Physics.Linecast(eye, centre + new Vector3(x, y, z), out var hit, _solid);
                if (Sighting.PlaceInSight(blocked, hit.point.x - centre.x, hit.point.z - centre.z, radius)) return true;
            }
            return false;
        }

        /// <summary>
        /// Whether a deed done where it says is in sight of the eye: a line to a creature's middle
        /// over it meets nothing solid, or stops within a step of it, on what the deed was done to
        /// (<see cref="Sighting.DeedInSight"/>). In sight wherever there is no eye to look from.
        /// </summary>
        public static bool SeesDeed(float x, float y, float z)
        {
            var player = Player.m_localPlayer;
            if (player == null) return true;
            Bind();
            var eye = (GameCamera.instance != null ? GameCamera.instance.transform : player.transform).position;
            var point = new Vector3(x, y + Sighting.DeedHeight, z);
            var blocked = Physics.Linecast(eye, point, out var hit, _solid);
            return Sighting.DeedInSight(blocked, blocked ? Vector3.Distance(hit.point, point) : 0f);
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
