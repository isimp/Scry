using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scry
{
    /// <summary>Filming the stage each frame: its ground, framing and cut, the fog, ambient light and world's lights kept off it while its camera renders, its grass, and its creatures above a cut drawn again.</summary>
    internal static partial class Stage
    {
        /// <summary>The main camera Scry took the stage's layer from, to give it back when the stage is taken down.</summary>
        private static Camera _hiddenFrom;

        /// <summary>The main camera sees the stage's layer again, as it did before Scry took it, as the stage is taken down.</summary>
        private static void GiveLayerBack()
        {
            if (_hiddenFrom != null && _layer >= 0) _hiddenFrom.cullingMask |= StageMask;
            _hiddenFrom = null;
        }

        /// <summary>Films the stage, when the panel showed it in the last couple of frames.</summary>
        public static void Render()
        {
            Expire();
            if (_camera == null || Time.frameCount - _wantedFrame > 2) return;

            // With nothing on the stage there is nothing to film: the panel draws the texture
            // only while a copy is on it.
            if (_subject == null && Played.Count == 0) return;

            if (Spin && !Dragging && _subject != null) TurnTo(Yaw + SpinDegreesPerSecond * Time.unscaledDeltaTime, Pitch);

            EnsureTexture();
            Settle();
            _camera.renderingPath = PathOverride ?? RenderingPath.UsePlayerSettings;
            if (!Guard.Run(Feature.StageGround, GroundPart, KeepGround)) PutGroundAway();
            Frame();
            KeepCreaturesToCut();

            var mask = StageMask;
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
            var light = (_look ?? Presets[_lighting]).Ambient;

            try
            {
                RenderSettings.fog = false;
                RenderSettings.ambientLight = light;
                RenderSettings.ambientSkyColor = light;
                RenderSettings.ambientEquatorColor = light;
                RenderSettings.ambientGroundColor = light * 0.7f;
                KeepWorldLightsOff(mask);

                _camera.cullingMask = mask;
                // The water reads the depth of what is under it.
                _camera.depthTextureMode = WaterShown ? DepthTextureMode.Depth : DepthTextureMode.None;
                DrawGrass();
                _camera.Render();

                // A floor's cut is the near plane laid along it, which cut the creatures with all
                // else. What of them is above it is drawn again over the picture, by a projection
                // that keeps only what is above the cut (StageCamera.AboveCutRow): nothing below
                // the cut can stand before it, as the camera looks from above.
                if (_aboveCut != null && CreatureLayer != _layer && Creatures.AnyMade)
                {
                    var projection = _camera.projectionMatrix;
                    var clear = _camera.clearFlags;
                    try
                    {
                        _camera.ResetProjectionMatrix();
                        var above = _camera.projectionMatrix;
                        var row = _aboveCut.Value;
                        above.SetRow(2, new Vector4(row.X, row.Y, row.Z, row.W));
                        _camera.projectionMatrix = above;
                        _camera.cullingMask = 1 << CreatureLayer;
                        _camera.clearFlags = CameraClearFlags.Depth;
                        _camera.Render();
                    }
                    finally
                    {
                        _camera.cullingMask = mask;
                        _camera.clearFlags = clear;
                        _camera.projectionMatrix = projection;
                    }
                }
            }
            finally
            {
                RenderSettings.fog = fog;
                RenderSettings.ambientLight = ambient;
                RenderSettings.ambientSkyColor = sky;
                RenderSettings.ambientEquatorColor = equator;
                RenderSettings.ambientGroundColor = ground;
                LetWorldLightsBack();
            }
        }

        /// <summary>
        /// The world's directional lights that reach the stage and the layers each lit before:
        /// the sun, and a lightning strike's flash (<c>Thunder.DoFlash</c> makes one for a moment),
        /// which would light the stage as it lights the world. Kept off the stage's layers while
        /// it is filmed; its own lights are left as they are.
        /// </summary>
        private static readonly List<KeyValuePair<Light, int>> WorldLights = new List<KeyValuePair<Light, int>>();

        private static void KeepWorldLightsOff(int mask)
        {
            WorldLights.Clear();
            // Unity marks it old but still answers it from its own list of lights, without the
            // search through the scene that it suggests in its place, which a frame cannot afford.
#pragma warning disable CS0618
            KeepOff(Light.GetLights(LightType.Directional, _layer), mask);
            if (CreatureLayer != _layer) KeepOff(Light.GetLights(LightType.Directional, CreatureLayer), mask);
#pragma warning restore CS0618
        }

        private static void KeepOff(Light[] lights, int mask)
        {
            foreach (var light in lights)
            {
                if (light == null || light == _key || light == _fill || light == _rim || (light.cullingMask & mask) == 0) continue;
                WorldLights.Add(new KeyValuePair<Light, int>(light, light.cullingMask));
                light.cullingMask &= ~mask;
            }
        }

        private static void LetWorldLightsBack()
        {
            foreach (var pair in WorldLights) if (pair.Key != null) pair.Key.cullingMask = pair.Value;
            WorldLights.Clear();
        }
    }
}
