using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What a skill's level changes in what the game works out, each rule as the game's code has
    /// it. The game counts a level whole, with what effects add to it (<c>Skills.GetSkillLevel</c>),
    /// and most of its rules take that as a share of 100 that stops at 100
    /// (<c>Skills.GetSkillFactor</c>); a summon's strength and a shield's absorbing take the level
    /// itself, so effects lift them past the top too.
    /// </summary>
    internal static class SkillMath
    {
        /// <summary>The highest level a skill rises to (<c>Skills.Skill.Raise</c>).</summary>
        public const float Top = 100f;

        /// <summary>The level the game works with: the one kept, with what effects add, counted whole.</summary>
        public static float Level(float kept, float fromEffects) => (float)Math.Floor(kept + fromEffects);

        /// <summary>
        /// Whether the character has learned a skill: kept at level 1 or more. What effects add
        /// teaches none, and a death can take a first level back below 1 (<c>Skills.OnDeath</c>).
        /// </summary>
        public static bool Learned(float kept) => kept >= 1f;

        /// <summary>
        /// The least and most share of its damage a hit of anything but a player deals, rolled
        /// each hit (<c>Character.GetRandomSkillFactor</c>: the square root of an even roll from
        /// 0.75 to 1, times the factor a summon is given, 1 for any other).
        /// </summary>
        public static (float Least, float Most) CreatureHitShare => ((float)Math.Sqrt(0.75), 1f);

        /// <summary>A level as the share of the top most rules take, kept within nothing and all.</summary>
        public static float Share(float level) => Clamp01(level / Top);

        /// <summary>
        /// The least and most share of its damage and knockback a hit deals, rolled evenly between
        /// (<c>Skills.GetRandomSkillRange</c>): from 0.4 at no skill to all of it at the top, 0.15
        /// either way, kept within nothing and all.
        /// </summary>
        public static (float Least, float Most) HitShare(float level)
        {
            var middle = Lerp(0.4f, 1f, Share(level));
            return (Clamp01(middle - 0.15f), Clamp01(middle + 0.15f));
        }

        /// <summary>
        /// The share of its stamina, eitr or health an attack costs, a bow's drain while drawn too
        /// (<c>Attack.GetAttackStamina</c>, <c>Attack.GetAttackEitr</c>, <c>Attack.GetAttackHealth</c>,
        /// <c>ItemDrop.ItemData.GetDrawStaminaDrain</c>): a third less at the top.
        /// </summary>
        public static float CostShare(float level) => 1f - 0.33f * Share(level);

        /// <summary>How long a full draw takes (<c>Humanoid.GetAttackDrawPercentage</c>): the bow's own time at no skill, a fifth of it at the top.</summary>
        public static float DrawTime(float listed, float level) => Lerp(listed, listed * 0.2f, Share(level));

        /// <summary>How long reloading takes (<c>ItemDrop.ItemData.GetWeaponLoadingTime</c>): its own time at no skill, half of it at the top.</summary>
        public static float ReloadTime(float listed, float level) => Lerp(listed, listed * 0.5f, Share(level));

        /// <summary>What a block holds back (<c>ItemDrop.ItemData.GetBlockPower</c>): its own and half of it again at the top.</summary>
        public static float Block(float listed, float level) => listed + listed * Share(level) * 0.5f;

        /// <summary>How much faster running is (<c>Player.GetRunSpeedFactor</c>): a quarter at the top.</summary>
        public static float RunSpeedShare(float level) => 1f + Share(level) * 0.25f;

        /// <summary>The share of its stamina running drains (<c>Player.CheckRun</c>): half at the top.</summary>
        public static float RunDrainShare(float level) => Lerp(1f, 0.5f, Share(level));

        /// <summary>A figure the game moves from one end at no skill to the other at the top: swimming's drain, fishing's, a scythe's reach.</summary>
        public static float Between(float atNone, float atTop, float level) => Lerp(atNone, atTop, Share(level));

        /// <summary>How much stronger a jump is, up and forward alike (<c>Character.Jump</c>): two fifths at the top.</summary>
        public static float JumpShare(float level) => 1f + Share(level) * 0.4f;

        /// <summary>The share of its stamina a dodge costs (<c>Player.GetDodgeStaminaUse</c>): half at the top.</summary>
        public static float DodgeShare(float level) => Lerp(1f, 0.5f, Share(level));

        /// <summary>The share of its stamina sneaking drains (<c>Player.OnSneaking</c>): a quarter at the top, by the square root of the share, so most comes early.</summary>
        public static float SneakDrainShare(float level) => Lerp(1f, 0.25f, (float)Math.Sqrt(Share(level)));

        /// <summary>
        /// The share of a creature's sight and alert range it sees you within while you sneak
        /// (<c>Player.UpdateStealth</c>, read by <c>BaseAI.CanSeeTarget</c> and <c>MonsterAI.UpdateAI</c>),
        /// by how lit you are, nothing in the dark to all in full light.
        /// </summary>
        public static float Stealth(float level, float light) =>
            Clamp01(Lerp(0.5f + light * 0.5f, 0.2f + light * 0.4f, Share(level)));

        /// <summary>How much faster a mount runs with you riding (<c>Character.GetRunSpeedFactor</c>): a quarter at the top.</summary>
        public static float MountSpeedShare(float level) => 1f + Share(level) * 0.25f;

        /// <summary>The share of its stamina a mount loses running or swimming with you riding (<c>Sadle.UpdateRiding</c>): half at the top.</summary>
        public static float MountDrainShare(float level) => Lerp(1f, 0.5f, Share(level));

        /// <summary>The share of its time making something takes at a station that trains the skill (<c>InventoryGui.UpdateRecipe</c>): less by the share of its most.</summary>
        public static float CraftTimeShare(float level, float mostSaved) => 1f - Share(level) * mostSaved;

        /// <summary>
        /// How likely a bonus is, made, cooked or picked (<c>InventoryGui.DoCrafting</c>,
        /// <c>CookingStation.OnInteract</c>, <c>Pickable.Interact</c>): the share of its most likely.
        /// </summary>
        public static float BonusChance(float level, float atTop) => Share(level) * atTop;

        /// <summary>The share of its stamina building with a tool costs (<c>Player.GetBuildStamina</c>): half at the top.</summary>
        public static float BuildStaminaShare(float level) => 1f - 0.5f * Share(level);

        /// <summary>The share of its wear a tool takes placing a piece (<c>Player.GetPlaceDurability</c>): less by the share of the tool's most saved.</summary>
        public static float WearShare(float level, float mostSaved) => 1f - mostSaved * Share(level);

        /// <summary>What a summon's hits are times (<c>SpawnAbility.Spawn</c>): one and the whole level times the staff's share.</summary>
        public static float SummonStrength(float level, float perLevel) => 1f + level * perLevel;

        /// <summary>What a shield absorbs (<c>SE_Shield.SetLevel</c>): its own and so much for each whole level.</summary>
        public static float ShieldAbsorb(float listed, float perLevel, float level) => listed + perLevel * level;

        /// <summary>
        /// Which of a staff's steps a summon takes (<c>SpawnAbility.Spawn</c>): read from the last
        /// back, the first whose level is reached; -1 for none.
        /// </summary>
        public static int SummonStep(IReadOnlyList<int> fromLevels, float level)
        {
            for (var i = fromLevels.Count - 1; i >= 0; i--)
            {
                if (level >= fromLevels[i]) return i;
            }
            return -1;
        }

        /// <summary>What the next level needs (<c>Skills.Skill.GetNextLevelRequirement</c>): the next whole level to the power 1.5, halved, and a half.</summary>
        public static float NextLevelNeeds(float level) => (float)Math.Pow(Math.Floor(level + 1f), 1.5) * 0.5f + 0.5f;

        /// <summary>
        /// How many rises of an amount take a skill to its next level (<c>Skills.Skill.Raise</c>):
        /// each adds to its progress, and the level comes once the progress holds what it needs;
        /// none at the top or for an amount of nothing.
        /// </summary>
        public static int RisesToNext(float level, float progress, float each)
        {
            if (level >= Top || each <= 0f) return 0;
            var left = NextLevelNeeds(level) - progress;
            return Math.Max(1, (int)Math.Ceiling(left / each - 0.0001f));
        }

        /// <summary>How much of a level a death takes (<c>Skills.LowerAllSkills</c>): its share of it.</summary>
        public static float DeathLoss(float level, float share) => level * share;

        private static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        private static float Lerp(float from, float to, float share) => from + (to - from) * Clamp01(share);
    }
}
