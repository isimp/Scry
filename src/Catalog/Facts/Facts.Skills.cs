using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>A skill's facts: what it changes at no skill, at the character's level and at the top, what raises it, what it works on, and what a death takes.</summary>
    internal sealed partial class Facts
    {
        // ----- Skills -----

        /// <summary>
        /// A skill's page: the game's description of it, a table of what it changes at no skill,
        /// at the character's level and at the top (<see cref="SkillPageWords.Changes"/>, from
        /// what the world's prefabs use it for, <see cref="SkillReading"/>), what raises it, the
        /// effects adding levels or making it rise faster, what it works on, and what a death
        /// takes of it (<c>Skills.OnDeath</c>, <c>Player.OnDeath</c>), a world changing that or how
        /// fast skills rise saying so.
        /// </summary>
        private void Skill(SkillSource skill)
        {
            Description = skill.Description;
            _skilled = true;
            Hooked(HookedRule.Skills);
            var type = (Skills.SkillType)skill.Type;
            var level = YourSkills.LevelOf(type);
            var inputs = SkillReading.Inputs(type);
            Table(SkillPageWords.ChangesTitle, SkillPageWords.ChangesColumns(level), SkillPageWords.Changes(inputs, level));
            if (!skill.OfTheGame && !Rows.Any(r => r.Title == SkillPageWords.ChangesTitle)) AddUnsure(SkillPageWords.ChangesTitle, SkillPageWords.OnlyItsMod, UnsureWords.ModSkill);
            var left = SkillPageWords.NotChanged(skill.Name);
            if (left != null) Add(left.Value.Label, left.Value.Text);
            Add(SkillPageWords.RisesLabel, SkillPageWords.RisesBy(skill.Name));

            var related = SkillReading.RelatedTo(type);
            Listed(SkillPageWords.TrainedWithTitle, related.Trains);
            Listed(SkillPageWords.ToolsTitle, related.Tools);
            Listed(SkillPageWords.StationsTitle, related.Stations);
            Listed(SkillPageWords.CookingTitle, related.Cooking);
            Listed(SkillPageWords.PickedTitle, related.Picked);
            EffectsOnSkill(type);

            var player = Player.m_localPlayer != null ? Player.m_localPlayer : GamePrefabs.Person.OrNull()?.GetComponent<Player>();
            var skills = player != null ? player.GetComponent<Skills>() : null;
            if (skills != null) Add(SkillPageWords.OnDeathLabel, SkillPageWords.OnDeath(skills.m_DeathLowerFactor, player.m_hardDeathCooldown));
            Part("world settings", () =>
            {
                var reset = ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.DeathSkillsReset);
                var note = WorldWords.SkillNote(Game.m_skillGainRate, Game.m_skillReductionRate, reset);
                Add("In this world", Naming.Capital(note));
            });
        }

        /// <summary>
        /// The character's level of a skill and the faint label of what is told at it, marking
        /// these facts as told at the character's levels; none without a character or a skill.
        /// </summary>
        private (string At, float Level, string Name)? YourAt(Skills.SkillType skill)
        {
            if (skill == Skills.SkillType.None || skill == Skills.SkillType.All) return null;
            _skilled = true;
            Hooked(HookedRule.Skills);
            if (!YourSkills.Known) return null;
            var level = YourSkills.LevelOf(skill);
            var name = SkillName(skill);
            return (SkillWords.At(name, level), level, name);
        }

        /// <summary>A faint line under a pair: what it comes to at the character's level of a skill; none without a character, a skill or a figure.</summary>
        private void YoursAt(string label, Skills.SkillType skill, System.Func<float, string> value)
        {
            var you = YourAt(skill);
            if (you == null) return;
            var text = SkillWords.Yours(you.Value.At, value(you.Value.Level));
            if (text == null) return;
            Yours[label] = text;
            YoursTips[label] = SkillWords.YoursTip(you.Value.Name);
        }

        /// <summary>The skill using it trains, going to the skill's page.</summary>
        private void TrainsSkill(Skills.SkillType skill)
        {
            var page = EntryKeys.For(Kind.Skill, skill.ToString());
            Add("Trains", SkillName(skill), EntryOf(page) != null ? page : null);
        }

        /// <summary>A skill's name as its page names it, else as Scry's words name it.</summary>
        internal static string SkillName(Skills.SkillType skill) =>
            EntryOf(EntryKeys.For(Kind.Skill, skill.ToString()))?.ShownName ?? SkillWords.Name(skill.ToString(), CatalogBuilder.Localize);

        /// <summary>A row listing prefabs of the catalog, each a chip going to it; with the compendium on, only those seen show.</summary>
        private void Listed(string title, IEnumerable<string> prefabs)
        {
            var row = new Row { Title = title, Lists = true };
            foreach (var name in prefabs.Distinct())
            {
                var entry = EntryOf(name);
                if (entry != null) row.Items.Add(EntryChip(entry));
            }
            if (row.Items.Count > 0) Rows.Add(row);
        }

        /// <summary>
        /// The effects adding levels to the skill (<c>SE_Stats.ModifySkillLevel</c>: its second
        /// skill only where it has a first) and those making it rise faster
        /// (<c>SE_Stats.ModifyRaiseSkill</c>), each a chip with how much.
        /// </summary>
        private void EffectsOnSkill(Skills.SkillType type)
        {
            var effects = ObjectDB.instance != null ? ObjectDB.instance.m_StatusEffects : null;
            if (effects == null) return;
            var levels = new Row { Title = SkillPageWords.AddsLevelsTitle };
            var faster = new Row { Title = SkillPageWords.FasterTitle };
            foreach (var effect in effects.OfType<SE_Stats>())
            {
                if (effect == null) continue;
                var page = EntryOf(EntryKeys.For(Kind.StatusEffect, effect.name));
                if (page == null) continue;
                var added = 0f;
                if (effect.m_skillLevel != Skills.SkillType.None)
                {
                    if (effect.m_skillLevel == type || effect.m_skillLevel == Skills.SkillType.All) added += effect.m_skillLevelModifier;
                    if (effect.m_skillLevel2 == type || effect.m_skillLevel2 == Skills.SkillType.All) added += effect.m_skillLevelModifier2;
                }
                if (added != 0f)
                {
                    var chip = EntryChip(page);
                    chip.Amount = SkillPageWords.AddsLevels(added);
                    levels.Items.Add(chip);
                }
                if (effect.m_raiseSkill != Skills.SkillType.None && (effect.m_raiseSkill == type || effect.m_raiseSkill == Skills.SkillType.All) && effect.m_raiseSkillModifier != 0f)
                {
                    var chip = EntryChip(page);
                    chip.Amount = SkillPageWords.RisesFaster(effect.m_raiseSkillModifier);
                    faster.Items.Add(chip);
                }
            }
            if (levels.Items.Count > 0) Rows.Add(levels);
            if (faster.Items.Count > 0) Rows.Add(faster);
        }
    }
}
