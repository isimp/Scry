using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The breadcrumb over a dungeon's inside, in the stage's top left where the kind badge stands
    /// elsewhere (<see cref="DungeonTrail"/>): the dungeon, a click on it showing the whole of it
    /// with the roof on, a menu beside it going into the entrance or the deepest room; the level
    /// open or the one the room gone into is on, a click on it coming back out of the room to it;
    /// and the room gone into, a click on it framing it again. The part where the view is stands
    /// lit. The menu takes the mouse before anything under it and is drawn over everything;
    /// Escape, a click elsewhere or another selection closes it.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>Whether the stage shows a dungeon's inside, which the breadcrumb heads.</summary>
        private static bool TrailShown(Entry entry) => Stage.HasInside && Stage.ShowsInside && ExampleOf(entry) != null;

        /// <summary>The parts the breadcrumb was last drawn with, for the self-test.</summary>
        [Diagnostic]
        public static List<(TrailPart Part, string Text, bool Current)> TrailForTest { get; private set; } = new List<(TrailPart, string, bool)>();

        /// <summary>Draws the breadcrumb in the stage's top left, taking the room the kind badge would.</summary>
        private static void Breadcrumb(Explorer explorer, Entry entry, Rect rect)
        {
            var e = Event.current;
            var h = U(22f);
            var x = rect.x + U(10f);
            var y = rect.y + U(10f);
            var start = x;
            var gone = Stage.RoomGoneInto;
            var mark = gone != null ? Stage.ExampleMarkOfRoom(gone) : Stage.Cutting ? Stage.CutLevel : -1;
            var level = mark >= 0 ? Stage.ExampleMark(mark) : null;
            var trail = DungeonTrail.Of(ShownName(explorer, entry.Key, entry.DisplayName), level, gone != null ? BoxRoomName(explorer, gone) : null);
            if (e.type == EventType.Repaint) TrailForTest = trail;
            var stepW = Skin.Width(Skin.FaintLabel, PlanBoxWords.TrailStep);
            for (var i = 0; i < trail.Count; i++)
            {
                var (part, text, current) = trail[i];
                if (i > 0)
                {
                    GUI.Label(new Rect(x, y, stepW, h), PlanBoxWords.TrailStep, Skin.FaintLabel);
                    x += stepW;
                }
                var style = current ? Skin.ChipOn : Skin.Chip;
                var chip = new Rect(x, y, Mathf.Min(Skin.Width(style, text) + U(4f), U(220f)), h);
                if (chip.Contains(e.mousePosition)) AskTip("trail:" + part, TrailTip(part, gone != null));
                if (GUI.Button(chip, text, Skin.Fitted(style, chip))) Follow(part, gone);
                x = chip.xMax + U(2f);
                if (part != TrailPart.Dungeon) continue;
                // Beside the dungeon, the menu going into its entrance or its deepest room.
                var menu = new Rect(x, y, h, h);
                if (menu.Contains(e.mousePosition)) AskTip("trail:menu", PlanBoxWords.TrailMenuTip);
                if (GUI.Button(menu, PlanBoxWords.TrailMenuMark, Skin.IconButton)) TrailMenuToggle(entry, OnScreen(menu));
                x = menu.xMax + U(2f);
            }
            BadgeTakes(x - start);
            StagePiece("trail", new Rect(start, y, x - start, h));
            CountDrawn(PanelPart.Trail);
        }

        /// <summary>What a part of the breadcrumb says it does.</summary>
        private static string TrailTip(TrailPart part, bool inRoom) =>
            part == TrailPart.Dungeon ? PlanBoxWords.TrailDungeonTip(BoxRooms.Count)
            : part == TrailPart.Level ? PlanBoxWords.TrailLevelTip(inRoom)
            : PlanBoxWords.TrailRoomTip;

        /// <summary>What a click on a part of the breadcrumb does: the whole dungeon with the roof on, back out of the room to its level, or the room framed again.</summary>
        private static void Follow(TrailPart part, PlacedRoom gone)
        {
            switch (part)
            {
                case TrailPart.Dungeon:
                    Stage.WholeDungeon();
                    break;
                case TrailPart.Level:
                    if (gone != null) Stage.ComeOut();
                    break;
                default:
                    Stage.View("Fit");
                    break;
            }
        }

        /// <summary>Clicks a part of the breadcrumb as the mouse does, for the self-test.</summary>
        [Diagnostic]
        public static void ClickTrailForTest(TrailPart part) => Follow(part, Stage.RoomGoneInto);

        // ----- The menu beside the dungeon -----

        private static bool _trailMenuOpen;
        private static Entry _trailMenuFor;

        /// <summary>Where the menu's mark and its box are, on the screen.</summary>
        private static Rect _trailMenuChip, _trailMenuBox;

        private static float TrailMenuRowHeight => U(26f);

        /// <summary>Whether the menu beside the dungeon is open, for the self-test.</summary>
        [Diagnostic]
        public static bool TrailMenuOpen => _trailMenuOpen;

        /// <summary>The menu's rows: what each says, its tip, and the room it goes into, none where the example has none.</summary>
        private static (string Name, string Tip, PlacedRoom Room)[] TrailMenuRows() => new[]
        {
            (PlanBoxWords.Entrance, PlanBoxWords.EntranceTip, Stage.ExampleWays?.Entrance),
            (PlanBoxWords.Deepest, PlanBoxWords.DeepestTip, Stage.ExampleWays?.Deepest),
        };

        /// <summary>Opens or closes the menu under its mark, its left edge on the mark's.</summary>
        private static void TrailMenuToggle(Entry entry, Rect markScreen)
        {
            if (_trailMenuOpen)
            {
                _trailMenuOpen = false;
                return;
            }
            _trailMenuOpen = true;
            _trailMenuFor = entry;
            _trailMenuChip = markScreen;
            var w = Mathf.Max(Skin.Width(Skin.Label, PlanBoxWords.Entrance), Skin.Width(Skin.Label, PlanBoxWords.Deepest)) + U(32f);
            _trailMenuBox = new Rect(markScreen.x, markScreen.yMax + U(4f), w, TrailMenuRows().Length * TrailMenuRowHeight + U(8f));
        }

        /// <summary>Lets go of the entry the menu was last opened for.</summary>
        private static void ForgetTrailMenu()
        {
            _trailMenuOpen = false;
            _trailMenuFor = null;
        }

        /// <summary>Goes into a row's room as a click on it does, for the self-test.</summary>
        [Diagnostic]
        public static void ChooseTrailMenuForTest(int row)
        {
            var rows = TrailMenuRows();
            _trailMenuOpen = false;
            if (row >= 0 && row < rows.Length && rows[row].Room != null) Stage.GoInto(rows[row].Room);
        }

        /// <summary>
        /// Before anything draws: the open menu takes a press over it, going into the room of the
        /// row under it; a press elsewhere, but on its mark, closes it, as do Escape and another
        /// selection or the inside put away. True when it took the event.
        /// </summary>
        private static bool TrailMenuInput(Explorer explorer)
        {
            if (!_trailMenuOpen) return false;
            if (explorer.Selected != _trailMenuFor || !Stage.HasInside || !Stage.ShowsInside)
            {
                _trailMenuOpen = false;
                return false;
            }
            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                _trailMenuOpen = false;
                e.Use();
                return true;
            }
            var mouse = GUIUtility.GUIToScreenPoint(e.mousePosition);
            var over = _trailMenuBox.Contains(mouse);
            if (e.type == EventType.MouseDown)
            {
                if (!over)
                {
                    if (!_trailMenuChip.Contains(mouse)) _trailMenuOpen = false;
                    return false;
                }
                ChooseTrailMenuForTest(Mathf.FloorToInt((mouse.y - _trailMenuBox.y - U(4f)) / TrailMenuRowHeight));
                e.Use();
                return true;
            }
            if (over && (e.type == EventType.MouseUp || e.type == EventType.ScrollWheel || e.type == EventType.MouseDrag))
            {
                e.Use();
                return true;
            }
            return false;
        }

        /// <summary>Draws the open menu over everything else.</summary>
        private static void TrailMenuDraw()
        {
            if (!_trailMenuOpen || _trailMenuFor == null) return;
            var e = Event.current;
            var box = Here(_trailMenuBox);
            if (e.type == EventType.Repaint) Skin.Box(box, Skin.Panel, Skin.Outline);
            var rows = TrailMenuRows();
            for (var i = 0; i < rows.Length; i++)
            {
                var row = new Rect(box.x + U(4f), box.y + U(4f) + i * TrailMenuRowHeight, box.width - U(8f), TrailMenuRowHeight);
                var hover = row.Contains(e.mousePosition);
                if (hover) AskTip("trail-menu:" + rows[i].Name, rows[i].Tip);
                if (e.type != EventType.Repaint) continue;
                if (hover) Skin.Fill(row, Skin.Alpha(Skin.Text, 0.08f));
                GUI.Label(new Rect(row.x + U(8f), row.y, row.width - U(8f), row.height), rows[i].Name, rows[i].Room != null ? Skin.Label : Skin.DimLabel);
            }
            CountDrawn(PanelPart.TrailMenu);
        }
    }
}
