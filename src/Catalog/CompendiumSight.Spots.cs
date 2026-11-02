using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The sight's part for the map: where a thing worth finding again stands, kept as a spot
    /// (<see cref="SeenSpotsKeeper"/>) once seen within the map's reach, along a line leaves stop
    /// as well as the ground and rock; and a spot taken away as its thing is destroyed, unless it
    /// only becomes its broken form where it stands (<see cref="SpotKinds.Replaced"/>).
    /// </summary>
    internal static partial class CompendiumSight
    {
        /// <summary>The layers the map's line of sight stops at: those the book's does (<c>_solid</c>), and leaves (viewblock), as a creature's eye stops at them (<c>BaseAI.m_viewBlockMask</c>).</summary>
        private static int _spotSolid = -1;

        /// <summary>The game's keeper of things, once watched for things destroyed.</summary>
        private static ZDOMan _watchedGone;

        /// <summary>The world the spots are kept by, and the game's number for it, worked out again only as that number changes, so a look makes no text.</summary>
        private static string _spotWorld;
        private static long _spotWorldId;

        /// <summary>Whether a thing is of a kind whose spot is kept at all: worth finding again, recording on, the world letting the map keep spots, and the character's spots readable.</summary>
        private static bool SpotsWanted(Shape shape) =>
            shape.Spot != null && Settings.RecordSeenSpots && Settings.SeenOnMapReach > 0f && !SeenSpotsKeeper.Unreadable;

        /// <summary>
        /// The kind a thing whose spot is wanted (<see cref="SpotsWanted"/>) would be kept by where
        /// it stands, while none is kept there yet and the world keeps fewer than its most; none
        /// otherwise, so the thing is let go as any other once seen. Before the character's spots
        /// are read, the kind still, so the thing is held to be looked at once they are.
        /// </summary>
        private static string SpotToKeep(Shape shape, Vector3 at)
        {
            if (!SeenSpotsKeeper.Ready) return shape.Spot;
            var world = SpotWorld();
            var spots = SeenSpotsKeeper.Spots;
            return world == null || spots.CountIn(world) >= SeenSpots.MostAWorld || spots.Has(world, shape.Spot, at.x, at.z) ? null : shape.Spot;
        }

        /// <summary>
        /// A thing seen whose spot is still to be kept, the map's line not reaching it from here:
        /// kept to be looked at each round while the character's spots are being read; else resting
        /// until the eye moves, one just beyond the map's reach until a move could bring it within.
        /// </summary>
        private static Looked SpotUnseen(Shape shape, float away, float spotReach)
        {
            if (!SeenSpotsKeeper.Ready) return Looked.Kept;
            var beyond = spotReach + Viewpoint.Step;
            return Unchanged(shape, away <= spotReach * spotReach || away > beyond * beyond);
        }

        /// <summary>The world the spots are kept by (<see cref="CompendiumMap.World"/>); none outside a world.</summary>
        private static string SpotWorld()
        {
            var net = ZNet.instance;
            if (net == null || net.GetWorld() == null) return null;
            var id = net.GetWorldUID();
            if (_spotWorld == null || id != _spotWorldId)
            {
                _spotWorldId = id;
                _spotWorld = CompendiumMap.World;
            }
            return _spotWorld;
        }

        /// <summary>
        /// The things worth finding again loaded now, those let go among them looked at again, as
        /// after this world's spots are forgotten: each let go once its spot was kept is looked
        /// for anew without walking away and back.
        /// </summary>
        public static void LookAgainForSpots()
        {
            if (ZNetScene.instance == null) return;
            foreach (var thing in Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None))
            {
                var zdo = thing.GetZDO();
                if (zdo != null && SpotKinds.Of(zdo.GetPrefab()) != null && !Things.Contains(thing)) Woke(thing);
            }
        }

        /// <summary>Whether the map's line of sight reaches a point: nothing solid and no leaves in between, what the line meets of the thing itself aside.</summary>
        private static bool SpotInSight(Vector3 eye, Vector3 point, Transform thing)
        {
            if (_spotSolid < 0) _spotSolid = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "viewblock");
            return !Physics.Linecast(eye, point, out var hit, _spotSolid) || (thing != null && hit.collider != null && hit.collider.transform.IsChildOf(thing));
        }

        /// <summary>Watches the game's keeper of things for things destroyed, once a world (<see cref="SeenSpotsKeeper.Update"/>).</summary>
        public static void WatchGone()
        {
            var keeper = ZDOMan.instance;
            if (keeper == null || ReferenceEquals(keeper, _watchedGone)) return;
            _watchedGone = keeper;
            keeper.m_onZDODestroyed += Gone;
        }

        /// <summary>Stops watching the game's keeper of things and forgets the world the spots were kept by, as the world is left.</summary>
        private static void ForgetSpotsWorld()
        {
            if (_watchedGone != null) _watchedGone.m_onZDODestroyed -= Gone;
            _watchedGone = null;
            _spotWorld = null;
            _spotWorldId = 0;
        }

        /// <summary>
        /// A thing destroyed: its spot taken away where it is worth finding again and does not only
        /// become its broken form. Called for everything the game destroys, an arrow or an effect
        /// as well, so guarded without a new delegate a call, and what is not worth finding again
        /// passed over by a look in a table.
        /// </summary>
        private static void Gone(ZDO zdo) => Guard.Run(Feature.SeenSpots, "forgetting a thing gone", ForgetGone, zdo);

        private static readonly System.Action<ZDO> ForgetGone = zdo =>
        {
            var prefab = zdo.GetPrefab();
            var kind = SpotKinds.Of(prefab);
            if (kind == null || SpotKinds.Replaced(prefab)) return;
            var at = zdo.GetPosition();
            SeenSpotsKeeper.Gone(kind, at.x, at.z);
        };
    }
}
