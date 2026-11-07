using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>Why a page's Show on map can show nothing now; none where it can.</summary>
    internal enum MapBlock
    {
        None,

        /// <summary>The world has no map (the game's no-map setting).</summary>
        NoMap,

        /// <summary>The server lets the map keep no spots (its map reach is 0).</summary>
        NoReach,

        /// <summary>Keeping where things were seen is off, and none were kept before.</summary>
        RecordingOff,

        /// <summary>The character's file of spots could not be read.</summary>
        Unreadable,

        /// <summary>None of this kind was seen in this world yet.</summary>
        NoneSeen,
    }

    /// <summary>What Show on map says: its strip on the map, its button's tip, and the You box's line.</summary>
    internal static class SpotWords
    {
        /// <summary>The You box's label for how many of a kind the character saw (<see cref="YouLine"/>).</summary>
        public const string YouLabel = "Where you saw it";

        /// <summary>The words of the link and the button that show them on the map.</summary>
        public const string ShowOnMap = "Show on map";

        /// <summary>The strip's button taking the dots away.</summary>
        public const string Hide = "Hide";

        /// <summary>The strip's button keeping the kind shown on the map, every time it opens; lit while it is kept.</summary>
        public const string Keep = "Keep on the map";

        /// <summary>The tip of the page's own Keep on the map, keeping its kind without showing it.</summary>
        public static string KeepTip(bool kept) => kept
            ? "Kept on Valheim's map: a click takes it off, as the title row's On the map list does"
            : "Keeps this kind on Valheim's map, standing every time it opens, without opening it now";

        /// <summary>The title row's button listing what is kept on the map, with how many.</summary>
        public static string KeptButton(int kept) => kept <= 0 ? "On the map" : "On the map  " + Numbers.Count(kept);

        /// <summary>The list's heading and foot, and its row of every kind of dungeon.</summary>
        public const string KeptHeading = "Kept on Valheim's map, every time it opens";
        public const string KeptFoot = "A name opens its page · × takes it off the map";
        public const string TakeAllOff = "Take all off";
        public const string DungeonsRow = "Dungeons, every kind";
        public const string NothingKept = "Nothing is kept on the map. Keep on the map, on a page or on the map's strip, keeps a kind there.";

        /// <summary>The tip of a page's Keep on the map for a kind kept with others, by the list's line that keeps it.</summary>
        public static string KeptWith(string line) => $"Kept on the map with {line}; the title row's On the map list takes it off";

        /// <summary>The list's last line where more are kept than fit.</summary>
        public static string KeptMore(int more) => $"and {Numbers.Count(more)} more kept; take some off to see them";

        /// <summary>How many of a kept kind stand: those seen, and the squares of a place.</summary>
        public static string KeptCount(int seen, int squares, int told = 0)
        {
            if (seen == 0 && squares == 0 && told == 0) return "none seen yet";
            var said = seen > 0 ? Numbers.Count(seen) + " you saw" : null;
            if (squares > 0) said = (said != null ? said + " · " : "") + Naming.Count(squares, "square", "squares");
            if (told > 0) said = (said != null ? said + " · " : "") + Numbers.Count(told) + " told";
            return said;
        }

        /// <summary>How many dungeons are marked, walked or seen, and the squares or the spots the server told of those never come near.</summary>
        public static string DungeonsCount(int marked, int squares, int told = 0) =>
            Numbers.Count(marked) + " marked" + (squares > 0 ? " · " + Naming.Count(squares, "square", "squares") : "") + (told > 0 ? " · " + Numbers.Count(told) + " told" : "");

        /// <summary>The list's line where more marks are kept than stand at once.</summary>
        public static string Capped(int shown, int all) => $"The nearest {Numbers.Count(shown)} of {Numbers.Count(all)} stand; the farthest are left out";

        /// <summary>A dot pointed at: its kind, then how far and which way it is from you.</summary>
        public static string DotHover(string name, float metres, string direction) =>
            name + "\n" + (metres < Beside ? "you saw it here, beside you" : $"you saw it here · {Distance(metres)} {direction}");

        /// <summary>A hollow dot pointed at, one the server told of: its kind, then how far and which way it is from you.</summary>
        public static string ToldHover(string name, float metres, string direction) =>
            name + "\n" + (metres < Beside ? "the server says one stands here, beside you" : $"the server says one stands here · {Distance(metres)} {direction}");

        /// <summary>A square pointed at: what stands somewhere inside it, how many of each, and in which ground.</summary>
        public static string SquareHover(IEnumerable<(string Name, int Count)> inside, bool explored, bool shared)
        {
            var kinds = string.Join(", ", inside.Select(k => Numbers.Count(k.Count) + " " + k.Name));
            var ground = explored && shared ? "in ground you explored or read on a map" : shared ? "on a map you read" : "in ground you explored";
            return "Somewhere in this square\n" + kinds + " you never came near\n" + ground;
        }

        /// <summary>A place of several kinds shown all at once, as the strip names it.</summary>
        public static string AllKindsName(string family, int kinds) => $"{family}, all {Numbers.Count(kinds)} kinds";

        /// <summary>The strip's button showing all of a place's kinds, lit while they are shown, or the page's kind alone again.</summary>
        public static string AllKindsChip(int kinds) => $"All {Numbers.Count(kinds)} kinds";

        /// <summary>The tip of a page's all-kinds switch, the same switch as the strip's: what a click does, lit or not.</summary>
        public static string AllKindsTip(string family, int kinds, bool on) => on
            ? $"All {Numbers.Count(kinds)} kinds of {family} are shown and kept together: a click goes back to this kind alone"
            : $"Shows and keeps all {Numbers.Count(kinds)} kinds of {family} on the map, here and on the map's strip";

        /// <summary>How near the nearest stands to be beside you rather than at a distance.</summary>
        private const float Beside = 5f;

        /// <summary>
        /// The strip over the map: the kind, how many the character saw, how far and which way the
        /// nearest is, and how many more stand somewhere in the squares shaded, or where the server told them, or
        /// why there are none (<see cref="SquaresNote"/>); with none seen, the squares or those
        /// told alone.
        /// </summary>
        public static string Strip(string name, int count, float? nearest, string direction, int more = 0, string note = null, int told = 0)
        {
            string said;
            if (count == 0 && told > 0 && more == 0) said = $"{name} · {Numbers.Count(told)} the server told of";
            else if (count == 0 && more > 0 && told == 0) said = more == 1 ? $"{name} · 1 in a square, somewhere inside it" : $"{name} · {Numbers.Count(more)} in squares, somewhere inside each";
            else
            {
                said = $"{name} · {Numbers.Count(count)} you saw";
                if (nearest is float metres) said += metres < Beside ? " · nearest beside you" : $" · nearest {Distance(metres)} {direction}";
                if (more > 0) said += $" · {Numbers.Count(more)} more in squares";
                if (told > 0) said += $" · {Numbers.Count(told)} more the server told of";
            }
            return note != null ? said + " · " + note : said;
        }

        /// <summary>Why a place's strip shows no squares; none where the server told them.</summary>
        public static string SquaresNote(SquaresHeard heard)
        {
            switch (heard)
            {
                case SquaresHeard.Waiting: return "squares once the server has read your map";
                case SquaresHeard.NoScry: return "no squares: this server has no Scry";
                case SquaresHeard.OtherScry: return "no squares: this server runs another version of Scry";
                case SquaresHeard.Off: return "no squares on this server";
                default: return null;
            }
        }

        /// <summary>A distance as the strip gives it: to the metre under a hundred, in tens of metres under a kilometre, past it in kilometres.</summary>
        public static string Distance(float metres)
        {
            if (metres >= 1000f) return Numbers.Fixed(Math.Round(metres / 1000f, 1), 1) + " km";
            var rounded = metres < 100f ? Math.Round(metres) : Math.Round(metres / 10f) * 10f;
            return Numbers.Count((long)rounded) + " m";
        }

        /// <summary>Which way a point lies, by the map's compass: north up, z growing to the north, x to the east.</summary>
        public static string Direction(float dx, float dz)
        {
            var degrees = Math.Atan2(dx, dz) * 180.0 / Math.PI;
            var sector = (int)Math.Round((degrees + 360.0) % 360.0 / 45.0) % 8;
            return Compass[sector];
        }

        private static readonly string[] Compass = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

        /// <summary>The You box's line: how many of the kind the character saw in this world.</summary>
        public static string YouLine(int count) => $"{Numbers.Count(count)} in this world";

        /// <summary>The button's tip where it can show the character's dots, the server's squares, or both.</summary>
        public static string Tip(int count, int squares, int told = 0)
        {
            if (told > 0)
            {
                if (count <= 0) return told == 1 ? "Open Valheim's map with a hollow dot where the server told one stands" : $"Open Valheim's map with a hollow dot on each of the {Numbers.Count(told)} the server told of";
                return Tip(count) + (told == 1 ? ", and a hollow dot on 1 more the server told of" : $", and a hollow dot on each of {Numbers.Count(told)} more the server told of");
            }
            if (squares <= 0) return Tip(count);
            var shaded = squares == 1 ? "the square one of these stands somewhere in" : $"the {Numbers.Count(squares)} squares one of these stands somewhere in";
            if (count <= 0) return "Open Valheim's map with " + shaded;
            return Tip(count) + (squares == 1 ? ", and a square round 1 more" : $", and a square round each of {Numbers.Count(squares)} more");
        }

        /// <summary>The button's tip where it can show them.</summary>
        public static string Tip(int count) => count == 1
            ? "Open Valheim's map with a dot where your character saw it in this world"
            : $"Open Valheim's map with a dot on each of the {Numbers.Count(count)} your character saw in this world";

        /// <summary>The greyed button's tip: why it shows nothing now.</summary>
        public static string WhyNot(MapBlock block)
        {
            switch (block)
            {
                case MapBlock.NoMap: return "This world has no map.";
                case MapBlock.NoReach: return "This server keeps no spots for the map.";
                case MapBlock.RecordingOff: return "Keeping where your character saw things is off in Scry's settings.";
                case MapBlock.Unreadable: return "The file of where your character saw things could not be read.";
                case MapBlock.NoneSeen: return "Your character has not seen this in this world since Scry began keeping where things stand.";
                default: return null;
            }
        }
    }

    /// <summary>
    /// The kind Show on map shows now: its spots as kept when the button was pressed, by world,
    /// until Hide, until the map opened for them closes, or until it never opens (as in a world
    /// whose map the game keeps shut), so nothing waits for the next time the map opens.
    /// </summary>
    internal sealed class MapShowing
    {
        /// <summary>How many frames the map may take to open after the button before the spots are dropped.</summary>
        public const int WaitTicks = 30;

        private static readonly IReadOnlyList<(int X, int Z)> None = Array.Empty<(int, int)>();

        private string _world;
        private bool _opened;
        private int _waited;

        public string Kind { get; private set; }
        public string Name { get; private set; }
        public IReadOnlyList<(int X, int Z)> Spots { get; private set; } = None;

        public bool Showing => Kind != null;

        /// <summary>The key several kinds shown at once go by, each kind's key in it.</summary>
        public static string AllKindsKey(IEnumerable<string> kinds) => string.Join("+", kinds);

        /// <summary>The kinds a page offers to show and keep at once: each kept as spots once, in order, a member keeping none left out; none where fewer than two are left.</summary>
        public static IReadOnlyList<string> KindsOffered(IEnumerable<string> kinds)
        {
            var offered = kinds.Where(k => k != null).Distinct(StringComparer.Ordinal).ToList();
            return offered.Count >= 2 ? offered : (IReadOnlyList<string>)Array.Empty<string>();
        }

        /// <summary>What Show on map and Keep on the map act on: all the kinds offered while the all-kinds switch is lit, else the page's own kind.</summary>
        public static string KeyFor(string kind, IReadOnlyList<string> kinds, bool all) => all && kinds != null && kinds.Count >= 2 ? AllKindsKey(kinds) : kind;

        /// <summary>
        /// The resources one page offers as its kinds: those the game names alike when looked at
        /// (<see cref="Entry.HoverName"/>), as the ribs and the skull both read Petrified Bone, by
        /// their keys' order; none for a resource the game names on its own terms or not at all.
        /// </summary>
        public static IReadOnlyList<Entry> NamedAlike(Entry entry, IEnumerable<Entry> all)
        {
            if (entry == null || entry.Kind != Scry.Kind.Resource || string.IsNullOrEmpty(entry.HoverName)) return Array.Empty<Entry>();
            return all.Where(e => e != null && e.Kind == Scry.Kind.Resource && e.HoverName == entry.HoverName).OrderBy(e => e.Key, StringComparer.Ordinal).ToList();
        }

        /// <summary>A kind's spots shown in a world, in place of any shown before.</summary>
        public void Show(string world, string kind, string name, IReadOnlyList<(int X, int Z)> spots)
        {
            _world = world;
            Kind = kind;
            Name = name;
            Spots = spots ?? None;
            _opened = false;
            _waited = 0;
        }

        public void Hide()
        {
            _world = null;
            Kind = null;
            Name = null;
            Spots = None;
        }

        /// <summary>Each frame: whether the large map is open, and the world played.</summary>
        public void Tick(bool largeOpen, string world)
        {
            if (!Showing) return;
            if (!string.Equals(world, _world, StringComparison.Ordinal)) Hide();
            else if (largeOpen) _opened = true;
            else if (_opened || ++_waited >= WaitTicks) Hide();
        }
    }
}
