using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class ClipLeftoverTests
    {
        // Kin share one animator: every Charred kind's holds the greatsword swings, the bow, the
        // beam and the bolt of all of them, and each kind carries the items of only some. The game
        // starts an attack only by the trigger of an item the creature carries (Attack.Start), so
        // the clips the other triggers lead to are never played by this one.

        private static Dictionary<string, IReadOnlyList<string>> LeadsTo(params (string Trigger, string[] Clips)[] runs)
        {
            var map = new Dictionary<string, IReadOnlyList<string>>();
            foreach (var (trigger, clips) in runs) map[trigger] = clips;
            return map;
        }

        [Fact]
        public void TheClipsOfAnAttackNothingItCarriesStartsAreNotPlayedByIt()
        {
            var leads = LeadsTo(("attack_bow", new[] { "Draw Bow" }), ("attack_swing", new[] { "Attack Swing Greatsword" }), ("attack_bolt", new[] { "Attack Magic Bolt" }));
            var unplayed = ClipLeftovers.Unplayed(leads, pulled: new[] { "attack_bow" }, playedOtherwise: new string[0]);
            Assert.Equal(new[] { "Attack Magic Bolt", "Attack Swing Greatsword" }, Sorted(unplayed));
        }

        [Fact]
        public void AClipAnAttackItCarriesAlsoLeadsToIsPlayed()
        {
            var leads = LeadsTo(("attack_slash", new[] { "Attack Slash", "Recover" }), ("attack_slash_old", new[] { "Attack Slash Old", "Recover" }));
            Assert.Equal(new[] { "Attack Slash Old" }, Sorted(ClipLeftovers.Unplayed(leads, new[] { "attack_slash" }, new string[0])));
        }

        [Fact]
        public void AClipItPlaysAnotherWayIsPlayed()
        {
            // Its death, a jump, an idle: the game plays those by its own actions.
            var leads = LeadsTo(("attack_explode", new[] { "Attack Explosion", "Death Explosion" }));
            Assert.Equal(new[] { "Attack Explosion" }, Sorted(ClipLeftovers.Unplayed(leads, new string[0], new[] { "Death Explosion" })));
        }

        [Fact]
        public void OnlyAttacksAreAskedAbout()
        {
            // An emote, a jump, sitting down: the game's code or the player starts those, never an item.
            var asked = ClipLeftovers.Candidates(new[] { "attack_bow", "taunt", "attack_taunt", "swing_sledge", "unarmed_attack0", "jump", "emote_wave", "stagger", "attach_chair", "dodge" });
            Assert.Equal(new[] { "attack_bow", "taunt", "attack_taunt", "swing_sledge", "unarmed_attack0" }, asked);
        }

        [Fact]
        public void TriggersTheGamesOwnCodePullsAreNeverLeftOver()
        {
            // Humanoid.StartAttack pulls "attack_abort" to stop any attack (Attack.Stop): not an attack of its own.
            Assert.Empty(ClipLeftovers.Candidates(new[] { "attack_abort" }));
        }

        [Fact]
        public void EachClipLeftOverIsToldByTheAttackThatLeadsThere()
        {
            // A person plays a greatsword's swing once it holds one: its clips are told by the trigger, for the stance of what pulls it.
            var leads = LeadsTo(("greatsword0", new[] { "Greatsword Attack 1" }), ("swing_axe0", new[] { "axe_swing" }));
            var by = ClipLeftovers.UnplayedBy(leads, new[] { "swing_axe0" }, new string[0]);
            Assert.Equal("greatsword0", by["Greatsword Attack 1"]);
            Assert.False(by.ContainsKey("axe_swing"));
        }

        [Fact]
        public void ACreatureWithAnimatorUnseenHasNothingLeftOver()
        {
            Assert.Empty(ClipLeftovers.Unplayed(null, new[] { "attack_bow" }, new string[0]));
        }

        [Fact]
        public void AClipNotPlayedByThisCreatureIsListedOnlyWhileFiltering()
        {
            Assert.Equal("Not played by this creature", ClipWords.UnplayedHeading);
            Assert.Equal("Draw Bow  ·  not played by this creature", ClipWords.Unplayed("Draw Bow"));
        }

        private static string[] Sorted(IEnumerable<string> names)
        {
            var list = new List<string>(names);
            list.Sort(System.StringComparer.Ordinal);
            return list.ToArray();
        }
    }
}
