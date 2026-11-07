using System.Collections;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for a page showing and keeping all its kinds on the map at once.</summary>
    internal static partial class SelfTest
    {
        /// <summary>The kinds of Petrified Bone that keep spots, ribs and skull, by their keys.</summary>
        private static readonly string[] BoneKinds = { "giant_ribs", "giant_skull" };

        /// <summary>
        /// The ribs and the skull of Petrified Bone, which the game names alike, are one page's two
        /// kinds on the map, as the tar pits are one place's three: a made-up character who knows them
        /// gets Show all and Keep all beside Show on map and Keep on the map on each page, their
        /// tips naming the kinds; pictured.
        /// </summary>
        private static IEnumerator AllKindsOnAPage(Probe p)
        {
            var ribs = X.Catalog.FirstOrDefault(e => e.Name == "giant_ribs");
            var skull = X.Catalog.FirstOrDefault(e => e.Name == "giant_skull");
            var pits = TarPits.Select(n => X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && e.Name == n)).ToList();
            if (ribs == null || skull == null || pits.Any(e => e == null))
            {
                p.Skip("there are no giant ribs and skull, or no tar pits, in this game's catalog");
                yield break;
            }
            p.Check(ribs.Kind == Kind.Resource && ribs.HoverName != null && ribs.HoverName == skull.HoverName && MapSpots.FamilyNameOf(skull) == CatalogBuilder.Localize(ribs.HoverName),
                "the ribs and the skull go by the name the game shows when looking at them", $"{ribs.HoverName} / {skull.HoverName}: {MapSpots.FamilyNameOf(skull)}");

            var book = new CompendiumBook();
            // Known, so their pages open: a page only seen is closed and offers nothing for the map.
            foreach (var met in new[] { ribs, skull }.Concat(pits)) book.Raise(met.Key, KnownLevel.Known);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Strict);
            Session.ShowKnownInstead(view);
            try
            {
                if (!Session.IsOpen) Session.Show(null);
                KeepView();
                ScryPanel.Compact = false;
                yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
                var kinds = MapSpots.KindsOf(skull, X).Select(MapSpots.KindOf).ToList();
                p.Check(kinds.SequenceEqual(BoneKinds), "Petrified Bone's page offers its ribs and its skull as its kinds", string.Join(", ", kinds));
                var pitKinds = MapSpots.KindsOf(pits[0], X).Count;
                p.Check(pitKinds == pits.Count, "a tar pit's page offers all three tar pits", Numbers.Count(pitKinds));

                var names = MapSpots.KindsOf(skull, X).Select(k => k.ShownName).ToList();
                var tip = SpotWords.ShowAllTip(names);
                p.Check(names.Count == 2 && names.All(n => tip.Contains(n)), "Show all names both kinds in its tip", tip);
                Select(skull);
                yield return Until(() => X.Selected == skull, 3);
                yield return Settled();
                var shows = ScryPanel.Drawn(PanelPart.PageShowAll);
                var keeps = ScryPanel.Drawn(PanelPart.PageKeepAll);
                yield return Until(() => ScryPanel.Drawn(PanelPart.PageShowAll) > shows && ScryPanel.Drawn(PanelPart.PageKeepAll) > keeps, 3);
                p.Check(ScryPanel.Drawn(PanelPart.PageShowAll) > shows && ScryPanel.Drawn(PanelPart.PageKeepAll) > keeps, "the page draws Show all 2 kinds and Keep all 2 kinds beside its own two");
                yield return Picture(p, "Petrified Bone's page, its map actions");
            }
            finally
            {
                Session.ShowKnownInstead(null);
            }
        }
    }
}
