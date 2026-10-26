using System.Collections;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for the stars a creature can come with.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// A creature's stars follow what in the world can give it a level: a Lox, which the world
        /// spawns at level one alone, reads "never" with no stars table and the stage offers it none;
        /// a Boar keeps two; a summoned skeleton has none; and Hildir's cave cultists get two from the
        /// cave's own levels once the places are read. The counts of each are noted.
        /// </summary>
        private static IEnumerator StarsFollowSources(Probe p)
        {
            var lox = Pick(Kind.Creature, "Lox");
            var boar = Pick(Kind.Creature, "Boar");
            if (lox == null || boar == null)
            {
                p.Skip("there is no Lox or Boar in this world's catalog");
                yield break;
            }
            p.Check(CreatureStars.Of(lox.Name) == (0, false), "a Lox, spawned at level one alone, never comes with stars", $"{CreatureStars.Of(lox.Name)}");
            var loxFacts = Facts.For(lox);
            p.Check(loxFacts.Pairs.Any(pair => pair.Key == CombatWords.StarsTitle && pair.Value == CombatWords.NeverStars) && loxFacts.Rows.All(r => r.Title != CombatWords.StarsTitle),
                "its page says so, with no stars table");
            p.Check(CreatureStars.Of(boar.Name) == (2, false) && Facts.For(boar).Rows.Any(r => r.Title == CombatWords.StarsTitle), "a Boar comes with up to two stars, its table shown", $"{CreatureStars.Of(boar.Name)}");
            var summoned = Pick(Kind.Creature, "Skeleton_Friendly");
            if (summoned != null) p.Check(CreatureStars.Of(summoned.Name).Stars == 0, "a skeleton summoned by a staff comes with none, its level set to one", $"{CreatureStars.Of(summoned.Name)}");
            var cultist = Pick(Kind.Creature, "Fenring_Cultist_Hildir");
            if (cultist != null && Locations.Now == Locations.State.Read)
                p.Check(CreatureStars.Of(cultist.Name).Stars == 2, "Hildir's cave cultists come with two stars, by the levels the cave sets", $"{CreatureStars.Of(cultist.Name)}");
            else p.Note("the places are not read, so the levels they set wait");

            Select(lox);
            yield return Until(() => CopyOf(lox) != null, 15);
            p.Check(ScryPanel.StarsOfferedForTest.Count == 1 && ScryPanel.StarsOfferedForTest[0] == 1, "the stage offers a Lox no stars", string.Join(", ", ScryPanel.StarsOfferedForTest.Select(level => Numbers.Count(level))));

            var creatures = X.Catalog.Where(e => e.Kind == Kind.Creature).Select(e => (e.Name, Shown: CreatureStars.Of(e.Name))).ToList();
            p.Note($"{Numbers.Count(creatures.Count(c => c.Shown.Stars > 0 && !c.Shown.Unsure))} creatures can come with stars, {Numbers.Count(creatures.Count(c => c.Shown.Stars == 0))} never, "
                + $"{Numbers.Count(creatures.Count(c => c.Shown.Unsure))} of mods unsure: {string.Join(", ", creatures.Where(c => c.Shown.Unsure).Select(c => c.Name).Take(20))}");
        }
    }
}
