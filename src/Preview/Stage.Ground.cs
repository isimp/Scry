using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The stage's side of the Ground backdrop: whether a biome's ground lies under the model and
    /// of which biome (the entry's own, or one picked in the View box), told each frame to the
    /// ground itself (<see cref="BiomeGround"/>), which keeps what it makes. Where the world has no
    /// terrain to borrow from yet, or what is shown is underground, the plain floor stays. While
    /// the ground lies, the stage's light and sky take the colours the game's most common weather
    /// gives the biome at the time of day the lighting is set to, and its key light casts shadows.
    /// </summary>
    internal static partial class Stage
    {
        private static readonly Texture2D[] SkyTextures = new Texture2D[5];

        /// <summary>The biome's ground under the model, with what is painted, grows and lies on it.</summary>
        private static readonly BiomeGround TheGround = new BiomeGround();

        /// <summary>The ground's biome as picked in the View box: Auto for the entry's own (<see cref="StageGround.Choices"/>); remembered.</summary>
        public static string GroundChoice
        {
            get => _groundChoice;
            set => _groundChoice = Array.IndexOf(StageGround.Choices, value) >= 0 ? value : StageGround.Choices[0];
        }

        private static string _groundChoice = "Auto";

        /// <summary>For the self-test: a biome whose ground to lay whatever is shown; null for the shown entry's own.</summary>
        public static string GroundBiomeOverride { get; set; }

        /// <summary>For the self-test: a rendering path for the stage's camera to try; null for the game's own setting.</summary>
        public static RenderingPath? PathOverride { get; set; }

        /// <summary>For the self-test: the distance the terrain's shader is given to hide the ground by (<see cref="BiomeGround.HideDistance"/>).</summary>
        [ForTest]
        public static float? GroundHideDistance
        {
            get => TheGround.HideDistance;
            set => TheGround.HideDistance = value;
        }

        /// <summary>For the self-test: what the ground's material holds and how it is drawn.</summary>
        [ForTest]
        public static string GroundMaterialTold() => TheGround.MaterialTold(_camera, InView);

        /// <summary>For the self-test: the biome whose ground is laid now, its paints, its grass and its water.</summary>
        [ForTest]
        public static string GroundShown => TheGround.Shown;
        [ForTest]
        public static int GroundPaintCount => TheGround.PaintCount;
        public static int GrassCount => TheGround.GrassCount;
        public static bool WaterShown => TheGround.WaterShown;

        /// <summary>For the self-test: how far out the grass grows, how near the middle, how far round the model it is kept off, and how far the ground reaches.</summary>
        [ForTest]
        public static (float Farthest, float Nearest, float ShortOut, float Reach) GrassSpread => TheGround.GrassSpread;

        /// <summary>For the self-test: whether the grass is being scattered, the last grass shown meanwhile.</summary>
        [ForTest]
        public static bool GrassScattering => TheGround.Scattering;

        /// <summary>The biome whose ground the model stands on, or null on the plain floor or the grid.</summary>
        public static string GroundUnderfoot => TheGround.On ? TheGround.Biome : null;

        /// <summary>Whether what is shown is underground: a dungeon's inside, or a room of one.</summary>
        private static bool Underground => (_lastShown?.Source is PlaceSource place && place.IsRoom) || ExampleInside;

        /// <summary>Each frame: the ground or the plain floor as the backdrop has it, of the biome of what is shown.</summary>
        private static void KeepGround()
        {
            var biome = GroundBiomeOverride ?? StageGround.Chosen(GroundChoice, _lastShown?.Biomes);
            var changed = TheGround.Keep(StageGround.Ground(_backdrop, Underground), biome, _root, _layer);
            var floor = !StageGround.Grid(_backdrop) && !TheGround.On;
            if (_floor != null && _floor.activeSelf != floor) _floor.SetActive(floor);
            // The light and sky take the biome's colours, and the key light casts shadows, while it is laid.
            if (changed) ApplyLighting();
        }

        /// <summary>
        /// A fault in laying the ground is told once and puts the ground away for the frame, the
        /// plain floor standing in, so the stage is filmed all the same.
        /// </summary>
        private const string GroundPart = "laying the stage's ground";

        private const string SettlePart = "shaping the ground a place stands on";

        /// <summary>After a fault in laying the ground: puts it away for the frame, the plain floor back, so the stage is filmed all the same.</summary>
        private static void PutGroundAway()
        {
            TheGround.PutAway();
            if (_floor != null && !StageGround.Grid(_backdrop)) _floor.SetActive(true);
        }

        /// <summary>
        /// The ground under the model at its height: first the shape the place's own modifiers give it,
        /// with its liquids and settling parts, whatever the backdrop; then the biome's ground the
        /// Ground backdrop lays, put away for the frame where laying it fails.
        /// </summary>
        private static void GroundUnder(float groundY)
        {
            Guard.Run(Feature.PlacesGroundOnStage, SettlePart, KeepSettle, groundY);
            if (!Guard.Run(Feature.StageGround, GroundPart, LayGround, groundY)) PutGroundAway();
        }

        /// <summary>Lays the ground under the model at its height, its edge fading into the backdrop's colour, the horizon's with the sky.</summary>
        private static void LayGround(float groundY)
        {
            var look = _look ?? Presets[_lighting];
            var sky = StageGround.Sky(_backdrop);
            var edge = sky ? look.Horizon : look.Backdrop;
            edge.a = 1f;
            var size = SubjectSize;
            var shortOut = StageGround.ShortGrassOut(Mathf.Max(size.x, size.z) / 2f, size.y, StageGround.StandsAs(_lastShown?.Kind));
            TheGround.Lay(groundY, _lastShown, sky, _frameRadius, shortOut, Origin, edge, _root, _layer, TheSettle);
        }

        private static void DrawGrass()
        {
            // A fast stage leaves its grass out.
            if (Settings.StageQuality == StageQuality.Fast) return;
            TheGround.DrawGrass(_camera, _layer);
        }

        /// <summary>The ground the shown place's own modifiers shape, its liquids and its settling parts, whatever the backdrop.</summary>
        private static readonly GroundSettle TheSettle = new GroundSettle();

        /// <summary>For the self-test: the shaped ground, its middle, the liquids laid and the parts at rest.</summary>
        [ForTest]
        public static GroundSettle GroundSettled => TheSettle;

        /// <summary>For the self-test: the height of the Ground backdrop's own mesh in the world nearest a point across, or null where none is laid.</summary>
        [ForTest]
        public static float? GroundMeshHeightNear(float x, float z) => TheGround.MeshHeightNear(x, z);

        /// <summary>Takes what a copy brings to its ground, once it stands: its paints, its shaping, its liquids and its settling parts.</summary>
        private static void TakeGround(PlaceGround ground, GameObject room)
        {
            if (ground == null) return;
            TheGround.TakePaints(ground.Paints, room);
            TheSettle.Take(ground, room);
        }

        /// <summary>Lets go of what the example's rooms brought to the ground, as its rooms go, or of all of it.</summary>
        private static void ForgetGroundTaken(bool rooms)
        {
            TheGround.ForgetPaints(rooms);
            TheSettle.Forget(rooms);
        }

        /// <summary>Works the shaped ground out again where its modifiers moved, its liquids and settling parts with it; its middle over the model's root at the ground's flat height.</summary>
        private static void KeepSettle(float groundY) => TheSettle.Keep(new Vector3(Origin.x, groundY, Origin.z));

        /// <summary>Lets go of the ground and its look, as the stage is taken down.</summary>
        private static void ForgetGround()
        {
            TheGround.Forget();
            _look = null;
        }

        // ----- The biome's light and sky -----

        /// <summary>The lighting as the stage stands now: the preset chosen, in the colours of the biome laid with the Ground backdrop.</summary>
        private static Lighting _look;

        /// <summary>Works out the lighting as the stage stands now, and keeps it.</summary>
        private static Lighting LookNow() => _look = Look();

        private static readonly Dictionary<string, Texture2D> BiomeSkies = new Dictionary<string, Texture2D>();

        /// <summary>
        /// The lighting chosen, its sun, ambient, backdrop and sky in the colours the game's most
        /// common weather in the biome has at the lighting's time of day (<c>EnvSetup</c>); the
        /// preset itself without a biome laid, for the cave, or where the weather is not known.
        /// </summary>
        private static Lighting Look()
        {
            var preset = Presets[_lighting];
            var biome = TheGround.Biome;
            if (!TheGround.On || biome == null || !(StageGround.TimeFor(preset.Name) is StageGround.Time time)) return preset;
            var weather = BiomeWeather(biome);
            if (weather == null) return preset;
            Color sun, ambient, fog, fogSun;
            var power = preset.KeyPower;
            switch (time)
            {
                case StageGround.Time.Evening:
                    sun = weather.m_sunColorEvening;
                    ambient = Color.Lerp(weather.m_ambColorDay, weather.m_ambColorNight, 0.5f);
                    fog = weather.m_fogColorEvening;
                    fogSun = weather.m_fogColorSunEvening;
                    break;
                case StageGround.Time.Night:
                    sun = weather.m_sunColorNight;
                    ambient = weather.m_ambColorNight;
                    fog = weather.m_fogColorNight;
                    fogSun = weather.m_fogColorSunNight;
                    break;
                default:
                    sun = weather.m_sunColorDay;
                    ambient = weather.m_ambColorDay;
                    fog = weather.m_fogColorDay;
                    fogSun = weather.m_fogColorSunDay;
                    power *= Mathf.Clamp(weather.m_lightIntensityDay / 1.2f, 0.4f, 1.4f);
                    break;
            }
            fog.a = fogSun.a = sun.a = ambient.a = 1f;
            return new Lighting
            {
                Name = preset.Name, Key = sun, KeyPower = power, KeyAngle = preset.KeyAngle, Fill = preset.Fill, FillPower = preset.FillPower,
                Rim = preset.Rim, RimPower = preset.RimPower, Ambient = ambient, Backdrop = fog,
                SkyTop = fog * 0.85f, Horizon = Color.Lerp(fog, fogSun, 0.5f), Ground = fog * 0.5f,
            };
        }

        /// <summary>The weather most common in a biome, as the game weighs its weathers (<c>EnvMan.m_biomes</c>).</summary>
        private static EnvSetup BiomeWeather(string biome)
        {
            if (EnvMan.instance == null || !Enum.TryParse<Heightmap.Biome>(biome, out var wanted)) return null;
            EnvSetup best = null;
            var weight = float.NegativeInfinity;
            foreach (var setup in EnvMan.instance.m_biomes)
            {
                if (setup == null || setup.m_biome != wanted || setup.m_environments == null) continue;
                foreach (var entry in setup.m_environments)
                {
                    if (entry?.m_env == null || entry.m_weight <= weight) continue;
                    best = entry.m_env;
                    weight = entry.m_weight;
                }
            }
            return best;
        }

        /// <summary>The sky for the look: the lighting's own, or one in the biome's colours, made once.</summary>
        private static Texture2D SkyFor(Lighting look)
        {
            if (ReferenceEquals(look, Presets[_lighting]))
            {
                if (SkyTextures[_lighting] == null) SkyTextures[_lighting] = Floor.SkyTexture(look.SkyTop, look.Horizon, look.Ground);
                return SkyTextures[_lighting];
            }
            var key = look.Name + "|" + TheGround.Biome;
            if (!BiomeSkies.TryGetValue(key, out var sky) || sky == null) BiomeSkies[key] = sky = Floor.SkyTexture(look.SkyTop, look.Horizon, look.Ground);
            return sky;
        }
    }
}
