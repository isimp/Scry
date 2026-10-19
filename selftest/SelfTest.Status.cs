using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for a status effect's look on the stage's person and its page's text.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Feeling sick (the game's Puke): its look stands on the person's jaw at its own size in
        /// the world, as the game puts it on a character, plays out and plays again; under the
        /// loose rules its text keeps "well", which only an unseen place Scry names by its
        /// prefab's words (the Swamp's well) once hid.
        /// </summary>
        private static IEnumerator StatusLookRead(Probe p)
        {
            var sick = X.Find(EntryKeys.For(Kind.StatusEffect, "Puke"));
            if (sick == null)
            {
                p.Skip("there is no Feeling sick");
                yield break;
            }
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            Select(sick);
            yield return Until(() => Stage.Subject != null && Looks.LookForTest.Any(c => c != null), 10);
            var look = Looks.LookForTest.Where(c => c != null).ToList();
            if (!p.Check(Stage.Subject != null && look.Count > 0, "its look is put on the person", Numbers.Count(look.Count))) yield break;

            var jaw = Utils.FindChild(Stage.Subject.transform, "Jaw");
            var flow = look.FirstOrDefault(c => c.name.StartsWith("fx_Puke", System.StringComparison.Ordinal));
            if (jaw == null || flow == null) p.Note("no jaw on the person, or no fx_Puke in the look: " + string.Join(", ", look.Select(c => c.name)));
            else
            {
                var size = flow.transform.lossyScale.x;
                var off = Vector3.Distance(flow.transform.position, jaw.position);
                p.Note($"fx_Puke: {Numbers.Fixed(off, 3)} m from the jaw, {Numbers.Fixed(size, 2)} times its size");
                p.Check(off < 0.05f && size > 0.8f && size < 1.25f, "its look stands on the jaw at its own size in the world, as the game puts it");
            }
            yield return PictureOf(p, "facts", "status Feeling sick on the person");

            // Played out, a click on the stage plays it again.
            yield return Until(() => Stage.LookPlayedOut, 15);
            p.Check(Stage.LookPlayedOut, "its look plays out, and the stage says a click plays it again");
            Previews.Replay();
            yield return Until(() => Stage.Subject != null && Looks.LookForTest.Any(c => c != null), 10);
            p.Check(!Stage.LookPlayedOut && Looks.LookForTest.Any(c => c != null), "played again, its look is back on the person");

            // Its text under the loose rules, the character knowing only it.
            var book = new CompendiumBook();
            book.Raise(sick.Key, KnownLevel.Known);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Loose);
            Session.ShowKnownInstead(view);
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            var text = Facts.For(sick).Description;
            var masked = ScryPanel.AsDrawn(X, text);
            p.Note("its text under the loose rules: " + masked);
            p.Check(text.Length > 0 && masked.IndexOf(CompendiumWords.Hidden, System.StringComparison.Ordinal) < 0, "its text keeps every plain word, no name of the game's in it", masked);
        }
    }
}
