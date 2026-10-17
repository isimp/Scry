using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The floor ruler at the stage's right edge, while a place is shown with floors: a tick for
    /// each floor, the one opened lit, and a bar where the cut is (at the top with the roof on).
    /// A click on a tick opens that floor, a click or drag elsewhere sets the cut there, above
    /// all the place has puts the roof on, and the wheel over it steps a floor up or down. In a
    /// room of a dungeon gone into, cut from above, it is the room's (<see cref="RoomRuler"/>):
    /// its own levels and the levels of the rooms above it, the cut kept within it and the roof
    /// its own, so nothing on it leaves the room; cut from the side, there is none.
    /// </summary>
    internal static partial class ScryPanel
    {

        /// <summary>Where the ruler and the stage were last drawn, on the screen, and the heights the ruler spans.</summary>
        private static Rect _rulerScreen;
        private static Rect _stageScreen;
        private static int _stageFrame = -10;
        private static float _rulerLow, _rulerHigh;

        /// <summary>Whether the room's ruler was drawn last, rather than the floors', for the self-test.</summary>
        [Diagnostic]
        public static bool RoomRulerShown { get; private set; }

        /// <summary>Where the ruler's bar was drawn last and the ruler's own top and bottom, on the panel, for the self-test to tell the bar stays on it.</summary>
        [Diagnostic]
        public static (float Bar, float Top, float Bottom) RulerBarForTest { get; private set; }

        /// <summary>Whether the mouse is over the stage drawn in the last frames, for Page Up and Down.</summary>
        private static bool MouseOverStage(Vector2 mouse) =>
            Time.frameCount - _stageFrame <= 2 && _stageScreen.Contains(GUIUtility.GUIToScreenPoint(mouse));

        private static void NoteStage(Rect stage)
        {
            _stageScreen = OnScreen(stage);
            _stageFrame = Time.frameCount;
        }

        /// <summary>Steps the cut as Page Up and Down do: in a room gone into, its ruler's marks; else a floor.</summary>
        private static void StepCutOrRoom(bool down)
        {
            if (Stage.RoomGoneInto != null && Stage.ShowsInside)
            {
                Stage.StepRoomCut(down);
                return;
            }
            Stage.StepCut(down);
        }

        /// <summary>Where the ruler stands at the stage's right edge, and its roof button over it.</summary>
        private static Rect RulerRect(Rect stage, out Rect roof)
        {
            var w = U(16f);
            var top = stage.y + U(80f);
            var rect = new Rect(stage.xMax - w - U(10f), top, w, Mathf.Max(U(60f), stage.yMax - U(44f) - top));
            roof = new Rect(rect.center.x - U(11f), rect.y - U(30f), U(22f), U(22f));
            return rect;
        }

        private static void FloorRuler(Rect stage)
        {
            var floors = Stage.FloorHeights;
            var cuts = Stage.CutHeights;
            if (!Stage.HasFloors || Stage.Subject == null || cuts.Count != floors.Count) return;
            // In a room gone into, the ruler is the room's; cut from the side, it has no height to cut at.
            if (Stage.RoomGoneInto != null && Stage.HasInside && Stage.ShowsInside)
            {
                if (Stage.RoomRulerNow is RoomRuler ruler) RoomRulerDrawn(stage, ruler);
                return;
            }
            if (Event.current.type == EventType.Repaint) RoomRulerShown = false;

            var e = Event.current;
            var rect = RulerRect(stage, out var roof);
            // The roof over the ruler: lit while it is on, a click takes it off or puts it back.
            var roofOver = roof.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                Skin.Icon(roof, Skin.Roof, !Stage.Cutting ? Skin.Accent : roofOver ? Skin.Text : Skin.Dim);
                CountDrawn(PanelPart.RoofButton);
            }
            if (roofOver) AskTip("roof", StageWords.RoofTip(Stage.Cutting));
            if (GUI.Button(roof, GUIContent.none, GUIStyle.none)) Stage.ToggleRoof();

            var low = Mathf.Min(floors[floors.Count - 1], Stage.ModelBottom) - 0.5f;
            var high = Mathf.Max(cuts[0], Stage.ModelTop) + 1f;
            _rulerLow = low;
            _rulerHigh = high;
            _rulerScreen = OnScreen(rect);
            float Y(float height) => rect.yMax - (height - low) / Mathf.Max(0.01f, high - low) * rect.height;
            var roofY = Y(PlaceView.RoofAbove(cuts[0], Stage.ModelTop));

            if (e.type == EventType.Repaint)
            {
                CountDrawn(PanelPart.Ruler);
                StagePiece("ruler", new Rect(rect.x - U(4f), roof.y, rect.width + U(8f), rect.yMax - roof.y));
                Skin.Fill(new Rect(rect.center.x - U(1f), rect.y, U(2f), rect.height), Skin.Light(0.22f));
                for (var i = 0; i < floors.Count; i++)
                {
                    var y = Y(floors[i]);
                    // Opened by the plan's levels, a level's first mark whole, the rest of its groups shorter.
                    var tickW = Stage.ExampleMark(i) is (bool begins, string _) && !begins ? rect.width * 0.6f : rect.width;
                    Skin.Fill(new Rect(rect.center.x - tickW / 2f, y - U(1f), tickW, U(2f)), i == Stage.CutLevel ? Skin.Accent : Skin.Light(0.6f));
                }
                var cutY = Stage.Cutting ? Y(Stage.CutAt) : rect.y;
                Skin.Fill(new Rect(rect.x - U(4f), cutY - U(1.5f), rect.width + U(8f), U(3f)), Skin.Accent);
                RulerBarForTest = (cutY, rect.y, rect.yMax);

                // The floor opened named beside the cut, with an example's rooms on it.
                var named = Stage.Cutting ? Stage.ExampleMark(Stage.CutLevel)?.Words ?? PlaceView.FloorLabel(Stage.CutLevel, floors.Count, Stage.ExampleRoomsOnFloor) : null;
                if (named != null) RulerLabel(stage, rect, cutY, named);
            }

            var area = new Rect(rect.x - U(6f), rect.y - U(6f), rect.width + U(12f), rect.height + U(12f));
            if (!area.Contains(e.mousePosition)) return;
            var near = -1;
            for (var i = 0; i < floors.Count; i++)
            {
                if (Mathf.Abs(Y(floors[i]) - e.mousePosition.y) <= U(6f)) near = i;
            }
            AskTip("ruler", near >= 0 ? StageWords.FloorTip(Stage.ExampleMark(near)?.Words ?? PlaceView.CutLabel(near, floors.Count)) : StageWords.RulerTip(e.mousePosition.y < roofY));
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (near >= 0) Stage.OpenLevel(near);
                else RulerAt(GUIUtility.GUIToScreenPoint(e.mousePosition).y);
                StartDrag(Drag.Ruler);
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                Stage.StepCut(e.delta.y > 0f);
                e.Use();
            }
        }

        /// <summary>
        /// The ruler of the room gone into: from its lowest level up to the top of the rooms above
        /// it, a whole mark for each of its own levels and a shorter one for each level of the
        /// rooms above, the one the cut stands over lit and named beside the bar; the roof button
        /// puts the room's roof on, bringing the rooms above back, and nothing on it leaves the room.
        /// </summary>
        private static void RoomRulerDrawn(Rect stage, RoomRuler ruler)
        {
            var e = Event.current;
            var rect = RulerRect(stage, out var roof);
            var roofOn = Stage.RoomRoofOn;
            var roofOver = roof.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                RoomRulerShown = true;
                Skin.Icon(roof, Skin.Roof, roofOn ? Skin.Accent : roofOver ? Skin.Text : Skin.Dim);
                CountDrawn(PanelPart.RoofButton);
            }
            if (roofOver) AskTip("roof", StageWords.RoomRoofTip(roofOn));
            if (GUI.Button(roof, GUIContent.none, GUIStyle.none)) Stage.ToggleRoof();

            var low = ruler.Low - 0.5f;
            var high = ruler.High + 0.5f;
            _rulerLow = low;
            _rulerHigh = high;
            _rulerScreen = OnScreen(rect);
            float Y(float height) => rect.yMax - (height - low) / Mathf.Max(0.01f, high - low) * rect.height;
            var lit = roofOn ? -1 : ruler.TickLit(Stage.CutAt, Stage.RoomCutFrom);

            if (e.type == EventType.Repaint)
            {
                CountDrawn(PanelPart.Ruler);
                StagePiece("ruler", new Rect(rect.x - U(4f), roof.y, rect.width + U(8f), rect.yMax - roof.y));
                Skin.Fill(new Rect(rect.center.x - U(1f), rect.y, U(2f), rect.height), Skin.Light(0.22f));
                for (var i = 0; i < ruler.Ticks.Count; i++)
                {
                    var tick = ruler.Ticks[i];
                    var y = Y(tick.Height);
                    var tickW = tick.Own ? rect.width : rect.width * 0.6f;
                    Skin.Fill(new Rect(rect.center.x - tickW / 2f, y - U(1f), tickW, U(2f)), i == lit ? Skin.Accent : Skin.Light(tick.Own ? 0.6f : 0.4f));
                }
                var cutY = roofOn ? rect.y : Mathf.Clamp(Y(Stage.CutAt), rect.y, rect.yMax);
                Skin.Fill(new Rect(rect.x - U(4f), cutY - U(1.5f), rect.width + U(8f), U(3f)), Skin.Accent);
                RulerBarForTest = (cutY, rect.y, rect.yMax);
                RulerLabel(stage, rect, cutY, roofOn ? StageWords.RoomRoofOn : lit >= 0 ? MarkWords(ruler, lit) : null);
            }

            var area = new Rect(rect.x - U(6f), rect.y - U(6f), rect.width + U(12f), rect.height + U(12f));
            if (!area.Contains(e.mousePosition)) return;
            var near = -1;
            for (var i = 0; i < ruler.Ticks.Count; i++)
            {
                if (Mathf.Abs(Y(ruler.Ticks[i].Height) - e.mousePosition.y) <= U(6f)) near = i;
            }
            if (near >= 0)
            {
                var words = MarkWords(ruler, near);
                AskTip("ruler", ruler.Ticks[near].Own ? StageWords.FloorTip(words) : StageWords.RoomsAboveTip(words));
            }
            else AskTip("ruler", StageWords.RoomRulerTip(e.mousePosition.y < Y(ruler.High)));
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (near >= 0) Stage.OpenRoomTick(near);
                else RulerAt(GUIUtility.GUIToScreenPoint(e.mousePosition).y);
                StartDrag(Drag.Ruler);
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                Stage.StepRoomCut(e.delta.y > 0f);
                e.Use();
            }
        }

        /// <summary>What a mark of the room's ruler is called: a level of its own as its card names it, or the rooms above on a level.</summary>
        private static string MarkWords(RoomRuler ruler, int index)
        {
            var tick = ruler.Ticks[index];
            return tick.Own ? RoomCardWords.Level(tick.Height, ruler.Ground) : StageWords.RoomsAboveMark(tick.Rooms, tick.Height - ruler.Ground);
        }

        /// <summary>A name beside the ruler's bar, on a ground of the stage's colour.</summary>
        private static void RulerLabel(Rect stage, Rect rect, float cutY, string named)
        {
            if (named == null) return;
            var labelW = Skin.Width(Skin.DimLabel, named) + U(12f);
            var label = new Rect(rect.x - U(8f) - labelW, Mathf.Clamp(cutY - U(11f), stage.y, stage.yMax - U(22f)), labelW, U(22f));
            Skin.Fill(label, Skin.Alpha(Skin.Stage, 0.75f));
            StagePiece("floor name", label);
            GUI.Label(new Rect(label.x + U(6f), label.y, label.width - U(6f), label.height), named, Skin.DimLabel);
        }

        /// <summary>The cut set at a height on the ruler, by the mouse's place on the screen: in a room gone into, kept within it, over its top its roof on; else above all the place has, the roof on.</summary>
        private static void RulerAt(float screenY)
        {
            if (_rulerScreen.height <= 0f) return;
            var share = Mathf.Clamp01((_rulerScreen.yMax - screenY) / _rulerScreen.height);
            var height = _rulerLow + share * (_rulerHigh - _rulerLow);
            if (Stage.RoomGoneInto != null && Stage.ShowsInside)
            {
                if (Stage.RoomRulerNow is RoomRuler ruler) Stage.CutRoomAt(height > ruler.High ? float.PositiveInfinity : height);
                return;
            }
            var cuts = Stage.CutHeights;
            if (cuts.Count == 0) return;
            if (height > PlaceView.RoofAbove(cuts[0], Stage.ModelTop)) Stage.OpenLevel(cuts.Count);
            else Stage.CutTo(height);
        }
    }
}
