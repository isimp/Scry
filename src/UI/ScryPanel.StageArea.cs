using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The selected entry's stage in the panel: the picture, its handle, the input over it, and the chips and buttons on it.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>The room of the example the mouse is on the stage over, lit on the plan too.</summary>
        private static string _stageRoom;

        /// <summary>
        /// The strip under the stage: dragged, it makes the stage taller or shorter; double-clicked,
        /// it puts the usual height back.
        /// </summary>
        private static void StageHandle(Rect strip)
        {
            var e = Event.current;
            var hover = strip.Contains(e.mousePosition) || _drag == Drag.StageSize;
            var bar = new Rect(strip.center.x - U(24f), strip.y + U(4f), U(48f), U(4f));
            Skin.Fill(bar, hover ? Skin.Dim : Skin.Outline);
            if (strip.Contains(e.mousePosition)) AskTip("stage-size", "Drag to make the stage taller or shorter, double-click for its usual height");

            if (e.type == EventType.MouseDown && e.button == 0 && strip.Contains(e.mousePosition))
            {
                if (e.clickCount == 2) ResetStageSize();
                else StartDrag(Drag.StageSize);
                e.Use();
            }
        }

        /// <summary>The room of the stage's example the left button went down on, gone to if it comes up without a drag.</summary>
        private static string _stageRoomDown;

        /// <summary>Where the stage's picture was last drawn, on the screen, for a drag to tell where on it the mouse is.</summary>
        private static Rect _pictureScreen;

        /// <summary>Where a point of the screen is on the stage's picture, 0 to 1 across and 0 to 1 up.</summary>
        private static Vector2 OnPicture(Vector2 screen) => _pictureScreen.width <= 0f || _pictureScreen.height <= 0f ? new Vector2(0.5f, 0.5f)
            : new Vector2((screen.x - _pictureScreen.x) / _pictureScreen.width, 1f - (screen.y - _pictureScreen.y) / _pictureScreen.height);

        /// <summary>What the stage says while a dungeon's or camp's example is read and built; null once it stands.</summary>
        private static string ExampleProgress(Entry entry)
        {
            if (!(entry.Source is PlaceSource place) || place.IsRoom || place.Contents?.Dungeon == null || !ExampleLayouts.Of(entry)) return null;
            if (ExampleLayouts.Example == null) return DungeonWords.Reading(ExampleLayouts.Read, ExampleLayouts.Total);
            var total = Stage.ExampleRoomsTotal;
            return total > 0 && Stage.ExampleRoomsShown < total ? DungeonWords.Building(Stage.ExampleRoomsShown, total) : null;
        }

        private static void StageArea(Explorer explorer, Entry entry, Rect rect)
        {
            var e = Event.current;
            Skin.Box(rect, Skin.Stage);

            if (Stage.IsStaged(entry))
            {
                var inner = new Rect(rect.x + U(3f), rect.y + U(3f), rect.width - U(6f), rect.height - U(6f));
                if (e.type == EventType.Repaint) Stage.Request((int)inner.width, (int)inner.height);
                _pictureScreen = OnScreen(inner);

                // An effect's stage stands once its copy has played out, empty.
                if ((Stage.Subject != null || entry.Kind == Kind.Effect) && Stage.Texture != null)
                {
                    var drawn = Timing.Start();
                    if (e.type == EventType.Repaint) GUI.DrawTexture(inner, Stage.Texture, ScaleMode.StretchToFill, false);
                    Timing.Add("stage texture", drawn);
                }
                else if (entry.Kind != Kind.Effect)
                {
                    // A location or room is shown once its model has loaded and its copy is made.
                    var note = entry.Source is PlaceSource place ? LocationWords.StageNote(Stage.Building ? PlaceLoad.Loading : PlaceAssets.State(place)) : null;
                    GUI.Label(inner, note ?? "This one could not be previewed.", Skin.CenterDim);
                }

                // The example's plan in the corner, which takes the mouse where it is.
                var plan = Stage.Subject != null ? PlanOverlay(explorer, entry, inner) : Rect.zero;

                // A room of the example under the mouse is named, and a click on it that is not a
                // drag goes to its entry.
                string roomKey = null;
                _stageRoom = null;
                var still = _drag == Drag.None || (_drag == Drag.Orbit && _dragMoved < U(5f));
                if (Stage.ExampleRoomsShown > 0 && still && inner.Contains(e.mousePosition) && !plan.Contains(e.mousePosition))
                {
                    var point = new Vector2((e.mousePosition.x - inner.x) / inner.width, 1f - (e.mousePosition.y - inner.y) / inner.height);
                    var room = Stage.ExampleRoomAt(point);
                    if (room != null)
                    {
                        var key = EntryKeys.For(Kind.Location, room.Room.Name);
                        var known = InCatalog(explorer, key);
                        var name = ShownName(explorer, key, LocationWords.RoomName(room.Room.Name));
                        AskTip("stageroom:" + room.Room.Name, StageWords.RoomTip(name, known));
                        if (known) roomKey = key;
                        _stageRoom = room.Room.Name;
                    }
                }
                if (e.type == EventType.MouseUp && e.button == 0 && _drag == Drag.Orbit && _stageRoomDown != null && _dragMoved < U(5f))
                {
                    var go = _stageRoomDown;
                    _stageRoomDown = null;
                    Go(explorer, go);
                }

                // An effect that has played out plays again on a click of its stage that is not a drag.
                var playedOut = entry.Kind == Kind.Effect && !Previews.Repeat && Stage.Finished;
                if (playedOut && e.type == EventType.MouseUp && e.button == 0 && _drag == Drag.Orbit && _dragMoved < U(5f) && rect.Contains(e.mousePosition)) Previews.Replay();

                // The camera's buttons show only while the mouse is on the stage, as its hint does,
                // so the model is not framed by controls while it is looked at.
                var over = rect.Contains(e.mousePosition) || _drag == Drag.Orbit;
                var viewsW = over ? ViewButtons(inner) : 0f;
                var textW = inner.width - U(24f) - viewsW;
                if (playedOut)
                {
                    PictureNote(inner, textW, StageWords.PlayedOut);
                }
                else if (over)
                {
                    PictureNote(inner, textW, Stage.Cutting ? StageHintCut : StageHint);
                }
                else if (ExampleProgress(entry) is string progress)
                {
                    PictureNote(inner, textW, progress);
                }
                else if (Stage.Subject != null && Stage.ShowsGrid)
                {
                    // On the grid, how big the model is, in the same metres as its squares.
                    var size = Stage.SubjectSize;
                    PictureNote(inner, textW, StageWords.Grid(size.x, size.y, size.z));
                }

                // The stage's own buttons and its floor ruler come before its dragging, which would otherwise take their clicks.
                NoteStage(rect);
                StageButtons(entry, rect);
                FloorRuler(rect);

                if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    if (e.clickCount == 2) Stage.ResetView();
                    _stageRoomDown = e.clickCount == 2 ? null : roomKey;
                    StartDrag(Drag.Orbit);
                    Stage.Dragging = true;
                    e.Use();
                }
                else if (e.type == EventType.MouseDown && e.button == 1 && rect.Contains(e.mousePosition))
                {
                    StartDrag(Drag.Pan);
                    e.Use();
                }
                else if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
                {
                    // With Shift the wheel moves the cut of a place opened, down as it scrolls down;
                    // held, the turn can come sideways. Otherwise it zooms toward what is under the pointer.
                    if (e.shift && Stage.Cutting) Stage.CutBy(-StageCamera.WheelTurn(e.delta.x, e.delta.y) * 0.25f);
                    else Stage.ZoomBy(e.delta.y, OnPicture(GUIUtility.GUIToScreenPoint(e.mousePosition)));
                    e.Use();
                }
            }
            else if (entry.Kind == Kind.Sound)
            {
                var drawn = Timing.Start();
                SoundCard(entry, rect);
                Timing.Add("stage card", drawn);
            }
            else if (entry.Kind == Kind.StatusEffect)
            {
                var drawn = Timing.Start();
                StatusCard(entry, rect);
                Timing.Add("stage card", drawn);
            }
            else if (entry.Kind == Kind.Mod)
            {
                var drawn = Timing.Start();
                ModCard(entry, rect);
                Timing.Add("stage card", drawn);
            }
            else if (entry.Kind == Kind.Biome)
            {
                var drawn = Timing.Start();
                BiomeCard(entry, rect);
                Timing.Add("stage card", drawn);
            }
            else if (entry.Kind == Kind.Raid)
            {
                var drawn = Timing.Start();
                RaidCard(entry, rect);
                Timing.Add("stage card", drawn);
            }
            else
            {
                var middle = new Rect(rect.x + U(30f), rect.y + rect.height / 2f - U(34f), rect.width - U(60f), U(70f));
                GUI.Label(new Rect(middle.x, middle.y, middle.width, U(28f)), "Nothing to see or hear", Skin.Center);
                GUI.Label(new Rect(middle.x, middle.y + U(30f), middle.width, U(40f)),
                    "It has no model, particles, light or sound. Often a spawner or a controller.", Skin.CenterDim);
            }

            KindBadge(entry, new Vector2(rect.x + U(10f), rect.y + U(10f)));
        }

        /// <summary>
        /// A note at the bottom left of the stage's picture, clear of the camera's buttons, on a
        /// dark backing so it reads over any ground or sky; a long one wraps rather than shrinks.
        /// </summary>
        private static void PictureNote(Rect inner, float width, string text)
        {
            var textW = Mathf.Min(width - U(12f), Skin.Width(Skin.PictureNote, text) + U(2f));
            var h = Skin.Height(Skin.PictureNote, text, textW);
            var box = new Rect(inner.x + U(8f), inner.yMax - U(8f) - h - U(6f), textW + U(12f), h + U(6f));
            if (Event.current.type == EventType.Repaint) Skin.Fill(box, Skin.PictureBacking);
            GUI.Label(new Rect(box.x + U(6f), box.y + U(3f), textW, h), text, Skin.PictureNote);
        }

        /// <summary>
        /// Front, side and top views and a fit, in the stage's bottom right corner. Picking a view
        /// holds the model still, so it stays in that view. Returns the width they take.
        /// </summary>
        private static float ViewButtons(Rect inner)
        {
            var h = U(22f);
            var y = inner.yMax - U(8f) - h;
            var x = inner.xMax - U(8f);
            var views = new[] { ("Fit", "Frame it whole again"), ("Top", "Look down on it"), ("Side", "Look at it from the side"), ("Front", "Look at it from the front") };
            foreach (var (name, tip) in views)
            {
                var w = Skin.Width(Skin.Chip, name) + U(2f);
                x -= w;
                var chip = new Rect(x, y, w, h);
                x -= U(4f);
                if (chip.Contains(Event.current.mousePosition)) AskTip("view:" + name, tip);
                if (!GUI.Button(chip, name, Skin.Fitted(Skin.Chip, chip))) continue;

                Stage.View(name);
                if (name != "Fit" && Stage.Spin)
                {
                    Stage.Spin = false;
                    SaveRects();
                }
            }
            return inner.xMax - U(8f) - x;
        }

        /// <summary>
        /// The stage's chips in its top right corner: View, for the spin, backdrop, lighting and
        /// person (<see cref="ViewRows"/>), and what a place shown has to show or not, its
        /// example inside or its entrance, and its creatures. Its roof is put on and taken off
        /// above the ruler, and its plan folds on the plan itself.
        /// </summary>
        private const string StageHint = "Drag to turn, right-drag to move, scroll to zoom where you point, double-click to reset";

        private const string StageHintCut = "Drag to turn, right-drag to move, scroll to zoom where you point, Page Up/Down or the ruler for floors, Shift-scroll to move the cut";

        private static void StageButtons(Entry entry, Rect rect)
        {
            var h = U(24f);
            var y = rect.y + U(10f);
            var x = rect.xMax - U(10f);

            // Everything the row will hold, measured first, so it can move clear of the kind badge:
            // the View chip, and what the place shown has to show or not.
            var texts = new List<string> { "View" };
            if (Stage.HasInside) texts.Add(StageWords.Inside(Stage.Inside));
            if (Stage.HasCreatures) texts.Add("Creatures");
            var total = texts.Sum(t => Skin.Width(Skin.Chip, t) + U(10f));
            if (x - total < rect.x + U(10f) + _badgeWidth + U(10f)) y += h + U(8f);

            Rect last = default;
            bool Chip(string text, bool on, string tip, bool can = true)
            {
                var style = on ? Skin.ChipOn : Skin.Chip;
                var w = Skin.Width(style, text) + U(4f);
                x -= w;
                var chip = new Rect(x, y, w, h);
                last = chip;
                x -= U(6f);
                CountDrawn(PanelPart.StageChip);
                if (chip.Contains(Event.current.mousePosition)) AskTip("stage:" + tip, tip);
                var enabled = GUI.enabled;
                GUI.enabled = enabled && can;
                var clicked = GUI.Button(chip, text, Skin.Fitted(style, chip));
                GUI.enabled = enabled;
                return clicked && can;
            }

            // How it is seen, set and mostly left, in a box of its own.
            var viewing = _viewOpen && _viewFor == entry;
            if (Chip("View", viewing, "How it is seen: spin, backdrop, lighting and a person for size")) ViewToggle(entry, OnScreen(last));
            else if (viewing) ViewPlace(entry, OnScreen(last));
            if (Stage.HasCreatures && Chip("Creatures", Stage.CreaturesShown,
                    StageWords.CreaturesTip(Stage.CreaturesShown)))
            {
                Stage.CreaturesShown = !Stage.CreaturesShown;
                SaveRects();
            }
            if (Stage.HasInside && Chip(StageWords.Inside(Stage.Inside), Stage.Inside, StageWords.InsideTip(Stage.Inside)))
            {
                Stage.Inside = !Stage.Inside;
            }
        }

    }
}
