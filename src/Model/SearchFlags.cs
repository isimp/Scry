using System.Collections.Generic;

namespace Scry
{
    /// <summary>What the catalog read of a thing for the search's is: term, as its details tell the same.</summary>
    internal sealed class FlagFacts
    {
        /// <summary>A creature that is a boss (<c>Character.m_boss</c>), can be tamed (it has a <c>Tameable</c>) or flies (<c>Character.m_flying</c>).</summary>
        public bool Boss, Tameable, Flying;

        /// <summary>An item's type by the game's name (<c>ItemDrop.ItemData.ItemType</c>); null for anything else.</summary>
        public string ItemType;

        /// <summary>An item eaten for health, stamina or eitr.</summary>
        public bool Food;

        /// <summary>A piece some tool builds; an item an enabled recipe makes.</summary>
        public bool Buildable, Craftable;

        /// <summary>Nothing to see or hear (<see cref="Entry.Empty"/>); put down to a mod by clues rather than its registry.</summary>
        public bool Silent, Unsure;
    }

    /// <summary>
    /// What a thing is, in the one word the search's is: term takes for it: a boss, something
    /// to tame, to wear, to eat, to fight or shoot with, to build or craft, or what Scry has
    /// nothing to show of or is not sure of.
    /// </summary>
    internal static class SearchFlags
    {
        /// <summary>Every flag, in the order they are told.</summary>
        public static readonly string[] All = { "boss", "tameable", "flying", "wearable", "food", "weapon", "ammo", "buildable", "craftable", "silent", "unsure" };

        /// <summary>The item types worn as armour, a cape, gloves, a belt or a trinket.</summary>
        private static readonly HashSet<string> Worn = new HashSet<string> { "Helmet", "Chest", "Legs", "Hands", "Shoulder", "Utility", "Trinket" };

        /// <summary>The item types fought with, as the list groups weapons.</summary>
        private static readonly HashSet<string> Weapons = new HashSet<string> { "OneHandedWeapon", "TwoHandedWeapon", "TwoHandedWeaponLeft", "Bow", "Attach_Atgeir" };

        private static readonly HashSet<string> Shot = new HashSet<string> { "Ammo", "AmmoNonEquipable" };

        /// <summary>The flags a thing has, in the order of <see cref="All"/>.</summary>
        public static string[] Of(FlagFacts facts)
        {
            var type = facts.ItemType ?? "";
            var flags = new List<string>();
            void Flag(bool on, string flag)
            {
                if (on) flags.Add(flag);
            }
            Flag(facts.Boss, "boss");
            Flag(facts.Tameable, "tameable");
            Flag(facts.Flying, "flying");
            Flag(Worn.Contains(type), "wearable");
            Flag(facts.Food, "food");
            Flag(Weapons.Contains(type), "weapon");
            Flag(Shot.Contains(type), "ammo");
            Flag(facts.Buildable, "buildable");
            Flag(facts.Craftable, "craftable");
            Flag(facts.Silent, "silent");
            Flag(facts.Unsure, "unsure");
            return flags.ToArray();
        }

        /// <summary>The search help's line for is:, naming every flag.</summary>
        public static string HelpLine() =>
            "What it is: boss, tameable, flying, wearable, food, weapon, ammo, buildable (a piece a tool builds), craftable (an item a recipe makes), silent (nothing to see or hear) or unsure (put down to a mod by clues).";
    }
}
