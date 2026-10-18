using System.Collections.Generic;
using Xunit;

namespace Scry.Tests
{
    public class SkillNotesTests
    {
        // What the pages say around the figures at the character's skill: the words of a weapon's
        // draw and reload, costs to a tenth where the skill leaves fractions, a world that changes
        // how skills rise or fall, and mods hooking into the game's skill rules.

        [Fact]
        public void CostsAtYourSkillKeepATenth()
        {
            Assert.Equal("8.9 stamina, 17.8 eitr", CombatWords.Costs(8.878f, 17.756f, 0f, 0f, 1));
            Assert.Equal("8.88 stamina", CombatWords.Costs(8.878f, 0f, 0f, 0f));
            Assert.Equal("12.4 stamina a second", CombatWords.DrawCost(12.43f, 1));
            Assert.Equal("12.43 stamina a second", CombatWords.DrawCost(12.43f));
        }

        [Fact]
        public void ABowsDrawAndACrossbowsReloadHaveLinesOfTheirOwn()
        {
            Assert.Equal("Full draw", CombatWords.FullDraw);
            Assert.Equal("Reloads in", CombatWords.Reloads);
        }

        [Fact]
        public void AWorldThatChangesHowSkillsRiseOrFallSaysSo()
        {
            // Game.m_skillGainRate and m_skillReductionRate, and the key resetting every skill on death; never counted in the figures.
            Assert.Null(WorldWords.SkillNote(1f, 1f, false));
            Assert.Equal("skills rise ×1.5 as fast (skill gain), not counted in these figures", WorldWords.SkillNote(1.5f, 1f, false));
            Assert.Equal("a death takes ×2 as much (skill loss), not counted in these figures", WorldWords.SkillNote(1f, 2f, false));
            Assert.Equal("skills rise ×3 as fast (skill gain) and a death resets every skill, not counted in these figures", WorldWords.SkillNote(3f, 2f, true));
        }

        [Fact]
        public void ModsHookingIntoSkillsAreSaid()
        {
            Assert.Equal("Mods and what your skills change", ModHookWords.Label(HookedRule.Skills));
            Assert.Equal("Skill Tweaks hooks into skills: what a level changes may differ from what is told",
                ModHookWords.Note(HookedRule.Skills, new List<string> { "Skill Tweaks" }));
        }
    }
}
