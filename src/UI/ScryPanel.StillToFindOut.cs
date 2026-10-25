using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What a page under the strict rules still keeps back, and how much of it was found out (<see cref="PageFinds"/>).</summary>
    internal static partial class ScryPanel
    {
        /// <summary>
        /// What a page under the strict rules still keeps back, gathered from what it draws: the
        /// facts whose numbers wait for a deed, the weak spots not found, the damage types not told,
        /// the attacks not taken, the drops or yields not counted, each cell and each line of where
        /// an item comes from one, and the star levels and gear the stage still lacks; none
        /// otherwise. With it, how much of all it keeps back was found out (<see cref="PageFinds"/>),
        /// each deed's proof once.
        /// </summary>
        private static (List<(string Label, string Value)> Still, PageFinds Finds) StillToFindOut(Explorer explorer, Facts facts)
        {
            var finds = new PageFinds();
            if (!explorer.LeavingOut || facts == null) return (NothingStill, finds);
            var deeds = new List<string>();
            var counted = new HashSet<string>(System.StringComparer.Ordinal);
            var spots = 0;
            // A topic that hinges on a thing not met is left out whole (Facts.PartHinges): nothing of it to find.
            bool Hinged(string part) => part != null && facts.PartHinges.TryGetValue(part, out var hinge) && explorer.HidesHinged(hinge);
            for (var i = 0; i < facts.Pairs.Count; i++)
            {
                var pair = facts.Pairs[i];
                if (Hinged(facts.PairParts[i]) || !facts.Proofs.TryGetValue(pair.Key, out var proof)) continue;
                var kept = explorer.KeepsBack(proof);
                if (counted.Add(proof)) finds.Count(kept);
                if (!kept) continue;
                if (EntryKeys.IsWeakSpot(proof)) spots++;
                else deeds.Add(proof);
            }
            var types = new List<string>();
            var attacks = 0;
            var uncounted = new List<Tally>();
            foreach (var row in facts.Rows)
            {
                if (Hinged(row.Part)) continue;
                if (row.HitReveals && row.Cells != null) Typed(explorer, row, types, finds);
                foreach (var proof in row.LineProofs.Values.Concat(row.CellProofs.Values))
                {
                    var kept = explorer.KeepsBack(proof);
                    if (counted.Add(proof)) finds.Count(kept);
                    if (kept && !deeds.Contains(proof)) deeds.Add(proof);
                }
                if (row.AttackCells != null) attacks += Attacked(explorer, row, finds);
                if (row.Tally == Tally.None) continue;
                // Each counted cell is one find, as a damage type or an attack is.
                var notCounted = false;
                foreach (var item in row.Items)
                {
                    if (explorer.LeavesOut(item.Prefab, GameShows(explorer, row, item))) continue;
                    notCounted |= Counted(explorer, row, item.Prefab, finds);
                }
                foreach (var item in row.LineItems) notCounted |= Counted(explorer, row, item, finds);
                if (notCounted && !uncounted.Contains(row.Tally)) uncounted.Add(row.Tally);
            }
            Sourced(explorer, facts, deeds, uncounted, finds);
            var (starsUnseen, gearUnseen) = SightLacks(explorer, facts, finds);
            return (CompendiumWords.StillToFindOut(deeds, types, attacks, spots, uncounted, starsUnseen, gearUnseen), finds);
        }

        /// <summary>A counted cell of a row, as found or kept back; whether it is kept back.</summary>
        private static bool Counted(Explorer explorer, Facts.Row row, string item, PageFinds finds)
        {
            var amount = explorer.StrictAmount(row.Tally, row.TallyOf, item);
            if (amount == null) return false;
            var kept = amount == CompendiumWords.Hidden;
            finds.Count(kept);
            return kept;
        }

        /// <summary>
        /// Each line of where an item comes from that the page draws (<see cref="WhereBlock"/>),
        /// counted as found or kept back where the rules put the character's count or a smudge in
        /// its place (<see cref="Explorer.StrictSourceAmount"/>): a way's count among the tallies
        /// still to count, a producer's pace among the deeds. Lines left out whole are not counted.
        /// </summary>
        private static void Sourced(Explorer explorer, Facts facts, List<string> deeds, List<Tally> uncounted, PageFinds finds)
        {
            foreach (var source in facts.Where)
            {
                if (source.AllPlay || source.Record == null || explorer.LeavesOut(source.Prefab) || explorer.LeavesOutAll(source.MetIn)) continue;
                var amount = explorer.StrictSourceAmount(source.Record);
                if (amount == null) continue;
                var kept = amount == CompendiumWords.Hidden;
                finds.Count(kept);
                if (!kept) continue;
                var shownBy = SourceWords.ShownBy(source.Record);
                if (shownBy != null)
                {
                    if (!deeds.Contains(shownBy)) deeds.Add(shownBy);
                    continue;
                }
                var by = SourceWords.CountedBy(source.Record);
                if (by != null && !uncounted.Contains(by.Value)) uncounted.Add(by.Value);
            }
        }

        /// <summary>What sight still lacks for a creature's stage: the star levels and the gear not seen up close, each counted as found or kept back.</summary>
        private static (List<int> Stars, List<string> Gear) SightLacks(Explorer explorer, Facts facts, PageFinds finds)
        {
            var stars = new List<int>();
            var gear = new List<string>();
            if (!(explorer.Selected is Entry page) || page.Kind != Kind.Creature) return (stars, gear);
            var offered = explorer.StarsOffered(page);
            // Only the stars the stage can stand (StarsOffered, by the creature's level looks): one it cannot show is nothing to see.
            var most = Math.Min(facts.Stars, Math.Max(0, page.ExtraLevels));
            for (var level = 1; level <= most; level++)
            {
                var seen = offered.Contains(level + 1);
                finds.Count(!seen);
                if (!seen) stars.Add(level);
            }
            // Each item counts; the rows name an item once, and one never met as any name never met.
            foreach (var (prefab, name) in facts.Gear)
            {
                var seen = explorer.WearsShown(page.Key, prefab);
                finds.Count(!seen);
                var shown = explorer.NotMet(prefab) ? CompendiumWords.Hidden : name;
                if (!seen && !gear.Contains(shown)) gear.Add(shown);
            }
            return (stars, gear);
        }

        /// <summary>Each damage type a grid a hit reveals tells, counted as found or kept back, those kept back named.</summary>
        private static void Typed(Explorer explorer, Facts.Row row, List<string> types, PageFinds finds)
        {
            foreach (var cell in row.Cells)
            {
                if (cell.Tone == Tone.Quiet) continue;
                var hidden = explorer.HidesDamage(cell.Type);
                finds.Count(hidden);
                if (hidden) types.Add(cell.Type);
            }
        }

        /// <summary>Each attack an attack table's lines play, counted as found or kept back; how many are kept back.</summary>
        private static int Attacked(Explorer explorer, Facts.Row row, PageFinds finds)
        {
            var kept = 0;
            foreach (var (_, link) in row.Lines)
            {
                if (!EntryKeys.PlaysAttack(link, out _, out _)) continue;
                var hides = explorer.HidesAttack(link);
                finds.Count(hides);
                if (hides) kept++;
            }
            return kept;
        }

        private static readonly List<(string Label, string Value)> NothingStill = new List<(string, string)>();
    }
}
