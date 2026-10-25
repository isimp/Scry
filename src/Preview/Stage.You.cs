using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scry
{
    /// <summary>
    /// Where the player stands, on the stage over the world's own dungeon they are in: an arrow
    /// on the floor facing as they face and a thin post over it, in the accent, one mesh drawn
    /// with the outlines past the cut, so the cut never hides it. Laid again only as the player
    /// moves or turns.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>The arrow's reach ahead of the player, back and to each side, and the post's height, in metres.</summary>
        private const float YouAhead = 3f, YouBack = 2f, YouAside = 2.2f, YouNotch = 1f, YouPost = 6f;

        private static readonly Color YouColour = new Color(0.960f, 0.720f, 0.340f, 1f);

        /// <summary>The arrow's two triangles and the post's line, by the vertices laid.</summary>
        private static readonly int[] YouArrowIndices = { 0, 1, 2, 0, 2, 3 }, YouPostIndices = { 4, 5 };

        private static Mesh _youMesh;
        private static MeshRenderer _youRenderer;
        private static Vector3 _youAt;
        private static float _youYaw = float.NaN;
        private static Vector3 _youHolderAt;

        /// <summary>Whether the player's mark stands on the stage, for the self-test.</summary>
        [Diagnostic]
        public static bool YouMarkShown => _youRenderer != null && _youRenderer.gameObject.activeSelf;

        /// <summary>Each film: lays the player's mark where they stand in the world's own dungeon shown, again only as they move; none elsewhere.</summary>
        private static void KeepYouMark()
        {
            var holder = TheExample.Holder;
            var example = TheExample.Of;
            if (holder == null || !holder.activeInHierarchy || !TheExample.IsDungeon || !ExampleLayouts.YouAreIn || !ReferenceEquals(example, ExampleLayouts.Example)
                || !DungeonWalks.YouStand(out var where, out var facing))
            {
                PutYouAway();
                return;
            }
            var local = example.FromGeneratorPoint(where);
            var at = new Vector3(local.X, local.Y, local.Z);
            var yaw = example.FromGeneratorYaw(facing);
            var holderAt = holder.transform.position;
            if (YouMarkShown && at == _youAt && Mathf.Approximately(yaw, _youYaw) && holderAt == _youHolderAt) return;
            if (!EnsureYou()) return;
            _youAt = at;
            _youYaw = yaw;
            _youHolderAt = holderAt;
            Touch();

            var root = _root.transform;
            var turn = Quaternion.Euler(0f, yaw, 0f);
            var floor = at + new Vector3(0f, 0.05f, 0f);
            Vector3 Laid(Vector3 v) => root.InverseTransformPoint(holder.transform.TransformPoint(v));
            var vertices = new List<Vector3>
            {
                Laid(floor + turn * new Vector3(0f, 0f, YouAhead)),
                Laid(floor + turn * new Vector3(-YouAside, 0f, -YouBack)),
                Laid(floor + turn * new Vector3(0f, 0f, -YouNotch)),
                Laid(floor + turn * new Vector3(YouAside, 0f, -YouBack)),
                Laid(floor),
                Laid(floor + new Vector3(0f, YouPost, 0f)),
            };
            var colours = new Color[vertices.Count];
            for (var i = 0; i < colours.Length; i++) colours[i] = YouColour;
            _youMesh.Clear();
            _youMesh.subMeshCount = 2;
            _youMesh.SetVertices(vertices);
            _youMesh.colors = colours;
            _youMesh.SetIndices(YouArrowIndices, MeshTopology.Triangles, 0);
            _youMesh.SetIndices(YouPostIndices, MeshTopology.Lines, 1);
            _youMesh.RecalculateBounds();
            _youRenderer.gameObject.SetActive(true);
        }

        private static void PutYouAway()
        {
            if (_youRenderer != null && _youRenderer.gameObject.activeSelf) _youRenderer.gameObject.SetActive(false);
            _youYaw = float.NaN;
        }

        /// <summary>Makes the mark's mesh the first time it is laid; false where no shader to draw it with is loaded.</summary>
        private static bool EnsureYou()
        {
            if (_youRenderer != null) return true;
            if (_root == null || OutlineMaterial() == null) return false;
            var made = new GameObject("Scry you are here") { layer = OutlineLayer };
            made.transform.SetParent(_root.transform, false);
            _youMesh = Kept.Add(new Mesh { name = "Scry you are here" });
            made.AddComponent<MeshFilter>().sharedMesh = _youMesh;
            _youRenderer = made.AddComponent<MeshRenderer>();
            _youRenderer.sharedMaterials = new[] { _outlineMaterial, _outlineMaterial };
            _youRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _youRenderer.receiveShadows = false;
            made.SetActive(false);
            return true;
        }

        /// <summary>Lets go of the mark, taken down with the stage.</summary>
        private static void ForgetYou()
        {
            _youRenderer = null;
            _youMesh = null;
            _youYaw = float.NaN;
        }
    }
}
