using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A single spot crossed off or highlighted (<see cref="SpotModes"/>): a click on a dot goes
    /// through the modes, as the game's own click crosses its pins, on a merged mark for every spot it
    /// stands for, kept with the character for its world; a right click opens a menu of the same and
    /// the page (<see cref="MapMenu"/>). Crossed spots stand dimmed under a cross, or not at all where the settings ask
    /// (<see cref="CrossedMarks"/>); highlighted ones bigger, in an accent ring, over the game's pins.
    /// </summary>
    internal static partial class MapSpots
    {
        /// <summary>Each dot's mode, by the same index as the dots standing.</summary>
        private static readonly List<SpotMode> StandingModes = new List<SpotMode>();

        /// <summary>What the modes lay on the dots at rest and on those in a spread.</summary>
        private static readonly SpotMarks ModeMarks = new SpotMarks();
        private static readonly SpotMarks FanModes = new SpotMarks();

        /// <summary>How crossed spots stood as last placed, and whether a mode changed since the stacks were gathered, so a spread stays open under a right click.</summary>
        private static CrossedMarks _crossedFor;
        private static bool _modesChanged;

        /// <summary>How many crosses and highlighted marks stand on the large map, for the self-test.</summary>
        [ForTest]
        public static (int Crosses, int Highlights) ModesShownForTest => (ModeMarks.CrossesShown, ModeMarks.HighlightsShown);

        /// <summary>Where the dot of a spot stands on the screen, for the self-test; none where none stands.</summary>
        [ForTest]
        public static Vector2? DotCentreAtForTest(int x, int z)
        {
            for (var i = 0; i < _standing && i < PlacedFrom.Count; i++)
            {
                var dot = StandingDots[PlacedFrom[i]];
                if (dot.X == x && dot.Z == z) return new Vector2(Dots[i].Rect.position.x, Dots[i].Rect.position.y);
            }
            return null;
        }

        /// <summary>Where the dot of a spot stands in the spread, for the self-test; none where none does.</summary>
        [ForTest]
        public static Vector2? FannedDotOfSpotForTest(int x, int z)
        {
            for (var k = 0; k < FanFrom.Count && k < FanDots.Count; k++)
            {
                var dot = StandingDots[FanFrom[k]];
                if (dot.X == x && dot.Z == z) return new Vector2(FanDots[k].Rect.position.x, FanDots[k].Rect.position.y);
            }
            return null;
        }

        /// <summary>A spot's mode in the world played, for the character played.</summary>
        public static SpotMode ModeAt(int x, int z)
        {
            var book = CompendiumKeeper.Book;
            var world = CompendiumMap.World;
            return book != null && world != null ? book.SpotModeAt(world, x, z) : SpotMode.Plain;
        }

        /// <summary>Whether a spot of a mode stands on the map at all, as the settings ask.</summary>
        public static bool ModeStands(SpotMode mode) => mode != SpotMode.Crossed || Settings.CrossedMarks != CrossedMarks.Hidden;

        /// <summary>Whether crossed spots are to stand otherwise than as last placed: the marks worked out again as the setting changes.</summary>
        public static void FollowCrossedSetting()
        {
            if (_crossedFor == Settings.CrossedMarks) return;
            _crossedFor = Settings.CrossedMarks;
            MapKept.MarksChanged();
        }

        private static SpotMode ModeOf(int dot) => dot >= 0 && dot < StandingModes.Count ? StandingModes[dot] : SpotMode.Plain;

        /// <summary>Each dot's mode read as the dots are worked out.</summary>
        private static void WorkOutModes()
        {
            StandingModes.Clear();
            foreach (var (x, z, _, _, _) in StandingDots) StandingModes.Add(ModeAt(x, z));
        }

        /// <summary>The dot a click at a point of the screen is for: none on the map shut, off every dot, on a stack at rest, or on a pin the click is the game's for.</summary>
        private static int DotClicked(Vector2 screen, out float size)
        {
            size = 0f;
            var map = Minimap.instance;
            if (map == null || map.m_mode != Minimap.MapMode.Large || _holder == null || !_holder.gameObject.activeInHierarchy) return -1;
            var (from, wide) = DotUnder(screen);
            size = wide;
            if (from < 0 || from >= StandingDots.Count) return -1;
            return MapLook.ClickIsMarks(Settings.MarksOverPins, PinUnder(map, screen, wide)) ? from : -1;
        }

        /// <summary>Every spot a dot stands for: its own, or a merged mark's.</summary>
        private static List<(int X, int Z)> SpotsOfDot(int dot)
        {
            var spots = new List<(int X, int Z)>();
            if (MergedCount(dot) > 1) foreach (var member in Merged.MembersOf(dot)) spots.Add((StandingDots[member].X, StandingDots[member].Z));
            else spots.Add((StandingDots[dot].X, StandingDots[dot].Z));
            return spots;
        }

        /// <summary>A click of the map's own at a point of the screen (<see cref="MapMarkClick"/>): the dot's next mode there; false where it is on none, or the press was a drag.</summary>
        public static bool TakeLeftClick(Vector2 screen)
        {
            var from = DotClicked(screen, out var size);
            if (from < 0 || !MapMarks.Clicked((_pressedAt.x, _pressedAt.y), (screen.x, screen.y), size)) return false;
            SetModes(SpotsOfDot(from), SpotModes.Next(ModeOf(from)));
            return true;
        }

        /// <summary>A mode given to spots, the marks placed again and the book kept at once.</summary>
        public static void SetModes(IReadOnlyList<(int X, int Z)> spots, SpotMode mode)
        {
            var book = CompendiumKeeper.Book;
            var world = CompendiumMap.World;
            if (book == null || world == null) return;
            foreach (var (x, z) in spots) book.SetSpotMode(world, x, z, mode);
            _modesChanged = true;
            MapKept.MarksChanged();
            CompendiumKeeper.Save(now: true);
        }

        /// <summary>What a right click at a point of the screen is on, for the menu: the dot's kind, its name, its mode and its spots; false where it is on none.</summary>
        public static bool DotMenuAt(Vector2 screen, out (string Kind, string Name, SpotMode Mode, List<(int X, int Z)> Spots) dot)
        {
            dot = default;
            var from = DotClicked(screen, out _);
            if (from < 0 || from >= StandingKinds.Count) return false;
            dot = (StandingKinds[from], StandingDots[from].Name, ModeOf(from), SpotsOfDot(from));
            return true;
        }

        /// <summary>The page of a dot's kind: its key where the catalog is read, else its first kind's own, which opens once it is.</summary>
        public static string PageOf(string kind) => string.IsNullOrEmpty(kind) ? null : WorldCatalog.Find(kind)?.Key ?? MapKept.KindsIn(kind)[0];

        /// <summary>The menu standing open: its name, where the right click was in the GUI's own coordinates, its lines, and where it was drawn and the frame.</summary>
        private static readonly List<(string Words, System.Action Act)> MenuLines = new List<(string, System.Action)>();
        private static Rect _menuRect;
        private static int _menuFrame = -10;

        public static string MenuTitle { get; private set; }

        public static Vector2 MenuAt { get; private set; }

        public static IReadOnlyList<(string Words, System.Action Act)> MenuItems => MenuLines;

        public static bool MenuOpen => MenuTitle != null;

        /// <summary>The menu's lines, for the self-test.</summary>
        [ForTest]
        public static List<string> MenuWordsForTest => MenuLines.ConvertAll(line => line.Words);

        /// <summary>A line of the menu chosen, as a click on it does, for the self-test.</summary>
        [ForTest]
        public static void PickMenuForTest(int line)
        {
            var act = MenuLines[line].Act;
            CloseMenu();
            act();
        }

        /// <summary>The menu opened at a point of the screen, the pointer's words put away while it stands.</summary>
        public static void OpenMenu(string title, Vector2 screen, IEnumerable<(string Words, System.Action Act)> lines)
        {
            MenuLines.Clear();
            MenuLines.AddRange(lines);
            MenuTitle = title;
            MenuAt = new Vector2(screen.x, Screen.height - screen.y);
            _menuRect = Rect.zero;
            ReadPointerAgain();
        }

        public static void CloseMenu()
        {
            if (!MenuOpen) return;
            MenuTitle = null;
            MenuLines.Clear();
            _menuRect = Rect.zero;
            ReadPointerAgain();
        }

        /// <summary>Where the menu was drawn and the frame, for a press on it to be kept from the map under it.</summary>
        public static void MenuDrawn(Rect at)
        {
            _menuRect = at;
            _menuFrame = Time.frameCount;
        }

        private static bool PointerOnMenu(Vector2 gui) => Time.frameCount <= _menuFrame + 1 && _menuRect.Contains(gui);

        /// <summary>Whether a press on the map is not the map's: on the strip or the list, or anywhere while the menu stands, a press outside it only putting it away.</summary>
        public static bool PressTaken() => MenuOpen || PointerOnStrip();

        /// <summary>Whether a mode changed since last asked, taken once.</summary>
        private static bool TakeModesChanged()
        {
            var changed = _modesChanged;
            _modesChanged = false;
            return changed;
        }

        /// <summary>Lets go of what the modes hold with the dots.</summary>
        private static void ForgetModes()
        {
            StandingModes.Clear();
            ModeMarks.Forget();
            FanModes.Forget();
            _modesChanged = false;
            MenuLines.Clear();
            MenuTitle = null;
        }
    }
}
