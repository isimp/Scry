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
            if (room == null || holder == null || !holder.activeInHierarchy || !Cutting && !SideCutting || _outlineAway)
            {
                if (_outline != null && _outline.gameObject.activeSelf) _outline.gameObject.SetActive(false);
                return;
            }
            var lift = holder.transform.localPosition.y;
            var roomTop = room.Position.Y + room.Room.Size.Y / 2f;
            var top = TheCut.Held ? RoomOutline.Top(CutAt - lift, roomTop) : roomTop;
            if (_outline != null && _outline.gameObject.activeSelf && ReferenceEquals(room, _outlinedRoom) && top == _outlinedTop) return;
            if (!EnsureOutline()) return;

            Lay(_outline, holder.transform, room, TheExample.FocusGround, top, 1f);
            _outlinedRoom = room;
            _outlinedTop = top;
        }

        /// <summary>
        /// The room a way on the card of the room gone into, or the plan box, points at, lit on the
        /// stage by a fainter, thinner line of the same amber; none. Set again each time the panel is drawn.
        /// </summary>
        public static PlacedRoom LitRoom { get; set; }

        /// <summary>The same amber, fainter, round the room lit from the card.</summary>
        private static readonly Color LitColour = new Color(OutlineColour.r, OutlineColour.g, OutlineColour.b, 0.7f);

        private static LineRenderer _litOutline;
        private static PlacedRoom _litDrawn;
        private static float _litTop = float.NaN;

        /// <summary>Whether the room lit from the card is outlined, for the self-test.</summary>
        [Diagnostic]
        public static bool LitOutlineShown => _litOutline != null && _litOutline.gameObject.activeSelf;

        /// <summary>
        /// Each film: stands the fainter line, half as wide, round the room lit from the card or the plan box, from its ground to
        /// its top or just under the cut; one wholly above the cut, put away, shows its outline
        /// just under the cut, where it lies above. Puts it away with none lit.
        /// </summary>
        private static void KeepLitOutline()
        {
            var room = LitRoom;
            var holder = TheExample.Holder;
            if (room == null || room == TheExample.Focused || holder == null || !holder.activeInHierarchy || _outlineAway)
            {
                if (_litOutline != null && _litOutline.gameObject.activeSelf) _litOutline.gameObject.SetActive(false);
                return;
            }
            var lift = holder.transform.localPosition.y;
            var roomTop = room.Position.Y + room.Room.Size.Y / 2f;
            var top = Cutting ? RoomOutline.Top(CutAt - lift, roomTop) : roomTop;
            if (_litOutline != null && _litOutline.gameObject.activeSelf && ReferenceEquals(room, _litDrawn) && top == _litTop) return;
            if (!EnsureLine(ref _litOutline, "Scry lit room outline", LitColour)) return;
            Lay(_litOutline, holder.transform, room, Mathf.Min(TheExample.GroundOf(room), top), top, 0.5f);
            _litDrawn = room;
            _litTop = top;
        }

        /// <summary>Lays a line round a room's box from a ground to a top, in the example's space, as wide as the view of the room gone into has it.</summary>
        private static void Lay(LineRenderer line, Transform holder, PlacedRoom room, float ground, float top, float width)
        {
            var path = RoomOutline.Path(room.Corners(), ground, top);
            var points = new Vector3[path.Count];
            for (var i = 0; i < path.Count; i++) points[i] = holder.TransformPoint(new Vector3(path[i].X, path[i].Y, path[i].Z));
            line.positionCount = points.Length;
            line.SetPositions(points);
            // As wide as the room gone into's framing has it; with none, as wide on the picture by the camera's distance.
            var reach = TheExample.FocusAcross() is Vector3 across ? across.z
                : _camera != null ? Vector3.Distance(_camera.transform.position, holder.TransformPoint(new Vector3(room.Position.X, room.Position.Y, room.Position.Z))) * 0.27f : 5f;
            line.widthMultiplier = Mathf.Max(0.02f, reach * 0.012f) * width;
            line.gameObject.SetActive(true);
        }

        /// <summary>Makes the outline the first time a room is gone into; false where no shader to draw it with is loaded.</summary>
        private static bool EnsureOutline() => EnsureLine(ref _outline, "Scry room outline", OutlineColour);

        /// <summary>Makes a line drawn over everything in a colour, the first time it is wanted; false where no shader to draw it with is loaded.</summary>
        private static bool EnsureLine(ref LineRenderer line, string name, Color colour)
        {
            if (line != null) return true;
            if (_root == null || OutlineMaterial() == null) return false;
            var made = new GameObject(name) { layer = _layer };
            made.transform.SetParent(_root.transform, false);
            line = made.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.sharedMaterial = _outlineMaterial;
            line.startColor = line.endColor = colour;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
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
            _litOutline = null;
            _litDrawn = null;
            _litTop = float.NaN;
            LitRoom = null;
        }
    }
}
