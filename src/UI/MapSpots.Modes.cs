using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A single spot crossed off or highlighted (<see cref="SpotModes"/>): a right click on a dot
    /// goes through the modes, on a merged mark for every spot it stands for, kept with the character
    /// for its world. Crossed spots stand dimmed under a cross, or not at all where the settings ask
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

        /// <summary>
        /// A right click of the map's own at a point of the screen (<see cref="MapRightClick"/>): on the
        /// strip or the list nothing happens, on a dot its next mode, on a merged mark for every spot it
        /// stands for; false where it is on neither, or on a pin the click is the game's for, as a left click's is.
        /// </summary>
        public static bool TakeRightClick(Vector2 screen)
        {
            var map = Minimap.instance;
            if (map == null || map.m_mode != Minimap.MapMode.Large || _holder == null || !_holder.gameObject.activeInHierarchy) return false;
            if (PointerOnStrip()) return true;
            var (from, size) = DotUnder(screen);
            if (from < 0 || from >= StandingDots.Count) return false;
            if (!MapLook.ClickIsMarks(Settings.MarksOverPins, PinUnder(map, screen, size))) return false;
            var book = CompendiumKeeper.Book;
            var world = CompendiumMap.World;
            if (book == null || world == null) return false;
            var to = SpotModes.Next(ModeOf(from));
            if (MergedCount(from) > 1) foreach (var member in Merged.MembersOf(from)) book.SetSpotMode(world, StandingDots[member].X, StandingDots[member].Z, to);
            else book.SetSpotMode(world, StandingDots[from].X, StandingDots[from].Z, to);
            _modesChanged = true;
            MapKept.MarksChanged();
            CompendiumKeeper.Save(now: true);
            return true;
        }

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
        }
    }
}
