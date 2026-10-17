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
    /// what stands on it (<see cref="FloorFrame"/>). Which floor is opened and where it is cut
    /// is kept by <see cref="FloorCut"/>; the camera tilts down to look in as one is opened.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>The floors of the place shown and the cut opening one of them.</summary>
        private static readonly FloorCut TheCut = new FloorCut();

        /// <summary>The floors of the place shown, from the top down, above its root as if at size one; none for anything else.</summary>
        private static IReadOnlyList<float> Floors => TheCut.Floors;

        /// <summary>How steeply the camera looks down on a place opened, to see in over its walls.</summary>
        private const float CutPitch = 40f;

        /// <summary>Whether the model shown can be cut open.</summary>
        public static bool HasFloors => TheCut.HasFloors;

        /// <summary>Whether it is cut open now.</summary>
        public static bool Cutting => TheCut.Cutting;

        /// <summary>What the cut's chip says.</summary>
        public static string CutLabel => TheCut.Label;

        /// <summary>Where it is cut, above its root as if at size one.</summary>
        public static float CutAt => TheCut.At;

        /// <summary>The floors, from the top down, and where each is cut, for the ruler.</summary>
        public static IReadOnlyList<float> FloorHeights => TheCut.Floors;
        public static IReadOnlyList<float> CutHeights => TheCut.Cuts;

        /// <summary>The ceiling over a floor, from the top, as found; null with none found over it.</summary>
        public static float? CeilingOver(int level) => TheCut.CeilingOver(level);

        /// <summary>Which floor is opened, from the top; <see cref="FloorHeights"/>' count for the roof on.</summary>
        public static int CutLevel => TheCut.Level;

        /// <summary>
        /// How low and how high the model reaches, above its root as if at size one: high, as
        /// high as its rays met where its floors were found (<see cref="FloorRecord.Highest"/>),
        /// whatever of it is drawn, else as high as it draws.
        /// </summary>
        public static float ModelBottom => _subject == null ? 0f : _bounds.min.y - Origin.y;
        public static float ModelTop => _subject == null ? 0f : float.IsNaN(_floorTop) ? _bounds.max.y - Origin.y : _floorTop;

        /// <summary>The highest the rays met over the place whose floors the cut holds, above its root; NaN where not known.</summary>
        private static float _floorTop = float.NaN;

        // Opening or stepping to a floor leaves the camera where it was put, so the view moves
        // only when it is moved by hand and stepping through floors keeps the angle chosen. A
        // room shown alone is looked down into the first time it opens (SetFloors), as nothing
        // has framed it yet.

        /// <summary>Opens the floor below, or the top floor from the roof; goes up a floor, or puts the roof back from the top floor; out of a room gone into.</summary>
        public static void StepCut(bool down)
        {
            var framed = LevelFramed();
            TheExample.Unfocus();
            TheCut.Step(down);
            KeepOrFreshView(framed);
        }

        /// <summary>Takes the roof off, opening the floor last opened, or puts it back; in a room gone into, its own roof, the room still gone into.</summary>
        public static void ToggleRoof()
        {
            if (TheExample.Focused != null)
            {
                if (!CutFromSide) TheCut.ToggleRoof();
                return;
            }
            TheCut.ToggleRoof();
            FreshView();
        }

        /// <summary>Opens a floor (from the top), or puts the roof on past the last; out of a room gone into.</summary>
        public static void OpenLevel(int level)
        {
            var framed = LevelFramed();
            TheExample.Unfocus();
            TheCut.Open(level);
            KeepOrFreshView(framed);
        }

        /// <summary>The frame the view has on the floor opened, outside a room gone into; none with the roof on or in a room.</summary>
        private static Vector4? LevelFramed() => TheExample.Focused == null && !TheCut.Held ? ShownLevelFrame : null;

        /// <summary>
        /// After the floor opened changed: from a floor to another the view stays on the spot it
        /// looked at, at its zoom and as dragged, its height following the floor (<see cref="KeepLevelFrame"/>);
        /// from or to the roof, or out of a room, it frames afresh.
        /// </summary>
        private static void KeepOrFreshView(Vector4? framed)
        {
            if (framed is Vector4 kept && TheCut.Cutting && !TheCut.Held) KeepLevelFrame(kept);
            else FreshView();
        }

        /// <summary>Cuts at a height set by hand, opening the floor it is over (<see cref="PlaceView.LevelAt"/>); out of a room gone into.</summary>
        public static void CutTo(float height)
        {
            if (!TheCut.HasFloors) return;
            TheExample.Unfocus();
            TheCut.CutTo(height);
        }

        /// <summary>Moves the cut up or down.</summary>
        public static void CutBy(float metres) => TheCut.CutBy(metres);

        /// <summary>Moves the cut as Shift and the wheel do (<see cref="FloorCut.Wheel"/>): a room's cut in the room gone into, within its ruler, else through the floors, opening the one it is over; a room cut from the side has no height to move.</summary>
        public static void WheelCut(float metres)
        {
            if (TheExample.Focused != null && CutFromSide) return;
            var framed = LevelFramed();
            var was = TheCut.Level;
            if (!TheCut.Held) TheExample.Unfocus();
            TheCut.Wheel(metres);
            // Moved into another floor, the view stays on the spot it looked at; the wheel frames nothing afresh.
            if (framed is Vector4 kept && TheCut.Level != was && TheCut.Cutting && !TheCut.Held) KeepLevelFrame(kept);
        }

        /// <summary>
        /// Takes the floors of the place shown. The first time for an entry, a room is opened on
        /// its top floor and looked down into, and a location keeps its roof; a new copy of the
        /// same (rolled again, or loaded) keeps the cut it had.
        /// </summary>
        private static void SetFloors(Entry entry, IEnumerable<float> floors, bool open, System.Func<float, float?> ceilingOver, float top)
        {
            _floorTop = top;
            if (TheCut.Take(entry, floors, open, ceilingOver) && TheCut.Cutting) TurnTo(Yaw, CutPitch);
        }

        /// <summary>Takes floors found anew for the place shown (an example's, as its rooms come in), keeping the floor opened where it can.</summary>
        private static void RefreshFloors(IEnumerable<float> floors, System.Func<float, float?> ceilingOver, float top)
        {
            _floorTop = top;
            TheCut.Refresh(floors, ceilingOver);
        }

        /// <summary>
        /// The floors of a place's copy, made with its colliders: found in the model, a location's
        /// ground among them; a room with nothing found keeps its doorways' heights, a location its root.
        /// </summary>
        private static List<FloorHit> _floorHits = new List<FloorHit>();
        private static List<string> _floorNames = new List<string>();

        /// <summary>The highest the rays met over the place shown alone, above its root; NaN where none met it.</summary>
        private static float _placeTop = float.NaN;

        /// <summary>The ceiling over a floor of the place shown alone, from its rays (<see cref="FloorFinder.CeilingOver(IEnumerable{FloorHit}, float)"/>).</summary>
        private static readonly System.Func<float, float?> PlaceCeiling = floor => FloorFinder.CeilingOver(_floorHits, floor);

        /// <summary>What each floor of the location or room shown stands on, from its rays, for the self-test to tell (<see cref="FloorMakers"/>).</summary>
        public static List<string> FloorMakersNow() => FloorMakers.Tell(_floorHits.Select(h => h.Height).ToList(), _floorNames, Floors);

        private static List<float> FloorsOf(GameObject copy, PlaceSource place, PlaceRoll roll)
        {
            var hits = new List<FloorHit>();
            var names = new List<string>();
            // A room is walked into by its doorways, a location from its ground and the open sky.
            var doorways = place.Contents?.Room?.Doorways;
            var start = !place.IsRoom ? FloorWalk.Start.Outdoors : doorways != null && doorways.Count > 0 ? FloorWalk.Start.At(doorways.Select(d => d.Position)) : null;
            _placeTop = float.NaN;
            var found = FloorFinder.Floors(hits, FloorProbe.Read(copy, copy.transform, _layer, hits, 0, place.Prefab, place.IsRoom, roll, names, start: start, top: t => _placeTop = t));
            _floorHits = hits;
            _floorNames = names;
            if (place.IsRoom) return found.Count > 0 ? found : PlaceView.RoomFloors(place.Contents?.Room);
            return PlaceView.WithGround(found);
        }

        private static void ClearFloors() => TheCut.Clear();

        /// <summary>
        /// A sloping cut in the world: a point of it, at the head of its slope's low end, and the
        /// way to what it keeps, down from it and square to it.
        /// </summary>
        private static (Vector3 At, Vector3 Kept) SlopeInWorld(RoomCutPlane slope)
        {
            var holder = TheExample.Holder.transform;
            var low = holder.TransformPoint(new Vector3(slope.Low.X, slope.Low.Y, slope.Low.Z));
            var high = holder.TransformPoint(new Vector3(slope.High.X, slope.High.Y, slope.High.Z));
            var side = holder.TransformDirection(new Vector3(-(slope.High.Z - slope.Low.Z), 0f, slope.High.X - slope.Low.X));
            var up = Vector3.Cross(side, high - low).normalized;
            if (up.y < 0f) up = -up;
            return (low, -up);
        }

        /// <summary>How high the cut is at a point of the world, along the slope where one is laid; out of reach with none.</summary>
        private static float CutHeightAt(Vector3 point)
        {
            if (!Cutting || _subject == null) return float.PositiveInfinity;
            if (!SlopeLaid(out var slope)) return Origin.y + CutAt * _scale;
            var holder = TheExample.Holder.transform;
            var local = holder.InverseTransformPoint(point);
            return holder.TransformPoint(new Vector3(local.x, slope.HeightAt(local.x, local.z), local.z)).y;
        }

        /// <summary>The same, made once, for the creatures to be kept to the cut by.</summary>
        private static readonly System.Func<Vector3, float> CutHeightAtPoint = CutHeightAt;

        /// <summary>
        /// Lays the camera's near plane along the cut, once the camera is placed for the frame,
        /// when the camera is above it; otherwise the camera's own projection stands.
        /// </summary>
        private static void ApplyCut()
        {
            _camera.ResetProjectionMatrix();
            _aboveCut = null;
            if (_subject == null) return;
            if (SideCutting && SideCutThrough() is Vector3 through)
            {
                ApplySideCut(through);
                return;
            }
            if (!Cutting) return;

            // A point of the cut and the way to what it keeps, in the world: level at the cut's
            // height, or along the slope over the room gone into.
            var eye = _camera.transform.position;
            Vector3 at, kept;
            if (SlopeLaid(out var slope))
            {
                (at, kept) = SlopeInWorld(slope);
            }
            else
            {
                at = new Vector3(eye.x, Origin.y + CutAt * _scale, eye.z);
                kept = Vector3.down;
            }
            if (Vector3.Dot(eye - at, kept) >= -0.05f) return;

            // The oblique projection tilts the far plane too, so it is pushed out to keep the
            // backdrop in the picture.
            _camera.farClipPlane *= 4f;
            var toCamera = _camera.worldToCameraMatrix;
            var point = toCamera.MultiplyPoint(at);
            var normal = toCamera.MultiplyVector(kept).normalized;
            var plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal));
            _camera.projectionMatrix = _camera.CalculateObliqueMatrix(plane);

            var up = -normal;
            _aboveCut = StageCamera.AboveCutRow(up.x, up.y, up.z, -Vector3.Dot(point, up), _camera.nearClipPlane);
        }

        /// <summary>Lays the camera's near plane level at a height above the location's root, as one film of a level's group of rooms takes it; the camera's own projection where it is not above it.</summary>
        private static void LayCutAt(float cut)
        {
            _camera.ResetProjectionMatrix();
            var eye = _camera.transform.position;
            var at = new Vector3(eye.x, Origin.y + cut * _scale, eye.z);
            if (Vector3.Dot(eye - at, Vector3.down) >= -0.05f) return;
            var toCamera = _camera.worldToCameraMatrix;
            var point = toCamera.MultiplyPoint(at);
            var normal = toCamera.MultiplyVector(Vector3.down).normalized;
            _camera.projectionMatrix = _camera.CalculateObliqueMatrix(new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal)));
        }

        /// <summary>
        /// While a cut is laid, the depth row the creatures are drawn again with, keeping what of
        /// them is above it (<see cref="StageCamera.AboveCutRow"/>); null while nothing is cut.
        /// </summary>
        private static (float X, float Y, float Z, float W)? _aboveCut;
    }
}
