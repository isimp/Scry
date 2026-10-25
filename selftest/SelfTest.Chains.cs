using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for chains with a step the character has not met.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// With the compendium on, a chain stands whole whatever the character met of it, a step not
        /// met reading ??? and going nowhere: a made-up character who knows every step of Ancient
        /// Seed's summoning chain but the last, The Elder's power, sees it whole through the altar,
        /// the power alone ???. Then every chain the character played has a step of not met is named,
        /// as those that stood hidden whole before.
        /// </summary>
        private static IEnumerator ChainsWithStepsNotMet(Probe p)
        {
            var seed = Pick(Kind.Item, "AncientSeed");
            if (seed == null)
            {
                p.Skip("there is no Ancient Seed");
                yield break;
            }
            if (!Knowledge.Summons().Any(s => s.Item == seed.Name))
            {
                p.Skip("the altars are known once the locations are read; not read yet");
                yield break;
            }
            var row = Facts.For(seed).Rows.FirstOrDefault(r => r.Chain != null && r.Title == ChainWords.Summoning);
            if (!p.Check(row != null, "Ancient Seed has a summoning chain")) yield break;
            var steps = row.Chain.Steps.SelectMany(s => s).ToList();
            var book = new CompendiumBook();
            foreach (var key in steps.Take(steps.Count - 1))
            {
                var entry = X.Find(key);
                if (entry != null) book.Raise(entry.Key, KnownLevel.Known);
            }
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Loose);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            ScryPanel.ChainsDrawnForTest.Clear();
            X.Select(seed);
            yield return GoTo("chain");
            yield return Until(() => ScryPanel.ChainsDrawnForTest.ContainsKey(ChainWords.Summoning), 3);
            var drawn = ScryPanel.ChainsDrawnForTest.TryGetValue(ChainWords.Summoning, out var d) ? d : new List<(string Key, bool Unmet)>();
            var said = string.Join(" > ", drawn.Select(s => s.Unmet ? CompendiumWords.Hidden : s.Key));
            p.Check(drawn.Count == steps.Count && drawn.Count(s => s.Unmet) == 1 && drawn.Count > 0 && drawn[drawn.Count - 1].Unmet,
                "the seed's summoning chain stands whole, its power alone not met reading as a dash", said);
            p.Check(drawn.Any(s => !s.Unmet && X.Find(s.Key)?.Kind == Kind.Location), "and passes through The Elder's altar", said);
            yield return PictureOf(p, "chain", "Ancient Seed's summoning chain, the power not met");
            Session.ShowKnownInstead(null);
            yield return Until(() => X.Compendium != view, 3);

            // The character played: every chain it has a step of not met, which stood hidden whole before.
            if (!X.NamesOnlyMet)
            {
                p.Note("the character plays with the compendium off, so every chain shows whole for it");
                yield break;
            }
            var chains = X.Catalog.SelectMany(e => Chains.Of(e.Name).Concat(Chains.Of(e.Key))).Distinct().ToList();
            var partly = chains.Where(c => c.Steps.SelectMany(s => s).Any(X.LeavesOut) && c.Steps.SelectMany(s => s).Any(k => !X.LeavesOut(k))).ToList();
            p.Note($"{Numbers.Count(chains.Count)} chains, {Numbers.Count(partly.Count)} of them the character played has met partly, now shown with a smudge for the rest: "
                + string.Join("; ", partly.Take(16).Select(c => c.Title + ": " + string.Join(" > ", c.Steps.Select(s => string.Join("/", s.Select(k => X.LeavesOut(k) ? CompendiumWords.Hidden : k)))))));
        }
    }
}
