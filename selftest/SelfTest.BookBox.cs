using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for how the compendium's box stands its rows.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// The compendium's box stands its rows in columns: each level of the world's sources at
        /// one place down the box, the rules and the floor's biome in the same columns, the floor's
        /// arrows as far from its biome on either side, every ground's pin in one column after the
        /// levels; each segment as wide as its words drawn lit, none over another, all inside the
        /// box. Pictured.
        /// </summary>
        private static IEnumerator CompendiumBoxColumns(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            if (!p.Check(book != null, "the character's book is read")) yield break;
            if (book.Rule == CompendiumRule.Off)
            {
                p.Skip("the character plays with the compendium off, so its box shows its rules alone");
                yield break;
            }
            Session.ShowKnownInstead(null);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            var drawn = ScryPanel.Drawn(PanelPart.SourcesControl);
            ScryPanel.HoldCompendiumBox(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.SourcesControl) > drawn + 2, 3);
            var cells = ScryPanel.BookCellsForTest.ToList();
            if (!p.Check(cells.Any(c => c.Row == "rules") && cells.Any(c => c.Column >= 0 && c.Row != "rules" && c.Row != "floor"), "the box draws its rules and the world's sources", $"{Numbers.Count(cells.Count)} parts")) yield break;

            // Whatever stands first in a column, on any row, starts at the same place.
            var ragged = cells.Where(c => c.Column >= 0).GroupBy(c => c.Column)
                .Where(g => g.Max(c => c.At.x) - g.Min(c => c.At.x) > 0.5f)
                .Select(g => $"column {Numbers.Count(g.Key)}: " + string.Join(", ", g.Select(c => $"{c.Row} {c.Label} at {Numbers.Fixed(c.At.x, 0)}")))
                .ToList();
            p.Check(ragged.Count == 0, "each level stands at one place down the box, the rules and the floor in the same columns", string.Join("; ", ragged.Take(3)));
            var pins = cells.Where(c => c.Column == -1).ToList();
            p.Check(pins.Count == 0 || pins.Max(c => c.At.x) - pins.Min(c => c.At.x) <= 0.5f, "every ground's pin stands in one column", string.Join(", ", pins.Select(c => $"{c.Row} at {Numbers.Fixed(c.At.x, 0)}")));
            var lower = cells.FirstOrDefault(c => c.Row == "floor" && c.Label == "lower");
            var raise = cells.FirstOrDefault(c => c.Row == "floor" && c.Label == "raise");
            var biome = cells.FirstOrDefault(c => c.Row == "floor" && c.Column >= 0);
            if (lower.Row != null && raise.Row != null && biome.Row != null)
            {
                var (left, right) = (biome.At.x - lower.At.xMax, raise.At.x - biome.At.xMax);
                p.Check(Mathf.Abs(left - right) <= 0.5f && left > 0f, "the floor's arrows stand as far from its biome on either side", $"{Numbers.Fixed(left, 1)} and {Numbers.Fixed(right, 1)}");
            }
            else p.Note("no floor in the box, so its arrows are not measured");
            var clipped = cells.Where(c => !c.Fits).Select(c => $"{c.Row} {c.Label} {Numbers.Fixed(c.At.width, 0)} wide").ToList();
            p.Check(clipped.Count == 0, "each segment is as wide as its words drawn lit, so the one chosen is never clipped", string.Join(", ", clipped));
            var over = new List<string>();
            for (var i = 0; i < cells.Count; i++)
                for (var j = i + 1; j < cells.Count; j++)
                    if (cells[i].At.Overlaps(cells[j].At)) over.Add($"{cells[i].Row} {cells[i].Label} and {cells[j].Row} {cells[j].Label}");
            var box = ScryPanel.CompendiumBoxForTest;
            var outside = cells.Where(c => c.At.x < box.x - 0.5f || c.At.xMax > box.xMax + 0.5f || c.At.yMax > box.yMax + 0.5f).Select(c => $"{c.Row} {c.Label}").ToList();
            p.Check(over.Count == 0 && outside.Count == 0, "none of the box's parts stands over another or past the box", string.Join("; ", over.Concat(outside).Take(6)));
            yield return Picture(p, "compendium box in columns");
            ScryPanel.HoldCompendiumBox(false);
        }
    }
}
