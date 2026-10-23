using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for what hinges on a thing not met.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// With the compendium on, what exists only through a thing the character has not met is
        /// left out whole: a made-up character who knows the Lox but never met its saddle sees no
        /// Riding topic on its page and no saddled look on the stage; once the saddle is seen,
        /// both are there. Pictured without. Knowing Eikthyr alone, its page tells nothing of what
        /// its fall opens; knowing a creature that rolls sets of gear alone, the stage offers no
        /// look of gear never met.
        /// </summary>
        private static IEnumerator HingedOnAThingNotMet(Probe p)
        {
            var lox = Pick(Kind.Creature, "Lox");
            var saddle = lox?.Source is GameObject prefab ? prefab.GetComponent<Tameable>().OrNull()?.m_saddleItem : null;
            if (lox == null || saddle == null)
            {
                p.Skip("there is no Lox with a saddle");
                yield break;
            }
            var saddleKey = saddle.gameObject.name;
            var book = new CompendiumBook();
            book.Raise(lox.Key, KnownLevel.Known);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Loose);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            X.Select(lox);
            yield return Until(() => ScryPanel.TopicHeadingsForTest.Count > 0, 5);
            yield return null;
            var headings = ScryPanel.TopicHeadingsForTest;
            p.Check(!headings.Contains("Riding"), "the Lox's page has no Riding topic while its saddle was never met", string.Join(", ", headings.Where(h => h != null)));
            p.Check(ScryPanel.LooksHiddenForTest, "and the stage offers no saddled look");
            yield return Picture(p, "spoilers Lox before its saddle");

            book.Raise(saddleKey, KnownLevel.Seen);
            view.Tell(book, CompendiumRule.Loose);
            X.Know(view);
            yield return Until(() => ScryPanel.TopicHeadingsForTest.Contains("Riding"), 5);
            p.Check(ScryPanel.TopicHeadingsForTest.Contains("Riding"), "once the saddle is seen, the Riding topic is there", string.Join(", ", ScryPanel.TopicHeadingsForTest.Where(h => h != null)));
            p.Check(!ScryPanel.LooksHiddenForTest, "and the stage offers the saddled look");

            // A boss's fall: its page tells what it opens only once something of that was met.
            var boss = Pick(Kind.Creature, "Eikthyr");
            if (boss != null)
            {
                yield return OnlyKnowing(view, boss);
                p.Check(!ScryPanel.TopicHeadingsForTest.Contains("After it falls"), "Eikthyr's page tells nothing of what its fall opens while none of it was met",
                    string.Join(", ", ScryPanel.TopicHeadingsForTest.Where(h => h != null)));
            }

            // A creature's gear sets: a look of items never met is not offered.
            var geared = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && e.Source is GameObject g && g.GetComponent<Humanoid>() is Humanoid h && PrefabGear.RolledSets(h).Count >= 2);
            if (geared == null) p.Note("no creature rolls two sets of gear");
            else
            {
                yield return OnlyKnowing(view, geared);
                var all = PrefabGear.RolledSets(((GameObject)geared.Source).GetComponent<Humanoid>()).Count + 1;
                p.Check(ScryPanel.LooksOfferedForTest < all, $"{geared.Name}'s looks of gear never met are not offered", $"{Numbers.Count(ScryPanel.LooksOfferedForTest)} of {Numbers.Count(all)} offered");
            }
            Session.ShowKnownInstead(null);
            yield return Until(() => X.Compendium != view, 3);
        }

        /// <summary>The made-up character knows one creature alone, selected and laid out.</summary>
        private static IEnumerator OnlyKnowing(CompendiumView view, Entry entry)
        {
            var book = new CompendiumBook();
            book.Raise(entry.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Loose);
            X.Know(view);
            X.Select(entry);
            yield return Until(() => ScryPanel.TopicHeadingsForTest.Count > 0, 5);
            yield return null;
            yield return null;
        }
    }
}
