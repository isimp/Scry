using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>Framing the model: what it draws, where the camera looks from, and the person standing beside it.</summary>
    internal static partial class Stage
    {
        private static GameObject _person;
        private static Bounds _personBounds;
        private static Bounds _personLocal;
        private static Bounds _bounds;

        // Filled again on each use, so looking through a copy each frame makes no garbage.
        private static readonly List<Renderer> Renderers = new List<Renderer>();
        private static readonly List<Renderer> SolidRenderers = new List<Renderer>();
        private static readonly List<Renderer> LooseRenderers = new List<Renderer>();
        private static float _gridMetres = -1f;

        // What the camera frames, eased towards what is to be seen, so it does not jump about.
        private static float _frameRadius = -1f;

        // A model's resting pose is not always where its animation takes it (a bat flies lower
        // than it hangs), so what it draws is measured again while its animation first plays.
        private static float _settleUntil;
        private static float _settleFrom;
        private static bool _settled;

        /// <summary>The lowest point of the model's body, or where it stands, as if at size one.</summary>
        private static float _bodyMinY;

        /// <summary>Whether the floor is where the character stands, which what it draws does not move.</summary>
        private static bool _groundFixed;

        /// <summary>
        /// Measures a copy newly shown, and where it stands: measured again while its animation
        /// first plays, as its first pose can stand far from where it stands after (lying,
        /// raised, off to a side), a flyer over its flight a while longer.
        /// </summary>
        private static void MeasureNew(Entry entry)
        {
            _settleFrom = Time.unscaledTime + 0.25f;
            _settled = false;
            _settleUntil = _followEffect ? 0f : Time.unscaledTime + (_onFeet ? 0.5f : 1.5f);
            _bounds = Measure(_subject);
            _bodyMinY = Measure(_subject, body: true).min.y;

            // A character on its feet stands where the game stands it, on the bottom of its
            // capsule (Character's CapsuleCollider resting on the ground), whatever of its model
            // reaches below: a root's base is in the ground, a weapon may hang low.
            var grounded = _subjectIsPerson ? GamePrefabs.Person : _onFeet ? entry.Source as GameObject : null;
            var capsule = grounded != null ? grounded.GetComponent<CapsuleCollider>() : null;
            _groundFixed = capsule != null && capsule.direction == 1;
            if (_groundFixed) _bodyMinY = Origin.y + (capsule.center.y - capsule.height / 2f) * _baseScale.y;

            // A raid's creatures stand where they were put, on the stage's ground.
            if (entry.Source is RandomEvent)
            {
                _groundFixed = true;
                _bodyMinY = Origin.y;
            }

            // A location stands on the ground the game stands it on, a room on the floor it is
            // walked into on (PlaceView.Ground), not on the lowest thing it holds.
            if (entry.Source is PlaceSource shown)
            {
                _groundFixed = true;
                _bodyMinY = Origin.y + PlaceView.Ground(shown.Contents, shown.IsRoom) * _baseScale.y;
            }
        }

        /// <summary>Measures the copy again standing at a height of its own, as an example does inside or out.</summary>
        private static void MeasureStanding(float bodyMinY)
        {
            _bodyMinY = bodyMinY;
            _bounds = Unscaled(Measure(_subject));
        }

        /// <summary>Frames what is shown anew, from its whole size, rather than easing from what was framed.</summary>
        private static void FrameAnew() => _frameRadius = -1f;

        /// <summary>Has the grid tiled again for its size at the next frame, as a new grid is.</summary>
        private static void TileGridAnew() => _gridMetres = -1f;

        /// <summary>Lets go of the person standing beside the model, as the stage is taken down.</summary>
        private static void ForgetPerson() => _person = null;

        /// <summary>
        /// Moves what the camera looks at, and the camera with it at once, nearer or farther by a
        /// factor, so a zoom in the same frame starts from where this one left it.
        /// </summary>
        private static void MoveLook(Vector3 move, float factor)
        {
            var t = _camera.transform;
            var distance = Vector3.Distance(t.position, _lookAt) * factor;
            _lookAt += move;
            t.position = _lookAt - t.forward * distance;
        }

        /// <summary>What the model shows, as if at size one: above where it stands, a root's base in the ground left out.</summary>
        private static Bounds Shown
        {
            get
            {
                if (!_groundFixed || _bounds.min.y >= _bodyMinY || _bounds.max.y <= _bodyMinY) return _bounds;
                var shown = new Bounds();
                shown.SetMinMax(new Vector3(_bounds.min.x, _bodyMinY, _bounds.min.z), _bounds.max);
                return shown;
            }
        }

        /// <summary>What the camera looks at, moved off what it frames by <see cref="_pan"/>.</summary>
        private static Vector3 _lookAt;

        /// <summary>The middle of what is framed, gliding to what is to be framed.</summary>
        private static Vector3 _frameCenter;

        /// <summary>With a floor opened, the height on the stage of the floor the camera looks at; null for none.</summary>
        private static float? _lookedFloor;

        /// <summary>Whether the camera is still gliding to what it is to frame, for the self-test to wait on.</summary>
        public static bool Gliding { get; private set; }

        /// <summary>The height on the stage of a floor of the place shown, for the self-test.</summary>
        public static float FloorOnStage(int level) => level >= 0 && level < Floors.Count ? Origin.y + Floors[level] * _scale : float.NaN;

        /// <summary>Cuts at a height on the stage, for the self-test.</summary>
        public static void CutOnStage(float y)
        {
            if (_scale > 0f) CutTo((y - Origin.y) / _scale);
        }

        /// <summary>Whether a cut is laid in the picture, the camera above it, for the self-test.</summary>
        public static bool CutLaid => _aboveCut != null;

        private static void Frame()
        {
            // With a floor opened, the camera looks at that floor, framed on what stands on it.
            var step = Timing.Start();
            var floor = FloorFrame();
            Timing.Add("render frame floor", step);
            Vector3 target;
            float wantRadius;
            if (floor is Vector4 opened)
            {
                target = new Vector3(opened.x, opened.y, opened.z);
                wantRadius = opened.w;
            }
            else
            {
                var shown = Shown;
                var subject = new Bounds(Origin + (shown.center - Origin) * _scale, shown.size * _scale);
                var framed = subject;
                if (_person != null && _person.activeSelf) framed.Encapsulate(_personBounds);

                // The camera stays on the model, and backs off far enough to take in what an
                // effect reaches as well: an effect's own particles as they spread, and what was
                // played on the model (sparks, debris, a ragdoll, a fallen log), within a few
                // times its size.
                var center = framed.center + _pan;
                var own = Mathf.Max(0.05f, framed.extents.magnitude);
                var reach = own;
                if (_followEffect) Reach(_subject, center, ref reach);
                foreach (var played in Played)
                {
                    // Around a model, only what falls from it is followed: sparks and smoke stay
                    // where they are, a ragdoll, debris or a log is kept in the picture.
                    if (_followEffect || (played.Key != null && played.Key.GetComponentInChildren<Rigidbody>() != null)) Reach(played.Key, center, ref reach);
                }
                target = framed.center;
                wantRadius = StageFraming.ModelRadius(own, reach, _followEffect);
            }

            // Measuring leaves out what has no usable bounds, but should anything still come out
            // of it that is not a number, the camera frames the stage's middle as it frames a model
            // with nothing to measure, rather than being put nowhere.
            if (!Finite(target) || !Finite(wantRadius) || !Finite(_frameRadius) || !Finite(_pan) || !Finite(_frameCenter))
            {
                CenterView();
                var fallback = Unmeasured(Origin);
                target = fallback.center;
                wantRadius = fallback.extents.magnitude * StageFraming.ModelReach;
                _frameRadius = -1f;
                floor = null;
            }

            if (_frameRadius < 0f)
            {
                _frameRadius = wantRadius;
                _frameCenter = target;
            }
            else
            {
                // Out quickly, so nothing leaves the picture; back in slowly, once it settles; to a
                // floor stepped to quickly either way.
                _frameRadius = StageFraming.Glide(_frameRadius, wantRadius, floor != null, Time.unscaledDeltaTime);
                _frameCenter = Vector3.Lerp(_frameCenter, target, StageFraming.CentreShare(Time.unscaledDeltaTime));
            }
            Gliding = StageFraming.StillGliding(_frameRadius, wantRadius, Vector3.Distance(_frameCenter, target));

            // Looking at a floor, the view keeps to its height however it is moved.
            _lookedFloor = floor?.y;
            _lookAt = _frameCenter + (floor != null ? new Vector3(_pan.x, 0f, _pan.z) : _pan);
            var radius = _frameRadius;
            var distance = StageFraming.Distance(radius, FieldOfView, Zoom, OverCutDistance());

            var rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            var t = _camera.transform;
            t.rotation = rotation;
            t.position = _lookAt - rotation * Vector3.forward * distance;
            var (near, far) = StageFraming.Clipping(distance, radius);
            _camera.nearClipPlane = near;
            _camera.farClipPlane = far;
            _camera.aspect = (float)_width / _height;

            var groundY = Mathf.Min(FloorY, _person != null && _person.activeSelf ? _personBounds.min.y : FloorY);
            if (!Finite(groundY)) groundY = Origin.y;
            if (_sky != null && _sky.activeSelf)
            {
                var depth = _camera.farClipPlane * 0.95f;
                var tall = StageFraming.SkyHeight(depth, FieldOfView);
                _sky.transform.localPosition = new Vector3(0f, 0f, depth);
                _sky.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _sky.transform.localScale = new Vector3(tall * _camera.aspect, 1f, tall);
            }
            if (_grid != null && _grid.activeSelf)
            {
                // Whole tiles of five metres, an even number of them, so a five-metre line runs
                // under the middle of the model.
                var metres = StageFraming.GridMetres(radius);
                _grid.transform.position = new Vector3(Origin.x, groundY - 0.004f, Origin.z);
                _grid.transform.localScale = new Vector3(metres, 1f, metres);
                if (!Mathf.Approximately(metres, _gridMetres))
                {
                    _gridMetres = metres;
                    Floor.Tile(_grid, metres / Floor.GridMetres);
                }
            }

            // A biome's ground under the model, under the plain floor's height by as much as the
            // terrain's shader raises its bumps, so the model's feet stay on it.
            step = Timing.Start();
            if (!Guard.Run(Feature.StageGround, GroundPart, LayGround, groundY)) PutGroundAway();
            Timing.Add("render frame ground", step);

            if (_floor != null)
            {
                var floorY = groundY - 0.005f;
                _floor.transform.position = new Vector3(_lookAt.x, floorY, _lookAt.z);
                var size = StageFraming.FloorSize(radius);
                _floor.transform.localScale = new Vector3(size, size, size);
            }

            step = Timing.Start();
            ApplyCut();
            Timing.Add("render frame cut", step);
        }

        /// <summary>
        /// How far off the camera stays to be over a floor's cut, as a cut only opens what is
        /// seen from above it (<see cref="StageCamera.OverCut"/>): raised or moved, the cut takes
        /// the camera up with it rather than leave it under the cut and the place closed.
        /// </summary>
        private static float OverCutDistance() =>
            Cutting && _subject != null ? StageCamera.OverCut(Origin.y + CutAt * _scale - _lookAt.y, Pitch) : 0f;

        /// <summary>
        /// With a floor opened, what the camera looks at: the floor's height, over the middle of
        /// what stands on it (a dungeon example's rooms on that floor, else the whole model),
        /// framed on how far that reaches (<see cref="StageCamera.Across"/>); null for none.
        /// </summary>
        private static Vector4? FloorFrame()
        {
            if (!Cutting || _subject == null || TheCut.Level >= Floors.Count) return null;
            var y = Origin.y + Floors[TheCut.Level] * _scale;
            var across = ExampleFloorAcross();
            if (across == null)
            {
                var shown = Shown;
                var middle = Origin + (shown.center - Origin) * _scale;
                var extents = shown.extents * _scale;
                across = new Vector3(middle.x, middle.z, StageCamera.HalfAcross(extents.x * 2f, extents.z * 2f));
            }
            var a = across.Value;
            return new Vector4(a.x, y, a.y, StageFraming.FloorReach(a.z));
        }

        /// <summary>Takes in what the model draws while its animation first plays, as if at size one.</summary>
        private static void Settle()
        {
            if (_subject == null || Time.unscaledTime < _settleFrom || Time.unscaledTime > _settleUntil || Standin.IsDown(_subject) || _scale <= 0f) return;
            var now = Measure(_subject);
            var unscaled = new Bounds(Origin + (now.center - Origin) / _scale, now.size / _scale);
            if (unscaled.size.magnitude > _bounds.size.magnitude * 4f + 1f) return;
            var body = _groundFixed ? _bodyMinY : Origin.y + (Measure(_subject, body: true).min.y - Origin.y) / _scale;
            if (_settled)
            {
                _bounds.Encapsulate(unscaled);
                _bodyMinY = Mathf.Min(_bodyMinY, body);
            }
            else
            {
                _bounds = unscaled;
                _bodyMinY = body;
            }
            _settled = true;
            PlacePerson();
        }

        /// <summary>How far from the middle a copy draws now, leaving out what is runaway.</summary>
        private static void Reach(GameObject thing, Vector3 center, ref float reach)
        {
            if (thing == null || !thing.activeInHierarchy) return;
            thing.GetComponentsInChildren(false, Renderers);
            foreach (var renderer in Renderers)
            {
                if (!renderer.enabled) continue;
                var bounds = renderer.bounds;
                if (!Usable(bounds) || bounds.size.sqrMagnitude < 1e-6f || bounds.size.magnitude > 200f) continue;
                reach = Mathf.Max(reach, Vector3.Distance(center, bounds.center) + bounds.extents.magnitude);
            }
            Renderers.Clear();
        }

        /// <summary>
        /// The person stands beside the model, on the same ground, at the game's own player size,
        /// whatever size the model is shown at.
        /// </summary>
        private static void PlacePerson()
        {
            if (_root == null) return;

            // An item worn by a person already has one to judge it by.
            if (!_showPerson || _subject == null || _subjectIsPerson)
            {
                if (_person != null) _person.SetActive(false);
                return;
            }

            if (_person == null)
            {
                _person = Looks.PersonCopy(_root.transform, Origin, Quaternion.identity, _layer, null, out var prefab);
                if (_person == null) return;
                Gear.Body(prefab, _person);
                Tune(_person, audible: false);

                // Its size relative to its own root, measured once.
                var measured = Measure(_person);
                _personLocal = new Bounds(measured.center - _person.transform.position, measured.size);
            }

            _person.SetActive(true);

            // Its right side a little to the left of the model, its feet on the model's ground.
            var subjectMin = Origin + (_bounds.min - Origin) * _scale;
            var subjectCenter = Origin + (_bounds.center - Origin) * _scale;
            var wanted = new Vector3(StageFraming.PersonX(subjectMin.x, _personLocal.extents.x), FloorY + _personLocal.extents.y, subjectCenter.z);
            _person.transform.position = wanted - _personLocal.center;
            _personBounds = new Bounds(wanted, _personLocal.size);
        }

        /// <summary>
        /// What a copy draws, as bounds; of its <paramref name="body"/> only, leaving out what hangs
        /// on it, where it has anything else. Particles are left out when there is anything else,
        /// since their bounds are unsettled while they start.
        /// </summary>
        private static Bounds Measure(GameObject subject, bool body = false)
        {
            // A renderer whose bounds are not numbers (a mod's broken mesh) would make the whole
            // measure so, and the camera with it; it is left out.
            subject.GetComponentsInChildren(false, Renderers);
            try
            {
                if (body)
                {
                    var any = false;
                    var b = new Bounds();
                    foreach (var r in Renderers)
                    {
                        if (!r.enabled || r is ParticleSystemRenderer || r.GetComponentInParent<Hung>() != null) continue;
                        var rb = r.bounds;
                        if (!Usable(rb)) continue;
                        if (any) b.Encapsulate(rb);
                        else b = rb;
                        any = true;
                    }
                    if (any && b.size.sqrMagnitude >= 0.0001f && b.size.magnitude <= 2000f) return b;
                }

                foreach (var renderer in Renderers)
                {
                    if (!renderer.enabled || !Usable(renderer.bounds)) continue;
                    if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) LooseRenderers.Add(renderer);
                    else SolidRenderers.Add(renderer);
                }

                var use = SolidRenderers.Count > 0 ? SolidRenderers : LooseRenderers;
                if (use.Count == 0) return Unmeasured(subject.transform.position);

                var bounds = use[0].bounds;
                for (var i = 1; i < use.Count; i++) bounds.Encapsulate(use[i].bounds);

                // A degenerate or runaway size would put the camera nowhere useful.
                if (!Usable(bounds) || bounds.size.sqrMagnitude < 0.0001f || bounds.size.magnitude > 2000f) return Unmeasured(subject.transform.position);
                return bounds;
            }
            finally
            {
                Renderers.Clear();
                SolidRenderers.Clear();
                LooseRenderers.Clear();
            }
        }

        /// <summary>What a copy with nothing to measure is taken to be: two metres across, standing where it is.</summary>
        private static Bounds Unmeasured(Vector3 position) => new Bounds(position + Vector3.up, Vector3.one * 2f);

        /// <summary>Whether bounds are numbers throughout, neither NaN nor infinite.</summary>
        private static bool Usable(Bounds bounds) => Finite(bounds.center) && Finite(bounds.extents);

        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);

        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
    }
}
