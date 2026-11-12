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

    /// <summary>What an eye on the kinds list hides: one kind, a category's kinds, or every mark.</summary>
    internal enum EyeOn
    {
        Kind,
        Category,
        All,
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

        /// <summary>
        /// A stack of marks pointed at (<see cref="MarkStacks"/>): how many stand there, then each
        /// kind once, nearest first, with how many of it and how far and which way the nearest is;
        /// a kind the server told of said so, as its hollow dot is. In plain loops over marks as
        /// objects, so the first stack pointed at in a game compiles no new generic code mid-frame.
        /// </summary>
        public static string StackHover(IReadOnlyList<StackedMark> marks)
        {
            var kinds = new List<StackedMark>();
            var counts = new List<int>();
            for (var i = 0; i < marks.Count; i++)
            {
                var mark = marks[i];
                var at = -1;
                for (var k = 0; k < kinds.Count && at < 0; k++) if (kinds[k].Told == mark.Told && string.Equals(kinds[k].Name, mark.Name, StringComparison.Ordinal)) at = k;
                if (at < 0)
                {
                    kinds.Add(mark);
                    counts.Add(1);
                    continue;
                }
                counts[at]++;
                if (mark.Metres < kinds[at].Metres) kinds[at] = mark;
            }
            var order = new List<int>();
            for (var k = 0; k < kinds.Count; k++) order.Add(k);
            order.Sort((a, b) => kinds[a].Metres != kinds[b].Metres ? kinds[a].Metres.CompareTo(kinds[b].Metres) : string.CompareOrdinal(kinds[a].Name, kinds[b].Name));
            var said = $"{Numbers.Count(marks.Count)} marks here, point at one to name it alone";
            foreach (var k in order)
            {
                var (nearest, count) = (kinds[k], counts[k]);
                var where = nearest.Metres < Beside ? (nearest.Told ? "the server says one stands here, beside you" : "beside you")
                    : (nearest.Told ? "the server says one stands here · " : "") + (count > 1 ? "nearest " : "") + $"{Distance(nearest.Metres)} {nearest.Direction}";
                said += "\n" + (count > 1 ? Numbers.Count(count) + " " : "") + nearest.Name + " · " + where;
            }
            return said;
        }

        /// <summary>A kind's merged mark pointed at: how many of it stand there, whether the server told of them, and how far and which way the nearest is.</summary>
        public static string MergedHover(string name, int count, bool told, float metres, string direction) =>
            $"{Numbers.Count(count)} {name}{(told ? " the server told of" : "")} here\n" + (metres < Beside ? "nearest beside you" : $"nearest {Distance(metres)} {direction}");

        /// <summary>A kind's chip on the map's kinds strip: its name, how many of it you saw, how many the server told of apart, and how many are crossed off.</summary>
        public static string KindChip(string name, int seen, int told, int crossed = 0) =>
            (told == 0 ? name + " " + Numbers.Count(seen)
            : seen == 0 ? $"{name} · {Numbers.Count(told)} told"
            : $"{name} {Numbers.Count(seen)} · {Numbers.Count(told)} told")
            + (crossed > 0 ? $" · {Numbers.Count(crossed)} crossed" : "");

        /// <summary>A mark's words with what it says of its mode on a line under them.</summary>
        public static string WithModeHint(string said, SpotMode mode, int spots) => said + "\n" + ModeHint(mode, spots);

        /// <summary>What a mark pointed at says of its mode and of what a click does next, for every spot of a merged mark, and that a right click offers more.</summary>
        public static string ModeHint(SpotMode mode, int spots)
        {
            var all = spots > 1 ? $" all {Numbers.Count(spots)}" : "";
            return (mode == SpotMode.Crossed ? "crossed off · click: highlight" + all
                : mode == SpotMode.Highlighted ? "highlighted · click: back to plain" + all
                : "click: cross off" + all) + " · right-click: more";
        }

        /// <summary>A line of the menu a right click opens on a mark, for every spot of a merged mark, naming the kind it hides.</summary>
        public static string MenuWord(MapMenuChoice choice, int spots, string kind = null)
        {
            var all = spots > 1 ? $" all {Numbers.Count(spots)}" : "";
            switch (choice)
            {
                case MapMenuChoice.Cross: return "Cross off" + all;
                case MapMenuChoice.Highlight: return "Highlight" + all;
                case MapMenuChoice.Plain: return "Back to plain" + all;
                case MapMenuChoice.ZoomIn: return "Zoom in on them";
                case MapMenuChoice.HideKind: return "Hide " + kind;
                case MapMenuChoice.HideAll: return "Hide all marks";
                default: return "Open its page";
            }
        }

        /// <summary>The menu's name: the mark's, with how many spots it stands for.</summary>
        public static string MenuTitle(string name, int spots) => spots > 1 ? $"{Numbers.Count(spots)} {name}" : name;

        /// <summary>The kinds list's title, before how many kinds it lists, and its shorter form where that does not fit.</summary>
        public const string KindsTitle = "Kinds on the map";

        public const string KindsTitleShort = "Kinds";

        /// <summary>A kind's row pointed at: its counts, the crossed ones too, then what a click does, picking it or showing it again where hidden.</summary>
        public static string KindRowTip(string name, int seen, int told, int crossed, bool picked, bool anyPicked, bool hidden) =>
            KindChip(name, seen, told, crossed) + "\n" + (hidden ? EyeTip(EyeOn.Kind, true) : KindChipTip(picked, anyPicked));

        /// <summary>The title's pill while anything is hidden: how much, its cross showing all of it again.</summary>
        public static string HiddenPill(int hidden) => $"{Numbers.Count(hidden)} hidden ×";

        public const string HiddenPillTip = "Show everything hidden again";

        /// <summary>A folded heading's badge: how many kinds under it are hidden.</summary>
        public static string HiddenBadge(int hidden) => $"{Numbers.Count(hidden)} hidden";

        /// <summary>The folded list's lines: their labels, and what they say of every kind alike, every mark hidden, the spots crossed off and the places told.</summary>
        public const string FoldedPicked = "Picked";
        public const string FoldedHidden = "Hidden";
        public const string FoldedAlike = "Every kind alike";
        public const string FoldedEveryMark = "every mark";
        public const string FoldedCrossed = "crossed off";
        public const string FoldedTold = "told by the server";

        /// <summary>An eye's tip on the kinds list: hiding a kind, a category's kinds or every mark, each staying kept, or showing them again.</summary>
        public static string EyeTip(EyeOn on, bool hidden) =>
            on == EyeOn.Kind ? (hidden ? "Show this kind on the map again" : "Hide this kind on the map; it stays kept")
            : on == EyeOn.Category ? (hidden ? "Show every kind under it again" : "Hide every kind under it; they stay kept")
            : hidden ? "Show Scry's marks on the map again" : "Hide all of Scry's marks on the map; they stay kept";

        /// <summary>The kinds list's foot, set apart as settings: its label, and its switches for the spots crossed off and the places the server told of.</summary>
        public const string FootLabel = "Show";
        public const string FootCrossed = "Crossed off";
        public const string FootTold = "Told";

        public static string CrossedChipTip(bool hidden) => hidden ? "Show the spots you crossed off again, faded" : "Hide the spots you crossed off";

        public static string ToldChipTip(bool hidden) => hidden ? "Show what the server told of again" : "Hide what the server told of: its squares and hollow dots";

        /// <summary>A category's name on the lists.</summary>
        public static string CategoryName(KeptCategory category) => category == KeptCategory.Dungeons ? "Dungeons" : category == KeptCategory.Locations ? "Locations" : "Resources";

        /// <summary>How many kinds a category's heading holds.</summary>
        public static string CategoryKinds(int kinds) => kinds == 1 ? "1 kind" : $"{Numbers.Count(kinds)} kinds";

        /// <summary>A category's heading's tip: picking every kind under it to stand out, or letting them fade again.</summary>
        public static string KindHeadingTip(bool picked, bool anyPicked) =>
            picked ? "Let these kinds fade with the others again"
            : anyPicked ? "Show every kind under it too"
            : "Show every kind under it, the others faded";

        /// <summary>A kind's chip's tip: picking it to stand out, alone or beside those picked, or letting it fade with the others again.</summary>
        public static string KindChipTip(bool picked, bool anyPicked) =>
            picked ? "Let this kind fade with the others again"
            : anyPicked ? "Show this kind too"
            : "Show this kind alone, the others faded; pick more to show them too";

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

        /// <summary>A page's button opening the map with all its kinds at once.</summary>
        public static string ShowAll(int kinds) => $"Show all {Numbers.Count(kinds)} kinds";

        /// <summary>Its tip, naming the kinds it shows.</summary>
        public static string ShowAllTip(IReadOnlyList<string> kinds) => $"Shows {Naming.Joined(kinds)} on the map at once";

        /// <summary>A page's button keeping all its kinds on the map as one line at once.</summary>
        public static string KeepAll(int kinds) => $"Keep all {Numbers.Count(kinds)} kinds";

        /// <summary>Its tip, naming the kinds it keeps: what a click does, kept or not.</summary>
        public static string KeepAllTip(IReadOnlyList<string> kinds, bool kept) => kept
            ? $"{Naming.Joined(kinds)} are kept on the map as one line: a click takes them off"
            : $"Keeps {Naming.Joined(kinds)} on Valheim's map as one line, every time it opens";

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
