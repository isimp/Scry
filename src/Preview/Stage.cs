using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The turntable in the panel: a copy of the selected prefab on a stage high above the world,
    /// filmed by a camera of its own into a texture the panel draws.
    ///
    /// The stage sits on a layer nothing in the game uses, which only its own camera and lights
    /// see. The main camera is told to leave that layer alone, the world's directional lights (the
    /// sun, a lightning strike's flash) too while the stage is filmed, and the fog and ambient
    /// light are set for the stage only while its camera renders, then put back.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Stage() => WorldCaches.Register(nameof(Stage), Clear);

        private static readonly Vector3 Origin = new Vector3(0f, 5000f, 0f);
        private const float FieldOfView = 30f;
        private static float SpinDegreesPerSecond => Settings.SpinSpeed;
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
        public static readonly string[] BackdropNames = StageGround.Backdrops;

        /// <summary>How many things the stage holds (its copy, what played on it), for the self-test.</summary>
        public static int Held => _root != null ? _root.transform.childCount : 0;

        /// <summary>How long the size the panel asks for must hold still before the texture is made again at it.</summary>
        private const float ResizeAfter = 0.15f;

        /// <summary>Whether the stage is turned by hand, which stops it spinning meanwhile.</summary>
        public static bool Dragging { get; set; }

        private static int _lighting;
        private static int _backdrop;
        private static bool _showPerson;

        public static Texture Texture => _texture;

        /// <summary>Whether the model turns on its own; starts as the config says.</summary>
        public static bool Spin { get; set; } = Settings.AutoSpin;

        /// <summary>Whether the floor ruled in metres is showing.</summary>
        public static bool ShowsGrid => _grid != null && _grid.activeSelf;

        /// <summary>The size of the model as shown, in metres.</summary>
        public static Vector3 SubjectSize => _bounds.size * _scale;

        /// <summary>What the camera looks at and how far it is from it, and the height of the floor it looks at with one opened, for the self-test.</summary>
        public static Vector3 LookAt => _lookAt;
        public static float LookDistance => _camera != null ? Vector3.Distance(_camera.transform.position, _lookAt) : 0f;
        public static float? LookedFloor => _lookedFloor;

        /// <summary>How far off the pointer may find the floor: a few times what is framed beyond the camera's distance.</summary>
        private static float Farthest => _camera == null ? 0f : (Vector3.Distance(_camera.transform.position, _lookAt) + _frameRadius) * 4f;

        private static Vec3 V(Vector3 v) => new Vec3(v.x, v.y, v.z);

        private static Vector3 U(Vec3 v) => new Vector3(v.X, v.Y, v.Z);

        /// <summary>Where the floor under the model is, as shown: under its body, not what it holds.</summary>
        private static float FloorY
        {
            get
            {
                return Origin.y + (_bodyMinY - Origin.y) * _scale;
            }
        }

        /// <summary>Where the model stands, from its root, as if at size one, for the self-test to see a place stands on its own ground.</summary>
        public static float Ground => _bodyMinY - Origin.y;

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

        /// <summary>Whether bounds are within what the stage camera looks at, for telling why something was not seen.</summary>
        public static bool InView(Bounds bounds)
        {
            return _camera != null && GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(_camera), bounds);
        }

        /// <summary>
        /// Writes every renderer on the stage copy to the log: where it sits, whether it is on,
        /// what it draws and how big it is. For finding out why part of a model does not show.
        /// </summary>
        public static string Dump()
        {
            if (_subject == null) return "Nothing is on the stage.";

            var lines = new List<string> { $"Scry stage copy {_subject.name}, layer {Numbers.Count(_layer)}, scale {Numbers.Amount(_scale)}:" };
            foreach (var renderer in _subject.GetComponentsInChildren<Renderer>(true))
            {
                var path = renderer.transform == _subject.transform ? renderer.name : Path(renderer.transform);
                var mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh
                    : renderer.GetComponent<MeshFilter>().OrNull()?.sharedMesh;
                var bones = renderer is SkinnedMeshRenderer s ? $", bones {Numbers.Count(s.bones.Length)}, bindposes {Numbers.Count((s.sharedMesh != null ? s.sharedMesh.bindposes.Length : 0))}, root {(s.rootBone != null ? s.rootBone.name : "none")}" : "";
                var material = renderer.sharedMaterial != null ? renderer.sharedMaterial.name + " / " + (renderer.sharedMaterial.shader != null ? renderer.sharedMaterial.shader.name : "no shader") : "no material";
                lines.Add($"  {path}: {renderer.GetType().Name}, active {renderer.gameObject.activeInHierarchy}, enabled {renderer.enabled}, layer {Numbers.Count(renderer.gameObject.layer)}, mesh {(mesh != null ? mesh.name : "none")}{bones}, {material}, bounds {Figures.Point(renderer.bounds.center)} size {Figures.Point(renderer.bounds.size)}");
            }

            foreach (var line in lines) Log.Report(line);
            return $"Wrote {Numbers.Count(lines.Count - 1)} renderers of {_subject.name} to the log.";
        }

        private static string Path(Transform t)
        {
            var parts = new List<string>();
            for (; t != null && t != _subject.transform; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
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
