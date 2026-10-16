using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scry
{
    internal static partial class Stage
    {
        /// <summary>The outline's amber, the panel's own accent, so the room gone into is marked as the plan marks a room.</summary>
        public static readonly Color OutlineColour = new Color(0.960f, 0.720f, 0.340f, 1f);

        /// <summary>The amber line round the room gone into (<see cref="RoomOutline"/>), drawn over everything; none until a room is first gone into.</summary>
        private static LineRenderer _outline;

        private static Material _outlineMaterial;

        /// <summary>The room and the outline's top it was last drawn for, so it is laid again only as either changes.</summary>
        private static PlacedRoom _outlinedRoom;
        private static float _outlinedTop = float.NaN;

        /// <summary>Whether the self-test has the outline put away, to film the stage without it.</summary>
        private static bool _outlineAway;

        /// <summary>Whether the outline stands round the room gone into, for the self-test.</summary>
        [Diagnostic]
        public static bool OutlineShown => _outline != null && _outline.gameObject.activeSelf;

        /// <summary>The shader the outline is drawn with, and whether it draws over everything, for the self-test; none before it is first drawn.</summary>
        [Diagnostic]
        public static string OutlineDrawnWith => _outlineMaterial == null ? null : _outlineMaterial.shader.name + (_outlineMaterial.HasProperty("_ZTest") ? ", over everything" : ", hidden by walls");

        /// <summary>Puts the outline away or back, for the self-test to film the stage without it.</summary>
        [Diagnostic]
        public static void OutlineAwayForTest(bool away) => _outlineAway = away;

        /// <summary>Each film: stands the outline round the room gone into, laid again as the room or the cut changes; puts it away with none.</summary>
        private static void KeepOutline()
        {
            var room = TheExample.Focused;
            var holder = TheExample.Holder;
            if (room == null || holder == null || !holder.activeInHierarchy || !Cutting || _outlineAway)
            {
                if (_outline != null && _outline.gameObject.activeSelf) _outline.gameObject.SetActive(false);
                return;
            }
            var lift = holder.transform.localPosition.y;
            var top = RoomOutline.Top(CutAt - lift, room.Position.Y + room.Room.Size.Y / 2f);
            if (_outline != null && _outline.gameObject.activeSelf && ReferenceEquals(room, _outlinedRoom) && top == _outlinedTop) return;
            if (!EnsureOutline()) return;

            var path = RoomOutline.Path(room.Corners(), TheExample.FocusGround, top);
            var points = new Vector3[path.Count];
            for (var i = 0; i < path.Count; i++) points[i] = holder.transform.TransformPoint(new Vector3(path[i].X, path[i].Y, path[i].Z));
            _outline.positionCount = points.Length;
            _outline.SetPositions(points);
            var reach = TheExample.FocusAcross() is Vector3 across ? across.z : 5f;
            _outline.widthMultiplier = Mathf.Max(0.02f, reach * 0.012f);
            _outline.gameObject.SetActive(true);
            _outlinedRoom = room;
            _outlinedTop = top;
        }

        /// <summary>Makes the outline the first time a room is gone into; false where no shader to draw it with is loaded.</summary>
        private static bool EnsureOutline()
        {
            if (_outline != null) return true;
            if (_root == null || OutlineMaterial() == null) return false;
            var line = new GameObject("Scry room outline") { layer = _layer };
            line.transform.SetParent(_root.transform, false);
            _outline = line.AddComponent<LineRenderer>();
            _outline.useWorldSpace = true;
            _outline.sharedMaterial = _outlineMaterial;
            _outline.startColor = _outline.endColor = OutlineColour;
            _outline.numCornerVertices = 2;
            _outline.numCapVertices = 2;
            _outline.shadowCastingMode = ShadowCastingMode.Off;
            _outline.receiveShadows = false;
            _outline.alignment = LineAlignment.View;
            return true;
        }

        /// <summary>
        /// The outline's material, made once: Unity's own shader for plain coloured lines, which
        /// takes how it is tested against what is drawn, set to draw over everything; else the
        /// sprites' shader, which walls hide. Found among the shaders loaded, as a shader named
        /// but not loaded is not found by name in the game.
        /// </summary>
        private static Material OutlineMaterial()
        {
            if (_outlineMaterial != null) return _outlineMaterial;
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                var shaders = Resources.FindObjectsOfTypeAll<Shader>();
                shader = shaders.FirstOrDefault(s => s.name == "Hidden/Internal-Colored");
                if (shader == null) shader = shaders.FirstOrDefault(s => s.name == "Sprites/Default");
            }
            if (shader == null) return null;
            _outlineMaterial = Kept.Add(new Material(shader) { name = "Scry room outline", color = Color.white, renderQueue = 4000 });
            if (_outlineMaterial.HasProperty("_ZTest")) _outlineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
            if (_outlineMaterial.HasProperty("_ZWrite")) _outlineMaterial.SetInt("_ZWrite", 0);
            if (_outlineMaterial.HasProperty("_Cull")) _outlineMaterial.SetInt("_Cull", (int)CullMode.Off);
            return _outlineMaterial;
        }

        /// <summary>Lets go of the outline, taken down with the stage.</summary>
        private static void ForgetOutline()
        {
            _outline = null;
            _outlinedRoom = null;
            _outlinedTop = float.NaN;
        }
    }
}
