using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The words the search's fight terms take, in small letters: the damage types a thing takes
    /// more of (weak:), less of (resists:) or none of (immune:), read off its resistance grid as
    /// the grid shows them; the types a hit deals (damage:); and the skill it trains (skill:).
    /// </summary>
    internal static class SearchFight
    {
        /// <summary>The damage types of a grid's cells in one tone, in the grid's order; a creature's quiet tool cells (<see cref="ResistWords.ForCreature"/>) are in none.</summary>
        public static string[] Taking(IEnumerable<ResistCell> cells, Tone tone) => cells.Where(c => c.Tone == tone).Select(c => c.Type.ToLowerInvariant()).ToArray();

        /// <summary>The damage types a hit deals, each once in the order given: those of more than none. "true" is the game's plain damage.</summary>
        public static string[] Dealt(IEnumerable<(string Type, float Amount)> figures) => figures.Where(f => f.Amount > 0f).Select(f => f.Type).Distinct().ToArray();

        /// <summary>The skill an item trains, by the name the details give it; none for none, or for a mod's skill Scry cannot name.</summary>
        public static string[] Skill(string name) => string.IsNullOrEmpty(name) || name == SkillWords.ModSkill ? Array.Empty<string>() : new[] { name.ToLowerInvariant() };
    }
}
