using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>Setting the stage up: its layer, camera, lights and texture.</summary>
    internal static partial class Stage
    {
        private static GameObject _root;
        private static Camera _camera;
        private static RenderTexture _texture;
        private static int _layer = -2;

        /// <summary>The layer nothing in the game uses, which the stage is drawn on and falling copies land with.</summary>
        public static int Layer
        {
            get
            {
                if (_layer == -2) _layer = FreeLayer();
                return _layer;
            }
        }

        private static GameObject _floor;
        private static GameObject _ground;
        private static Light _key, _fill, _rim;
        private static GameObject _sky;
        private static GameObject _grid;

        /// <summary>Takes the whole stage down; it is built again the next time it is needed.</summary>
        public static void Clear()
        {
            ClearSubject();
            if (_root != null) Object.Destroy(_root);
            _root = null;
            _camera = null;
            _floor = null;
            ForgetGround();
            _ground = null;
            _grid = null;
            _sky = null;
            _key = _fill = _rim = null;
            ForgetShown();
            ForgetPerson();
            Floor.Release();
            if (_texture != null)
            {
                _texture.Release();
                Object.Destroy(_texture);
                _texture = null;
            }

            GiveLayerBack();
        }

        private static bool Ensure()
        {
            if (_root != null && _camera != null) return true;

            if (_layer == -2) _layer = FreeLayer();
            if (_layer < 0) return false;

            // What a stage taken down with the scene rather than through Clear left behind.
            if (_root != null) Object.Destroy(_root);
            Floor.Release();

            _root = new GameObject("Scry stage");
            _root.transform.position = Origin;

            var cameraObject = new GameObject("Scry stage camera") { layer = _layer };
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

            _ground = new GameObject("Scry stage ground") { layer = _layer };
            _ground.transform.SetParent(_root.transform, false);
            _ground.AddComponent<BoxCollider>();

            _grid = Floor.Surface("Scry stage grid", _layer, Floor.GridTexture());
            TileGridAnew();
            if (_grid != null) _grid.transform.SetParent(_root.transform, true);

            _sky = Floor.Surface("Scry stage sky", _layer, null);
            if (_sky != null) _sky.transform.SetParent(cameraObject.transform, false);
            ApplyLighting();

            EnsureTexture();
            _camera.targetTexture = _texture;
            return true;
        }

        private static void ApplyLighting()
        {
            if (_camera == null) return;
            var preset = LookNow();

            Set(_key, preset.KeyAngle, preset.Key, preset.KeyPower);
            Set(_fill, new Vector3(15f, 55f, 0f), preset.Fill, preset.FillPower);
            Set(_rim, new Vector3(-20f, 170f, 0f), preset.Rim, preset.RimPower);
            _camera.backgroundColor = preset.Backdrop;

            // The plain floor and a biome's ground are kept each frame (KeepGround), as whether
            // there is ground goes with what is shown.
            var sky = StageGround.Sky(_backdrop);
            var grid = StageGround.Grid(_backdrop);
            if (_sky != null)
            {
                _sky.SetActive(sky);
                _sky.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = SkyFor(preset);
            }
            if (_grid != null) _grid.SetActive(grid);

            // On a biome's ground the key light casts shadows, so what stands there sits on it.
            if (_key != null)
            {
                _key.shadows = TheGround.On ? LightShadows.Soft : LightShadows.None;
                _key.shadowStrength = 0.7f;
            }
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
            var lightObject = new GameObject("Scry stage light") { layer = _layer };
            lightObject.transform.SetParent(parent, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.cullingMask = StageMask;
            return light;
        }

        private static void EnsureTexture()
        {
            if (_texture != null)
            {
                if (_texture.width == _width && _texture.height == _height) return;

                // While the panel or the stage is being resized the size changes on every frame;
                // the texture there is drawn stretched until the size holds still, rather than one
                // made and let go on each of them. The camera films at the size's shape meanwhile.
                if (Time.unscaledTime - _sizeSince < ResizeAfter) return;

                if (_camera != null) _camera.targetTexture = null;
                _texture.Release();
                Object.Destroy(_texture);
            }

            _texture = Kept.Add(new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4,
                name = "Scry stage",
            });
            _texture.Create();
            if (_camera != null) _camera.targetTexture = _texture;
        }

        /// <summary>
        /// The layer a place's creatures stand on, so what of them is above a floor's cut can be
        /// drawn again (<see cref="StepCreatures"/>): the next free one below the stage's, else one
        /// of those Unity keeps spare under 8 (7, 6 and 3), which have no name and nothing in the
        /// game draws on; the stage's own where there is none, and then they are cut as all else.
        /// </summary>
        private static int _creatureLayer = -2;
        private static int CreatureLayer
        {
            get
            {
                if (_creatureLayer != -2) return _creatureLayer;
                _creatureLayer = _layer;
                if (_layer < 0) return _creatureLayer;
                var candidates = new List<int>();
                for (var i = _layer - 1; i >= 8; i--) candidates.Add(i);
                candidates.AddRange(SpareLayers);
                foreach (var i in candidates)
                {
                    if (!string.IsNullOrEmpty(LayerMask.LayerToName(i))) continue;
                    _creatureLayer = i;
                    break;
                }
                Log.Note(_creatureLayer != _layer
                    ? $"Scry is using layer {Numbers.Count(_creatureLayer)} for the creatures standing on its stage."
                    : "Scry found no second free layer for the creatures standing on its stage, so a floor's cut cuts them too.");
                return _creatureLayer;
            }
        }

        /// <summary>The layers under 8 that Unity keeps spare, unnamed.</summary>
        private static readonly int[] SpareLayers = { 7, 6, 3 };

        /// <summary>Which layer the creatures stand on, for the self-test to tell.</summary>
        [Diagnostic]
        public static string CreatureLayerTold => _layer < 0 ? "no stage layer" : CreatureLayer == _layer ? $"the stage's own, {Numbers.Count(_layer)}: no second free layer" : $"{Numbers.Count(CreatureLayer)}, the stage's {Numbers.Count(_layer)}";

        /// <summary>The stage's layers: its own and its creatures'.</summary>
        private static int StageMask => _layer < 0 ? 0 : (1 << _layer) | (1 << CreatureLayer);

        /// <summary>The highest layer without a name, which nothing in the game draws on.</summary>
        private static int FreeLayer()
        {
            for (var i = 31; i >= 8; i--)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(i)))
                {
                    Log.Note($"Scry is using layer {Numbers.Count(i)} for its preview stage.");
                    return i;
                }
            }

            Log.Warn("Scry found no free layer for its preview stage, so the panel shows no turntable. Showing in the world still works.");
            return -1;
        }
    }
}
