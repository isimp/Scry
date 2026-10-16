using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// What the card of a dungeon's room gone into says (<see cref="RoomCard"/>): where the room
    /// is, the creatures and the chests and pickups this copy rolled in it, its ways on and its
    /// levels, each section's lines in the order they are listed.
    /// </summary>
    internal static class RoomCardWords
    {
        private const string Apart = "  ·  ";

        public const string Creatures = "Creatures in this roll";
        public const string Loot = "Chests and pickups";
        public const string Ways = "Ways on";
        public const string Levels = "Levels in this room";
        public const string OpenPage = "Open its page";
        public const string Whole = "Whole dungeon";

        /// <summary>What a section with nothing in it says.</summary>
        public const string None = "None in this roll";

        public const string CreaturesTip = "The creatures this copy's spawn points put in the room; another copy rolls them anew";
        public const string LootTip = "The chests and pickups this copy's rolls put in the room, each with how likely a room of its kind holds it";
        public const string LevelTip = "Click to cut at head height over this level";
        public const string OpenPageTip = "The page of this kind of room";
        public const string WholeTip = "Back out to the whole dungeon with the roof on";

        /// <summary>
        /// Where a room is: how far its ground lies below or above the entrance's, in whole
        /// metres, how many rooms in from the entrance it is, where a way reaches it, and how many
        /// ways lead on from it.
        /// </summary>
        public static string Where(float rise, int stepsIn, int ways)
        {
            var parts = new List<string>();
            if (stepsIn == 0) parts.Add("The entrance");
            else
            {
                var metres = WholeMetres(rise);
                parts.Add(metres == 0 ? "Level with the entrance" : $"{Numbers.Count(Math.Abs(metres))} m {(metres < 0 ? "below" : "above")} the entrance");
                if (stepsIn > 0) parts.Add(Naming.Count(stepsIn, "room in", "rooms in"));
            }
            parts.Add(Naming.Count(ways, "way", "ways"));
            return string.Join(Apart, parts);
        }

        /// <summary>Names counted together, "Draugr ×3", the most first, as many in the order of their names.</summary>
        public static List<string> Counted(IEnumerable<string> names) => names
            .GroupBy(n => n, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => Naming.Repeated(g.Key, g.Count()))
            .ToList();

        /// <summary>
        /// Chests and pickups with how likely each is there, counted together by name and chance
        /// to a tenth of a percent: what is always there first, then the likeliest, then by name.
        /// </summary>
        public static List<(string Name, string Odds)> Odds(IEnumerable<(string Name, float Chance)> parts) => parts
            .GroupBy(p => (p.Name, Tenths: (int)Math.Round(p.Chance * 1000f)))
            .OrderByDescending(g => g.Key.Tenths).ThenBy(g => g.Key.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Naming.Repeated(g.Key.Name, g.Count()), Chance(g.First().Chance, g.Count())))
            .ToList();

        /// <summary>A line of a list as it reads: its name, then its odds where it has any.</summary>
        public static string Row(string name, string odds) => odds != null ? name + "  " + odds : name;

        private static string Chance(float chance, int count)
        {
            if (chance >= 0.9995f) return "always";
            return count > 1 ? DropWords.Share(chance) + " each" : DropWords.Share(chance);
        }

        /// <summary>A way on to a room: its name, how far up or down where its ground is off the level (<see cref="RoomFocus.SameLevel"/>), and whether it is a dead end.</summary>
        public static string Way(string name, float rise, bool deadEnd)
        {
            var words = name;
            if (Math.Abs(rise) > RoomFocus.SameLevel) words += $", {Numbers.Count(Math.Abs(WholeMetres(rise)))} m {(rise > 0f ? "up" : "down")}";
            if (deadEnd) words += ", dead end";
            return words;
        }

        /// <summary>A level of a room: its ground, or how far up or down from it.</summary>
        public static string Level(float level, float ground)
        {
            var metres = WholeMetres(level - ground);
            return metres == 0 ? "Ground" : $"{Numbers.Count(Math.Abs(metres))} m {(metres > 0 ? "up" : "down")}";
        }

        /// <summary>The lines that fit in so many: all of them, or one fewer and how many more there are.</summary>
        public static List<string> Fit(List<string> lines, int room) => Fit(lines, room, more => more);

        /// <summary>The same for lines of any kind, the last line saying how many more.</summary>
        public static List<T> Fit<T>(IReadOnlyList<T> lines, int room, Func<string, T> more)
        {
            if (room <= 0) return new List<T>();
            if (lines.Count <= room) return lines.ToList();
            var kept = lines.Take(room - 1).ToList();
            kept.Add(more($"and {Numbers.Count(lines.Count - (room - 1))} more"));
            return kept;
        }

        private static int WholeMetres(float metres) => (int)Math.Round(metres, MidpointRounding.AwayFromZero);
    }

    /// <summary>A way on from a room gone into, as its card lists it: the room it leads to and what its chip says.</summary>
    internal struct WayOn
    {
        public PlacedRoom To;
        public string Words;
    }

    /// <summary>
    /// The card of a dungeon's room gone into, in the stage's top left: its name and where it is,
    /// the creatures and the chests and pickups this copy rolled in it, its ways on as chips that
    /// go into their rooms, its own levels, its page and the way back out (<see cref="RoomCardWords"/>).
    /// </summary>
    internal static class RoomCard
    {
        /// <summary>The ways on from a room: the nearest the entrance first, by how many rooms in each is, then by what each says.</summary>
        public static List<WayOn> WaysOn(DungeonWays ways, PlacedRoom room, Func<PlacedRoom, float> ground, Func<PlacedRoom, string> name)
        {
            var from = ground(room);
            return ways.WaysOf(room)
                .Select(to => (Steps: ways.StepsIn(to), Way: new WayOn { To = to, Words = RoomCardWords.Way(name(to), ground(to) - from, ways.IsDeadEnd(to)) }))
                .OrderBy(w => w.Steps < 0 ? int.MaxValue : w.Steps).ThenBy(w => w.Way.Words, StringComparer.OrdinalIgnoreCase)
                .Select(w => w.Way)
                .ToList();
        }
    }
}
