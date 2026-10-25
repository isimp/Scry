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
            if (_hiddenFrom != null && _layer >= 0) _hiddenFrom.cullingMask |= HiddenMask;
            _hiddenFrom = null;
        }

        /// <summary>How often the stage has been filmed, for the self-test to tell it still is.</summary>
        public static int Filmed { get; private set; }

        /// <summary>How many films the last frame took and how long they took together, for the self-test.</summary>
        [Diagnostic]
        public static (int Films, double Ms) LastFilms { get; private set; }

        /// <summary>How long the camera took to film the stage the last time, for the self-test.</summary>
        public static double LastFilmMs { get; private set; }

        /// <summary>Whether the outlines are drawn past the cut, on a layer of their own, for the self-test.</summary>
        [Diagnostic]
        public static bool OutlinesPastCutForTest => OutlinesPastCut;

        /// <summary>How often the outlines have been drawn past the cut, for the self-test.</summary>
        [Diagnostic]
        public static int OutlinesFilmed { get; private set; }

        /// <summary>The command buffer that draws the outlines at the end of a film, and the camera it is laid on.</summary>
        private static CommandBuffer _outlineBuffer;
        private static Camera _outlineBufferOn;

        /// <summary>
        /// Lays the outlines of the room gone into and the room lit in the camera's next film, drawn
        /// at its very end with the camera's view and its projection without the cut, so a room's
        /// whole box shows however low the cut is, at no cost of filming again; the line draws over
        /// everything as it did with the rest. With none to draw, or none past the cut, the film
        /// draws none.
        /// </summary>
        private static void OutlinesInFilm(bool draw)
        {
            if (_camera == null) return;
            if (_outlineBufferOn != _camera)
            {
                if (_outlineBuffer == null) _outlineBuffer = new CommandBuffer { name = "Scry room outlines" };
                if (_outlineBufferOn != null) _outlineBufferOn.RemoveCommandBuffer(CameraEvent.AfterEverything, _outlineBuffer);
                _camera.AddCommandBuffer(CameraEvent.AfterEverything, _outlineBuffer);
                _outlineBufferOn = _camera;
            }
            _outlineBuffer.Clear();
            if (!draw || !OutlinesPastCut || _outlineMaterial == null || !OutlineShown && !LitOutlineShown && !GhostsShown) return;

            // The projection the camera has without a cut, its own put back after.
            var projection = _camera.projectionMatrix;
            _camera.ResetProjectionMatrix();
            var plain = _camera.projectionMatrix;
            if (projection != plain) _camera.projectionMatrix = projection;
            _outlineBuffer.SetViewProjectionMatrices(_camera.worldToCameraMatrix, plain);
            if (OutlineShown) _outlineBuffer.DrawRenderer(_outline, _outlineMaterial);
            if (LitOutlineShown) _outlineBuffer.DrawRenderer(_litOutline, _outlineMaterial);
            if (GhostsShown) _outlineBuffer.DrawRenderer(_ghostRenderer, _outlineMaterial);
            OutlinesFilmed++;
        }

        /// <summary>Films the stage, when the panel showed it in the last couple of frames.</summary>
        public static void Render()
        {
            Expire();
            if (_camera == null || Time.frameCount - _wantedFrame > 2) return;

            // With nothing on the stage there is nothing to film: the panel draws the texture
            // only while a copy is on it, or an effect's stage once its copy has played out.
            if (_subject == null && Played.Count == 0 && _lastShown?.Kind != Kind.Effect) return;

            if (Spin && !Dragging && _subject != null) TurnTo(Yaw + SpinDegreesPerSecond * Time.unscaledDeltaTime, Pitch);

            KeepBeforeFilm();
            // Nothing moved or changed: the last film stays on the panel.
            if (!FilmDue()) return;
            _camera.renderingPath = PathOverride ?? RenderingPath.UsePlayerSettings;
            var step = Timing.Start();

            var mask = StageMask;
            var hidden = HiddenMask;
            var main = GameCamera.instance != null ? GameCamera.instance.GetComponent<Camera>() : null;
            if (main != null && (main.cullingMask & hidden) != 0)
            {
                main.cullingMask &= ~hidden;
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
                step = Timing.Start();
                DrawGrass();
                Timing.Add("render grass", step);
                // The outlines are drawn at the end of the frame's last film.
                var creaturesAgain = _aboveCut != null && CreatureLayer != _layer && Creatures.AnyMade;
                // A dungeon's level opened whole that one cut cannot open is filmed a group at a time.
                var films = FilmsNow();
                step = Timing.Start();
                var filming = System.Diagnostics.Stopwatch.GetTimestamp();
                if (films != null) FilmGroups(films, creaturesAgain);
                else
                {
                    OutlinesInFilm(!creaturesAgain);
                    _camera.Render();
                }
                LastFilmMs = (System.Diagnostics.Stopwatch.GetTimestamp() - filming) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                LastFilms = (films?.Count ?? 1, LastFilmMs);
                Timing.Add(films != null ? "render films" : "render film", step);
                Filmed++;

                // A floor's cut is the near plane laid along it, which cut the creatures with all
                // else. What of them is above it is drawn again over the picture, by a projection
                // that keeps only what is above the cut (StageCamera.AboveCutRow): nothing below
                // the cut can stand before it, as the camera looks from above.
                if (creaturesAgain)
                {
                    OutlinesInFilm(true);
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
                        step = Timing.Start();
                        _camera.Render();
                        Timing.Add("render above cut", step);
                    }
                    finally
                    {
                        _camera.cullingMask = mask;
                        _camera.clearFlags = clear;
                        _camera.projectionMatrix = projection;
                    }
                }
                OutlinesInFilm(false);
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
        /// Films a level of a dungeon's example opened whole, a film for each group of its rooms one
        /// cut opens (<see cref="LevelCuts"/>), from its lowest group up: each with the near plane at
        /// its own cut and only its own rooms, the rooms of the levels below going with the lowest,
        /// each painted over the one before, as the creatures above the cut are; the outlines go
        /// with the last.
        /// </summary>
        private static void FilmGroups(List<(int Group, float Cut)> films, bool creaturesAgain)
        {
            var projection = _camera.projectionMatrix;
            var clear = _camera.clearFlags;
            try
            {
                for (var i = 0; i < films.Count; i++)
                {
                    // The stage's ground goes with the first film alone, so a later one does not paint it over the rooms before.
                    if (i == 1) FilmWithoutStageParts();
                    TheExample.FilmOnly(films[i].Group, withBelow: i == 0, withAbove: i == films.Count - 1);
                    LayCutAt(films[i].Cut);
                    _camera.clearFlags = i == 0 ? clear : CameraClearFlags.Depth;
                    OutlinesInFilm(i == films.Count - 1 && !creaturesAgain);
                    _camera.Render();
                    TheExample.FilmAll();
                }
            }
            finally
            {
                TheExample.FilmAll();
                FilmStagePartsBack();
                _camera.clearFlags = clear;
                _camera.projectionMatrix = projection;
            }
        }

        /// <summary>The stage's own parts left out of a film, its ground and floor, to be let back after.</summary>
        private static readonly List<Renderer> StagePartsOff = new List<Renderer>();
        private static readonly List<Renderer> StageParts = new List<Renderer>();

        /// <summary>Leaves the stage's own parts on its layer, everything under its root but the copy shown, out of the films that follow.</summary>
        private static void FilmWithoutStageParts()
        {
            if (_root == null || _subject == null) return;
            var root = _root.transform;
            for (var c = 0; c < root.childCount; c++)
            {
                var child = root.GetChild(c);
                if (!child.gameObject.activeInHierarchy || _subject.transform.IsChildOf(child)) continue;
                child.GetComponentsInChildren(false, StageParts);
                foreach (var part in StageParts)
                {
                    if (part == null || part.forceRenderingOff || part.gameObject.layer != _layer) continue;
                    part.forceRenderingOff = true;
                    StagePartsOff.Add(part);
                }
            }
            StageParts.Clear();
        }

        private static void FilmStagePartsBack()
        {
            foreach (var part in StagePartsOff) if (part != null) part.forceRenderingOff = false;
            StagePartsOff.Clear();
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
