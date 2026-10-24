using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// One page of each kind read from one made-up book under the strict, the loose and no rules,
    /// each pictured, so a look over the pictures shows every kind under every rule at once.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>The pages audited, a kind each, by the prefab or key to pick: a creature, a boss, an item, a piece, a station, a resource, a spawner, a place and a dungeon.</summary>
        private static readonly (Kind Kind, string Name)[] AuditPages =
        {
            (Kind.Creature, "Troll"), (Kind.Creature, "Eikthyr"), (Kind.Item, "TrollHide"), (Kind.Piece, "woodwall"), (Kind.Piece, "piece_workbench"),
            (Kind.Resource, "Beech1"), (Kind.Spawner, "Spawner_GreydwarfNest"), (Kind.Location, "WoodVillage1"), (Kind.Location, "Crypt2"),
        };

        private static IEnumerator PagesAudited(Probe p)
        {
            var pages = new List<Entry>();
            foreach (var (kind, name) in AuditPages)
            {
                var page = Pick(kind, name);
                if (page != null) pages.Add(page);
                else p.Note($"no {kind} {name} in this world's catalog");
            }
            if (pages.Count == 0)
            {
                p.Skip("none of the pages to audit is in the catalog");
                yield break;
            }
            var book = new CompendiumBook();
            book.Now = () => new Moment(12, "Meadows");
            foreach (var page in pages) book.Raise(page.Key, KnownLevel.Known, way: CameBy.Fought);
            foreach (var name in new[] { "Hammer", "Wood", "Stone" })
            {
                var met = Pick(Kind.Item, name);
                if (met != null) book.Raise(met.Key, KnownLevel.Known, way: CameBy.Held);
            }
            var troll = pages.FirstOrDefault(e => e.Name == "Troll");
            if (troll != null)
            {
                book.FoundOut(troll.Key, EntryKeys.Did(Deed.Killed));
                book.Dropped(troll.Name, new[] { ("TrollHide", 2) });
                book.Sighted(troll.Name, 1, null);
            }
            // A creature only seen, for the list's faint row and its page of what sight gave.
            var boar = Pick(Kind.Creature, "Boar");
            var meadows = X.Find(EntryKeys.For(Kind.Biome, "Meadows"));
            if (boar != null)
            {
                book.Raise(boar.Key, KnownLevel.Seen);
                book.Sighted(boar.Name, 0, null);
                if (meadows != null) book.Witness(boar.Key, meadows.Key);
            }
            var view = new CompendiumView(X.Entries);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            foreach (var rule in new[] { CompendiumRule.Strict, CompendiumRule.Loose, CompendiumRule.Off })
            {
                view.Tell(book, rule);
                Session.ShowKnownInstead(view);
                yield return Until(() => ReferenceEquals(X.Compendium, view) && X.Compendium.Rule == rule, 3);
                var ruleName = CompendiumWords.RuleName(rule);
                foreach (var page in pages)
                {
                    X.Select(page);
                    yield return Until(() => X.Selected == page, 3);
                    yield return null;
                    yield return null;
                    p.Check(X.LevelOf(page) == KnownLevel.Known && X.Selected == page, $"{page.ShownName} opens under {ruleName}");
                    yield return Picture(p, $"audit {ruleName} {page.ShownName}");
                    // Every topic of the page with the compendium on, each gone to and pictured where it stands.
                    if (rule == CompendiumRule.Off) continue;
                    foreach (var (id, heading) in ScryPanel.TopicsForTest.ToList())
                    {
                        if (heading == null) continue;
                        ScryPanel.GoToSection(id);
                        yield return Until(() => ScryPanel.StandsAt(id), 2);
                        if (!ScryPanel.StandsAt(id))
                        {
                            p.Note($"{page.ShownName} under {ruleName}: its {heading} was not gone to");
                            continue;
                        }
                        yield return Picture(p, $"audit {ruleName} {page.ShownName} {heading}");
                    }
                }
                // The list with a row only seen, faint and noted so, and that creature's page of what sight gave.
                if (boar != null && rule != CompendiumRule.Off)
                {
                    X.Select(boar);
                    yield return Until(() => X.Selected == boar, 3);
                    yield return null;
                    yield return Picture(p, $"audit {ruleName} list with Boar only seen");
                }
                // Learned, by day, under this rule.
                X.LearnedOnly = true;
                yield return null;
                yield return null;
                yield return Picture(p, $"audit {ruleName} Learned");
                X.LearnedOnly = false;
                yield return null;
            }
        }
    }
}
