using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// For the self-test: what LINKED holds on each kind of page, walked over every page, so a
    /// group can be moved into a topic of In the game knowing what it brings. Each kind tells how
    /// many of its pages hold LINKED, each of its groups on how many pages, how many links, the
    /// kinds of thing they go to and a few pages as examples. Kinds and groups with the most come first.
    /// </summary>
    internal sealed class LinkedTally
    {
        /// <summary>How many pages of a group are shown as examples, and how many links of each.</summary>
        private const int ExamplePages = 4;
        private const int ExampleLinks = 5;

        private sealed class Group
        {
            public int Links;
            public readonly Dictionary<string, int> To = new Dictionary<string, int>(StringComparer.Ordinal);
            public readonly SortedDictionary<string, List<string>> Pages = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        }

        private sealed class PageKind
        {
            public int Pages;
            public readonly HashSet<string> Holding = new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, Group> Groups = new Dictionary<string, Group>(StringComparer.Ordinal);
            public int Links => Groups.Values.Sum(g => g.Links);
        }

        private readonly Dictionary<Kind, PageKind> _kinds = new Dictionary<Kind, PageKind>();

        /// <summary>How many pages were walked.</summary>
        public int Pages => _kinds.Values.Sum(k => k.Pages);

        /// <summary>How many links LINKED holds over every page.</summary>
        public int Links => _kinds.Values.Sum(k => k.Links);

        /// <summary>Counts a page walked.</summary>
        public void Page(Kind kind) => Of(kind).Pages++;

        /// <summary>Counts a row of LINKED on a page: its group and what each of its chips goes to, with the kind of entry it is, or what it is where it is none.</summary>
        public void Add(Kind kind, string page, string group, IEnumerable<(string Key, string To)> targets)
        {
            var of = Of(kind);
            if (!of.Groups.TryGetValue(group, out var counted)) of.Groups[group] = counted = new Group();
            if (!counted.Pages.TryGetValue(page, out var links)) counted.Pages[page] = links = new List<string>();
            foreach (var (key, to) in targets)
            {
                links.Add(key);
                counted.Links++;
                counted.To[to] = counted.To.TryGetValue(to, out var n) ? n + 1 : 1;
            }
            if (links.Count > 0) of.Holding.Add(page);
        }

        private PageKind Of(Kind kind)
        {
            if (!_kinds.TryGetValue(kind, out var of)) _kinds[kind] = of = new PageKind();
            return of;
        }

        /// <summary>A line for each kind of page, the most links first, and under it a line for each of its groups, the most pages first.</summary>
        public List<string> Lines()
        {
            var lines = new List<string>();
            foreach (var kind in _kinds.OrderByDescending(k => k.Value.Links).ThenBy(k => k.Key.ToString(), StringComparer.Ordinal))
            {
                var of = kind.Value;
                var holding = $"{kind.Key} pages: {Numbers.Count(of.Holding.Count)} of {Numbers.Count(of.Pages)} hold LINKED";
                lines.Add(of.Links > 0 ? holding + ", " + Counted(of.Links, "link") : holding);
                foreach (var group in of.Groups.OrderByDescending(g => g.Value.Pages.Count).ThenBy(g => g.Key, StringComparer.Ordinal))
                {
                    lines.Add("  " + Line(group.Key, group.Value));
                }
            }
            return lines;
        }

        private static string Line(string name, Group group)
        {
            var to = string.Join(", ", group.To.OrderByDescending(t => t.Value).ThenBy(t => t.Key, StringComparer.Ordinal).Select(t => $"{t.Key} {Numbers.Count(t.Value)}"));
            var examples = group.Pages.Take(ExamplePages).Select(p => p.Key + ": " + string.Join(", ", p.Value.Take(ExampleLinks)) + More(p.Value.Count - ExampleLinks, ""));
            var line = $"{name}: {Counted(group.Pages.Count, "page")}, {Counted(group.Links, "link")}, to {to}; {string.Join("; ", examples)}";
            return line + (group.Pages.Count > ExamplePages ? "; " + More(group.Pages.Count - ExamplePages, " pages").TrimStart() : "");
        }

        private static string Counted(int count, string thing) => $"{Numbers.Count(count)} {thing}{(count == 1 ? "" : "s")}";

        private static string More(int more, string what) => more > 0 ? $" +{Numbers.Count(more)}{what}" : "";
    }
}
