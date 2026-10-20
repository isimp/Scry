namespace Scry
{
    /// <summary>What the stage says: the grid's measure, its chips and their tips, its roof and floor ruler, and a room's tip.</summary>
    internal static class StageWords
    {
        /// <summary>The grid's note: its measure, and the model's size in the same metres.</summary>
        public static string Grid(float width, float height, float depth) =>
            $"Squares of 1 m, lines every 5 m  \u00B7  {Metres(height)} m tall, {Metres(width)} \u00D7 {Metres(depth)} m";

        /// <summary>A size to a tenth under ten metres, whole above.</summary>
        private static string Metres(float value) => value < 10f ? Numbers.Fixed(value, 1) : Numbers.Amount(value, 0);

        /// <summary>A room's tip: its name, and where it can be gone to, that a click does.</summary>
        public static string RoomTip(string name, bool canGo) => canGo ? name + "\nClick to go to it" : name;

        /// <summary>A part of the stage's cut control.</summary>
        public static string CutModeName(CutMode mode) => mode == CutMode.Closed ? "No cut" : mode == CutMode.Top ? "Cut from above" : "Cut from the side";

        /// <summary>What a part of the cut control does, in a room gone into to the room alone.</summary>
        public static string CutModeTip(CutMode mode, bool inRoom)
        {
            switch (mode)
            {
                case CutMode.Closed:
                    return inRoom ? "No cut: the room's roof on, the rooms above it back, still in the room" : "No cut: the roof on";
                case CutMode.Top:
                    return inRoom ? "Cut from above: the room's roof off at head height over its floor" : "Cut from above: the roof off over a floor or level";
                default:
                    return "Cut from the side: the half of every room facing you taken away";
            }
        }

        /// <summary>A way on's tip on the room card: its words, and that a click goes into its room.</summary>
        public static string GoIntoTip(string name) => name + "\nGo into it";

        /// <summary>
        /// The peek on a room of a dungeon's example under the mouse, on the stage or in the plan
        /// box: its name, where it is, how many creatures and chests and pickups this copy rolled
        /// in it (none for a room of the world's own dungeon, shown by its layout alone), and what
        /// a click and a double-click do; the room gone into, how to come back out.
        /// </summary>
        public static string RoomPeek(string name, string where, int creatures, int chests, bool goneInto, bool layoutOnly = false) =>
            name + "\n" + where + (layoutOnly ? "" : "\nCreatures " + Numbers.Count(creatures) + "  ·  Chests and pickups " + Numbers.Count(chests))
            + (goneInto ? "\nEsc, the breadcrumb's level or the mouse's back button comes back out" : "\nPick it, or double-click to go into it");

        /// <summary>The link on the card of a room picked, not gone into, and its tip.</summary>
        public const string GoInLink = "Go in";
        public const string GoInTip = "Go into the room picked, as a double-click on it does";

        /// <summary>The chip of a place with an inside, by which side is shown.</summary>
        public static string Inside(bool inside) => inside ? "Inside" : "Outside";

        /// <summary>The inside chip's tip.</summary>
        public static string InsideTip(bool inside) => inside
            ? "Show its entrance outside"
            : "Show the example dungeon inside, laid out as the game lays out a new one";

        /// <summary>The creatures chip's tip.</summary>
        public static string CreaturesTip(bool shown) => shown
            ? "Put away the creatures its spawn points put here"
            : "Show the creatures its spawn points put here, rolled anew with every preview";

        /// <summary>The mark of the stage's key sheet, in its corner.</summary>
        public const string KeysMark = "?";

        /// <summary>
        /// What the stage answers to, a control a line, as its ? shows: turning, moving and
        /// zooming; with floors, every notch and key stepping them, the ruler's drag cutting
        /// anywhere; in a room gone into, its marks, on past its ends, and coming back out; inside
        /// a dungeon, picking a room and going into it.
        /// </summary>
        public static string KeySheet(bool cut, bool inRoom, bool rooms = false)
        {
            var sheet = inRoom
                ? "Drag: turn around the room\nWheel: zoom toward the pointer\nShift and the wheel, Page Up and Down: the room's next mark; past its ends, the next level\nEsc, the breadcrumb's level or the mouse's back button: come back out"
                : "Drag: turn it\nRight-drag: move it\nWheel: zoom toward the pointer\nDouble-click: frame it again" + (cut ? "\nShift and the wheel, Page Up and Down, the wheel on the ruler or the plan: the next floor or level\nDrag on the ruler: cut anywhere" : "");
            return rooms ? sheet + "\nClick a room: pick it, Esc letting go of it\nDouble-click a room or Enter: go into it" : sheet;
        }

        /// <summary>The floor ruler's tip over a floor's mark.</summary>
        public static string FloorTip(string floor) => floor + ": open it";

        /// <summary>The floor ruler's tip elsewhere: above the roof's mark, or between its marks, a dungeon's named levels, a place's floors.</summary>
        public static string RulerTip(bool aboveRoof, bool levels) => aboveRoof
            ? "Put the roof on"
            : "Cut here, or drag; the wheel or Page Up and Down step a " + (levels ? "level" : "floor");

        /// <summary>A mark of the ruler opening the whole dungeon by the plan's levels: the level by its depth, however many cuts it is filmed in.</summary>
        public static string LevelMark(string depth) => $"Level {depth}";

        /// <summary>A mark of the ruler in a room gone into for the rooms above on a level: how many, and how far up from its ground in whole metres.</summary>
        public static string RoomsAboveMark(int rooms, float rise) =>
            $"{Numbers.Count(rooms)} {(rooms == 1 ? "room" : "rooms")}, {Numbers.Count((int)System.Math.Round(rise))} m up";

        /// <summary>The tip of a mark of the rooms above on the room's ruler.</summary>
        public static string RoomsAboveTip(string mark) => mark + ": cut over them, bringing them back";

        /// <summary>What the room's ruler says beside its bar with the room's roof on.</summary>
        public const string RoomRoofOn = "Roof on";

        /// <summary>The ruler's tip in a room gone into: above its top, or along it.</summary>
        public static string RoomRulerTip(bool aboveTop) => aboveTop
            ? "Put the room's roof on"
            : "Cut the room here, or drag; the wheel or Page Up and Down step a mark, past its ends, the next level";

        /// <summary>The spin's tip.</summary>
        public static string SpinTip(bool spinning) => spinning ? "Hold it still" : "Turn it";

        /// <summary>The View box's speeds of the model's turning, in degrees a second; a click goes on to the next.</summary>
        public static readonly float[] SpinSpeeds = { 7f, 14f, 28f, 56f };

        /// <summary>A speed as the View box says it, in degrees a second.</summary>
        public static string SpinSpeedName(float speed) => Numbers.Count((int)System.Math.Round(speed)) + "\u00B0/s";

        public const string SpinSpeedTip = "How fast it turns by itself; a click goes on to the next speed";

        /// <summary>The View box's row of the lights of the rooms dimmed below a dungeon's opened floor, and its two values.</summary>
        public const string LightsBelow = "Lights below";
        public const string LightsKept = "Kept";
        public const string StageLight = "Stage light";
        public const string LightsBelowTip = "Whether the rooms dimmed below the floor opened keep their own lights, as the game lights them; with the stage's light alone a big dungeon is filmed in about half the time";

        /// <summary>What an effect's stage says once it has played out, its stage left standing.</summary>
        public const string PlayedOut = "It has played out: click the stage to play it again";
    }
}
