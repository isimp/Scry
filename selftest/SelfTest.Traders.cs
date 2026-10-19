using System.Collections;
using System.Linq;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// Every trader stands facing the camera on the stage: each is rigged as a person and turned
        /// to its front as its animator poses it, though its prefab faces otherwise; each pictured.
        /// </summary>
        private static IEnumerator TradersFaceCamera(Probe p)
        {
            var traders = X.Catalog.Where(e => e.Kind == Kind.Trader).ToList();
            if (traders.Count == 0)
            {
                p.Skip("there are no traders");
                yield break;
            }
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            foreach (var trader in traders)
            {
                Select(trader);
                yield return Until(() => Stage.SubjectFacesCamera() != null, 5);
                // A moment of its idle playing, as a player sees it.
                yield return Until(() => false, 0.5f);
                var faces = Stage.SubjectFacesCamera();
                p.Note($"{trader.ShownName}: turned {Numbers.Fixed(Stage.LastFrontTurn, 0)} degrees to its front");
                if (faces == null) p.Note($"{trader.ShownName} is not rigged as a person, so its facing is not told");
                else p.Check(faces == true, $"{trader.ShownName} stands facing the camera");
                yield return Picture(p, "trader " + trader.Name + " facing");
            }
        }

        /// <summary>
        /// Haldor's page as a player meets it: a trader of the Traders tab, selling everything his
        /// store does, the game's upgrades among it with no page to go to; with the compendium on,
        /// a made-up character who knows him sees the rows waiting for a boss not yet fallen in this
        /// world counted, not named: under the loose rules each row's title with how many, under the
        /// strict rules one line at the section's end, both set apart as hints. Pictured each way.
        /// </summary>
        private static IEnumerator TraderPage(Probe p)
        {
            var haldor = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Trader && e.Name == "Haldor");
            if (haldor == null)
            {
                p.Skip("there is no Haldor in the catalog");
                yield break;
            }
            p.Note("traders: " + string.Join(", ", X.Catalog.Where(e => e.Kind == Kind.Trader).Select(e => e.ShownName)));
            var facts = Facts.For(haldor);
            var sells = facts.Rows.Where(r => r.Title.StartsWith(StationWords.Sells(null), System.StringComparison.Ordinal)).ToList();
            p.Note(string.Join(" | ", sells.Select(r => r.Title + ": " + string.Join(", ", r.Items.Select(i => i.Name + " " + i.Amount)))));
            p.Check(sells.Count > 0, "Haldor's page tells what he sells");
            var upgrades = sells.SelectMany(r => r.Items).Where(i => string.IsNullOrEmpty(i.Prefab)).ToList();
            if (upgrades.Count > 0) p.Check(upgrades.All(u => u.Name.Length > 0 && u.Amount.Length > 0), "the game's upgrades he sells are named and priced, with no page to go to", string.Join(", ", upgrades.Select(u => u.Name)));
            else p.Note("he sells no upgrade without an item in this game");

            var locked = sells.Where(r => r.WaitsFor != null && (ZoneSystem.instance == null || !ZoneSystem.instance.GetGlobalKey(r.WaitsFor))).ToList();
            p.Note($"{Numbers.Count(locked.Count)} of his rows wait for a boss not yet fallen in this world");
            var book = new CompendiumBook();
            book.Raise(haldor.Key, KnownLevel.Known);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Loose);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            X.Select(haldor);
            yield return null;
            p.Check(X.FoundOf(Kind.Trader) == 1, "the Traders tab lists the one trader the character knows", Numbers.Count(X.FoundOf(Kind.Trader)));
            // Rigged as a person, he stands facing the camera, though his prefab faces otherwise.
            yield return Until(() => Stage.SubjectFacesCamera() != null, 5);
            var faces = Stage.SubjectFacesCamera();
            if (faces == null) p.Note("Haldor is not rigged as a person here, so his facing is not told");
            else p.Check(faces == true, "Haldor stands facing the camera");
            var unlock = ScryPanel.Drawn(PanelPart.ToUnlock);
            var hints = ScryPanel.Drawn(PanelPart.HintLine);
            ScryPanel.UnlockTitlesForTest.Clear();
            ScryPanel.KeepUnlockTitlesForTest = true;
            yield return PictureOf(p, "facts", "trader Haldor loose");
            ScryPanel.KeepUnlockTitlesForTest = false;
            // Rows reading alike once a boss not met is kept back ("Sells once ??? is defeated") are told as one.
            var twice = ScryPanel.UnlockTitlesForTest.GroupBy(t => t).Where(g => g.Count() > 1).Select(g => g.Key.Title).Distinct().ToList();
            p.Note("waiting rows drawn: " + string.Join("; ", ScryPanel.UnlockTitlesForTest.Select(t => t.Title).Distinct()));
            p.Check(twice.Count == 0, "rows waiting for bosses not met, alike once kept back, are told as one", string.Join("; ", twice));
            if (locked.Count > 0)
            {
                p.Check(ScryPanel.Drawn(PanelPart.ToUnlock) > unlock && ScryPanel.Drawn(PanelPart.HintLine) > hints,
                    "under the loose rules each row waiting for a boss not yet fallen says how many it holds, as a hint", Numbers.Count(ScryPanel.Drawn(PanelPart.ToUnlock) - unlock));
                view.Tell(book, CompendiumRule.Strict);
                yield return Until(() => X.LeavingOut, 3);
                unlock = ScryPanel.Drawn(PanelPart.ToUnlock);
                yield return PictureOf(p, "facts", "trader Haldor strict");
                p.Check(ScryPanel.Drawn(PanelPart.ToUnlock) > unlock, "under the strict rules they are counted at the section's end", Numbers.Count(ScryPanel.Drawn(PanelPart.ToUnlock) - unlock));
            }
            else p.Note("every boss his wares wait for has fallen in this world: nothing to count");
            Session.ShowKnownInstead(null);
        }
    }
}
