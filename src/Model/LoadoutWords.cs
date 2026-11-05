using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The words of a creature's gear rows in Adjust (<see cref="Loadout"/>): each row by the
    /// game's list it rolls from, unless the list holds none of its kind, as the game rolls a
    /// Dvergr's hair from its shield list; each choice by its game name, unless that name is set
    /// on the prefab untranslated and another choice shares it, as "Iron plate armor" names every
    /// Dvergr's and Jotun's hair and every goblin's armour, a name the game never shows.
    /// </summary>
    internal static class LoadoutWords
    {
        /// <summary>The words for no item chosen.</summary>
        public const string Nothing = "Nothing";

        /// <summary>A row named by none of its list: its choices' one item type, else this.</summary>
        public const string Rolled = "Rolled";

        private static readonly string[] Labels = { "Weapon", "Shield", "Armour", "Holding" };

        /// <summary>The game's item types each row's list is meant for, by <see cref="Loadout.Row"/>.</summary>
        private static readonly string[][] Kinds =
        {
            new[] { "OneHandedWeapon", "TwoHandedWeapon", "TwoHandedWeaponLeft", "Bow", "Torch", "Tool" },
            new[] { "Shield" },
            new[] { "Helmet", "Chest", "Legs", "Shoulder", "Hands", "Utility", "Trinket" },
            new[] { "OneHandedWeapon", "TwoHandedWeapon", "TwoHandedWeaponLeft", "Bow", "Torch", "Tool" },
        };

        /// <summary>A row's label: its list's, where any of its items is of the list's kind; else the item type all its items share, else <see cref="Rolled"/>.</summary>
        /// <param name="row">The row.</param>
        /// <param name="types">The game's item types of its items, nothing chosen left out.</param>
        public static string RowLabel(Loadout.Row row, IReadOnlyList<string> types)
        {
            var kind = Kinds[(int)row];
            if (types.Count == 0 || types.Any(t => Array.IndexOf(kind, t) >= 0)) return Labels[(int)row];
            return types.Distinct().Count() == 1 ? Naming.Capital(Naming.Readable(types[0])) : Rolled;
        }

        /// <summary>
        /// A row's choices' words, in order: nothing chosen as <see cref="Nothing"/>; one whose
        /// untranslated name another prefab of the row shares, or with no name, by its prefab's
        /// words; one whose translated name another shares, that name with its prefab; any other by its name.
        /// </summary>
        public static List<string> Choices(IReadOnlyList<(string Prefab, string Shown, bool Translated, string Type)> options)
        {
            var named = options.Select(o => string.IsNullOrEmpty(o.Prefab) ? (Nothing, "")
                : string.IsNullOrEmpty(o.Shown) || (!o.Translated && options.Any(other => other.Shown == o.Shown && other.Prefab != o.Prefab))
                    ? (Naming.Capital(Naming.Readable(o.Prefab)), o.Prefab)
                    : (o.Shown, o.Prefab)).ToList();
            return Naming.TellApart(named);
        }
    }
}
