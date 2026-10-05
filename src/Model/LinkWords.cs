using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>What a link's chip and tip say: what it goes to, with its note where short, or the animation it plays there.</summary>
    internal static class LinkWords
    {
        // The groups of links between prefabs (Relations), each a heading under Linked or in a topic of In the game.
        public const string AnimationSounds = "Animation sounds and effects";
        public const string PlayedByAnimation = "Played by an animation of";
        public const string Footsteps = "Footsteps";
        public const string FootstepOf = "Footstep of";
        public const string Carries = "Carries";
        public const string CarriedBy = "Carried by";
        public const string Spawns = "Spawns";
        public const string SpawnedBy = "Spawned by";
        public const string StatusEffects = "Status effects";
        public const string GivenBy = "Given by";
        public const string Upgrades = "Upgrades";
        public const string UpgradeOf = "Upgrade of";
        public const string Items = "Items";
        public const string ItemOf = "Item of";

        /// <summary>A link that goes to a creature and plays one of its animations, or goes to it alone.</summary>
        public static string Animation(string shown, string animation) => animation.Length > 0 ? shown + " \u00B7 " + animation : shown;

        /// <summary>The tip of a link to a creature's animation.</summary>
        public static string AnimationTip(string shown, string animation) =>
            animation.Length > 0 ? $"Go to {shown} and play its {animation} animation" : PanelWords.GoTo(shown);

        /// <summary>A link's chip, with its note where it has one short enough.</summary>
        public static string Chip(string shown, IReadOnlyList<string> notes) =>
            notes.Count == 1 && notes[0].Length > 0 && notes[0].Length <= 28 ? shown + " \u00B7 " + notes[0] : shown;

        /// <summary>A link's tip: where it goes, and its notes, the first twelve.</summary>
        public static string Tip(string shown, IReadOnlyList<string> notes) =>
            PanelWords.GoTo(shown) + (notes.Count > 0 ? "\n" + string.Join("\n", notes.Take(12)) : "");
    }
}
