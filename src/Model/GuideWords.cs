using System.Collections.Generic;

namespace Scry
{
    /// <summary>A table in the compendium's guide: its columns' heads, and each row's name with a cell for each column.</summary>
    internal sealed class GuideTable
    {
        public GuideTable(string[] columns, params (string Row, string[] Cells)[] rows)
        {
            Columns = columns;
            Rows = rows;
        }

        public string[] Columns { get; }
        public (string Row, string[] Cells)[] Rows { get; }
    }

    /// <summary>An example in the compendium's guide: what it shows, the lines a page says, each a label and what it says beside it, and a line under them.</summary>
    internal sealed class GuideExample
    {
        public GuideExample(string title, IReadOnlyList<(string Label, string Said)> lines, string foot = null)
        {
            Title = title;
            Lines = lines;
            Foot = foot;
        }

        public string Title { get; }
        public IReadOnlyList<(string Label, string Said)> Lines { get; }
        public string Foot { get; }
    }

    /// <summary>A heading of the compendium's guide: its caption, one short line saying what it is for, the words that follow, and where they help, a table and examples.</summary>
    internal sealed class GuideSection
    {
        public GuideSection(string caption, string lead, string words, GuideTable table = null, params GuideExample[] examples)
        {
            Caption = caption;
            Lead = lead;
            Words = words;
            Table = table;
            Examples = examples ?? System.Array.Empty<GuideExample>();
        }

        public string Caption { get; }
        public string Lead { get; }
        public string Words { get; }
        public GuideTable Table { get; }
        public GuideExample[] Examples { get; }
    }

    /// <summary>
    /// The compendium's guide, opened by the ? in its box: each heading opens with what it is for
    /// in a line, the three rules side by side, and examples in the very lines a page shows,
    /// worked out by what writes them on a page, so they never drift from it.
    /// </summary>
    internal static class GuideWords
    {
        public static readonly GuideSection[] Sections =
        {
            new GuideSection("Off, Loose and Strict", "How much a page shows before your character finds it out",
                "What the game's own menus show comes with the thing: a recipe's materials, a piece's cost and tool, a store's wares. A thing your character only saw stands on the stage with its page closed, telling what sight gave. A page's title row counts how much of what it names your character has met, and under Strict how much of what it kept back was found out.",
                new GuideTable(new[] { "Off", "Loose", "Strict" },
                    ("Things never met", new[] { "shown", "counted, not named", "counted, not named" }),
                    ("The numbers the game never shows", new[] { "shown", "shown", "once found out" }),
                    ("What goes together", new[] { "shown", "shown", "once seen together" }),
                    ("A creature on the stage", new[] { "any stars and gear", "any stars and gear", "as seen up close" })),
                new GuideExample("A Troll you met, under Loose",
                    new[] { ("Title row", CompendiumWords.MetOf(3, 6, false)), ("Drops", "Troll hide, Coins"), ("", CompendiumWords.MeetLine("loot", 1)) },
                    "Its whole page opens; a drop your character never met is counted, not named."),
                new GuideExample("The same Troll, under Strict, never killed", StrictTroll(), "Each number kept back says what shows it.")),
            new GuideSection("Known up to", "Beat a biome's boss, and that tier can show as known",
                "Known up to shows everything of the biomes whose bosses your character has beaten as known, tier by tier, marked with stairs. You can lower it again in the compendium's box.",
                null,
                new GuideExample("Example", System.Array.Empty<(string, string)>(), "With Eikthyr beaten, every page of the Meadows shows as known, though your character never met all of it.")),
            new GuideSection("Peeking", "A look at a page whole, without it counting as found",
                "The eye beside the compendium's button shows the page you are on whole until you go to another page. Every page, on the strip that then shows, does the same for all pages until you close the panel."),
            new GuideSection("Learned and You", "What your character came to know, day by day",
                "Learned lists what your character learned. Under Strict it also lists the numbers your character found out, which Loose shows from the start. A row there opens its page at what changed, lit for a moment. A page's You box tells when your character first saw the thing and came to know it, and how its fights went."),
            new GuideSection("What this world shows as known", "Places your character knows of without having been there",
                "Each source can show more of the world as known: Ground you explored, Shared maps (read at a cartography table), Places marked for you (by a Vegvisir or rune stone), Places on every map, and The whole world, counted. What a source shows is marked with a folded map. Your book keeps only what your character met, so turning a source down takes back all it showed."),
            new GuideSection("Your map", "What Scry stands on Valheim's map, and keeping it there",
                "Show on map on a page marks where your character saw its kind, a dot each, in the kind's own colour and shape. A place in ground you explored but never came near stands as the square it lies somewhere in, or as a hollow dot where the server tells where. Keep on the map keeps a kind there every time the map opens; the title row's On the map list holds what is kept and takes it off, every kind of dungeon among it from the start in their amber marks. Pointing at a mark says what it is.",
                null,
                new GuideExample("The map's strip", new[] { ("", SpotWords.Strip("Copper deposit", 6, 140f, "north-east", 3)) })),
            new GuideSection("Playing together", "What friends' games and a server bring to yours",
                "Where your friends have Scry, what their game runs near you counts for you as it would alone. A server can set the rules for everyone; the box then shows them locked, your own choices kept beneath."),
        };

        /// <summary>A Troll's page under Strict before your character killed one or saw what it drops: its title row's count, then what is still to find out, as its You box says it.</summary>
        private static List<(string Label, string Said)> StrictTroll()
        {
            var lines = new List<(string Label, string Said)> { ("Title row", CompendiumWords.FoundOutCount(20, 35, false)) };
            lines.AddRange(CompendiumWords.StillToFindOut(new[] { EntryKeys.Did(Deed.Killed) }, System.Array.Empty<string>(), 0, 0, new[] { Tally.Fell }));
            return lines;
        }
    }
}
