using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>Under the strict rules a page counts the names it leaves out: its title chip, its You box, its sections' last lines and the search's is:left agree.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// A made-up character that fought a creature and saw one of its drops fall: under the
        /// strict rules the creature's page leaves the rest of what it names out, its title chip
        /// counts them and is not lit, its You box says to see them together, is:left finds it,
        /// and the page counts as many as its sections do at their ends; then pages of other
        /// kinds, known for the test, the same.
        /// </summary>
        private static IEnumerator StrictNamesRead(Probe p)
        {
            var fought = Pick(Kind.Creature, "Troll", "Greydwarf");
            var drops = fought?.TermLinks("drops");
            var drop = drops != null && drops.Count > 0 ? drops[0] : null;
            if (!p.Check(fought != null && drop != null, "a creature that drops something, to make up a character with")) yield break;
            var book = new CompendiumBook();
            book.Raise(fought.Key, KnownLevel.Known);
            // As in play, a drop seen falling is seen, and seen together with what dropped it.
            book.Raise(drop.Key, KnownLevel.Seen);
            book.Witness(fought.Key, drop.Key);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Strict);
            X.Select(fought);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view) && X.LeavingOut, 3);
            if (!p.Check(ReferenceEquals(X.Compendium, view) && X.LeavingOut, "the list is the made-up character's, under the strict rules")) yield break;
            var still = ScryPanel.Drawn(PanelPart.StillToFindOut);
            yield return Until(() => ScryPanel.Drawn(PanelPart.StillToFindOut) > still + 2, 3);
            p.Check(ScryPanel.FindsChipForTest != null && ScryPanel.FindsChipForTest != CompendiumWords.AllFoundOut, "while it leaves names out, its title chip counts them and is not lit", ScryPanel.FindsChipForTest);
            p.Check(ScryPanel.StillForTest.Contains("See it together"), "its You box says to see them together", string.Join("; ", ScryPanel.StillForTest));
            p.Check(view.Is(fought, "left", view.Today), "and is:left finds its page");
            yield return NamesCountedAsDrawn(p, fought);

            // Pages of other kinds count the names they leave out as their sections do, and is:left finds those the chip does not light.
            var pages = new[] { Kind.Item, Kind.Piece, Kind.Resource, Kind.Location, Kind.StatusEffect, Kind.Effect, Kind.Sound }
                .SelectMany(kind => X.Catalog.Where(e => e.Kind == kind && e.Links.Count >= 3).Take(2)).ToList();
            foreach (var page in pages) book.Raise(page.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Strict);
            foreach (var page in pages) yield return NamesCountedAsDrawn(p, page);
        }

        /// <summary>
        /// A page under either rules counts the names it leaves out where its sections count
        /// them at their ends, gathered without drawing as the panel draws them; and is:left finds
        /// it exactly when its title chip is not lit. Shown first, so it is drawn whole.
        /// </summary>
        private static IEnumerator NamesCountedAsDrawn(Probe p, Entry page)
        {
            X.Select(page);
            var repaints = ScryPanel.Repaints;
            yield return Until(() => ScryPanel.Repaints >= repaints + 3 && ScryPanel.NamesDrawnForTest.Of == page, 3);
            if (!X.CountingFinds)
            {
                p.Note($"{page.Name}: shown whole, nothing to count");
                yield break;
            }
            // The finds are gathered again at most once a second.
            yield return new WaitForSecondsRealtime(1.2f);
            var (of, left, folded) = ScryPanel.NamesDrawnForTest;
            var lined = ScryPanel.NamesLinedForTest;
            if (folded) p.Note($"{page.Name}: a section that names things is folded, its count not held against a draw");
            else p.Check(of == page && left == lined, $"{page.Name}: the page counts the names it leaves out as its sections do at their ends", $"sections {Numbers.Count(left)}, page {Numbers.Count(lined)}");
            var (found, all) = ScryPanel.FindsForTest;
            p.Check(X.Compendium.Is(page, "left", X.Compendium.Today) == (found < all), $"{page.Name}: is:left finds it exactly when its title chip is not lit",
                $"{Numbers.Count(found)} of {Numbers.Count(all)}, chip {ScryPanel.FindsChipForTest}");
        }
    }
}
