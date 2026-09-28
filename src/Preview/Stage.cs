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
    internal static partial class Stage
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Stage() => WorldCaches.Register(nameof(Stage), Clear);

        private static readonly Vector3 Origin = new Vector3(0f, 5000f, 0f);
        private const float FieldOfView = 30f;
        private static float SpinDegreesPerSecond => Plugin.SpinSpeed;
        private const float PlayedSeconds = 8f;

        // Prefabs face along +Z, so the camera starts in front of them, a little to one side.
        private const float FrontYaw = 205f;
        private const float FrontPitch = 12f;

        /// <summary>A way of lighting the stage: three lights that turn with the camera, an ambient and a backdrop.</summary>
        private sealed class Lighting
        {
            public string Name;
            public Color Key, Fill, Rim, Ambient, Backdrop, SkyTop, Horizon, Ground;
            public float KeyPower, FillPower, RimPower;
            public Vector3 KeyAngle = new Vector3(35f, -40f, 0f);
        }

        private static readonly Lighting[] Presets =
        {
            new Lighting
            {
                Name = "Studio", Key = new Color(1f, 0.95f, 0.86f), KeyPower = 1.15f, Fill = new Color(0.70f, 0.78f, 1f), FillPower = 0.45f,
                Rim = new Color(1f, 0.85f, 0.65f), RimPower = 0.7f, Ambient = new Color(0.40f, 0.41f, 0.45f), Backdrop = new Color(0.105f, 0.112f, 0.135f),
                SkyTop = new Color(0.16f, 0.17f, 0.21f), Horizon = new Color(0.26f, 0.27f, 0.31f), Ground = new Color(0.08f, 0.085f, 0.10f),
            },
            new Lighting
            {
                Name = "Day", Key = new Color(1f, 0.96f, 0.88f), KeyPower = 1.35f, KeyAngle = new Vector3(50f, -30f, 0f), Fill = new Color(0.62f, 0.74f, 1f), FillPower = 0.5f,
                Rim = new Color(1f, 0.95f, 0.85f), RimPower = 0.3f, Ambient = new Color(0.52f, 0.56f, 0.62f), Backdrop = new Color(0.38f, 0.50f, 0.64f),
                SkyTop = new Color(0.28f, 0.46f, 0.74f), Horizon = new Color(0.78f, 0.84f, 0.90f), Ground = new Color(0.30f, 0.33f, 0.24f),
            },
            new Lighting
            {
                Name = "Dusk", Key = new Color(1f, 0.62f, 0.38f), KeyPower = 1.1f, KeyAngle = new Vector3(12f, -50f, 0f), Fill = new Color(0.55f, 0.45f, 0.80f), FillPower = 0.35f,
                Rim = new Color(1f, 0.5f, 0.3f), RimPower = 0.8f, Ambient = new Color(0.33f, 0.27f, 0.36f), Backdrop = new Color(0.22f, 0.15f, 0.20f),
                SkyTop = new Color(0.18f, 0.14f, 0.30f), Horizon = new Color(0.95f, 0.55f, 0.30f), Ground = new Color(0.15f, 0.10f, 0.10f),
            },
            new Lighting
            {
                Name = "Night", Key = new Color(0.55f, 0.65f, 1f), KeyPower = 0.55f, KeyAngle = new Vector3(40f, -30f, 0f), Fill = new Color(0.2f, 0.25f, 0.45f), FillPower = 0.25f,
                Rim = new Color(0.5f, 0.6f, 1f), RimPower = 0.45f, Ambient = new Color(0.13f, 0.15f, 0.23f), Backdrop = new Color(0.03f, 0.04f, 0.07f),
                SkyTop = new Color(0.01f, 0.02f, 0.05f), Horizon = new Color(0.09f, 0.13f, 0.24f), Ground = new Color(0.02f, 0.02f, 0.03f),
            },
            new Lighting
            {
                Name = "Cave", Key = new Color(1f, 0.62f, 0.3f), KeyPower = 0.9f, KeyAngle = new Vector3(10f, 60f, 0f), Fill = new Color(0.3f, 0.3f, 0.35f), FillPower = 0.1f,
                Rim = new Color(0.4f, 0.4f, 0.5f), RimPower = 0.2f, Ambient = new Color(0.10f, 0.08f, 0.07f), Backdrop = new Color(0.02f, 0.02f, 0.02f),
                SkyTop = new Color(0.02f, 0.02f, 0.02f), Horizon = new Color(0.11f, 0.07f, 0.05f), Ground = new Color(0.03f, 0.02f, 0.02f),
            },
        };

        public static readonly string[] LightingNames = { "Studio", "Day", "Dusk", "Night", "Cave" };
        /// <summary>
        /// What stands behind and under the model: the lighting's own colour, a sky with a horizon
        /// in the lighting's colours, a floor ruled in one-metre squares for judging size, or both.
        /// </summary>
        public static readonly string[] BackdropNames = { "Plain", "Sky", "Grid", "Sky and grid" };

        private static GameObject _root;
        private static Camera _camera;
        private static RenderTexture _texture;
        private static int _layer = -2;
        private static GameObject _floor;
        private static GameObject _ground;
        private static Light _key, _fill, _rim;
        private static GameObject _sky;
        private static GameObject _grid;
        private static readonly Texture2D[] SkyTextures = new Texture2D[5];
        private static Entry _lastShown;

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
        private static float _sizeSince;
        private static readonly List<KeyValuePair<GameObject, float>> Played = new List<KeyValuePair<GameObject, float>>();

        /// <summary>How long the size the panel asks for must hold still before the texture is made again at it.</summary>
        private const float ResizeAfter = 0.15f;

        /// <summary>The main camera Scry took the stage's layer from, to give it back when the stage is taken down.</summary>
        private static Camera _hiddenFrom;

        // Filled again on each use, so looking through a copy each frame makes no garbage.
        private static readonly List<Renderer> Renderers = new List<Renderer>();
        private static readonly List<Renderer> SolidRenderers = new List<Renderer>();
        private static readonly List<Renderer> LooseRenderers = new List<Renderer>();
        private static readonly List<ParticleSystem> Particles = new List<ParticleSystem>();
        private static readonly List<AudioSource> Sources = new List<AudioSource>();
        private static readonly List<Animator> Animators = new List<Animator>();

        public static float Yaw = FrontYaw;
        public static float Pitch = FrontPitch;
        public static float Zoom = 1f;
        public static bool Dragging;

        private static int _lighting;
        private static int _backdrop;
        private static bool _showPerson;

        public static Texture Texture => _texture;

        /// <summary>Whether the model turns on its own; starts as the config says.</summary>
        public static bool Spin = Plugin.AutoSpin;

        /// <summary>Whether the floor ruled in metres is showing.</summary>
        public static bool ShowsGrid => _grid != null && _grid.activeSelf;

        /// <summary>The size of the model as shown, in metres.</summary>
        public static Vector3 SubjectSize => _bounds.size * _scale;

        private static float _gridMetres = -1f;

        // What the camera frames, eased towards what is to be seen, so it does not jump about.
        private static float _frameRadius = -1f;

        // A model's resting pose is not always where its animation takes it (a bat flies lower
        // than it hangs), so what it draws is measured again while its animation first plays.
        private static float _settleUntil;
        private static float _settleFrom;
        private static bool _settled;

        // A creature that walks is measured for a short moment once its animation runs, a flyer
        // over its flight; the floor is under what it draws then.
        private static bool _onFeet;

        /// <summary>How far the view is moved off the model, by dragging with the right button.</summary>
        private static Vector3 _pan;

        /// <summary>Moves the view across the stage, as the mouse drags it, at the distance the camera is from the model.</summary>
        public static void Pan(Vector2 delta)
        {
            if (_camera == null) return;
            var t = _camera.transform;
            var distance = Vector3.Distance(t.position, Origin + _pan);
            var step = distance * 0.0016f;
            _pan += -t.right * delta.x * step + t.up * delta.y * step;
        }

        /// <summary>Where the floor under the model is, as shown: under its body, not what it holds.</summary>
        private static float FloorY
        {
            get
            {
                return Origin.y + (_bodyMinY - Origin.y) * _scale;
            }
        }

        /// <summary>The lowest point of the model's body, or where it stands, as if at size one.</summary>
        private static float _bodyMinY;

        /// <summary>Whether the floor is where the character stands, which what it draws does not move.</summary>
        private static bool _groundFixed;
        private static bool _followEffect;

        /// <summary>The layer nothing in the game uses, which the stage is drawn on and falling copies land with.</summary>
        public static int Layer
        {
            get
            {
                if (_layer == -2) _layer = FreeLayer();
                return _layer;
            }
        }
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
                _backdrop = Mathf.Clamp(value, 0, BackdropNames.Length - 1);
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
            width = Mathf.Clamp(width, 64, 2048);
            height = Mathf.Clamp(height, 64, 2048);
            if (width == _width && height == _height) return;
            _width = width;
            _height = height;
            _sizeSince = Time.unscaledTime;
        }

        /// <summary>Puts a fresh copy of the entry on the stage, with the modifiers applied.</summary>
        public static void Show(Entry entry, Modifiers modifiers)
        {
            ClearSubject();
            if (entry != _lastShown)
            {
                _lastShown = entry;
                ResetView();
            }
            if (!IsStaged(entry) || !(entry.Source is GameObject || entry.Source is StatusEffect)) return;
            if (!Ensure()) return;

            // Making it and dressing it are told as "selection copy" and "selection dress",
            // measuring it and applying the modifiers as "selection measure" and "selection apply".
            _subjectIsPerson = Looks.IsWorn(entry) || entry.Kind == Kind.StatusEffect;
            _subject = Looks.Copy(entry, modifiers, _root.transform, Origin, Quaternion.identity, _layer, "selection");
            if (_subject == null) return;

            Tune(_subject, entry.Kind == Kind.Effect);

            var started = Timing.Start();
            _madeAt = Time.unscaledTime;
            _followEffect = entry.Kind == Kind.Effect;
            var character = (entry.Source as GameObject)?.GetComponent<Character>();
            _onFeet = character != null && !character.m_flying;
            // The first pose, before the animation has run, can stand far from where the model
            // stands after (lying, raised, off to a side), so the size is taken again once it
            // has run a moment; a flying creature is measured over its flight a while longer.
            _settleFrom = Time.unscaledTime + 0.25f;
            _settled = false;
            _settleUntil = _followEffect ? 0f : Time.unscaledTime + (_onFeet ? 0.5f : 1.5f);
            _baseScale = _subject.transform.localScale;
            _bounds = Measure(_subject);
            _bodyMinY = Measure(_subject, body: true).min.y;

            // A character on its feet stands where the game stands it, on the bottom of its
            // capsule (Character's CapsuleCollider resting on the ground), whatever of its model
            // reaches below: a root's base is in the ground, a weapon may hang low.
            var grounded = _subjectIsPerson ? ZNetScene.instance?.GetPrefab("Player") : _onFeet ? entry.Source as GameObject : null;
            var capsule = grounded != null ? grounded.GetComponent<CapsuleCollider>() : null;
            _groundFixed = capsule != null && capsule.direction == 1;
            if (_groundFixed) _bodyMinY = Origin.y + (capsule.center.y - capsule.height / 2f) * _baseScale.y;
            Timing.Add("selection measure", started);

            started = Timing.Start();
            Apply(modifiers);
            Timing.Add("selection apply", started);
        }

        /// <summary>Whether an entry has something to put on the stage.</summary>
        public static bool IsStaged(Entry entry)
        {
            if (entry == null || entry.Kind == Kind.Sound) return false;
            if (entry.Kind == Kind.StatusEffect) return Looks.ShowsOnPerson(entry);
            return !entry.Empty;
        }

        /// <summary>The modifiers that change without making a new copy.</summary>
        public static void Apply(Modifiers modifiers)
        {
            if (_subject == null) return;

            _scale = modifiers.Scale;
            _subject.transform.localScale = _baseScale * modifiers.Scale;
            _subject.GetComponentsInChildren(true, Animators);
            foreach (var animator in Animators) animator.speed = modifiers.AnimationSpeed;
            Animators.Clear();
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
                _subject.GetComponentsInChildren(false, Particles);
                _subject.GetComponentsInChildren(false, Sources);
                var moving = Particles.Count > 0 || Sources.Count > 0;
                var playing = Particles.Exists(p => p.IsAlive(false)) || Sources.Exists(s => s.isPlaying);
                Particles.Clear();
                Sources.Clear();
                return moving && !playing;
            }
        }

        /// <summary>
        /// Plays an effect list on the stage copy, as the game would play it on the prefab: on the
        /// named part of its body when there is one, attached when the list says so. Heard as if
        /// beside you.
        /// </summary>
        public static List<(string, GameObject)> PlayList(EffectList list, Transform part = null, string skip = null, Vector3? point = null)
        {
            var made = new List<(string, GameObject)>();
            if (_subject == null || list?.m_effectPrefabs == null) return made;

            var center = point ?? (part != null ? part.position : Origin + (_bounds.center - Origin) * _scale);
            foreach (var data in list.m_effectPrefabs)
            {
                if (data == null || !data.m_enabled || data.m_prefab == null || data.m_prefab.name == skip) continue;
                var debris = Ghost.IsDebris(data.m_prefab);
                if (!debris && Ghost.IsWholeModel(data.m_prefab)) continue;

                // The game looks for the named part and hangs the effect on it only when it
                // gives a parent; what it places at a point (a hit, a thrown thing's release) it
                // gives none (EffectList.Create).
                var anchor = part != null ? part : _subject.transform;
                var at = center;
                if (point == null && !string.IsNullOrEmpty(data.m_childTransform))
                {
                    var child = Utils.FindChild(anchor, data.m_childTransform);
                    if (child != null)
                    {
                        anchor = child;
                        at = child.position;
                    }
                }

                GameObject copy;
                if (debris)
                {
                    if (!Falling.Ready(_layer)) continue;
                    PlaceGround();
                    copy = Falling.Debris(data.m_prefab, _root.transform, at, anchor.rotation, _layer, _layer);
                }
                else
                {
                    copy = data.m_attach && point == null
                        ? Ghost.MakeOn(data.m_prefab, anchor, at, anchor.rotation, _layer)
                        : Ghost.Make(data.m_prefab, _root.transform, at, anchor.rotation, _layer);
                }
                if (copy == null) continue;
                if (!debris) Ghost.Magnify(copy, _scale);
                Tune(copy, audible: !Previews.WorldHeard);
                Played.Add(new KeyValuePair<GameObject, float>(copy, Time.unscaledTime + PlayedSeconds));
                made.Add((data.m_prefab.name, copy));
            }
            return made;
        }

        /// <summary>
        /// Hangs a copy of a prefab on one of the subject's bones, heard as if beside you. It
        /// goes with the subject, so nothing else has to clear it.
        /// </summary>
        public static GameObject Hang(GameObject prefab, Transform joint)
        {
            if (_subject == null || joint == null) return null;
            var copy = Ghost.MakeOn(prefab, joint, joint.position, joint.rotation, _layer);
            if (copy != null) Tune(copy, audible: !Previews.WorldHeard);
            return copy;
        }

        /// <summary>
        /// Lets the stage copy die as the game lets it: its ragdoll falls where it stood, in its
        /// level's colours and its armour, and lies there as long as the game leaves it before
        /// its parting effect plays and the creature stands again.
        /// </summary>
        public static GameObject Fall(global::Ragdoll ragdoll, GameObject creature, int level, IList<GameObject> gear)
        {
            if (_subject == null || ragdoll == null || Standin.IsDown(_subject) || !Falling.Ready(_layer)) return null;
            PlaceGround();

            var fallen = Falling.Ragdoll(ragdoll, creature, _subject, level, _scale, gear, _root.transform, _layer, _layer);
            if (fallen == null) return null;
            Tune(fallen, audible: !Previews.WorldHeard);

            var seconds = Mathf.Clamp(ragdoll.m_ttl, 3f, 12f);
            Standin.For(fallen, _subject, seconds, ragdoll.m_removeEffect, onStage: true);
            Played.Add(new KeyValuePair<GameObject, float>(fallen, Time.unscaledTime + seconds + 1f));
            return fallen;
        }

        /// <summary>
        /// Destroys the stage copy as the game destroys the prefab: it is gone while what it
        /// leaves behind falls (its parts, its log and stump, the debris its list throws), and then
        /// it stands again.
        /// </summary>
        public static GameObject Destroy(GameObject prefab, EffectList list)
        {
            if (_subject == null || Standin.IsDown(_subject) || !Falling.Ready(_layer)) return null;
            PlaceGround();

            var seconds = Falling.DebrisSeconds(list);
            var away = _camera != null ? Vector3.ProjectOnPlane(_camera.transform.forward, Vector3.up).normalized : Vector3.forward;
            var left = Falling.Breaks(prefab, list) ? Falling.Break(prefab, _subject, _root.transform, _layer, _layer) : null;
            if (left == null)
            {
                left = Falling.Fell(prefab, _subject, _root.transform, _layer, _layer, away);
                if (left != null) seconds = 10f;
            }
            if (left == null)
            {
                left = new GameObject("Scry destroyed");
                left.transform.SetParent(_root.transform, false);
            }
            if (Falling.Leave(prefab, _subject, left.transform, _layer, _layer, away))
            {
                Tune(left, audible: !Previews.WorldHeard);
                seconds = Mathf.Max(seconds, 8f);
            }

            Standin.For(left, _subject, seconds, null, onStage: true);
            Played.Add(new KeyValuePair<GameObject, float>(left, Time.unscaledTime + seconds + 1f));
            return left;
        }

        /// <summary>
        /// Lets the stage copy of something the game leaves to physics (a log, an item) fall from
        /// where it stands and roll or tumble on the ground; it stands again after a while.
        /// </summary>
        public static GameObject LetFall(GameObject prefab)
        {
            if (_subject == null || Standin.IsDown(_subject) || !Falling.Ready(_layer)) return null;
            PlaceGround();
            var away = _camera != null ? Vector3.ProjectOnPlane(_camera.transform.right, Vector3.up).normalized : Vector3.right;
            var loose = Falling.Loose(prefab, _subject, _root.transform, _layer, _layer, away);
            if (loose == null) return null;
            Tune(loose, audible: !Previews.WorldHeard);
            Standin.For(loose, _subject, LooseSeconds, null, onStage: true);
            Played.Add(new KeyValuePair<GameObject, float>(loose, Time.unscaledTime + LooseSeconds + 1f));
            return loose;
        }

        /// <summary>How long something let fall lies before the copy stands again.</summary>
        public const float LooseSeconds = 8f;

        /// <summary>The invisible ground falling copies land on, level with the floor under the model.</summary>
        private static void PlaceGround()
        {
            if (_ground == null) return;
            var subject = new Bounds(Origin + (_bounds.center - Origin) * _scale, _bounds.size * _scale);
            var reach = Mathf.Max(2f, subject.extents.magnitude * 10f);
            _ground.transform.position = new Vector3(subject.center.x, FloorY - 0.5f, subject.center.z);
            _ground.transform.localScale = new Vector3(reach, 1f, reach);
        }

        /// <summary>Whether bounds are within what the stage camera looks at, for telling why something was not seen.</summary>
        public static bool InView(Bounds bounds)
        {
            return _camera != null && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(_camera), bounds);
        }

        public static void Adopt(GameObject copy, float seconds)
        {
            if (copy == null || _root == null) return;
            copy.transform.SetParent(_root.transform, true);
            Ghost.SetLayer(copy.transform, _layer);
            Tune(copy, audible: !Previews.WorldHeard);
            Played.Add(new KeyValuePair<GameObject, float>(copy, Time.unscaledTime + seconds));
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
            _frameRadius = -1f;
            _pan = Vector3.zero;
        }

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

        /// <summary>Films the stage, when the panel showed it in the last couple of frames.</summary>
        public static void Render()
        {
            Expire();
            if (_camera == null || Time.frameCount - _wantedFrame > 2) return;

            // With nothing on the stage there is nothing to film: the panel draws the texture
            // only while a copy is on it.
            if (_subject == null && Played.Count == 0) return;

            if (Spin && !Dragging && _subject != null) Yaw += SpinDegreesPerSecond * Time.unscaledDeltaTime;

            EnsureTexture();
            Settle();
            Frame();

            var mask = 1 << _layer;
            var sun = EnvMan.instance != null ? EnvMan.instance.m_dirLight : null;
            var main = GameCamera.instance != null ? GameCamera.instance.GetComponent<Camera>() : null;
            if (main != null && (main.cullingMask & mask) != 0)
            {
                main.cullingMask &= ~mask;
                _hiddenFrom = main;
            }

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
            _ground = null;
            _person = null;
            _grid = null;
            _sky = null;
            _key = _fill = _rim = null;
            _lastShown = null;
            Floor.Release();
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
                _texture = null;
            }

            // The main camera sees the layer again, as it did before Scry took it.
            if (_hiddenFrom != null && _layer >= 0) _hiddenFrom.cullingMask |= 1 << _layer;
            _hiddenFrom = null;
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

        /// <summary>
        /// Heard as if beside you, or not at all while the world copy is heard. A sound kept
        /// quiet is also kept from playing: the game lets only so many of one sound play at once,
        /// some anywhere at all (`AudioMan.RequestPlaySound`), so a muted one playing here would
        /// keep the world copy's from playing there.
        /// </summary>
        private static void Tune(GameObject copy, bool audible)
        {
            foreach (var source in copy.GetComponentsInChildren<AudioSource>(true))
            {
                if (audible) source.spatialBlend = 0f;
                else source.mute = true;
            }
            if (audible) return;
            foreach (var sfx in copy.GetComponentsInChildren<ZSFX>(true)) sfx.enabled = false;
        }
    }
}
