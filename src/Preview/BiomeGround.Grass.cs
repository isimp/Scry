using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>The ground's grass and small plants: scattered as the game scatters them around you, drawn many times over.</summary>
    internal sealed partial class BiomeGround
    {
        /// <summary>A kind of grass drawn many times over (<c>InstanceRenderer</c>): its mesh and material, whether it casts shadows, and where each stands.</summary>
        private sealed class GrassKind
        {
            public Mesh Mesh;
            public Material Material;
            public ShadowCastingMode Shadows;
            public readonly List<Matrix4x4[]> Batches = new List<Matrix4x4[]>();
            public readonly List<int> Counts = new List<int>();
            public int Count;
        }

        private readonly List<GrassKind> _grass = new List<GrassKind>();
        private readonly List<GameObject> _grassCopies = new List<GameObject>();

        /// <summary>
        /// Scatters the biome's grass and small plants over the ground as the game scatters them
        /// around you (<c>ClutterSystem.GenerateVegPatch</c>): patch by patch, each kind for the
        /// biome with its own dice for the patch (<see cref="StageGround.PatchSeed"/>), its places
        /// kept by its forest, noise, height, slope, sea depth, vegetation and cleared ground rules,
        /// the ground taken at a height typical of the biome (<see cref="StageGround.Altitude"/>).
        /// As many of each kind grow as the game grows at the player's grass quality
        /// (<see cref="StageGround.Amount"/>), short round the model (<see cref="StageGround.StemScale"/>).
        /// It grows out to where the ground has faded away, eighty metres at the most, thinning as
        /// the ground fades (<see cref="StageGround.GrassKept"/>), so grass and ground fade together.
        /// A patch a step, a few milliseconds a frame (<see cref="StepScatter"/>), the grass shown
        /// staying until the new is done, then lifted with the ground where it moved meanwhile.
        /// </summary>
        private IEnumerator ScatterGrass(GameObject root, int layer)
        {
            var system = ClutterSystem.instance;
            if (system == null || root == null || !Enum.TryParse<Heightmap.Biome>(Biome, out var biome))
            {
                ForgetGrass();
                yield break;
            }
            var paints = PaintsNow();
            var ground = Heightmap.m_paintMaskNothing;
            ground.a = StageGround.MaskShare(Biome);
            var bare = (ground.r, ground.g, ground.b, ground.a);
            var altitude = StageGround.Altitude(Biome);
            var depth = _waterOver ?? 0f;
            var size = Mathf.Max(1f, system.m_grassPatchSize);
            var half = size / 2f;
            var reach = Mathf.Min(StageGround.GrassOut(_across), MostGrassReach);
            // The share of the ground's half width each stem stands at, by which it thins; with the
            // reach cut short, thinning as if the ground ended there.
            var thinsBy = reach / 0.95f;
            var patches = Mathf.CeilToInt(reach / size);
            _patchesAll = (2 * patches + 1) * (2 * patches + 1);
            _patchesDone = 0;
            var at = _at;
            // The place's own shape, as it stood when scattering began; heights and slopes from the ground's middle.
            var shape = ShapeNow;
            var shortOut = _shortOut;
            var quality = _quality;
            var farthest = 0f;
            var nearest = float.MaxValue;
            var kinds = new Dictionary<GameObject, GrassKind>();
            for (var px = -patches; px <= patches; px++)
            {
                for (var pz = -patches; pz <= patches; pz++)
                {
                    var cx = at.x + px * size;
                    var cz = at.z + pz * size;
                    _patchesDone++;
                    if (new Vector2(cx - at.x, cz - at.z).magnitude > reach) continue;
                    for (var i = 0; i < system.m_clutter.Count; i++)
                    {
                        var clutter = system.m_clutter[i];
                        if (clutter == null || !clutter.m_enabled || clutter.m_prefab == null || (clutter.m_biome & biome) == 0) continue;
                        UnityEngine.Random.InitState(StageGround.PatchSeed(px, pz, i));
                        var offset = new Vector3(clutter.m_fractalOffset, 0f, 0f);
                        var amount = StageGround.Amount(clutter.m_amount, quality, system.m_amountScale);
                        for (var j = 0; j < amount; j++)
                        {
                            var point = new Vector3(UnityEngine.Random.Range(cx - half, cx + half), 0f, UnityEngine.Random.Range(cz - half, cz + half));
                            float yaw = UnityEngine.Random.Range(0, 360);
                            var kept = UnityEngine.Random.value;
                            var away = new Vector2(point.x - at.x, point.z - at.z).magnitude;
                            if (kept >= StageGround.GrassKept(away / thinsBy)) continue;
                            var (nx, ny, nz) = shape.NormalAt(point.x - at.x, point.z - at.z);
                            if (!Grows(clutter, point, offset, altitude, depth, bare, paints, ny)) continue;
                            _laidAny = true;
                            farthest = Mathf.Max(farthest, away);
                            nearest = Mathf.Min(nearest, away);
                            point.y = clutter.m_snapToWater ? at.y + depth : at.y + Below + shape.HeightAt(point.x - at.x, point.z - at.z);
                            if (clutter.m_randomOffset != 0f) point.y += UnityEngine.Random.Range(0f - clutter.m_randomOffset, clutter.m_randomOffset);
                            // Turned about the ground's facing where it leans with it (ClutterSystem.GenerateVegPatch).
                            var turn = clutter.m_terrainTilt ? Quaternion.AngleAxis(yaw, new Vector3(nx, ny, nz)) : Quaternion.Euler(0f, yaw, 0f);
                            var stem = StageGround.StemScale(away, shortOut);
                            if (clutter.m_instanced) AddGrass(kinds, clutter, point, turn, stem);
                            else if (_scatteredCopies.Count < MostGrassCopies)
                            {
                                var copy = Ghost.Make(clutter.m_prefab, root.transform, point, turn, layer);
                                if (copy == null) continue;
                                copy.transform.localScale *= stem;
                                copy.SetActive(false);
                                _scatteredCopies.Add(copy);
                            }
                        }
                    }
                    yield return null;
                }
            }
            // Done: the new grass in the old's place, lifted where the ground moved meanwhile.
            ForgetGrass();
            _grass.AddRange(kinds.Values);
            foreach (var copy in _scatteredCopies) if (copy != null) copy.SetActive(true);
            _grassCopies.AddRange(_scatteredCopies);
            _scatteredCopies.Clear();
            _farthest = farthest;
            _nearest = nearest;
            if (Mathf.Abs(_at.y - at.y) > 0.0001f) LiftGrass(_at.y - at.y);
        }

        /// <summary>Whether a kind of grass grows at a point of the ground, by the game's rules for it.</summary>
        private static bool Grows(ClutterSystem.Clutter clutter, Vector3 point, Vector3 offset, float altitude, float depth, (float, float, float, float) bare, List<GroundPaint> paints, float facingUp = 1f)
        {
            if (clutter.m_inForest)
            {
                var forest = WorldGenerator.GetForestFactor(point);
                if (forest < clutter.m_forestTresholdMin || forest > clutter.m_forestTresholdMax) return false;
            }
            if (clutter.m_fractalScale > 0f)
            {
                var noise = Utils.Fbm(point * 0.01f * clutter.m_fractalScale + offset, 3, 1.6f, 0.7f);
                if (noise < clutter.m_fractalTresholdMin || noise > clutter.m_fractalTresholdMax) return false;
            }
            // As steep as the ground leans there, the place's own shape on flat ground: what grows only on a slope grows only where it leans.
            if (altitude < clutter.m_minAlt || altitude > clutter.m_maxAlt) return false;
            if (facingUp < Mathf.Cos(Mathf.PI / 180f * clutter.m_maxTilt) || facingUp > Mathf.Cos(Mathf.PI / 180f * clutter.m_minTilt)) return false;
            if (clutter.m_minOceanDepth != clutter.m_maxOceanDepth && (depth < clutter.m_minOceanDepth || depth > clutter.m_maxOceanDepth)) return false;
            var mask = StageGround.Painted(bare, point.x, point.z, paints);
            if (clutter.m_minVegetation != clutter.m_maxVegetation && (mask.A > clutter.m_maxVegetation || mask.A < clutter.m_minVegetation)) return false;
            if (!clutter.m_onCleared || !clutter.m_onUncleared)
            {
                var cleared = mask.R > 0.5f || mask.G > 0.5f || mask.B > 0.5f;
                if ((clutter.m_onCleared && !cleared) || (clutter.m_onUncleared && cleared)) return false;
            }
            return true;
        }

        /// <summary>One stem of a kind drawn many times over, at its size, as the game adds it (<c>InstanceRenderer.AddInstance</c>), times <paramref name="stem"/> where it grows short.</summary>
        private static void AddGrass(Dictionary<GameObject, GrassKind> kinds, ClutterSystem.Clutter clutter, Vector3 point, Quaternion turn, float stem)
        {
            var scale = UnityEngine.Random.Range(clutter.m_scaleMin, clutter.m_scaleMax) * stem;
            if (!kinds.TryGetValue(clutter.m_prefab, out var kind))
            {
                var renderer = clutter.m_prefab.GetComponent<InstanceRenderer>();
                kind = new GrassKind();
                if (renderer != null && renderer.m_mesh != null && renderer.m_material != null)
                {
                    kind.Mesh = renderer.m_mesh;
                    kind.Material = renderer.m_material;
                    kind.Shadows = renderer.m_shadowCasting;
                }
                kinds[clutter.m_prefab] = kind;
            }
            if (kind.Mesh == null) return;
            var instance = Matrix4x4.TRS(point, turn, clutter.m_prefab.GetComponent<InstanceRenderer>().m_scale * scale);
            if (kind.Batches.Count == 0 || kind.Counts[kind.Counts.Count - 1] == 1023)
            {
                kind.Batches.Add(new Matrix4x4[1023]);
                kind.Counts.Add(0);
            }
            var last = kind.Batches.Count - 1;
            kind.Batches[last][kind.Counts[last]] = instance;
            kind.Counts[last]++;
            kind.Count++;
        }

        /// <summary>Moves the grass up or down with the ground.</summary>
        private void LiftGrass(float by)
        {
            foreach (var kind in _grass)
            {
                for (var b = 0; b < kind.Batches.Count; b++)
                {
                    var batch = kind.Batches[b];
                    for (var i = 0; i < kind.Counts[b]; i++) batch[i].m13 += by;
                }
            }
            foreach (var copy in _grassCopies) if (copy != null) copy.transform.position += Vector3.up * by;
        }

        /// <summary>Before the stage is filmed: the grass drawn many times over, for its camera alone.</summary>
        public void DrawGrass(Camera camera, int layer)
        {
            if (!On || camera == null) return;
            foreach (var kind in _grass)
            {
                if (kind.Mesh == null || kind.Material == null) continue;
                for (var b = 0; b < kind.Batches.Count; b++)
                {
                    Graphics.DrawMeshInstanced(kind.Mesh, 0, kind.Material, kind.Batches[b], kind.Counts[b], null, kind.Shadows, true, layer, camera);
                }
            }
        }

        private void ForgetGrass()
        {
            _grass.Clear();
            foreach (var copy in _grassCopies) if (copy != null) Object.Destroy(copy);
            _grassCopies.Clear();
        }
    }
}
