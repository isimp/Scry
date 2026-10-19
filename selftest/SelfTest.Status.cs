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
        /// the world, as the game puts it on a character, pictured once its stream flows (it starts
        /// a moment late); it plays out as long as its parts last, and plays again on a click; under
        /// the loose rules its text keeps "well", which only an unseen place Scry names by its
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
            // Its stream starts a moment after the look is put on (its parts' delay; the splat bursts at once): pictured once it flows.
            var delay = Delay(look);
            yield return Until(() => false, delay + 0.5f);
            p.Check(Flowing(Looks.LookForTest), "its stream flows from the person", $"after {Numbers.Fixed(delay, 1)} s");
            yield return PictureOf(p, "facts", "status Feeling sick on the person");
            // Beside its button, faint: only the look. Nothing of it under the tabs.
            var parts = ScryPanel.ToolbarPartsForTest.ToList();
            var caption = parts.FirstOrDefault(t => t.Part == "caption");
            var button = parts.Where(t => t.Part == "action").Select(t => t.At).DefaultIfEmpty(Rect.zero).Max(r => r.xMax);
            p.Check(caption.Part != null && caption.At.width > 0f && caption.At.x >= button, "a faint caption beside its button says it shows only the look",
                caption.Part == null ? "none drawn" : $"at {Numbers.Fixed(caption.At.x, 0)}, the buttons ending at {Numbers.Fixed(button, 0)}");

            // Played out once its parts have run their course, a click on the stage plays it again.
            var lasts = Lasts(look);
            p.Note($"its look lasts {Numbers.Fixed(lasts, 1)} s by its parts");
            yield return Until(() => Stage.LookPlayedOut, lasts + 3f);
            p.Check(Stage.LookPlayedOut, "its look plays out, and the stage says a click plays it again");
            Previews.Replay();
            yield return Until(() => Stage.Subject != null && Looks.LookForTest.Any(c => c != null) && !Stage.LookPlayedOut, 10);
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

        /// <summary>How long the latest of a look's parts switched on waits before it starts.</summary>
        private static float Delay(System.Collections.Generic.IEnumerable<GameObject> look) =>
            look.Where(c => c != null).SelectMany(c => c.GetComponentsInChildren<ParticleSystem>(false)).Select(s => s.main.startDelay.constantMax).DefaultIfEmpty(0f).Max();

        /// <summary>Whether a look's particles show anything now.</summary>
        private static bool Flowing(System.Collections.Generic.IEnumerable<GameObject> look) =>
            look.Where(c => c != null).SelectMany(c => c.GetComponentsInChildren<ParticleSystem>(false)).Any(s => s.particleCount > 0);

        /// <summary>
        /// How long a look's parts run, by the longest of them switched on: its delay, how long it
        /// emits and how long the last of what it emits lives; a looping one counts once round.
        /// </summary>
        private static float Lasts(System.Collections.Generic.IEnumerable<GameObject> look) =>
            look.Where(c => c != null).SelectMany(c => c.GetComponentsInChildren<ParticleSystem>(false))
                .Select(s => s.main.startDelay.constantMax + s.main.duration + s.main.startLifetime.constantMax).DefaultIfEmpty(0f).Max();
    }
}
