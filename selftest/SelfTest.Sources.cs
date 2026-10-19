using System.Collections;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for what a world shows as known, source by source, in the compendium's box and on the pages.</summary>
    internal static partial class SelfTest
    {
        /// <summary>How each source of the character played showed before the test chose others, put back after it.</summary>
        private static SourceLevel[] _sourcesBefore;

        /// <summary>
        /// The compendium's box shows, while the compendium is on, a row of levels for each source
        /// of the world, the one chosen lit; choosing another keeps it in the character's book at
        /// once, and the levels it had are put back. A made-up character's book with a place found
        /// on shared maps and shown known lists it marked, its page saying so under the title;
        /// shown with its insides, its page names all it holds, met or not.
        /// </summary>
        private static IEnumerator WorldSourcesShown(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            if (!p.Check(book != null, "the character's book is read")) yield break;
            if (book.Rule == CompendiumRule.Off)
            {
                p.Skip("the character plays with the compendium off, so its box shows no sources");
                yield break;
            }
            _sourcesBefore = WorldSources.All.Select(book.Shows).ToArray();
            Session.ShowKnownInstead(null);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;

            var drawn = ScryPanel.Drawn(PanelPart.SourcesControl);
            ScryPanel.HoldCompendiumBox(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.SourcesControl) > drawn + 1, 3);
            p.Check(ScryPanel.Drawn(PanelPart.SourcesControl) > drawn, "the box shows what the world shows as known, a row for each source");
            p.Note("the sources show as " + string.Join(", ", WorldSources.All.Select(s => CompendiumWords.SourceName(s) + " " + CompendiumWords.LevelName(book.Shows(s)))));
            yield return Picture(p, "compendium box sources");

            // Shared maps raised a step is kept in the book at once; the levels it had come back after.
            var raised = book.Shows(WorldSource.SharedMaps) == SourceLevel.WithInsides ? SourceLevel.Known : book.Shows(WorldSource.SharedMaps) + 1;
            ScryPanel.ShowSource(WorldSource.SharedMaps, raised);
            yield return Until(() => KeptSources(CompendiumKeeper.FilePath).Contains("shows\tSharedMaps\t" + raised), 3);
            p.Check(book.Shows(WorldSource.SharedMaps) == raised && KeptSources(CompendiumKeeper.FilePath).Contains("shows\tSharedMaps\t" + raised),
                $"shared maps chosen {CompendiumWords.LevelName(raised)} is kept in the character's book");
            yield return Picture(p, "compendium box sources, shared maps raised");
            ScryPanel.HoldCompendiumBox(false);
            PutSourcesBack();

            // A made-up character: a place found on shared maps and shown known is marked as the source's.
            var place = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Location && Places.LabelOf(e) != null);
            if (place == null)
            {
                p.Note("no place with a page to show from a source");
                yield break;
            }
            var made = new CompendiumBook();
            made.FoundIn(CompendiumMap.World, WorldSource.SharedMaps, new[] { place.Name });
            made.Show(WorldSource.SharedMaps, SourceLevel.Known);
            var view = new CompendiumView(X.Entries);
            view.InWorld(CompendiumMap.World);
            view.Tell(made, CompendiumRule.Loose);
            Session.ShowKnownInstead(view);
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            p.Check(view.Of(place) == KnownLevel.Known && view.ByWorld(place) == WorldSource.SharedMaps, $"{place.ShownName} found on shared maps shows known, as the source's");
            var marks = ScryPanel.Drawn(PanelPart.FloorLine);
            X.Select(place);
            yield return Until(() => ScryPanel.Drawn(PanelPart.FloorLine) > marks, 3);
            p.Check(ScryPanel.Drawn(PanelPart.FloorLine) > marks, "its page says under the title where it is shown from");
            yield return PictureOf(p, "facts", "source shared maps known place");

            // Shown with its insides: its page names all it holds, met or not, nothing of it seen elsewhere.
            made.Show(WorldSource.SharedMaps, SourceLevel.WithInsides);
            view.Tell(made, CompendiumRule.Loose);
            yield return null;
            X.Select(place);
            yield return null;
            var held = Facts.For(place).Rows.Where(r => r.Items != null).SelectMany(r => r.Items).Select(i => i.Prefab)
                .FirstOrDefault(k => !string.IsNullOrEmpty(k) && X.Find(k) is Entry e && e.Kind != Kind.Location && view.Of(e) < KnownLevel.Seen);
            if (held == null) p.Note($"{place.ShownName}'s page names nothing the made-up character never met (its contents may not be read yet)");
            else
            {
                p.Check(view.HoldsNamed(place) && !X.LeavesOut(held) && !X.NotMet(held), $"shown with its insides, {place.ShownName}'s page names {held}, never met");
                p.Check(view.Of(X.Find(held)) < KnownLevel.Seen, "and it is seen nowhere else");
            }
            yield return PictureOf(p, "facts", "source shared maps place with insides");
            Session.ShowKnownInstead(null);
        }

        /// <summary>The lines of the character's book file that keep how its sources show.</summary>
        private static string KeptSources(string path) =>
            path != null && System.IO.File.Exists(path) ? string.Join("\n", System.IO.File.ReadAllLines(path).Where(l => l.StartsWith("shows\t", System.StringComparison.Ordinal))) : "";

        /// <summary>Puts back how the character's sources showed, should the part have stopped before it did.</summary>
        private static void PutSourcesBack()
        {
            if (_sourcesBefore == null) return;
            for (var s = 0; s < WorldSources.All.Length; s++) ScryPanel.ShowSource(WorldSources.All[s], _sourcesBefore[s]);
            _sourcesBefore = null;
        }
    }
}
