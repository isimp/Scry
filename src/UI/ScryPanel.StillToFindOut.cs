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
        /// an item comes from one, and the star levels and gear the stage still lacks; under the
        /// loose rules, which show every number, only the names it leaves out until met; none
        /// otherwise. With it, how much of all it keeps back was found out (<see cref="PageFinds"/>),
        /// each deed's proof once.
        /// </summary>
        private static (List<(string Label, string Value)> Still, PageFinds Finds) StillToFindOut(Explorer explorer, Facts facts)
        {
            var finds = new PageFinds();
            if (!explorer.CountingFinds || facts == null) return (NothingStill, finds);
            if (!explorer.LeavingOut)
            {
                _gatheredLined = PageNames(explorer, facts, finds);
                return (CompendiumWords.StillToFindOut(Array.Empty<string>(), Array.Empty<string>(), 0, 0, Array.Empty<Tally>(), namesApart: finds.NamesApart, namesUnmet: finds.NamesUnmet), finds);
            }
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
                    if (explorer.LeavesOut(item.Prefab, GameShows(explorer, row, item), TieOf(row))) continue;
                    notCounted |= Counted(explorer, row, item.Prefab, finds);
                }
                foreach (var item in row.LineItems) notCounted |= Counted(explorer, row, item, finds);
                if (notCounted && !uncounted.Contains(row.Tally)) uncounted.Add(row.Tally);
            }
            Sourced(explorer, facts, deeds, uncounted, finds);
            var (starsUnseen, gearUnseen) = SightLacks(explorer, facts, finds);
            _gatheredLined = PageNames(explorer, facts, finds);
            return (CompendiumWords.StillToFindOut(deeds, types, attacks, spots, uncounted, starsUnseen, gearUnseen, explorer.Selected?.Kind == Kind.Item, finds.NamesApart, finds.NamesUnmet), finds);
        }

        /// <summary>
        /// Each name the page shown could leave out until seen together with it, or under the loose
        /// rules until met (<see cref="Explorer.Hideable"/>), counted as left out or shown, wherever the page draws it: its facts and notes, the
        /// chips, tables and chains of its rows, where it comes from, its biomes, what plays it and
        /// where it is found, the links its topics show (not Linked's), and the lists it plays in. Gathered from what the page
        /// draws, as the numbers are, without drawing it, so a search finds what the chip counts;
        /// the parts of a list played last, which show only while it plays, are no part of the page.
        /// With it, how many of them the sections count at their ends (<see cref="StrictLeftLine"/>),
        /// the rest reading a smudge in a cell or a chain, for the self-test to hold against a draw.
        /// </summary>
        private static int PageNames(Explorer explorer, Facts facts, PageFinds finds)
        {
            var page = explorer.Selected;
            if (!explorer.CountingFinds || page == null) return 0;
            var names = new NameTally(explorer, finds);
            bool Hinged(string part) => part != null && facts.PartHinges.TryGetValue(part, out var hinge) && explorer.HidesHinged(hinge);

            for (var i = 0; i < facts.Pairs.Count; i++)
            {
                var key = facts.Pairs[i].Key;
                if (Hinged(facts.PairParts[i]) || !facts.Links.TryGetValue(key, out var linked)) continue;
                // A line only naming what the page lists waits for that to be met, said nowhere (FactPair).
                if (facts.Listing.Contains(key) && LeftOut(explorer, linked)) continue;
                names.Named(linked, GameShows(explorer, facts, key));
            }
            foreach (var row in facts.Rows)
            {
                if (!Hinged(row.Part) && !UnmetWhole(explorer, row)) RowNames(explorer, row, names);
            }
            foreach (var row in facts.UseRows) RowNames(explorer, row, names);
            foreach (var s in facts.Where)
            {
                names.Name(s.Prefab, s.AllPlay ? explorer.NotMet(s.Prefab) : explorer.LeavesOut(s.Prefab, false, TieOfSource(s.Record)) || explorer.LeavesOutAll(s.MetIn));
            }
            if (page.Kind != Kind.Biome) foreach (var biome in page.Biomes) names.Named(EntryKeys.For(Kind.Biome, biome));
            foreach (var user in UserKeys(explorer, page)) names.Named(user);
            if (PlacesShown(page))
            {
                foreach (var place in page.FoundIn) if (Places.LocationNamed(explorer.Catalog, place) is Entry location) names.Named(location.Key);
            }
            var rows = new LinkRowList();
            var placed = new PlacedRows();
            // Linked's own rows are none of the page's finds (LinksSection); what a topic shows of them is.
            MakeLinkRows(explorer, page, rows, placed);
            foreach (var pair in placed)
            {
                var items = pair.Key == LinkWords.Carries ? CarriedBesidesAttacks(facts, pair.Value) : pair.Value;
                var onceMet = FactLayout.ShownOnceMet(page.Kind, pair.Key);
                foreach (var item in items) names.Named(item.Key, onceMet);
            }
            foreach (var row in PlaysInRowsOf(page))
            {
                // A row none of whose owners the rules keep is left out whole, its owners counted; what plays along with it goes unseen.
                if (!row.Owners.Exists(o => o.Key == null || !explorer.LeavesOut(o.Key)))
                {
                    foreach (var owner in row.Owners) names.Name(owner.Key, true);
                    continue;
                }
                foreach (var owner in row.Owners) if (owner.Key != null) names.Named(owner.Key);
                if (row.List is EffectList) foreach (var member in row.Members) names.Named(member);
            }
            return names.Lined;
        }

        /// <summary>How many names the page gathered last leaves out where a section's last line counts them (<see cref="PageNames"/>).</summary>
        private static int _gatheredLined;

        /// <summary>The names a page could leave out, as they are gathered (<see cref="PageNames"/>).</summary>
        private sealed class NameTally
        {
            private readonly Explorer _explorer;
            private readonly PageFinds _finds;

            public NameTally(Explorer explorer, PageFinds finds)
            {
                _explorer = explorer;
                _finds = finds;
            }

            /// <summary>How many of the names left out a section's last line counts; the rest read a smudge where they stand.</summary>
            public int Lined { get; private set; }

            /// <summary>A name left out, or shown where it could have been; one the page could never leave out is no find.</summary>
            public void Name(string key, bool kept, bool lined = true, bool gameShown = false)
            {
                if (kept)
                {
                    _finds.Name(true, _explorer.NotMet(key));
                    if (lined) Lined++;
                }
                else if (_explorer.Hideable(key, gameShown)) _finds.Name(false);
            }

            /// <summary>A name the page leaves out as any list does (<see cref="Explorer.LeavesOut(string, bool, Tie?)"/>).</summary>
            public void Named(string key, bool gameShown = false, Tie? tie = null) => Name(key, _explorer.LeavesOut(key, gameShown, tie), true, gameShown);
        }

        /// <summary>
        /// The names one of the page's rows draws, as <see cref="FactRow"/> draws them: none of a
        /// row the game does not list yet, nor of wares counted as waiting, nor of a table kept
        /// back whole or an attack's line folded while it waits.
        /// </summary>
        private static void RowNames(Explorer explorer, Facts.Row row, NameTally names)
        {
            if (row.GameShown && explorer.RecipeWaits(row.Recipe)) return;
            if (row.WaitsFor != null && UnlockRows.Of(row.WaitsFor, explorer.NamesOnlyMet, WorldKeySet) == UnlockShown.Counted) return;
            if (row.Cells != null) return;
            if (row.Columns != null)
            {
                // A table every line of which is left out counts them all; one every line of which is kept back names nothing (TableRow).
                if (AllLinesLeftOut(explorer, row))
                {
                    foreach (var line in row.Lines) names.Name(line.Link, true);
                    return;
                }
                if (explorer.KeepsBackWhole(row.LineProofs, row.Lines.Count) != null) return;
                for (var l = 0; l < row.Lines.Count; l++)
                {
                    var link = row.Lines[l].Link;
                    if (LineLeftOut(explorer, row, link))
                    {
                        names.Name(link, true);
                        continue;
                    }
                    if (AttackKept(explorer, row, l)) continue;
                    if (!string.IsNullOrEmpty(link)) names.Named(link, LinesOnceMet(explorer, row));
                    // A cell naming what was not seen together reads a smudge, unless its numbers are kept back already (CellDrawn).
                    if (LineKept(explorer, row, l, out _)) continue;
                    for (var c = 1; c < row.Lines[l].Cells.Length; c++)
                    {
                        if (!row.CellLinks.TryGetValue((l, c), out var to) || CellKept(explorer, row, l, c, out _)) continue;
                        names.Name(to, explorer.LeavesOut(to), lined: false);
                    }
                }
                return;
            }
            if (row.Chain != null)
            {
                // A step not seen together reads a smudge in its chip (ChainRow).
                var shown = explorer.Selected;
                foreach (var step in row.Chain.Steps)
                {
                    foreach (var key in step)
                    {
                        if (shown != null && (shown.Name == key || shown.Key == key)) continue;
                        names.Name(key, explorer.LeavesOut(row.Chain, key), lined: false);
                    }
                }
                return;
            }
            var listed = row.Lists ? Listed(explorer, row.Items, i => i.Prefab) : row.Items;
            foreach (var item in listed) names.Named(item.Prefab, GameShows(explorer, row, item), TieOf(row));
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
                if (source.AllPlay || source.Record == null || explorer.LeavesOut(source.Prefab, false, TieOfSource(source.Record)) || explorer.LeavesOutAll(source.MetIn)) continue;
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
            // Where the server keeps nothing seen up close, nothing of the sort is to find: the rows would never come true.
            if (!(explorer.Selected is Entry page) || page.Kind != Kind.Creature || explorer.SeenReach <= 0f) return (stars, gear);
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
