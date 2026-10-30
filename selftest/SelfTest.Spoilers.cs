using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's cases for what hinges on a thing not met, and for Scry's own words naming none.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Scry's own fixed words (its word classes' constants) go through the name mask as the
        /// game's texts do, so a word of them that is also a thing's whole name reads a dash while
        /// the thing is not met: the game's Hook would blank "the hook" in a heading. A clash
        /// with a name of the game fails; one with a mod's name is noted, a mod's names being its
        /// own. Text a page writes outside the word classes is not checked.
        /// </summary>
        private static IEnumerator FixedWordsNameNoThing(Probe p)
        {
            var names = X.Catalog.Where(e => ScryPanel.Maskable(e) && e.Kind != Kind.Skill).ToList();
            var words = typeof(ScryPanel).Assembly.GetTypes().Where(t => t.Name.EndsWith("Words", StringComparison.Ordinal)).SelectMany(TypeFields.Constants).Distinct().ToList();
            p.Check(words.Contains(FishWords.BitesOn) && words.Contains(ChainWords.Summoning), $"the word classes' {Numbers.Count(words.Count)} fixed words are read, a fish's biting heading among them");
            var game = HeadingClashes.Of(words, names.Where(e => e.Origin == Origin.Vanilla).Select(e => e.ShownName).Distinct());
            var mods = HeadingClashes.Of(words, names.Where(e => e.Origin != Origin.Vanilla).Select(e => e.ShownName).Distinct());
            p.Check(game.Count == 0, $"none of {Numbers.Count(words.Count)} fixed words holds a name of the game's {Numbers.Count(names.Count(e => e.Origin == Origin.Vanilla))} things",
                string.Join("; ", game.Take(20).Select(c => $"\"{c.Heading}\" holds {c.Name}")));
            p.Note(mods.Count == 0 ? "and none a mod's thing's name" : $"{Numbers.Count(mods.Count)} hold a mod's thing's name: " + string.Join("; ", mods.Take(20).Select(c => $"\"{c.Heading}\" holds {c.Name}")));
            yield break;
        }

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

            // A creature's weapons are named as its attack table names its attacks, never ???.
            var armed = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && e.Source is GameObject a && a.GetComponent<Humanoid>() is Humanoid ah && ah.m_randomWeapon != null && ah.m_randomWeapon.Length >= 2);
            if (armed != null)
            {
                yield return OnlyKnowing(view, armed);
                yield return null;
                var weapons = ScryPanel.LoadoutNamesForTest.FirstOrDefault(r => r.Row == "Weapon").Names;
                p.Check(weapons != null && !weapons.Contains(CompendiumWords.Hidden), $"{armed.Name}'s weapons are named, as its attack table names them", weapons == null ? "no weapon row" : string.Join(", ", weapons));
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
