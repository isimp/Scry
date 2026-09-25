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
        private const float PlayedSeconds = 8f;

        // Prefabs face along +Z, so the camera starts in front of them, a little to one side.
        private const float FrontYaw = 205f;
        private const float FrontPitch = 12f;

        /// <summary>A way of lighting the stage: three lights that turn with the camera, an ambient and a backdrop.</summary>
        private sealed class Lighting
        {
            public string Name;
            public Color Key, Fill, Rim, Ambient, Backdrop;
            public float KeyPower, FillPower, RimPower;
            public Vector3 KeyAngle = new Vector3(35f, -40f, 0f);
        }

        private static readonly Lighting[] Presets =
        {
            new Lighting
            {
                Name = "Studio", Key = new Color(1f, 0.95f, 0.86f), KeyPower = 1.15f, Fill = new Color(0.70f, 0.78f, 1f), FillPower = 0.45f,
                Rim = new Color(1f, 0.85f, 0.65f), RimPower = 0.7f, Ambient = new Color(0.40f, 0.41f, 0.45f), Backdrop = new Color(0.105f, 0.112f, 0.135f),
            },
            new Lighting
            {
                Name = "Day", Key = new Color(1f, 0.96f, 0.88f), KeyPower = 1.35f, KeyAngle = new Vector3(50f, -30f, 0f), Fill = new Color(0.62f, 0.74f, 1f), FillPower = 0.5f,
                Rim = new Color(1f, 0.95f, 0.85f), RimPower = 0.3f, Ambient = new Color(0.52f, 0.56f, 0.62f), Backdrop = new Color(0.38f, 0.50f, 0.64f),
            },
            new Lighting
            {
                Name = "Dusk", Key = new Color(1f, 0.62f, 0.38f), KeyPower = 1.1f, KeyAngle = new Vector3(12f, -50f, 0f), Fill = new Color(0.55f, 0.45f, 0.80f), FillPower = 0.35f,
                Rim = new Color(1f, 0.5f, 0.3f), RimPower = 0.8f, Ambient = new Color(0.33f, 0.27f, 0.36f), Backdrop = new Color(0.22f, 0.15f, 0.20f),
            },
            new Lighting
            {
                Name = "Night", Key = new Color(0.55f, 0.65f, 1f), KeyPower = 0.55f, KeyAngle = new Vector3(40f, -30f, 0f), Fill = new Color(0.2f, 0.25f, 0.45f), FillPower = 0.25f,
                Rim = new Color(0.5f, 0.6f, 1f), RimPower = 0.45f, Ambient = new Color(0.13f, 0.15f, 0.23f), Backdrop = new Color(0.03f, 0.04f, 0.07f),
            },
            new Lighting
            {
                Name = "Cave", Key = new Color(1f, 0.62f, 0.3f), KeyPower = 0.9f, KeyAngle = new Vector3(10f, 60f, 0f), Fill = new Color(0.3f, 0.3f, 0.35f), FillPower = 0.1f,
                Rim = new Color(0.4f, 0.4f, 0.5f), RimPower = 0.2f, Ambient = new Color(0.10f, 0.08f, 0.07f), Backdrop = new Color(0.02f, 0.02f, 0.02f),
            },
        };

        /// <summary>Backdrops to choose from, the first being whatever the lighting brings.</summary>
        private static readonly Color?[] Backdrops =
        {
            null,
            new Color(0.07f, 0.075f, 0.09f),
            new Color(0.35f, 0.36f, 0.38f),
            new Color(0.78f, 0.78f, 0.80f),
        };

        public static readonly string[] LightingNames = { "Studio", "Day", "Dusk", "Night", "Cave" };
        public static readonly string[] BackdropNames = { "Match", "Dark", "Grey", "Light" };

        private static GameObject _root;
        private static Camera _camera;
        private static RenderTexture _texture;
        private static int _layer = -2;
        private static GameObject _floor;
        private static Light _key, _fill, _rim;

        private static GameObject _subject;
        private static GameObject _person;
        private static bool _subjectIsPerson;
        private static Bounds _personBounds;
        private static Bounds _personLocal;
        private static Vector3 _baseScale = Vector3.one;
        private static Bounds _bounds;
        private static float _scale = 1f;
        private static float _madeAt;
        private static int _wantedFrame = -10;
        private static int _width = 512;
        private static int _height = 512;
        private static readonly List<KeyValuePair<GameObject, float>> Played = new List<KeyValuePair<GameObject, float>>();

        public static float Yaw = FrontYaw;
        public static float Pitch = FrontPitch;
        public static float Zoom = 1f;
        public static bool Dragging;

        private static int _lighting;
        private static int _backdrop;
        private static bool _showPerson;

        public static Texture Texture => _texture;
        public static GameObject Subject => _subject;

        /// <summary>Which of <see cref="LightingNames"/> lights the stage.</summary>
        public static int LightingIndex
        {
            get => _lighting;
            set
            {
                _lighting = Mathf.Clamp(value, 0, Presets.Length - 1);
                ApplyLighting();
            }
        }

        /// <summary>Which of <see cref="BackdropNames"/> is behind the model.</summary>
        public static int BackdropIndex
        {
            get => _backdrop;
            set
            {
                _backdrop = Mathf.Clamp(value, 0, Backdrops.Length - 1);
                ApplyLighting();
            }
        }

        /// <summary>A person standing beside the model, to judge its size by.</summary>
        public static bool ShowPerson
        {
            get => _showPerson;
            set
            {
                _showPerson = value;
                PlacePerson();
            }
        }

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
            if (!IsStaged(entry) || !(entry.Source is GameObject)) return;
            if (!Ensure()) return;

            _subjectIsPerson = Looks.IsWorn(entry);
            _subject = Looks.Copy(entry, modifiers, _root.transform, Origin, Quaternion.identity, _layer);
            if (_subject == null) return;

            Tune(_subject, entry.Kind == Kind.Effect);

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
            PlacePerson();
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

        /// <summary>
        /// Plays an effect list on the stage copy, as the game would play it on the prefab: on the
        /// named part of its body when there is one, attached when the list says so. Heard as if
        /// beside you.
        /// </summary>
        public static void PlayList(EffectList list)
        {
            if (_subject == null || list?.m_effectPrefabs == null) return;

            var center = Origin + (_bounds.center - Origin) * _scale;
            foreach (var data in list.m_effectPrefabs)
            {
                if (data == null || !data.m_enabled || data.m_prefab == null || Ghost.IsWholeModel(data.m_prefab)) continue;

                var anchor = _subject.transform;
                var at = center;
                if (!string.IsNullOrEmpty(data.m_childTransform))
                {
                    var child = Utils.FindChild(_subject.transform, data.m_childTransform);
                    if (child != null)
                    {
                        anchor = child;
                        at = child.position;
                    }
                }

                var copy = Ghost.Make(data.m_prefab, data.m_attach ? anchor : _root.transform, at, anchor.rotation, _layer);
                if (copy == null) continue;
                Tune(copy, audible: true);
                Played.Add(new KeyValuePair<GameObject, float>(copy, Time.unscaledTime + PlayedSeconds));
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
            Yaw = FrontYaw;
            Pitch = FrontPitch;
            Zoom = 1f;
        }

        /// <summary>Films the stage, when the panel showed it in the last couple of frames.</summary>
        public static void Render()
        {
            Expire();
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
            var light = Presets[_lighting].Ambient;

            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientLight = light;
                RenderSettings.ambientSkyColor = light;
                RenderSettings.ambientEquatorColor = light;
                RenderSettings.ambientGroundColor = light * 0.7f;
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

        /// <summary>
        /// Writes every renderer on the stage copy to the log: where it sits, whether it is on,
        /// what it draws and how big it is. For finding out why part of a model does not show.
        /// </summary>
        public static string Dump()
        {
            if (_subject == null) return "Nothing is on the stage.";

            var lines = new List<string> { $"Scry stage copy {_subject.name}, layer {_layer}, scale {_scale}:" };
            foreach (var renderer in _subject.GetComponentsInChildren<Renderer>(true))
            {
                var path = renderer.transform == _subject.transform ? renderer.name : Path(renderer.transform);
                var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                var bones = renderer is SkinnedMeshRenderer s ? $", bones {s.bones.Length}, bindposes {(s.sharedMesh != null ? s.sharedMesh.bindposes.Length : 0)}, root {(s.rootBone != null ? s.rootBone.name : "none")}" : "";
                var material = renderer.sharedMaterial != null ? renderer.sharedMaterial.name + " / " + (renderer.sharedMaterial.shader != null ? renderer.sharedMaterial.shader.name : "no shader") : "no material";
                lines.Add($"  {path}: {renderer.GetType().Name}, active {renderer.gameObject.activeInHierarchy}, enabled {renderer.enabled}, layer {renderer.gameObject.layer}, mesh {(mesh != null ? mesh.name : "none")}{bones}, {material}, bounds {renderer.bounds.center} size {renderer.bounds.size}");
            }

            foreach (var line in lines) Plugin.Log.LogInfo(line);
            return $"Wrote {lines.Count - 1} renderers of {_subject.name} to the log.";
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (; t != null && t != _subject.transform; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        public static void ClearSubject()
        {
            if (_subject != null) Object.Destroy(_subject);
            _subject = null;
            foreach (var played in Played) if (played.Key != null) Object.Destroy(played.Key);
            Played.Clear();
        }

        /// <summary>Takes the whole stage down; it is built again the next time it is needed.</summary>
        public static void Clear()
        {
            ClearSubject();
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _camera = null;
            _floor = null;
            _person = null;
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
                _texture = null;
            }
        }

        private static void Expire()
        {
            var now = Time.unscaledTime;
            for (var i = Played.Count - 1; i >= 0; i--)
            {
                if (Played[i].Key != null && now < Played[i].Value) continue;
                if (Played[i].Key != null) Object.Destroy(Played[i].Key);
                Played.RemoveAt(i);
            }
        }

        private static void Frame()
        {
            var subject = new Bounds(Origin + (_bounds.center - Origin) * _scale, _bounds.size * _scale);
            var framed = subject;
            if (_person != null && _person.activeSelf) framed.Encapsulate(_personBounds);

            var center = framed.center;
            var radius = Mathf.Max(0.05f, framed.extents.magnitude);
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
                var floorY = Mathf.Min(subject.min.y, _person != null && _person.activeSelf ? _personBounds.min.y : subject.min.y) - 0.005f;
                _floor.transform.position = new Vector3(center.x, floorY, center.z);
                var size = radius * 3.2f;
                _floor.transform.localScale = new Vector3(size, size, size);
            }
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
            var wanted = new Vector3(subjectMin.x - 0.4f - _personLocal.extents.x, subjectMin.y + _personLocal.extents.y, subjectCenter.z);
            _person.transform.position = wanted - _personLocal.center;
            _personBounds = new Bounds(wanted, _personLocal.size);
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
            if (use.Count == 0) return new Bounds(subject.transform.position + Vector3.up, Vector3.one * 2f);

            var bounds = use[0].bounds;
            for (var i = 1; i < use.Count; i++) bounds.Encapsulate(use[i].bounds);

            // A degenerate or runaway size would put the camera nowhere useful.
            if (bounds.size.sqrMagnitude < 0.0001f || bounds.size.magnitude > 2000f) return new Bounds(subject.transform.position + Vector3.up, Vector3.one * 2f);
            return bounds;
        }

        /// <summary>
        /// Sound on the stage: an effect's is heard as if beside you, anything else stays quiet,
        /// since a creature or a fire humming in your ears while you browse is not a preview.
        /// </summary>
        private static void Tune(GameObject copy, bool audible)
        {
            foreach (var source in copy.GetComponentsInChildren<AudioSource>(true))
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
            _camera.cullingMask = 1 << _layer;
            _camera.fieldOfView = FieldOfView;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            _camera.useOcclusionCulling = false;

            _key = AddLight(cameraObject.transform);
            _fill = AddLight(cameraObject.transform);
            _rim = AddLight(cameraObject.transform);
            ApplyLighting();

            _floor = Floor.Make(_layer);
            if (_floor != null) _floor.transform.SetParent(_root.transform, true);

            EnsureTexture();
            _camera.targetTexture = _texture;
            return true;
        }

        private static void ApplyLighting()
        {
            if (_camera == null) return;
            var preset = Presets[_lighting];

            Set(_key, preset.KeyAngle, preset.Key, preset.KeyPower);
            Set(_fill, new Vector3(15f, 55f, 0f), preset.Fill, preset.FillPower);
            Set(_rim, new Vector3(-20f, 170f, 0f), preset.Rim, preset.RimPower);
            _camera.backgroundColor = Backdrops[_backdrop] ?? preset.Backdrop;
        }

        private static void Set(Light light, Vector3 angle, Color color, float power)
        {
            if (light == null) return;
            light.transform.localRotation = Quaternion.Euler(angle);
            light.color = color;
            light.intensity = power;
        }

        private static Light AddLight(Transform parent)
        {
            var lightObject = new GameObject("Scry stage light");
            lightObject.transform.SetParent(parent, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.cullingMask = 1 << _layer;
            return light;
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
