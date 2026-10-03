using System;

namespace Scry
{
    /// <summary>
    /// How a skill is named. One the game has goes by its name in the game's list. One a mod adds
    /// is only a number there, and its mod names it under the game's own word for a skill
    /// ("$skill_" and that number), which is where the game's skill screen looks too.
    /// </summary>
    internal static class SkillWords
    {
        public const string ModSkill = "a skill a mod adds";

        /// <summary>A skill's name from the game's name for it (or its number), or null for none.</summary>
        public static string Name(string skill, Func<string, string> localize)
        {
            if (string.IsNullOrEmpty(skill)) return null;
            if (!char.IsDigit(skill[0]) && skill[0] != '-') return Naming.FieldLabel(skill);
            var named = localize?.Invoke("$skill_" + skill);
            return string.IsNullOrEmpty(named) ? ModSkill : named;
        }
    }
}
