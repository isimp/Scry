using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// What of an item hangs on a character wearing it, and where, by the slot it is worn in and
    /// the names of its children, as <c>VisEquipment</c> hangs it. Armour and utility items
    /// (<c>AttachArmor</c>) hang every "attach_" part: "attach_skin" bound to the body, any other
    /// on the bone it names, as a Dvergr mage's staff, a utility item, hangs in its right hand by
    /// "attach_r.hand". Held and head items (<c>AttachItem</c>) hang their first "attach" part in
    /// the hand or on the head, or their "attach_skin" part on the body.
    /// </summary>
    internal static class WornParts
    {
        /// <summary>Where a part bound to the body's own bones goes, as "attach_skin" names it.</summary>
        public const string Body = "skin";

        private const string Attach = "attach";
        private const string AttachTo = "attach_";

        /// <summary>Whether the game hangs what is worn in a slot as armour (<c>VisEquipment.AttachArmor</c>): chest, legs, shoulders and utility.</summary>
        public static bool AsArmour(Slot slot) => slot == Slot.Chest || slot == Slot.Legs || slot == Slot.Shoulders || slot == Slot.Utility;

        /// <summary>
        /// The parts an item worn in a slot hangs, by their places among its children, each with
        /// where it goes: <see cref="Body"/>, a bone by its name, or none for the slot's own
        /// place (the hand or the head, by the item's type). None for an item with nothing to hang.
        /// </summary>
        public static List<(int Child, string On)> Of(Slot slot, IReadOnlyList<string> children)
        {
            var parts = new List<(int, string)>();
            if (children == null) return parts;
            for (var i = 0; i < children.Count; i++)
            {
                var name = children[i] ?? "";
                if (AsArmour(slot))
                {
                    if (name.StartsWith(AttachTo, System.StringComparison.Ordinal) && name.Length > AttachTo.Length) parts.Add((i, name.Substring(AttachTo.Length)));
                    continue;
                }
                if (name == Attach) parts.Add((i, null));
                else if (name == AttachTo + Body) parts.Add((i, Body));
                else continue;
                break;
            }
            return parts;
        }
    }
}
