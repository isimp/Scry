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
            (Kind.Creature, "Troll"), (Kind.Creature, "Eikthyr"), (Kind.Item, "TrollHide"), (Kind.Piece, "wood_wall"), (Kind.Piece, "piece_workbench"),
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
            var view = new CompendiumView(X.Entries);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            foreach (var rule in new[] { CompendiumRule.Strict, CompendiumRule.Loose, CompendiumRule.Off })
            {
                view.Tell(book, rule);
                Session.ShowKnownInstead(view);
                yield return Until(() => ReferenceEquals(X.Compendium, view) && X.Compendium.Rule == rule, 3);
                foreach (var page in pages)
                {
                    X.Select(page);
                    yield return Until(() => X.Selected == page, 3);
                    yield return null;
                    yield return null;
                    p.Check(X.LevelOf(page) == KnownLevel.Known && X.Selected == page, $"{page.ShownName} opens under {CompendiumWords.RuleName(rule)}");
                    yield return Picture(p, $"audit {CompendiumWords.RuleName(rule)} {page.ShownName}");
                }
            }
        }
    }
}
