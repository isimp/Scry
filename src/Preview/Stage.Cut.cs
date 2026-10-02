using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A location or room cut open to look into: everything above head height over one of its
    /// floors is cut away (<see cref="PlaceView"/>), its floors found in the model itself
    /// (<see cref="FloorProbe"/>). The cut is the camera's near plane laid along that height (an
    /// oblique projection), so it opens any model whatever its meshes are made of. A room opens on
    /// its top floor when first shown, a location keeps its roof; the stage's chip takes the roof
    /// off and puts it back, its arrows, Page Up and Down and the ruler step a floor up or down,
    /// the ruler sets the cut anywhere, and Shift with the wheel moves it. Looked at from below
    /// the cut, nothing is cut. With a floor opened the camera looks at that floor, framed on
    /// what stands on it (<see cref="FloorFrame"/>).
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>The floors of the place shown, from the top down, above its root as if at size one; none for anything else.</summary>
        private static readonly List<float> Floors = new List<float>();

        /// <summary>Where each floor is cut (<see cref="PlaceView.CutHeights"/>).</summary>
        private static List<float> _cuts = new List<float>();

        /// <summary>The floor last opened, which taking the roof off again opens.</summary>
        private static int _lastOpen;

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

        /// <summary>Where it is cut, above its root as if at size one.</summary>
        public static float CutAt => Cutting ? _cuts[_cutLevel] + _cutShift : float.PositiveInfinity;

        /// <summary>The floors, from the top down, and where each is cut, for the ruler.</summary>
        public static IReadOnlyList<float> FloorHeights => Floors;
        public static IReadOnlyList<float> CutHeights => _cuts;

        /// <summary>Which floor is opened, from the top; <see cref="FloorHeights"/>' count for the roof on.</summary>
        public static int CutLevel => _cutLevel;

        /// <summary>How low and how high the model reaches, above its root as if at size one.</summary>
        public static float ModelBottom => _subject == null ? 0f : _bounds.min.y - Origin.y;
        public static float ModelTop => _subject == null ? 0f : _bounds.max.y - Origin.y;

        /// <summary>Opens the floor below, or the top floor from the roof; goes up a floor, or puts the roof back from the top floor.</summary>
        public static void StepCut(bool down)
        {
            if (Floors.Count == 0) return;
            OpenLevel(PlaceView.StepCut(_cutLevel, Floors.Count, down));
        }

        /// <summary>Takes the roof off, opening the floor last opened, or puts it back.</summary>
        public static void ToggleRoof()
        {
            if (Floors.Count == 0) return;
            OpenLevel(Cutting ? Floors.Count : Mathf.Clamp(_lastOpen, 0, Floors.Count - 1));
        }

        /// <summary>Opens a floor (from the top), or puts the roof on past the last.</summary>
        public static void OpenLevel(int level)
        {
            if (Floors.Count == 0) return;
            _cutLevel = Mathf.Clamp(level, 0, Floors.Count);
            _cutShift = 0f;
            if (Cutting)
            {
                _lastOpen = _cutLevel;
                if (Pitch < CutPitch / 2f) Pitch = CutPitch;
            }
        }

        /// <summary>Cuts at a height set by hand, opening the floor it is over (<see cref="PlaceView.LevelAt"/>).</summary>
        public static void CutTo(float height)
        {
            if (Floors.Count == 0) return;
            var level = PlaceView.LevelAt(Floors, height);
            if (level != _cutLevel || !Cutting) OpenLevel(level);
            _cutShift = height - _cuts[_cutLevel];
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
            _cuts = PlaceView.CutHeights(Floors);
            if (ReferenceEquals(_cutFor, entry))
            {
                _cutLevel = Mathf.Min(_cutLevel, Floors.Count);
                return;
            }
            _cutFor = entry;
            _cutShift = 0f;
            _lastOpen = 0;
            _cutLevel = open ? 0 : Floors.Count;
            if (Cutting) Pitch = CutPitch;
        }

        /// <summary>Takes floors found anew for the place shown (an example's, as its rooms come in), keeping the floor opened where it can.</summary>
        private static void RefreshFloors(IEnumerable<float> floors)
        {
            var roofOn = !Cutting;
            Floors.Clear();
            Floors.AddRange(floors);
            _cuts = PlaceView.CutHeights(Floors);
            _cutLevel = roofOn ? Floors.Count : Mathf.Min(_cutLevel, Math.Max(0, Floors.Count - 1));
        }

        /// <summary>
        /// The floors of a place's copy, made with its colliders: found in the model, a location's
        /// ground among them; a room with nothing found keeps its doorways' heights, a location its root.
        /// </summary>
        private static List<FloorHit> _floorHits = new List<FloorHit>();
        private static List<string> _floorNames = new List<string>();

        /// <summary>What each floor of the location or room shown stands on, from its rays, for the self-test to tell (<see cref="FloorMakers"/>).</summary>
        public static List<string> FloorMakersNow() => FloorMakers.Tell(_floorHits.Select(h => h.Height).ToList(), _floorNames, Floors);

        private static List<float> FloorsOf(GameObject copy, PlaceSource place)
        {
            var hits = new List<FloorHit>();
            var names = new List<string>();
            var found = FloorFinder.Floors(hits, FloorProbe.Read(copy, copy.transform, _layer, hits, 0, names));
            _floorHits = hits;
            _floorNames = names;
            if (place.IsRoom) return found.Count > 0 ? found : PlaceView.RoomFloors(place.Contents?.Room);
            return PlaceView.WithGround(found);
        }

        private static void ClearFloors()
        {
            Floors.Clear();
            _cuts.Clear();
            _cutFor = null;
            _cutLevel = 0;
            _lastOpen = 0;
            _cutShift = 0f;
        }

        /// <summary>
        /// Lays the camera's near plane along the cut, once the camera is placed for the frame,
        /// when the camera is above it; otherwise the camera's own projection stands.
        /// </summary>
        private static void ApplyCut()
        {
            _camera.ResetProjectionMatrix();
            _aboveCut = null;
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

            var up = -normal;
            _aboveCut = StageCamera.AboveCutRow(up.x, up.y, up.z, -Vector3.Dot(point, up), _camera.nearClipPlane);
        }

        /// <summary>
        /// While a cut is laid, the depth row the creatures are drawn again with, keeping what of
        /// them is above it (<see cref="StageCamera.AboveCutRow"/>); null while nothing is cut.
        /// </summary>
        private static (float X, float Y, float Z, float W)? _aboveCut;
    }
}
