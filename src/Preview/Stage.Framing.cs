using UnityEngine;

namespace Scry
{
    /// <summary>Framing the model: what it draws, where the camera looks from, and the person standing beside it.</summary>
    internal static partial class Stage
    {
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

        private static void Frame()
        {
            var shown = Shown;
            var subject = new Bounds(Origin + (shown.center - Origin) * _scale, shown.size * _scale);
            var framed = subject;
            if (_person != null && _person.activeSelf) framed.Encapsulate(_personBounds);

            // The camera stays on the model, and backs off far enough to take in what an effect
            // reaches as well: an effect's own particles as they spread, and what was played on
            // the model (sparks, debris, a ragdoll, a fallen log), within a few times its size.
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
            var wantRadius = Mathf.Min(reach, _followEffect ? 25f : own * 1.6f);

            // Measuring leaves out what has no usable bounds, but should anything still come out
            // of it that is not a number, the camera frames the stage's middle as it frames a model
            // with nothing to measure, rather than being put nowhere.
            if (!Finite(center) || !Finite(wantRadius) || !Finite(_frameRadius))
            {
                _pan = Vector3.zero;
                var fallback = Unmeasured(Origin);
                center = fallback.center;
                wantRadius = fallback.extents.magnitude * 1.6f;
                _frameRadius = -1f;
            }

            if (_frameRadius < 0f)
            {
                _frameRadius = wantRadius;
            }
            else
            {
                // Out quickly, so nothing leaves the picture; back in slowly, once it settles.
                var rate = wantRadius > _frameRadius ? 5f : 1f;
                _frameRadius = Mathf.Lerp(_frameRadius, wantRadius, 1f - Mathf.Exp(-rate * Time.unscaledDeltaTime));
            }
            var radius = _frameRadius;
            var distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) * Zoom;

            var rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            var t = _camera.transform;
            t.rotation = rotation;
            t.position = center - rotation * Vector3.forward * distance;
            _camera.nearClipPlane = Mathf.Max(0.01f, distance * 0.01f);
            _camera.farClipPlane = distance + radius * 6f + 10f;
            _camera.aspect = (float)_width / _height;

            var groundY = Mathf.Min(FloorY, _person != null && _person.activeSelf ? _personBounds.min.y : FloorY);
            if (!Finite(groundY)) groundY = Origin.y;
            if (_sky != null && _sky.activeSelf)
            {
                var depth = _camera.farClipPlane * 0.95f;
                var tall = 2f * depth * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                _sky.transform.localPosition = new Vector3(0f, 0f, depth);
                _sky.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _sky.transform.localScale = new Vector3(tall * _camera.aspect, 1f, tall);
            }
            if (_grid != null && _grid.activeSelf)
            {
                // Whole tiles of five metres, an even number of them, so a five-metre line runs
                // under the middle of the model.
                var metres = Mathf.Max(10f, Mathf.Ceil(radius * 4f / 10f) * 10f);
                _grid.transform.position = new Vector3(Origin.x, groundY - 0.004f, Origin.z);
                _grid.transform.localScale = new Vector3(metres, 1f, metres);
                if (!Mathf.Approximately(metres, _gridMetres))
                {
                    _gridMetres = metres;
                    Floor.Tile(_grid, metres / Floor.GridMetres);
                }
            }

            if (_floor != null)
            {
                var floorY = groundY - 0.005f;
                _floor.transform.position = new Vector3(center.x, floorY, center.z);
                var size = radius * 3.2f;
                _floor.transform.localScale = new Vector3(size, size, size);
            }
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
                var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab("Player") : null;
                if (prefab == null) return;
                _person = Ghost.Make(prefab, _root.transform, Origin, Quaternion.identity, _layer);
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
            var wanted = new Vector3(subjectMin.x - 0.4f - _personLocal.extents.x, FloorY + _personLocal.extents.y, subjectCenter.z);
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
