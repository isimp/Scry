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

        /// <summary>A chip choosing how a dungeon's inside is cut: from above or from the side.</summary>
        public static string CutChip(bool side) => side ? "Side" : "Above";

        /// <summary>What each way of cutting a dungeon's inside takes away.</summary>
        public static string CutTip(bool side) => side
            ? "Cut from the side: the half of every room facing you taken away"
            : "Cut from above: the roof off over a floor or the room gone into";

        /// <summary>A room of a dungeon's example's tip: its name, and that a click goes into it, or, gone into, how to come back out.</summary>
        public static string GoIntoTip(string name, bool inside) => name + (inside ? "\nEsc or the roof button comes back out" : "\nClick to go in");

        /// <summary>The chip of a place with an inside, by which side is shown.</summary>
        public static string Inside(bool inside) => inside ? "Inside" : "Outside";

        /// <summary>The inside chip's tip.</summary>
        public static string InsideTip(bool inside) => inside
            ? "The example dungeon, laid out as the game lays out a new one; click for its entrance outside"
            : "Its entrance; click for the example dungeon inside";

        /// <summary>The creatures chip's tip.</summary>
        public static string CreaturesTip(bool shown) => shown
            ? "Puts away the creatures its spawn points put here"
            : "Shows the creatures its spawn points put here, rolled anew with every copy";

        /// <summary>The roof button's tip.</summary>
        public static string RoofTip(bool cutting) => cutting
            ? "Cut open over a floor: click to put the roof back on"
            : "Roof on: click to take it off, cutting away what is above head height over a floor";

        /// <summary>The floor ruler's tip over a floor's mark.</summary>
        public static string FloorTip(string floor) => floor + ": click to open it";

        /// <summary>The floor ruler's tip elsewhere: above the roof's mark, or between floors.</summary>
        public static string RulerTip(bool aboveRoof) => aboveRoof
            ? "Click to put the roof on"
            : "Click or drag to cut here; the wheel or Page Up and Down step a floor";

        /// <summary>The spin's tip.</summary>
        public static string SpinTip(bool spinning) => spinning ? "Turning; click to hold it still" : "Held still; click to turn it";

        /// <summary>What an effect's stage says once it has played out, its stage left standing.</summary>
        public const string PlayedOut = "It has played out: click the stage to play it again";
    }
}
