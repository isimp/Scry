using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for families: several prefabs that are one thing to a player.</summary>
    internal static partial class SelfTest
    {
        /// <summary>The three tar pits, one place's kinds.</summary>
        private static readonly string[] TarPits = { "TarPit1", "TarPit2", "TarPit3" };

        /// <summary>
        /// With the compendium on, a family is one row and one page switching among its kinds met:
        /// a made-up character under the strict rules who knows the first two tar pits and has only
        /// seen the third finds one "Tar pit" row, its page switching among the three, the third
        /// closed, and the kinds known set side by side where they differ; the blackmetal tower
        /// shield, known with its Fallen Warrior copy, is one row a search for the copy's prefab
        /// finds, never the Shadow copy it did not meet. Each way of making the shield is shown
        /// once. The families of the catalog are noted.
        /// </summary>
        private static IEnumerator FamiliesFold(Probe p)
        {
            var pits = TarPits.Select(n => Pick(Kind.Location, n)).ToList();
            var shield = Pick(Kind.Item, "ShieldBlackmetalTower");
            var fallen = Pick(Kind.Item, "FW_ShieldBlackmetalTower");
            var shadow = Pick(Kind.Item, "SP_ShieldBlackmetalTower");
            if (pits.Any(e => e == null) || shield == null || fallen == null || shadow == null)
            {
                p.Skip("the tar pits or the blackmetal tower shield and its copies are not in this world's catalog");
                yield break;
            }
            p.Check(pits[0].Family != null && pits[0].Family.Kinds.SequenceEqual(pits), "the three tar pits are one family's kinds", pits[0].Family == null ? "none" : string.Join(", ", pits[0].Family.Members.Select(m => m.Name)));
            p.Check(shield.Family != null && shield.Family.CopyOf(fallen) == shield && shield.Family.CopyOf(shadow) == shield, "the shield's FW_ and SP_ prefabs are copies of it",
                shield.Family == null ? "none" : string.Join(", ", shield.Family.Members.Select(m => m.Name + " = " + shield.Family.Label(m))));

            var book = new CompendiumBook();
            book.Raise(pits[0].Key, KnownLevel.Known);
            book.Raise(pits[1].Key, KnownLevel.Known);
            book.Raise(pits[2].Key, KnownLevel.Seen);
            book.Raise(shield.Key, KnownLevel.Known);
            book.Raise(fallen.Key, KnownLevel.Known);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Strict);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);

            X.KindFilter = null;
            X.Text = "tar pit";
            var rows = X.Results.Where(e => e.Family == pits[0].Family).ToList();
            p.Check(rows.Count == 1 && rows[0] == pits[0], "the tar pits are one row, by the first, known", string.Join(", ", X.Results.Select(e => e.Name)));
            p.Check(X.ShownAs(pits[0]) == "Tar pit" && X.FamilyNote(pits[0]) == CompendiumWords.FamilyNote(3, 2), "named Tar pit, telling the kinds met", X.ShownAs(pits[0]) + " / " + X.FamilyNote(pits[0]));
            X.Text = "FW_ShieldBlackmetalTower";
            p.Check(X.Results.Count == 1 && X.Results[0] == shield, "the Fallen Warrior copy's prefab finds the shield's row", string.Join(", ", X.Results.Select(e => e.Name)));
            X.Text = "SP_ShieldBlackmetalTower";
            p.Check(X.Results.Count == 0, "the Shadow copy never met is not found", string.Join(", ", X.Results.Select(e => e.Name)));

            X.Text = "tar pit";
            X.Select(pits[0]);
            yield return Until(() => ScryPanel.KindSegmentsForTest.Count == 3, 5);
            p.Check(ScryPanel.KindSegmentsForTest.Select(s => s.Member).SequenceEqual(pits), "the tar pit's page switches among its three kinds", string.Join(", ", ScryPanel.KindSegmentsForTest.Select(s => s.Member.Name)));
            var differed = ScryPanel.Drawn(PanelPart.KindsDiffer);
            yield return GoTo("kinds");
            yield return Until(() => ScryPanel.Drawn(PanelPart.KindsDiffer) > differed, 3);
            if (ScryPanel.Drawn(PanelPart.KindsDiffer) > differed) yield return Picture(p, "the tar pit's kinds side by side");
            else p.Note("the tar pits' kinds known show alike in their lines, so no table");
            X.SwitchTo(pits[2]);
            yield return Until(() => X.Selected == pits[2], 3);
            p.Check(X.Selected == pits[2] && X.SelectedIndex == X.Results.ToList().IndexOf(pits[0]) && !X.CanGoBack, "switching to the third shows its closed page on the same row", $"{X.Selected?.Name} at {Numbers.Count(X.SelectedIndex)}");
            yield return Picture(p, "the third tar pit, only seen, its switch leading back");
            X.SwitchTo(pits[0]);

            X.Text = "blackmetal tower";
            X.Select(shield);
            yield return Until(() => ScryPanel.KindSegmentsForTest.Count == 2, 5);
            p.Check(ScryPanel.KindSegmentsForTest.Select(s => s.Member).SequenceEqual(new[] { shield, fallen }), "the shield's page switches to its Fallen Warrior copy alone", string.Join(", ", ScryPanel.KindSegmentsForTest.Select(s => s.Member.Name)));
            yield return Picture(p, "the blackmetal tower shield's page");
            Session.ShowKnownInstead(null);
            yield return Until(() => X.Compendium != view, 3);
            X.Text = "";

            // Each way of making the shield once, whatever the game holds again.
            var made = Facts.For(shield).Rows.Count(r => r.Title != null && r.Title.StartsWith("Made at", System.StringComparison.Ordinal));
            var held = ObjectDB.instance == null ? 0 : ObjectDB.instance.m_recipes.Count(r => r != null && r.m_enabled && r.m_item != null && r.m_item.gameObject.name == shield.Name);
            p.Check(made == 1, "the shield's recipe shows once", $"{Numbers.Count(made)} rows of {Numbers.Count(held)} recipes held");
            if (ObjectDB.instance != null)
            {
                var again = ObjectDB.instance.m_recipes.Where(r => r != null && r.m_enabled && r.m_item != null).GroupBy(r => r.m_item.gameObject.name).Where(g => g.Count() > 1).ToList();
                p.Note($"{Numbers.Count(again.Count)} items the game holds several enabled recipes for: "
                    + string.Join("; ", again.Take(12).Select(g => g.Key + " (" + string.Join(", ", g.Select(r => r.name)) + ")")));
            }

            var families = X.Catalog.Select(e => e.Family).Where(f => f != null).Distinct().ToList();
            var byKind = families.GroupBy(f => f.Head.Kind).Select(g => $"{g.Key}: {Numbers.Count(g.Count())}");
            p.Note($"{Numbers.Count(families.Count)} families ({string.Join(", ", byKind)}); some: "
                + string.Join("; ", families.Take(24).Select(f => f.Name + " = " + string.Join(" / ", f.Members.Select(m => f.Label(m))))));
        }
    }
}
