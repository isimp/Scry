using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The turntable in the panel: a copy of the selected prefab on a stage high above the world,
    /// filmed by a camera of its own into a texture the panel draws.
    ///
    /// The stage sits on a layer nothing in the game uses, which only its own camera and lights
    /// see. The world's sun and the main camera are told to leave that layer alone, and the fog and
    /// ambient light are set for the stage only while its camera renders, then put back.
    /// </summary>
    internal static class Stage
    {
        private static readonly Vector3 Origin = new Vector3(0f, 5000f, 0f);
        private const float FieldOfView = 30f;
        private const float SpinDegreesPerSecond = 14f;

        private static readonly Color Backdrop = new Color(0.105f, 0.112f, 0.135f, 1f);
        private static readonly Color Ambient = new Color(0.40f, 0.41f, 0.45f, 1f);

        private static GameObject _root;
        private static Camera _camera;
        private static RenderTexture _texture;
        private static int _layer = -2;
        private static GameObject _floor;

        private static GameObject _subject;
        private static Vector3 _baseScale = Vector3.one;
        private static Bounds _bounds;
        private static float _scale = 1f;
        private static float _madeAt;
        private static int _wantedFrame = -10;
        private static int _width = 512;
        private static int _height = 512;

        public static float Yaw = 30f;
        public static float Pitch = 12f;
        public static float Zoom = 1f;
        public static bool Dragging;

        public static Texture Texture => _texture;
        public static GameObject Subject => _subject;

        /// <summary>Asked by the panel on each frame it shows the stage, with the size it shows it at.</summary>
        public static void Request(int width, int height)
        {
            _wantedFrame = Time.frameCount;
            _width = Mathf.Clamp(width, 64, 2048);
            _height = Mathf.Clamp(height, 64, 2048);
        }

        /// <summary>Puts a fresh copy of the entry on the stage, with the modifiers applied.</summary>
        public static void Show(Entry entry, Modifiers modifiers)
        {
            ClearSubject();
            if (!IsStaged(entry) || !(entry.Source is GameObject prefab)) return;
            if (!Ensure()) return;

            _subject = Ghost.Make(prefab, _root.transform, Origin, Quaternion.identity, _layer);
            if (_subject == null) return;

            if (entry.Kind == Kind.Creature) Looks.ApplyLevel(prefab, _subject, modifiers.Level);
            if (modifiers.WearAvailable) Looks.ApplyWear(prefab, _subject, modifiers.Wear);
            Tune(_subject, entry.Kind);

            _madeAt = Time.unscaledTime;
            _baseScale = _subject.transform.localScale;
            _bounds = Measure(_subject);
            Apply(modifiers);
        }

        /// <summary>Whether an entry has something to put on the stage.</summary>
        public static bool IsStaged(Entry entry)
        {
            return entry != null && !entry.Empty && entry.Kind != Kind.Sound && entry.Kind != Kind.StatusEffect;
        }

        /// <summary>The modifiers that change without making a new copy.</summary>
        public static void Apply(Modifiers modifiers)
        {
            if (_subject == null) return;

            _scale = modifiers.Scale;
            _subject.transform.localScale = _baseScale * modifiers.Scale;
            foreach (var animator in _subject.GetComponentsInChildren<Animator>(true)) animator.speed = modifiers.AnimationSpeed;
            ClipPlayer.SetSpeed(_subject, modifiers.AnimationSpeed);
        }

        /// <summary>
        /// Whether what is on the stage has played out: destroyed by its own timer, or with no
        /// particles alive and no sound playing. Only meaningful for effects.
        /// </summary>
        public static bool Finished
        {
            get
            {
                if (_subject == null) return true;
                if (Time.unscaledTime - _madeAt < 0.5f) return false;

                // Something with neither particles nor sound only ends when it is destroyed.
                var moving = false;
                foreach (var particles in _subject.GetComponentsInChildren<ParticleSystem>())
                {
                    moving = true;
                    if (particles.IsAlive(false)) return false;
                }
                foreach (var source in _subject.GetComponentsInChildren<AudioSource>())
                {
                    moving = true;
                    if (source.isPlaying) return false;
                }
                return moving;
            }
        }

        public static void Orbit(Vector2 delta)
        {
            Yaw += delta.x * 0.4f;
            Pitch = Mathf.Clamp(Pitch + delta.y * 0.3f, -20f, 85f);
        }

        public static void ZoomBy(float wheel)
        {
            Zoom = Mathf.Clamp(Zoom * (1f + wheel * 0.08f), 0.15f, 6f);
        }

        public static void ResetView()
        {
            Yaw = 30f;
            Pitch = 12f;
            Zoom = 1f;
        }

        /// <summary>Films the stage, when the panel showed it in the last couple of frames.</summary>
        public static void Render()
        {
            if (_camera == null || Time.frameCount - _wantedFrame > 2) return;

            if (Plugin.AutoSpin && !Dragging && _subject != null) Yaw += SpinDegreesPerSecond * Time.unscaledDeltaTime;

            EnsureTexture();
            Frame();

            var mask = 1 << _layer;
            var sun = EnvMan.instance != null ? EnvMan.instance.m_dirLight : null;
            var main = GameCamera.instance != null ? GameCamera.instance.GetComponent<Camera>() : null;
            if (main != null) main.cullingMask &= ~mask;

            var fog = RenderSettings.fog;
            var ambient = RenderSettings.ambientLight;
            var sky = RenderSettings.ambientSkyColor;
            var equator = RenderSettings.ambientEquatorColor;
            var ground = RenderSettings.ambientGroundColor;
            var sunMask = sun != null ? sun.cullingMask : 0;

            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientLight = Ambient;
                RenderSettings.ambientSkyColor = Ambient;
                RenderSettings.ambientEquatorColor = Ambient;
                RenderSettings.ambientGroundColor = Ambient * 0.7f;
                if (sun != null) sun.cullingMask = sunMask & ~mask;

                _camera.Render();
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderSettings.ambientLight = ambient;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                if (sun != null) sun.cullingMask = sunMask;
            }
        }

        public static void ClearSubject()
        {
            if (_subject != null) Object.Destroy(_subject);
            _subject = null;
        }

        /// <summary>Takes the whole stage down; it is built again the next time it is needed.</summary>
        public static void Clear()
        {
            ClearSubject();
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _camera = null;
            _floor = null;
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
                _texture = null;
            }
        }

        private static void Frame()
        {
            var center = Origin + (_bounds.center - Origin) * _scale;
            var radius = Mathf.Max(0.05f, _bounds.extents.magnitude) * _scale;
            var distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad) * Zoom;

            var rotation = Quaternion.Euler(Pitch, Yaw, 0f);
            var t = _camera.transform;
            t.rotation = rotation;
            t.position = center - rotation * Vector3.forward * distance;
            _camera.nearClipPlane = Mathf.Max(0.01f, distance * 0.01f);
            _camera.farClipPlane = distance + radius * 6f + 10f;
            _camera.aspect = (float)_width / _height;

            if (_floor != null)
            {
                var floorY = Origin.y + (_bounds.min.y - Origin.y) * _scale - 0.005f;
                _floor.transform.position = new Vector3(center.x, floorY, center.z);
                var size = radius * 3.2f;
                _floor.transform.localScale = new Vector3(size, size, size);
            }
        }

        /// <summary>
        /// The size the camera frames, from what the copy draws. Particles are left out when there
        /// is anything else, since their bounds are unsettled while they start.
        /// </summary>
        private static Bounds Measure(GameObject subject)
        {
            var solid = new List<Renderer>();
            var loose = new List<Renderer>();
            foreach (var renderer in subject.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled) continue;
                if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) loose.Add(renderer);
                else solid.Add(renderer);
            }

            var use = solid.Count > 0 ? solid : loose;
            if (use.Count == 0) return new Bounds(Origin + Vector3.up, Vector3.one * 2f);

            var bounds = use[0].bounds;
            for (var i = 1; i < use.Count; i++) bounds.Encapsulate(use[i].bounds);

            // A degenerate or runaway size would put the camera nowhere useful.
            if (bounds.size.sqrMagnitude < 0.0001f || bounds.size.magnitude > 2000f) return new Bounds(Origin + Vector3.up, Vector3.one * 2f);
            return bounds;
        }

        /// <summary>
        /// Sound on the stage: an effect's is heard as if beside you, anything else stays quiet,
        /// since a creature or a fire humming in your ears while you browse is not a preview.
        /// </summary>
        private static void Tune(GameObject subject, Kind kind)
        {
            var audible = kind == Kind.Effect;
            foreach (var source in subject.GetComponentsInChildren<AudioSource>(true))
            {
                if (audible) source.spatialBlend = 0f;
                else source.mute = true;
            }
        }

        private static bool Ensure()
        {
            if (_root != null && _camera != null) return true;

            if (_layer == -2) _layer = FreeLayer();
            if (_layer < 0) return false;

            _root = new GameObject("Scry stage");
            _root.transform.position = Origin;

            var cameraObject = new GameObject("Scry stage camera");
            cameraObject.transform.SetParent(_root.transform, false);
            _camera = cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Backdrop;
            _camera.cullingMask = 1 << _layer;
            _camera.fieldOfView = FieldOfView;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            _camera.useOcclusionCulling = false;

            AddLight(cameraObject.transform, new Vector3(35f, -40f, 0f), new Color(1f, 0.95f, 0.86f), 1.15f);
            AddLight(cameraObject.transform, new Vector3(15f, 55f, 0f), new Color(0.70f, 0.78f, 1f), 0.45f);
            AddLight(cameraObject.transform, new Vector3(-20f, 170f, 0f), new Color(1f, 0.85f, 0.65f), 0.7f);

            _floor = Floor.Make(_layer);
            if (_floor != null) _floor.transform.SetParent(_root.transform, true);

            EnsureTexture();
            _camera.targetTexture = _texture;
            return true;
        }

        private static void AddLight(Transform parent, Vector3 euler, Color color, float intensity)
        {
            var lightObject = new GameObject("Scry stage light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localRotation = Quaternion.Euler(euler);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << _layer;
        }

        private static void EnsureTexture()
        {
            if (_texture != null && _texture.width == _width && _texture.height == _height) return;

            if (_texture != null)
            {
                if (_camera != null) _camera.targetTexture = null;
                _texture.Release();
                Object.Destroy(_texture);
            }

            _texture = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                name = "Scry stage",
            };
            _texture.Create();
            if (_camera != null) _camera.targetTexture = _texture;
        }

        /// <summary>The highest layer without a name, which nothing in the game draws on.</summary>
        private static int FreeLayer()
        {
            for (var i = 31; i >= 8; i--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    Plugin.Log.LogInfo($"Scry is using layer {i} for its preview stage.");
                    return i;
                }
            }

            Plugin.Log.LogWarning("Scry found no free layer for its preview stage, so the panel shows no turntable. Showing in the world still works.");
            return -1;
        }
    }
}
