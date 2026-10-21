using UnityEngine;

namespace Scry
{
    internal static partial class Stage
    {
        /// <summary>How far the camera looks down while a dungeon's example is cut from the side: a little, so the cut's face is seen.</summary>
        private const float SidePitch = 20f;

        /// <summary>
        /// Whether a dungeon's example is cut from the side (<see cref="SideCut"/>): an upright
        /// plane through the room gone into's middle, else the example's, takes away the half of
        /// every room facing the camera. On by itself for an example that is mostly one stack of
        /// rooms (<see cref="DungeonWays.IsStack"/>), as Mörkhalla's.
        /// </summary>
        public static bool CutFromSide { get; private set; }

        /// <summary>Whether creatures are drawn again over the cut, as only a cut from above has them, for the self-test.</summary>
        [Diagnostic, ForTest]
        public static bool CreaturesRedrawnOverCut => _aboveCut != null;

        /// <summary>Whether the side cut is laid now: chosen, with a dungeon's example shown inside.</summary>
        public static bool SideCutting => CutFromSide && ExampleInside;

        /// <summary>
        /// Cuts a dungeon's example from the side or from above. A room gone into stays gone
        /// into: from the side it is cut through its middle with nothing put away, from above at
        /// head height over the level open.
        /// </summary>
        public static void SetCutFromSide(bool side)
        {
            CutFromSide = side;
            TheExample.FromSide = side;
            var room = TheExample.Focused;
            if (room != null) HoldRoomCut(room, TheExample.FocusLevel);
            if (ExampleInside) TurnTo(Yaw, side ? SidePitch : CutPitch);
        }

        /// <summary>
        /// Cuts as a part of the stage's cut control asks (<see cref="CutControl"/>): from the side;
        /// from above, taking the roof off; or closed, putting it on; in a room gone into, its own
        /// roof, the room still gone into.
        /// </summary>
        public static void ChooseCut(CutMode mode)
        {
            var side = mode == CutMode.Side;
            if (CutFromSide != side) SetCutFromSide(side);
            if (side) return;
            var closed = mode == CutMode.Closed;
            if (TheExample.Focused != null)
            {
                if (TheCut.HeldRoofOn != closed) TheCut.ToggleRoof();
                return;
            }
            if (TheCut.Cutting == closed) ToggleRoof();
        }

        /// <summary>Starts a dungeon's example cut from the side where it is mostly one stack of rooms, else from above.</summary>
        private static void ChooseCutForExample()
        {
            CutFromSide = TheExample.Ways != null && TheExample.Ways.IsStack;
            TheExample.FromSide = CutFromSide;
        }

        /// <summary>The point the side cut goes through, in the world: the room gone into's middle, else the middle of the example's rooms; none without an example.</summary>
        private static Vector3? SideCutThrough()
        {
            var holder = TheExample.Holder;
            var placed = TheExample.Placed;
            if (holder == null || placed == null || placed.Rooms.Count == 0) return null;
            var room = TheExample.Focused;
            if (room != null) return holder.transform.TransformPoint(new Vector3(room.Position.X, room.Position.Y, room.Position.Z));
            float x = 0f, y = 0f, z = 0f;
            foreach (var r in placed.Rooms)
            {
                x += r.Position.X;
                y += r.Position.Y;
                z += r.Position.Z;
            }
            var n = placed.Rooms.Count;
            return holder.transform.TransformPoint(new Vector3(x / n, y / n, z / n));
        }

        /// <summary>The way the side cut faces in the world: away from the camera, level with the ground.</summary>
        private static Vector3 SideCutFacing()
        {
            var eye = _camera.transform;
            var facing = SideCut.Facing(new Vec3(eye.forward.x, eye.forward.y, eye.forward.z), new Vec3(eye.up.x, eye.up.y, eye.up.z));
            return new Vector3(facing.X, 0f, facing.Z);
        }

        /// <summary>
        /// Lays the camera's near plane along the side cut, as <see cref="ApplyCut"/> does along a
        /// floor's: what is between the camera and the cut is not drawn. Creatures are cut with
        /// the rest, as those in the half taken away would stand before the cut's face.
        /// </summary>
        private static void ApplySideCut(Vector3 through)
        {
            var eye = _camera.transform.position;
            var facing = SideCutFacing();
            if (Vector3.Dot(eye - through, facing) >= -0.05f) return;
            _camera.farClipPlane *= 4f;
            var toCamera = _camera.worldToCameraMatrix;
            var point = toCamera.MultiplyPoint(through);
            var normal = toCamera.MultiplyVector(facing).normalized;
            var plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal));
            _camera.projectionMatrix = _camera.CalculateObliqueMatrix(plane);
        }

        /// <summary>Where a pick ray starts in the example's own space: at the side cut where it is laid, as nothing between the camera and the cut is drawn.</summary>
        private static Vector3 PickFrom(Transform holder, Vector3 from, Vector3 direction)
        {
            // Under a sloping cut, from where the ray crosses the slope.
            if (SlopeLaid(out var slope))
            {
                var low = new Vector3(slope.Low.X, slope.Low.Y, slope.Low.Z);
                var rise = new Vector3(slope.High.X, slope.High.Y, slope.High.Z) - low;
                var up = Vector3.Cross(new Vector3(-rise.z, 0f, rise.x), rise).normalized;
                var across = Vector3.Dot(direction, up);
                if (Mathf.Abs(across) < 1e-5f) return from;
                var crossing = Vector3.Dot(low - from, up) / across;
                return crossing > 0f ? from + direction * crossing : from;
            }
            if (!SideCutting || !(SideCutThrough() is Vector3 through)) return from;
            var at = holder.InverseTransformPoint(through);
            var facing = holder.InverseTransformDirection(SideCutFacing());
            var along = Vector3.Dot(direction, facing);
            if (Mathf.Abs(along) < 1e-5f) return from;
            var t = Vector3.Dot(at - from, facing) / along;
            return t > 0f ? from + direction * t : from;
        }
    }
}
