using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scry
{
    /// <summary>
    /// The ghosts of a dungeon's rooms put away above what is open: each room's footprint at its
    /// floor, faint thin lines in one mesh drawn with the outlines past the cut
    /// (<see cref="RoomOutline.Ghost"/>), so a room taken off the stage never just vanishes. Laid
    /// again only as the rooms put away change.
    /// </summary>
    internal static partial class Stage
    {
        /// <summary>The ghosts' colour, the panel's text faint.</summary>
        private static readonly Color GhostColour = new Color(0.93f, 0.91f, 0.87f, 0.35f);

        private static Mesh _ghostMesh;
        private static MeshRenderer _ghostRenderer;

        /// <summary>The rooms put away the ghosts were last laid for, and where the example then stood.</summary>
        private static readonly List<PlacedRoom> GhostsOf = new List<PlacedRoom>();
        private static readonly List<PlacedRoom> PutAwayNow = new List<PlacedRoom>();
        private static Vector3 _ghostsAt;

        /// <summary>Whether ghosts stand for rooms put away now.</summary>
        private static bool GhostsShown => _ghostRenderer != null && _ghostRenderer.gameObject.activeSelf;

        /// <summary>How many rooms the ghosts stand for, for the self-test; none while none show.</summary>
        [Diagnostic, ForTest]
        public static int GhostsDrawn => GhostsShown ? GhostsOf.Count : 0;

        /// <summary>Each film: lays the ghosts of the rooms put away inside a dungeon's example, again only as they change; none outside one.</summary>
        private static void KeepGhosts()
        {
            var holder = TheExample.Holder;
            if (holder == null || !holder.activeInHierarchy || !TheExample.IsDungeon || _outlineAway)
            {
                PutGhostsAway();
                return;
            }
            TheExample.PutAway(PutAwayNow);
            if (PutAwayNow.Count == 0)
            {
                PutGhostsAway();
                return;
            }
            var at = holder.transform.position;
            if (GhostsShown && at == _ghostsAt && SameRooms(PutAwayNow, GhostsOf)) return;
            if (!EnsureGhosts()) return;
            GhostsOf.Clear();
            GhostsOf.AddRange(PutAwayNow);
            _ghostsAt = at;
            var vertices = new List<Vector3>(GhostsOf.Count * 8);
            var root = _root.transform;
            foreach (var room in GhostsOf)
            {
                foreach (var end in RoomOutline.Ghost(room.Corners(), TheExample.GroundOf(room)))
                    vertices.Add(root.InverseTransformPoint(holder.transform.TransformPoint(new Vector3(end.X, end.Y, end.Z))));
            }
            var indices = new int[vertices.Count];
            var colours = new Color[vertices.Count];
            for (var i = 0; i < indices.Length; i++)
            {
                indices[i] = i;
                colours[i] = GhostColour;
            }
            _ghostMesh.Clear();
            _ghostMesh.SetVertices(vertices);
            _ghostMesh.colors = colours;
            _ghostMesh.SetIndices(indices, MeshTopology.Lines, 0);
            _ghostMesh.RecalculateBounds();
            _ghostRenderer.gameObject.SetActive(true);
        }

        private static bool SameRooms(List<PlacedRoom> a, List<PlacedRoom> b)
        {
            if (a.Count != b.Count) return false;
            for (var i = 0; i < a.Count; i++) if (!ReferenceEquals(a[i], b[i])) return false;
            return true;
        }

        private static void PutGhostsAway()
        {
            if (_ghostRenderer != null && _ghostRenderer.gameObject.activeSelf) _ghostRenderer.gameObject.SetActive(false);
            GhostsOf.Clear();
        }

        /// <summary>Makes the ghosts' mesh the first time a room is put away; false where no shader to draw it with is loaded.</summary>
        private static bool EnsureGhosts()
        {
            if (_ghostRenderer != null) return true;
            if (_root == null || OutlineMaterial() == null) return false;
            var made = new GameObject("Scry ghost rooms") { layer = OutlineLayer };
            made.transform.SetParent(_root.transform, false);
            _ghostMesh = Kept.Add(new Mesh { name = "Scry ghost rooms" });
            made.AddComponent<MeshFilter>().sharedMesh = _ghostMesh;
            _ghostRenderer = made.AddComponent<MeshRenderer>();
            _ghostRenderer.sharedMaterial = _outlineMaterial;
            _ghostRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _ghostRenderer.receiveShadows = false;
            made.SetActive(false);
            return true;
        }

        /// <summary>Lets go of the ghosts, taken down with the stage.</summary>
        private static void ForgetGhosts()
        {
            _ghostRenderer = null;
            _ghostMesh = null;
            GhostsOf.Clear();
        }
    }
}
