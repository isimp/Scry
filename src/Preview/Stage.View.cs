using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>Moving the stage's view by hand: turning it, zooming toward a point, dragging it along, and what is under a point of its picture.</summary>
    internal static partial class Stage
    {
        public static float Yaw { get; private set; } = FrontYaw;
        public static float Pitch { get; private set; } = FrontPitch;
        public static float Zoom { get; private set; } = 1f;

        /// <summary>How far the view is moved off what it frames, by dragging with the right button or zooming toward the pointer.</summary>
        private static Vector3 _pan;

        /// <summary>
        /// Moves the view as the mouse drags it with the right button, from one point of the
        /// picture to another (0 to 1 across, 0 to 1 up): what was under the pointer stays under
        /// it, on the floor looked at where one is opened (<see cref="StageCamera.DragOnLevel"/>),
        /// else as far off as what the camera looks at (<see cref="StageCamera.Drag"/>).
        /// </summary>
        public static void Pan(Vector2 from, Vector2 to)
        {
            if (_camera == null) return;
            var t = _camera.transform;
            if (_lookedFloor is float floor)
            {
                var along = StageCamera.DragOnLevel(V(t.position), V(RayAt(from)), V(RayAt(to)), floor, Farthest);
                if (along != null)
                {
                    _pan += new Vector3(along.Value.X, 0f, along.Value.Z);
                    return;
                }
            }
            var (right, up) = StageCamera.Drag(to.x - from.x, to.y - from.y, Vector3.Distance(t.position, _lookAt), FieldOfView, _camera.aspect);
            var move = t.right * right + t.up * up;
            _pan += _lookedFloor != null ? new Vector3(move.x, 0f, move.z) : move;
        }

        /// <summary>
        /// What is under a point of the picture (0 to 1 across, 0 to 1 up), which the wheel zooms
        /// toward: on the floor looked at where one is opened, else on the plane through what the
        /// camera looks at, facing it. Null before the camera has filmed.
        /// </summary>
        public static Vector3? PointUnder(Vector2 point)
        {
            if (_camera == null) return null;
            var eye = V(_camera.transform.position);
            var ray = V(RayAt(point));
            var on = _lookedFloor is float floor ? StageCamera.OnLevel(eye, ray, floor, Farthest) : null;
            on = on ?? StageCamera.OnFacing(eye, ray, V(_lookAt), V(_camera.transform.forward));
            return on is Vec3 at ? U(at) : (Vector3?)null;
        }

        /// <summary>Where a point of the stage's picture (0 to 1 across, 0 to 1 up) shows, for the self-test; null behind the camera.</summary>
        public static Vector2? PictureOf(Vector3 world)
        {
            if (_camera == null) return null;
            var shown = StageCamera.PictureOf(V(_camera.transform.InverseTransformPoint(world)), FieldOfView, _camera.aspect);
            return shown.HasValue ? new Vector2(shown.Value.X, shown.Value.Y) : (Vector2?)null;
        }

        /// <summary>The way from the camera through a point of its picture (0 to 1 across, 0 to 1 up).</summary>
        private static Vector3 RayAt(Vector2 point) => _camera.transform.rotation * U(StageCamera.Through(point.x, point.y, FieldOfView, _camera.aspect));

        public static void Orbit(Vector2 delta) => (Yaw, Pitch) = StageCamera.Orbit(Yaw, Pitch, delta.x, delta.y);

        /// <summary>
        /// Zooms by the wheel toward what is under the pointer at a point of the picture (0 to 1
        /// across, 0 to 1 up), which stays under it (<see cref="StageCamera.ZoomToward"/>); in as
        /// far as a couple of metres however big what is framed (<see cref="StageCamera.LeastZoom"/>).
        /// </summary>
        public static void ZoomBy(float wheel, Vector2? point = null)
        {
            var before = Zoom;
            // No nearer than keeps it over a floor's cut.
            Zoom = StageCamera.Zoomed(Zoom, wheel, StageFraming.FramedDistance(_frameRadius, FieldOfView), OverCutDistance());
            if (point == null || _camera == null || before <= 0f || Mathf.Approximately(Zoom, before)) return;
            if (!(PointUnder(point.Value) is Vector3 toward)) return;

            var factor = Zoom / before;
            var move = U(StageCamera.ZoomToward(V(_lookAt), V(toward), factor)) - _lookAt;
            if (_lookedFloor != null) move.y = 0f;
            _pan += move;
            MoveLook(move, factor);
        }

        public static void ResetView()
        {
            Yaw = FrontYaw;
            Pitch = Cutting ? CutPitch : FrontPitch;
            Zoom = 1f;
            FrameAnew();
            CenterView();
        }

        /// <summary>Turns the camera to look from these angles, as spinning, opening a floor or the self-test does.</summary>
        public static void TurnTo(float yaw, float pitch)
        {
            Yaw = yaw;
            Pitch = pitch;
        }

        /// <summary>Takes the view back onto what it frames, undoing a drag or a zoom toward the pointer.</summary>
        private static void CenterView() => _pan = Vector3.zero;

        /// <summary>Turns the camera to a view: "Front", "Side", "Top", or "Fit" to frame it whole again.</summary>
        public static void View(string name)
        {
            switch (name)
            {
                case "Front":
                    Yaw = 180f;
                    Pitch = 8f;
                    break;
                case "Side":
                    Yaw = 90f;
                    Pitch = 8f;
                    break;
                case "Top":
                    Pitch = 85f;
                    break;
            }
            Zoom = 1f;
            _pan = Vector3.zero;
        }
    }
}
