using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for an ancient root's sap, told on its page, and the sap extractor's note on it.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// An ancient root's page tells what the game's root holds: how much sap, how fast it
        /// refills, the extractor built on it, and each line the game shows on it with the sap it
        /// stands for; the extractor's Needs tells what its slow line means. Under the strict rules
        /// a root only seen keeps the amounts back and a tapped one shows them, both pictured.
        /// </summary>
        private static IEnumerator RootSapTold(Probe p)
        {
            // By name alone, whatever kind the catalog makes them, never another in their place.
            var root = X.Catalog.FirstOrDefault(e => e.Name == "YggdrasilRoot");
            var tap = X.Catalog.FirstOrDefault(e => e.Name == "piece_sapcollector");
            var game = GamePrefabs.Named("YggdrasilRoot").OrNull()?.GetComponent<ResourceRoot>();
            var gameTap = GamePrefabs.Named("piece_sapcollector").OrNull()?.GetComponent<SapCollector>();
            if (root == null || tap == null || game == null || gameTap == null)
            {
                p.Skip("there is no ancient root or sap extractor in this game's catalog");
                yield break;
            }
            var tapped = EntryKeys.Did(Deed.Tapped);
            var facts = Facts.For(root);
            string Said(Facts f, string label) => f.Pairs.FirstOrDefault(x => x.Key == label).Value;
            p.Check(Said(facts, "Holds") == RootWords.Holds(game.m_maxLevel) && Said(facts, "Refills") == RootWords.Refills(game.m_regenPerSec, game.m_maxLevel),
                "the root's page tells how much sap the game's root holds and how fast it refills", $"{Said(facts, "Holds")}; {Said(facts, "Refills")}");
            p.Check(facts.Links.TryGetValue("Tapped by", out var by) && by == tap.Name, "and the extractor built on it", by ?? "none");
            p.Check(facts.Proofs.TryGetValue("Holds", out var held) && held == tapped && facts.Proofs.TryGetValue("Refills", out var refills) && refills == tapped,
                "both wait for the root to be tapped under the strict rules");
            var table = facts.Rows.FirstOrDefault(r => r.Title == RootWords.LooksTitle);
            var lines = new[] { game.m_statusHigh, game.m_statusLow, game.m_statusEmpty }.Select(CatalogBuilder.Localize).ToArray();
            p.Check(table != null && table.Lines.Select(l => l.Cells[0]).SequenceEqual(lines) && table.Lines.Select(l => l.Cells[1]).SequenceEqual(RootWords.Bands(game.m_highThreshold, game.m_emptyTreshold))
                && lines.All(l => l.Length > 0 && !l.StartsWith("$", System.StringComparison.Ordinal)) && Enumerable.Range(0, 3).All(i => table.LineProofs.TryGetValue(i, out var proof) && proof == tapped),
                "each line the game shows on the root stands beside the sap it means, the sap kept back until tapped",
                table == null ? "no table" : string.Join("; ", table.Lines.Select(l => string.Join(" = ", l.Cells))));
            var slow = Said(Facts.For(tap), RootWords.SlowLabel);
            p.Check(slow == RootWords.Slows(CatalogBuilder.Localize(gameTap.m_drainingSlowText), game.m_emptyTreshold, game.m_regenPerSec), "the extractor's page says what its slow line means", slow ?? "none");

            // A made-up character's book: the root only seen, then tapped; the extractor known.
            var book = new CompendiumBook();
            book.Raise(root.Key, KnownLevel.Seen);
            book.Raise(tap.Key, KnownLevel.Known);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Strict);
            Session.ShowKnownInstead(view);
            try
            {
                if (!Session.IsOpen) Session.Show(null);
                KeepView();
                ScryPanel.Compact = false;
                yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
                X.Select(root);
                yield return null;
                var kept = ScryPanel.Drawn(PanelPart.FactKeptBack);
                yield return GoTo("sap");
                yield return Until(() => ScryPanel.Drawn(PanelPart.FactKeptBack) > kept, 3);
                p.Check(X.KeepsBack(tapped) && ScryPanel.Drawn(PanelPart.FactKeptBack) > kept, "a root only seen keeps its sap back, a smudge in its place");
                yield return PictureOf(p, "sap", "strict ancient root, only seen, its sap kept back");

                book.Raise(root.Key, KnownLevel.Known, way: CameBy.Tapped);
                book.FoundOut(root.Key, tapped);
                view.Tell(book, CompendiumRule.Strict);
                yield return Until(() => !X.KeepsBack(tapped), 3);
                p.Check(!X.KeepsBack(tapped), "tapped, the root shows its sap");
                yield return PictureOf(p, "sap", "strict ancient root, tapped, its sap shown");

                X.Select(tap);
                yield return null;
                yield return PictureOf(p, "does", "the sap extractor's needs, what its slow line means");
            }
            finally
            {
                Session.ShowKnownInstead(null);
            }
        }
    }
}
