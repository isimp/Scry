using System;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// Tags what only creatures have, their attacks and their gear, with the creatures carrying
    /// it. The game names an attack for its weapon alone ("Club") or in its own shorthand ("bjorn
    /// bite"), so a list holds eighteen clubs told apart only by number; tagged ("Club" beside
    /// "Troll"), each says whose it is. Carriers going by one name (a creature and its summoned
    /// copy) are one; two are named, more counted. A player's weapon a creature also carries, and
    /// an attack no creature carries, keep no tag.
    /// </summary>
    internal static class AttackTags
    {
        public static void Tell(IList<Entry> catalog)
        {
            var creatures = new Dictionary<string, Entry>(StringComparer.Ordinal);
            foreach (var entry in catalog) if (entry.Kind == Kind.Creature && !creatures.ContainsKey(entry.Key)) creatures[entry.Key] = entry;

            var attacks = Groups.CreatureAttacks.Name;
            var gear = Groups.CarriedByCreatures.Name;
            foreach (var entry in catalog)
            {
                if (entry.Kind != Kind.Item || (entry.Group != attacks && entry.Group != gear)) continue;
                var carriers = entry.Links.Where(l => l.Group == LinkWords.CarriedBy && creatures.ContainsKey(l.Target))
                    .Select(l => Naming.WithoutVariant(creatures[l.Target].ShownName))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
                if (carriers.Count == 0) continue;
                entry.Tag = carriers.Count <= 2 ? Naming.Joined(carriers) : Naming.Count(carriers.Count, "creature", "creatures");
                entry.TagNamesCarrier = true;
            }
        }
    }
}
