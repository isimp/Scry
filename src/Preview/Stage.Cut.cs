using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A location or room cut open to look into: everything above head height over one of its
    /// floors is cut away (<see cref="PlaceView"/>). The cut is the camera's near plane laid along
    /// that height (an oblique projection), so it opens any model whatever its meshes are made
    /// of. A room opens on its top floor when first shown, a location keeps its roof; the stage's
    /// chip steps down through the floors and puts the roof back, and Shift with the wheel moves
    /// the cut up or down. Looked at from below the cut, nothing is cut.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>The floors of the place shown, from the top down, above its root as if at size one; none for anything else.</summary>
        private static readonly List<float> Floors = new List<float>();

        /// <summary>Which floor is opened; one past the last for none.</summary>
        private static int _cutLevel;

        /// <summary>How far the cut has been moved from its floor's height, in metres.</summary>
        private static float _cutShift;

        /// <summary>The entry the floors are of, so a new copy of the same keeps its cut.</summary>
        private static Entry _cutFor;

        /// <summary>How steeply the camera looks down on a place opened, to see in over its walls.</summary>
        private const float CutPitch = 40f;

        /// <summary>Whether the model shown can be cut open.</summary>
        public static bool HasFloors => Floors.Count > 0;

        /// <summary>Whether it is cut open now.</summary>
        public static bool Cutting => _cutLevel < Floors.Count;

        /// <summary>What the cut's chip says.</summary>
        public static string CutLabel => PlaceView.CutLabel(_cutLevel, Floors.Count);

        /// <summary>Where it is cut, above its root as if at size one, for the self-test.</summary>
        public static float CutAt => Cutting ? PlaceView.CutHeight(Floors[_cutLevel]) + _cutShift : float.PositiveInfinity;

        /// <summary>Opens the next floor down, or puts the roof back after the lowest.</summary>
        public static void NextCut()
        {
            if (Floors.Count == 0) return;
            _cutLevel = (_cutLevel + 1) % (Floors.Count + 1);
            _cutShift = 0f;
            if (Cutting && Pitch < CutPitch / 2f) Pitch = CutPitch;
        }

        /// <summary>Moves the cut up or down.</summary>
        public static void CutBy(float metres)
        {
            if (Cutting) _cutShift += metres;
        }

        /// <summary>
        /// Takes the floors of the place shown. The first time for an entry, a room is opened on
        /// its top floor and looked down into, and a location keeps its roof; a new copy of the
        /// same (rolled again, or loaded) keeps the cut it had.
        /// </summary>
        private static void SetFloors(Entry entry, IEnumerable<float> floors, bool open)
        {
            Floors.Clear();
            if (floors != null) Floors.AddRange(floors);
            if (ReferenceEquals(_cutFor, entry))
            {
                _cutLevel = Mathf.Min(_cutLevel, Floors.Count);
                return;
            }
            _cutFor = entry;
            _cutShift = 0f;
            _cutLevel = open ? 0 : Floors.Count;
            if (Cutting) Pitch = CutPitch;
        }

        private static void ClearFloors()
        {
            Floors.Clear();
            _cutFor = null;
            _cutLevel = 0;
            _cutShift = 0f;
        }

        /// <summary>
        /// Lays the camera's near plane along the cut, once the camera is placed for the frame,
        /// when the camera is above it; otherwise the camera's own projection stands.
        /// </summary>
        private static void ApplyCut()
        {
            _camera.ResetProjectionMatrix();
            if (!Cutting || _subject == null) return;

            var y = Origin.y + CutAt * _scale;
            var eye = _camera.transform.position;
            if (eye.y <= y + 0.05f) return;

            // The oblique projection tilts the far plane too, so it is pushed out to keep the
            // backdrop in the picture.
            _camera.farClipPlane *= 4f;
            var toCamera = _camera.worldToCameraMatrix;
            var point = toCamera.MultiplyPoint(new Vector3(eye.x, y, eye.z));
            var normal = toCamera.MultiplyVector(Vector3.down).normalized;
            var plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal));
            _camera.projectionMatrix = _camera.CalculateObliqueMatrix(plane);
        }
    }
}
