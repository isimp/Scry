using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    internal static partial class CatalogBuilder
    {
        /// <summary>What a skill's entry is made of, as its details list it.</summary>
        private static readonly string[] SkillComponents = { "Skills" };

        /// <summary>
        /// Every skill the character can have (<c>Skills.m_skills</c>, the character's own list,
        /// which holds any skill a mod adds, else the player prefab's), an entry of its own though
        /// it is no prefab: named and described as the game's skill screen names it
        /// (<c>"$skill_"</c> and its name in small letters), with its icon, listed by what it is for.
        /// </summary>
        private static void SkillEntries(List<Entry> entries)
        {
            var player = Player.m_localPlayer != null ? Player.m_localPlayer.gameObject : GamePrefabs.Person;
            var defs = player != null ? player.GetComponent<global::Skills>().OrNull()?.m_skills : null;
            if (defs == null) return;
            var seen = new HashSet<int>();
            foreach (var def in defs)
            {
                if (def == null || def.m_skill == global::Skills.SkillType.None || def.m_skill == global::Skills.SkillType.All || !seen.Add((int)def.m_skill)) continue;
                var name = def.m_skill.ToString();
                Guard.Each(Feature.SkillPages, "skill entries", name, () =>
                {
                    var ofTheGame = Enum.IsDefined(typeof(global::Skills.SkillType), def.m_skill);
                    var shown = Localize(SkillWords.Token(name));
                    var group = Groups.Skill(ofTheGame ? name : "");
                    entries.Add(new Entry
                    {
                        Name = name,
                        DisplayName = shown.Length > 0 ? shown : SkillWords.Name(name, Localize),
                        Kind = Kind.Skill,
                        Origin = ofTheGame ? Origin.Vanilla : Origin.Mod,
                        Source = new SkillSource { Type = (int)def.m_skill, Name = name, Description = Localize(def.m_description), Step = def.m_increseStep, OfTheGame = ofTheGame },
                        Icon = def.m_icon,
                        Group = group.Name,
                        GroupOrder = group.Order,
                        Components = SkillComponents,
                    });
                });
            }
        }
    }
}
