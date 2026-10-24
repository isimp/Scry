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
        /// <summary>The title of a creature's row of searches for what to bring against it.</summary>
        public const string BringTitle = "What to bring";

        /// <summary>The elements gear resists: what an attack deals that some gear turns aside.</summary>
        private static readonly string[] Elements = { "fire", "frost", "lightning", "poison", "spirit" };

        /// <summary>
        /// What to bring against a creature, each a search with its words and what shows it under
        /// the strict rules: the weapons dealing a type it takes more of, once a hit told how that
        /// type lands (<see cref="EntryKeys.Damage"/>), and the gear resisting an element its
        /// attacks deal, once one of the attacks dealing it hit the character (their attacks' keys, <see cref="EntryKeys.Attack"/>).
        /// </summary>
        /// <param name="weakTo">The types it takes more of (<see cref="Taking"/>).</param>
        /// <param name="attacks">Its attacks, each by its item and the types it deals (<see cref="Dealt"/>).</param>
        public static List<(string Text, string Query, string[] ShownBy)> Bring(IEnumerable<string> weakTo, IEnumerable<(string Item, string[] Types)> attacks)
        {
            var bring = new List<(string, string, string[])>();
            foreach (var type in weakTo.Distinct())
            {
                bring.Add(("Weapons dealing " + type, SearchHelp.Term("damage", type) + " " + SearchHelp.Term("is", "weapon"), new[] { EntryKeys.Damage(ResistWords.TypeNamed(type)) }));
            }
            var all = attacks.ToList();
            foreach (var type in all.SelectMany(a => a.Types).Distinct().Where(t => Elements.Contains(t)))
            {
                var dealing = all.Where(a => a.Types.Contains(type)).Select(a => EntryKeys.Attack(a.Item)).Distinct().ToArray();
                bring.Add(("Gear resisting " + type, SearchHelp.Term("resists", type) + " " + SearchHelp.Term("is", "wearable"), dealing));
            }
            return bring;
        }

        /// <summary>The damage types a creature's attacks deal, each once in the order given, of those attacks <paramref name="taken"/> says hit the character.</summary>
        public static string[] DealtBy(IEnumerable<(string Item, string[] Types)> attacks, Func<string, bool> taken) =>
            attacks.Where(a => taken(a.Item)).SelectMany(a => a.Types).Distinct().ToArray();

        /// <summary>The damage types of a grid's cells in one tone, in the grid's order; a creature's quiet tool cells (<see cref="ResistWords.ForCreature"/>) are in none.</summary>
        public static string[] Taking(IEnumerable<ResistCell> cells, Tone tone) => cells.Where(c => c.Tone == tone).Select(c => c.Type.ToLowerInvariant()).ToArray();

        /// <summary>The damage types a hit deals, each once in the order given: those of more than none. "true" is the game's plain damage.</summary>
        public static string[] Dealt(IEnumerable<(string Type, float Amount)> figures) => figures.Where(f => f.Amount > 0f).Select(f => f.Type).Distinct().ToArray();

        /// <summary>The skill an item trains, by the name the details give it; none for none, or for a mod's skill Scry cannot name.</summary>
        public static string[] Skill(string name) => string.IsNullOrEmpty(name) || name == SkillWords.ModSkill ? Array.Empty<string>() : new[] { name.ToLowerInvariant() };
    }
}
