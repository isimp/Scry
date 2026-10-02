using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The floor ruler at the stage's right edge, while a place is shown with floors: a tick for
    /// each floor, the one opened lit, and a bar where the cut is (at the top with the roof on).
    /// A click on a tick opens that floor, a click or drag elsewhere sets the cut there, above
    /// the top floor puts the roof on, and the wheel over it steps a floor up or down.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>How often the ruler has been drawn, for the self-test.</summary>
        public static int RulersDrawn { get; private set; }

        /// <summary>Where the ruler and the stage were last drawn, on the screen, and the heights the ruler spans.</summary>
        private static Rect _rulerScreen;
        private static Rect _stageScreen;
        private static int _stageFrame = -10;
        private static float _rulerLow, _rulerHigh;

        /// <summary>Whether the mouse is over the stage drawn in the last frames, for Page Up and Down.</summary>
        private static bool MouseOverStage(Vector2 mouse) =>
            Time.frameCount - _stageFrame <= 2 && _stageScreen.Contains(GUIUtility.GUIToScreenPoint(mouse));

        private static void NoteStage(Rect stage)
        {
            _stageScreen = new Rect(GUIUtility.GUIToScreenPoint(stage.position), stage.size);
            _stageFrame = Time.frameCount;
        }

        /// <summary>How many times the roof button over the ruler has been drawn, for the self-test.</summary>
        public static int RoofButtonsDrawn { get; private set; }

        private static void FloorRuler(Rect stage)
        {
            var floors = Stage.FloorHeights;
            var cuts = Stage.CutHeights;
            if (!Stage.HasFloors || Stage.Subject == null || cuts.Count != floors.Count) return;

            var e = Event.current;
            var w = U(16f);
            var top = stage.y + U(80f);
            var rect = new Rect(stage.xMax - w - U(10f), top, w, Mathf.Max(U(60f), stage.yMax - U(44f) - top));
            // The roof over the ruler: lit while it is on, a click takes it off or puts it back.
            var roof = new Rect(rect.center.x - U(11f), rect.y - U(30f), U(22f), U(22f));
            var roofOver = roof.Contains(e.mousePosition);
            if (e.type == EventType.Repaint)
            {
                Skin.Icon(roof, Skin.Roof, !Stage.Cutting ? Skin.Accent : roofOver ? Skin.Text : Skin.Dim);
                RoofButtonsDrawn++;
            }
            if (roofOver) AskTip("roof", Stage.Cutting ? "Cut open over a floor: click to put the roof back on" : "Roof on: click to take it off, cutting away what is above head height over a floor");
            if (GUI.Button(roof, GUIContent.none, GUIStyle.none)) Stage.ToggleRoof();

            var low = Mathf.Min(floors[floors.Count - 1], Stage.ModelBottom) - 0.5f;
            var high = Mathf.Max(cuts[0], Stage.ModelTop) + 1f;
            _rulerLow = low;
            _rulerHigh = high;
            _rulerScreen = new Rect(GUIUtility.GUIToScreenPoint(rect.position), rect.size);
            float Y(float height) => rect.yMax - (height - low) / Mathf.Max(0.01f, high - low) * rect.height;
            var roofY = Y(cuts[0] + 0.5f);

            if (e.type == EventType.Repaint)
            {
                RulersDrawn++;
                Skin.Fill(new Rect(rect.center.x - U(1f), rect.y, U(2f), rect.height), new Color(1f, 1f, 1f, 0.22f));
                for (var i = 0; i < floors.Count; i++)
                {
                    var y = Y(floors[i]);
                    Skin.Fill(new Rect(rect.x, y - U(1f), rect.width, U(2f)), i == Stage.CutLevel ? Skin.Accent : new Color(1f, 1f, 1f, 0.6f));
                }
                var cutY = Stage.Cutting ? Y(Stage.CutAt) : rect.y;
                Skin.Fill(new Rect(rect.x - U(4f), cutY - U(1.5f), rect.width + U(8f), U(3f)), Skin.Accent);

                // The floor opened named beside the cut, with an example's rooms on it.
                var named = Stage.Cutting ? PlaceView.FloorLabel(Stage.CutLevel, floors.Count, Stage.ExampleRoomsOnFloor) : null;
                if (named != null)
                {
                    var labelW = Skin.Width(Skin.DimLabel, named) + U(12f);
                    var label = new Rect(rect.x - U(8f) - labelW, Mathf.Clamp(cutY - U(11f), stage.y, stage.yMax - U(22f)), labelW, U(22f));
                    Skin.Fill(label, new Color(Skin.Stage.r, Skin.Stage.g, Skin.Stage.b, 0.75f));
                    GUI.Label(new Rect(label.x + U(6f), label.y, label.width - U(6f), label.height), named, Skin.DimLabel);
                }
            }

            var area = new Rect(rect.x - U(6f), rect.y - U(6f), rect.width + U(12f), rect.height + U(12f));
            if (!area.Contains(e.mousePosition)) return;
            var near = -1;
            for (var i = 0; i < floors.Count; i++)
            {
                if (Mathf.Abs(Y(floors[i]) - e.mousePosition.y) <= U(6f)) near = i;
            }
            AskTip("ruler", near >= 0 ? PlaceView.CutLabel(near, floors.Count) + ": click to open it"
                : e.mousePosition.y < roofY ? "Click to put the roof on" : "Click or drag to cut here; the wheel or Page Up and Down step a floor");
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                if (near >= 0) Stage.OpenLevel(near);
                else RulerAt(GUIUtility.GUIToScreenPoint(e.mousePosition).y);
                _drag = Drag.Ruler;
                e.Use();
            }
            else if (e.type == EventType.ScrollWheel)
            {
                Stage.StepCut(e.delta.y > 0f);
                e.Use();
            }
        }

        /// <summary>The cut set at a height on the ruler, by the mouse's place on the screen; above the top floor's cut, the roof on.</summary>
        private static void RulerAt(float screenY)
        {
            var cuts = Stage.CutHeights;
            if (cuts.Count == 0 || _rulerScreen.height <= 0f) return;
            var share = Mathf.Clamp01((_rulerScreen.yMax - screenY) / _rulerScreen.height);
            var height = _rulerLow + share * (_rulerHigh - _rulerLow);
            if (height > cuts[0] + 0.5f) Stage.OpenLevel(cuts.Count);
            else Stage.CutTo(height);
        }
    }
}
