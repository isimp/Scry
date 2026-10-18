using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The selected entry's stage in the panel: the picture, its handle, the input over it, and the chips and buttons on it.</summary>
    internal static partial class ScryPanel
    {
        /// <summary>The name of the room of the example the mouse is on the stage over, lit by its name on a camp's plan.</summary>
        private static string _stageRoom;

        /// <summary>The room of the example the mouse is on the stage over, as last looked; inside a dungeon, lit in the plan box and on the stage; none.</summary>
        private static PlacedRoom _stageHover;

        /// <summary>The point of the stage's picture the self-test points the mouse at (0 to 1 across, 0 to 1 up); none.</summary>
        private static Vector2? _stagePointForTest;

        /// <summary>What stands over the stage, by name, where the panel last drew it, for the self-test to find any two that overlap.</summary>
        [Diagnostic]
        public static List<(string Name, Rect At)> StagePieces { get; } = new List<(string, Rect)>();

        /// <summary>Notes a piece standing over the stage where it is drawn, as the panel paints it.</summary>
        private static void StagePiece(string name, Rect at)
        {
            if (Event.current.type == EventType.Repaint && at.width > 0f && at.height > 0f) StagePieces.Add((name, at));
        }

        /// <summary>Nowhere on the panel, for the mouse while the self-test points it elsewhere.</summary>
        private static readonly Vector2 Nowhere = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

        /// <summary>Whether the player's own mouse counts for nothing in lighting a room, while the self-test points it where it wants.</summary>
        private static bool _mouseAwayForTest;

        /// <summary>Takes the player's own mouse out of lighting rooms on the stage, in the plan box and on the card, as the self-test runs, or lets it back.</summary>
        [Diagnostic]
        public static void MouseAwayForTest(bool away) => _mouseAwayForTest = away;

        /// <summary>The player's own mouse for lighting a room: nowhere while the self-test takes it out.</summary>
        private static Vector2 OwnMouse(Event e) => _mouseAwayForTest ? Nowhere : e.mousePosition;

        /// <summary>Points the mouse at a point of the stage's picture (0 to 1 across, 0 to 1 up), as the self-test does, until let go with none; the mouse in the plan box counts for nothing meanwhile.</summary>
        [Diagnostic]
        public static void HoverStageForTest(Vector2? point) => _stagePointForTest = point;

        /// <summary>The room the stage found under the mouse when last looked, for the self-test.</summary>
        [Diagnostic]
        public static PlacedRoom StageRoomHovered => _stageHover;

        /// <summary>Where the mouse is for finding the room under it on the stage: the self-test's point of the picture while it points at the stage, nowhere while it points at the plan box, else the player's own.</summary>
        private static Vector2 StageMouse(Event e, Rect inner) =>
            _stagePointForTest is Vector2 at ? new Vector2(inner.x + at.x * inner.width, inner.y + (1f - at.y) * inner.height)
            : _boxPointForTest.HasValue ? Nowhere : OwnMouse(e);

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

        /// <summary>The same room as placed, gone into if it comes up without a drag inside a dungeon's example.</summary>
        private static PlacedRoom _stageRoomDownAt;

        /// <summary>Where the mouse was as the room last come out of was first outlined, which it stays until the mouse moves from there.</summary>
        private static Vector2? _cameOutMouse;

        /// <summary>
        /// A click on a room of the stage's example: inside a dungeon's, it picks the room
        /// (<see cref="Stage.Pick"/>), outlined and shown on the card, a double-click going into
        /// it; in a camp's, it goes to its entry where that is known.
        /// </summary>
        private static void ClickRoom(Explorer explorer, PlacedRoom room, string key)
        {
            if (Stage.HasInside && room != null) Stage.Pick(room);
            else if (key != null) Go(explorer, key);
        }

        /// <summary>Whether a press on the stage came down on no room, so that, not a drag, it lets go of the room picked.</summary>
        private static bool _stageUnpick;

        /// <summary>Double-clicks a room of the stage's example as the mouse does, going into it, for the self-test; in a camp's, a click going to its entry.</summary>
        [Diagnostic]
        public static void DoubleClickRoomForTest(Explorer explorer, PlacedRoom room)
        {
            if (Stage.HasInside && room != null) Stage.GoInto(room);
            else ClickRoom(explorer, room, room == null ? null : EntryKeys.For(Kind.Location, room.Room.Name));
        }

        /// <summary>Clicks a room of the stage's example once, picking it, for the self-test.</summary>
        [Diagnostic]
        public static void PickRoomForTest(Explorer explorer, PlacedRoom room) =>
            ClickRoom(explorer, room, room == null ? null : EntryKeys.For(Kind.Location, room.Room.Name));

        /// <summary>Where the stage's picture was last drawn, on the screen, for a drag to tell where on it the mouse is.</summary>
        private static Rect _pictureScreen;

        /// <summary>Where a point of the screen is on the stage's picture, 0 to 1 across and 0 to 1 up.</summary>
        private static Vector2 OnPicture(Vector2 screen) => _pictureScreen.width <= 0f || _pictureScreen.height <= 0f ? new Vector2(0.5f, 0.5f)
            : new Vector2((screen.x - _pictureScreen.x) / _pictureScreen.width, 1f - (screen.y - _pictureScreen.y) / _pictureScreen.height);

        /// <summary>What the stage says of the example of the entry shown while it is read and built, for the self-test.</summary>
        [Diagnostic]
        public static string ExampleProgressForTest(Explorer explorer) => explorer.Shown != null ? ExampleProgress(explorer.Shown) : null;

        /// <summary>What the stage says while a dungeon's or camp's example is read and built; null once it stands.</summary>
        private static string ExampleProgress(Entry entry)
        {
            if (!(entry.Source is PlaceSource place) || place.IsRoom || place.Contents?.Dungeon == null || !ExampleLayouts.Of(entry)) return null;
            return DungeonWords.Progress(!Stage.InsideUnknown, ExampleLayouts.Example != null, ExampleLayouts.Read, ExampleLayouts.Total, Stage.ExampleRoomsShown, Stage.ExampleRoomsTotal);
        }

        private static void StageArea(Explorer explorer, Entry entry, Rect rect)
        {
            var e = Event.current;
            Skin.Box(rect, Skin.Stage);

            if (Stage.IsStaged(entry))
            {
                var inner = new Rect(rect.x + U(3f), rect.y + U(3f), rect.width - U(6f), rect.height - U(6f));
                if (e.type == EventType.Repaint)
                {
                    Stage.Request((int)inner.width, (int)inner.height);
                    StagePieces.Clear();
                }
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
                    var note = entry.Source is PlaceSource place ? LocationWords.StageNote(Stage.Building || Previews.ReadingPlace ? PlaceLoad.Loading : PlaceAssets.State(place)) : null;
                    GUI.Label(inner, note ?? "This one could not be previewed.", Skin.CenterDim);
                }

                // The example's plan in the corner, which takes the mouse where it is.
                // Inside a dungeon, the plan box with its views; a camp keeps its plan from above.
                if (e.type == EventType.Repaint) Stage.ViewShift = 0f;
                var plan = Stage.Subject == null ? Rect.zero
                    : Stage.HasInside && Stage.ShowsInside && ExampleOf(entry) != null ? DungeonPlanBox(explorer, inner)
                    : PlanOverlay(explorer, entry, inner);
                // The card of the room gone into in the other corner, which takes the mouse too.
                var card = Stage.Subject != null ? RoomCardBox(explorer, inner, plan) : Rect.zero;
                StagePiece("plan box", plan);
                StagePiece("room card", card);

                // A room of the example under the mouse is named, and a click on it that is not a
                // drag goes into it inside a dungeon's example, and to its entry in a camp's.
                string roomKey = null;
                PlacedRoom pointed = null;
                _stageRoom = null;
                _stageHover = null;
                var still = _drag == Drag.None || (_drag == Drag.Orbit && _dragMoved < U(5f));
                var mouse = StageMouse(e, inner);
                if (Stage.ExampleRoomsShown > 0 && still && inner.Contains(mouse) && !plan.Contains(mouse) && !card.Contains(mouse))
                {
                    var point = new Vector2((mouse.x - inner.x) / inner.width, 1f - (mouse.y - inner.y) / inner.height);
                    var room = Stage.ExampleRoomAt(point);
                    if (room != null)
                    {
                        var key = EntryKeys.For(Kind.Location, room.Room.Name);
                        var known = Goes(explorer, key);
                        var name = explorer.NotMet(key) ? CompendiumWords.Unknown(Kind.Location) : ShownName(explorer, key, LocationWords.RoomName(room.Room.Name));
                        AskTip("stageroom:" + room.Room.Name, Stage.HasInside ? Peek(explorer, room) : StageWords.RoomTip(name, known));
                        if (known) roomKey = key;
                        pointed = room;
                        _stageRoom = room.Room.Name;
                        _stageHover = room;
                    }
                }
                // Inside a dungeon, the room lit is outlined on the stage (RoomLight): a way chip of
                // the card hovered for it, else one under the mouse in the plan box or on the stage.
                // The room last come out of stays outlined until the mouse moves, the mouse the self-test points counted.
                var moving = _boxPointForTest ?? mouse;
                if (Stage.CameOutOf == null) _cameOutMouse = null;
                else if (e.type == EventType.Repaint)
                {
                    if (_cameOutMouse == null) _cameOutMouse = moving;
                    else if ((moving - _cameOutMouse.Value).sqrMagnitude > U(4f) * U(4f)) Stage.LetGoOfCameOut();
                }
                if (e.type == EventType.Repaint) Stage.LitRoom = Stage.HasInside && Stage.ShowsInside ? RoomLight.Lit(_cardLit, _boxHover, _stageHover) ?? Stage.PickedRoom ?? Stage.CameOutOf : null;
                // A click on no room, not a drag, lets go of the room picked.
                if (e.type == EventType.MouseUp && e.button == 0 && _stageUnpick)
                {
                    _stageUnpick = false;
                    if (_drag == Drag.Orbit && _dragMoved < U(5f)) Stage.Pick(null);
                }
                if (e.type == EventType.MouseUp && e.button == 0 && _drag == Drag.Orbit && _dragMoved < U(5f) && (_stageRoomDown != null || _stageRoomDownAt != null))
                {
                    var go = _stageRoomDown;
                    var into = _stageRoomDownAt;
                    _stageRoomDown = null;
                    _stageRoomDownAt = null;
                    ClickRoom(explorer, into, go);
                }

                // An effect that has played out plays again on a click of its stage that is not a drag.
                var playedOut = entry.Kind == Kind.Effect && !Previews.Repeat && Stage.Finished;
                if (playedOut && e.type == EventType.MouseUp && e.button == 0 && _drag == Drag.Orbit && _dragMoved < U(5f) && rect.Contains(e.mousePosition)) Previews.Replay();

                // The camera's buttons show only while the mouse is on the stage, so the model is not
                // framed by controls while it is looked at; the key sheet's ? stays in the corner.
                var over = rect.Contains(StageMouse(e, inner)) || _drag == Drag.Orbit;
                var viewsW = KeysMark(inner) + (over ? ViewButtons(inner) : 0f);
                var textW = inner.width - U(24f) - viewsW;
                if (playedOut)
                {
                    PictureNote(inner, textW, StageWords.PlayedOut);
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
                // Inside a dungeon, the breadcrumb where the kind badge stands, before the chips that keep clear of it.
                if (TrailShown(entry)) Breadcrumb(explorer, entry, rect);
                StageButtons(entry, rect);
                FloorRuler(rect);

                if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    // A double-click goes into the room under it inside a dungeon, else frames the view afresh.
                    if (e.clickCount == 2 && Stage.HasInside && pointed != null) Stage.GoInto(pointed);
                    else if (e.clickCount == 2) Stage.ResetView();
                    _stageRoomDown = e.clickCount == 2 ? null : roomKey;
                    _stageRoomDownAt = e.clickCount == 2 ? null : pointed;
                    _stageUnpick = e.clickCount == 1 && pointed == null && Stage.PickedRoom != null;
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
                    // With Shift each notch of the wheel steps the cut of a place opened, a level or a
                    // room's mark, down as it scrolls down; held, the turn can come sideways.
                    // Otherwise it zooms toward what is under the pointer.
                    if (e.shift && (Stage.Cutting || Stage.HasFloors)) WheelStep(StageCamera.WheelTurn(e.delta.x, e.delta.y));
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
            else if (entry.Kind == Kind.Skill)
            {
                var drawn = Timing.Start();
                SkillCard(entry, rect);
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

            if (!Stage.IsStaged(entry) || !TrailShown(entry))
            {
                KindBadge(entry, new Vector2(rect.x + U(10f), rect.y + U(10f)));
                StagePiece("kind", new Rect(rect.x + U(10f), rect.y + U(10f), _badgeWidth, U(22f)));
            }
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
            StagePiece("note", box);
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
            // Left of the key sheet's ?.
            var x = inner.xMax - U(8f) - h - U(4f);
            var views = new[] { ("Fit", "Fit", "Frame it whole again"), ("Top", "Look from top", "Look down on it"), ("Side", "Look from side", "Look at it from the side"), ("Front", "Look from front", "Look at it from the front") };
            foreach (var (name, label, tip) in views)
            {
                var w = Skin.Width(Skin.Chip, label) + U(2f);
                x -= w;
                var chip = new Rect(x, y, w, h);
                StagePiece("view buttons", chip);
                x -= U(4f);
                if (chip.Contains(Event.current.mousePosition)) AskTip("view:" + name, tip);
                if (!GUI.Button(chip, label, Skin.Fitted(Skin.Chip, chip))) continue;

                Stage.View(name);
                if (name != "Fit" && Stage.Spin)
                {
                    Stage.Spin = false;
                    SaveRects();
                }
            }
            return inner.xMax - U(8f) - h - U(4f) - x;
        }

        /// <summary>The ? in the stage's bottom right corner, whose tip is its key sheet for what is shown now (<see cref="StageWords.KeySheet"/>). Returns the width it takes.</summary>
        private static float KeysMark(Rect inner)
        {
            var size = U(22f);
            var mark = new Rect(inner.xMax - U(8f) - size, inner.yMax - U(8f) - size, size, size);
            StagePiece("keys", mark);
            CountDrawn(PanelPart.StageKeys);
            if (mark.Contains(Event.current.mousePosition)) AskTip("stage-keys", StageWords.KeySheet(Stage.HasFloors, Stage.RoomGoneInto != null, Stage.HasInside && Stage.ShowsInside));
            GUI.Label(mark, StageWords.KeysMark, Skin.IconButton);
            return size + U(4f);
        }

        /// <summary>
        /// The stage's controls in its top right corner, three groups from the right, each on a
        /// backing of the stage's colour so it reads over any picture, its pieces joined: View, for
        /// the spin, backdrop, lighting and person (<see cref="ViewRows"/>); what a place shown has
        /// to show or not, its example inside or its entrance, and its creatures; and how it is cut,
        /// three icons named by their tips (the roof on, from above, from the side). Its plan folds
        /// on the plan itself.
        /// </summary>
        private static void StageButtons(Entry entry, Rect rect)
        {
            var e = Event.current;
            var h = U(24f);
            var y = rect.y + U(10f);
            var x = rect.xMax - U(10f);
            var seam = U(1f);
            var apart = U(10f);
            var pad = U(3f);

            // Everything the row will hold, measured first, so it can move clear of the kind badge.
            var showsInside = Stage.HasOutside && !Stage.InsideUnknown;
            var shows = new List<string>();
            if (showsInside) shows.Add(StageWords.Inside(Stage.Inside));
            var inside = Stage.HasInside && Stage.ShowsInside;
            // The world's own dungeon inside is its layout alone, with no creatures to show.
            if (Stage.HasCreatures && !(inside && ExampleLayouts.ShowsWorlds)) shows.Add("Creatures");
            var cutModes = Stage.HasFloors && Stage.Subject != null ? CutControl.Offered(inside) : null;
            float TextW(string text) => Skin.Width(Skin.SmallCenter, text) + U(22f);
            var iconW = U(30f);
            var viewW = TextW("View");
            var showsW = shows.Sum(TextW) + seam * Mathf.Max(0, shows.Count - 1);
            var cutW = cutModes != null ? cutModes.Count * iconW + seam * (cutModes.Count - 1) : 0f;
            var total = viewW + pad * 2f + (showsW > 0f ? apart + showsW + pad * 2f : 0f) + (cutW > 0f ? apart + cutW + pad * 2f : 0f);
            if (x - total < rect.x + U(10f) + _badgeWidth + U(10f)) y += h + U(8f);

            // A group ending where the row has come to, on its backing; the row goes on left of it.
            Rect Group(float width)
            {
                var group = new Rect(x - pad - width, y, width, h);
                Skin.PillBox(new Rect(group.x - pad, y - pad, width + pad * 2f, h + pad * 2f), Skin.PictureBacking);
                x = group.x - pad - apart;
                return group;
            }

            // A piece of a group, its words or its icon dark on the accent while on; true when clicked.
            bool Piece(Rect part, bool on, bool first, bool last, string text, Texture2D icon, string tipKey, string tip)
            {
                var hover = part.Contains(e.mousePosition);
                Skin.PillPiece(part, on ? (hover ? Skin.Shade(Skin.Accent, 1.08f) : Skin.Accent) : hover ? Skin.RaisedHover : Skin.Raised, first, last);
                if (icon != null) Skin.Icon(new Rect(part.center.x - U(8f), part.center.y - U(8f), U(16f), U(16f)), icon, on ? Skin.OnAccent : Skin.Dim);
                else Skin.LabelIn(part, text, Skin.SmallCenter, on ? Skin.OnAccent : Skin.Dim);
                StagePiece("chips", part);
                if (hover) AskTip(tipKey, tip);
                return GUI.Button(part, GUIContent.none, GUIStyle.none);
            }

            // How it is seen, set and mostly left, in a box of its own.
            const string viewTip = "How it is seen: spin, backdrop, lighting and a person for size";
            var viewing = _viewOpen && _viewFor == entry;
            var view = Group(viewW);
            CountDrawn(PanelPart.StageChip);
            if (Piece(view, viewing, true, true, "View", null, "stage:" + viewTip, viewTip)) ViewToggle(entry, OnScreen(view));
            else if (viewing) ViewPlace(entry, OnScreen(view));

            // What it shows: a dungeon with nothing outside shows its inside, with no switch to the
            // outside; one only seen from outside shows its outside, with no switch to the inside.
            if (shows.Count > 0)
            {
                var group = Group(showsW);
                var px = group.x;
                for (var i = 0; i < shows.Count; i++)
                {
                    var part = new Rect(px, y, TextW(shows[i]), h);
                    px = part.xMax + seam;
                    CountDrawn(PanelPart.StageChip);
                    var isInside = showsInside && i == 0;
                    var on = isInside ? Stage.Inside : Stage.CreaturesShown;
                    var tip = isInside ? StageWords.InsideTip(Stage.Inside) : StageWords.CreaturesTip(Stage.CreaturesShown);
                    if (!Piece(part, on, i == 0, i == shows.Count - 1, shows[i], null, "stage:" + tip, tip)) continue;
                    if (isInside) Stage.Inside = !Stage.Inside;
                    else
                    {
                        Stage.CreaturesShown = !Stage.CreaturesShown;
                        SaveRects();
                    }
                }
            }

            // How it is cut: the roof on, from above, and inside a dungeon from the side, in a room
            // gone into acting on the room.
            if (cutModes != null)
            {
                CountDrawn(PanelPart.CutChip);
                var inRoom = inside && Stage.RoomGoneInto != null;
                var shown = CutControl.Shown(inside && Stage.CutFromSide, inRoom ? !Stage.RoomRoofOn : Stage.Cutting);
                var group = Group(cutW);
                for (var i = 0; i < cutModes.Count; i++)
                {
                    var mode = cutModes[i];
                    var on = mode == shown;
                    var part = new Rect(group.x + i * (iconW + seam), y, iconW, h);
                    var icon = mode == CutMode.Closed ? Skin.Roof : mode == CutMode.Top ? Skin.CutAbove : Skin.CutSide;
                    if (Piece(part, on, i == 0, i == cutModes.Count - 1, null, icon, "stage:cut:" + StageWords.CutModeName(mode), StageWords.CutModeTip(mode, inRoom)) && !on) Stage.ChooseCut(mode);
                }
            }
        }

        /// <summary>Cuts a dungeon's inside from the side or from above, the roof as it is.</summary>
        private static void ChooseCut(bool side)
        {
            if (Stage.CutFromSide != side) Stage.SetCutFromSide(side);
        }

        /// <summary>Chooses the cut as a click on its chip does, for the self-test.</summary>
        [Diagnostic]
        public static void ChooseCutForTest(bool side) => ChooseCut(side);

    }
}
