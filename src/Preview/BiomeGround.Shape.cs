using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Scry
{
    /// <summary>The ground's shape: its mesh laid by the shown place's own modifiers (<see cref="GroundSettle"/>).</summary>
    internal sealed partial class BiomeGround
    {
        /// <summary>The ground the shown place's own modifiers shape, as last told; none for flat ground.</summary>
        private GroundSettle _settle;

        /// <summary>The shape and width the mesh was last laid for.</summary>
        private int _shapeVersion = -1;
        private float _shapeAcross = -1f;

        /// <summary>The flat ground's mesh, kept, and the one made for a shaped ground, made again as the shape changes.</summary>
        private Mesh _flatMesh;
        private Mesh _shapedMesh;

        /// <summary>The ground's shape now: the place's own, or flat.</summary>
        private GroundShape ShapeNow => _settle?.Shape ?? GroundShape.Of(null);

        /// <summary>
        /// Lays the ground's mesh by the place's shape: the flat round piece where nothing shapes it,
        /// else one with rings half a metre apart over the shaped ground, each point as high as the
        /// shape has it there, then the flat piece's rings out to its edge.
        /// </summary>
        private void Reshape()
        {
            if (_terrain == null) return;
            var shape = ShapeNow;
            var filter = _terrain.GetComponent<MeshFilter>();
            if (shape.Flat)
            {
                if (_flatMesh == null) _flatMesh = _mesh;
                _mesh = _flatMesh;
            }
            else
            {
                if (_flatMesh == null) _flatMesh = _mesh;
                if (_shapedMesh != null) Object.Destroy(_shapedMesh);
                _shapedMesh = ShapedDisc(shape, Mathf.Max(1f, _across));
                _mesh = _shapedMesh;
            }
            filter.sharedMesh = _mesh;
            Colour();
        }

        /// <summary>The most rings the shaped ground has over its shaped middle, so the mesh stays within a 16-bit index.</summary>
        private const int MostShapedRings = 160;

        /// <summary>A round piece a metre across like <see cref="Disc"/>, its points as high as the shape has them at the ground's width.</summary>
        private static Mesh ShapedDisc(GroundShape shape, float across)
        {
            var edge = across / 2f;
            var fineOut = Mathf.Min(shape.Reach * 1.42f + 1f, edge);
            var step = Mathf.Max(0.5f, fineOut / MostShapedRings);
            var radii = new List<float>();
            for (var r = step; r < fineOut - 0.001f; r += step) radii.Add(r);
            radii.Add(fineOut);
            for (var k = 1; k <= 8; k++)
            {
                var r = edge * k / 8f;
                if (r > fineOut + 0.001f) radii.Add(r);
            }
            var around = Mathf.Clamp(Mathf.CeilToInt(2f * Mathf.PI * fineOut / 0.5f), 48, 384);
            var vertices = new Vector3[1 + radii.Count * around];
            var uvs = new Vector2[vertices.Length];
            vertices[0] = new Vector3(0f, shape.HeightAt(0f, 0f), 0f);
            uvs[0] = new Vector2(0.5f, 0.5f);
            for (var ring = 0; ring < radii.Count; ring++)
            {
                for (var a = 0; a < around; a++)
                {
                    var angle = a * Mathf.PI * 2f / around;
                    var x = Mathf.Cos(angle) * radii[ring];
                    var z = Mathf.Sin(angle) * radii[ring];
                    var i = 1 + ring * around + a;
                    vertices[i] = new Vector3(x / across, shape.HeightAt(x, z), z / across);
                    uvs[i] = new Vector2(x / across + 0.5f, z / across + 0.5f);
                }
            }
            var triangles = new List<int>();
            for (var a = 0; a < around; a++)
            {
                triangles.Add(0);
                triangles.Add(1 + (a + 1) % around);
                triangles.Add(1 + a);
            }
            for (var ring = 1; ring < radii.Count; ring++)
            {
                for (var a = 0; a < around; a++)
                {
                    var inner = 1 + (ring - 1) * around;
                    var outer = 1 + ring * around;
                    var next = (a + 1) % around;
                    triangles.Add(inner + a);
                    triangles.Add(inner + next);
                    triangles.Add(outer + a);
                    triangles.Add(inner + next);
                    triangles.Add(outer + next);
                    triangles.Add(outer + a);
                }
            }
            var mesh = Kept.Add(new Mesh { name = "Scry stage ground, shaped" });
            if (vertices.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>For the self-test: the height in the world of the mesh's point nearest a point across, or null where no ground is laid.</summary>
        public float? MeshHeightNear(float x, float z)
        {
            if (_terrain == null || !_terrain.activeInHierarchy || _mesh == null) return null;
            var vertices = _mesh.vertices;
            var t = _terrain.transform;
            var best = float.MaxValue;
            float? height = null;
            foreach (var v in vertices)
            {
                var world = t.TransformPoint(v);
                var d = (world.x - x) * (world.x - x) + (world.z - z) * (world.z - z);
                if (d >= best) continue;
                best = d;
                height = world.y;
            }
            return height;
        }
    }
}
