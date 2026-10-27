using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>
    /// The clips a person or creature plays only with other gear in hand. The game sets its stance
    /// from the held item (<c>Humanoid.SetAnimationState</c> sets statef and statei), and its
    /// animator moves, crouches, blocks and jumps by it, each stance in clips of its own; drawing,
    /// reeling and reloading are switches the item names (<c>Attack.m_drawAnimationState</c>,
    /// <c>m_reloadAnimation</c>, <c>m_chargeAnimationBool</c>). A clip seen under no stance and no
    /// switch is no one gear's, and stays.
    /// </summary>
    internal static class HeldClips
    {
        /// <param name="stances">The clips seen under each stance, by its number; null while not seen yet.</param>
        /// <param name="holds">The clips seen with each switch an item names on.</param>
        /// <param name="stance">The stance it stands in now.</param>
        /// <param name="held">The switches what it holds names.</param>
        /// <param name="holdStances">The stance of the items that name each switch, to tell what a clip of it is held with.</param>
        /// <param name="allClips">While other stances are still being swept, every clip of the animator: one not known its own yet is held back as another gear's, of a stance not known (-1); none once all are swept.</param>
        /// <param name="known">Clips something else tells it plays (its attacks, an emote seen), never held back while the stances are swept.</param>
        /// <returns>Each clip only other gear plays, with the stance it is held in, -1 where that is not known.</returns>
        public static Dictionary<string, int> OtherGear(IReadOnlyDictionary<int, HashSet<string>> stances, IReadOnlyDictionary<string, HashSet<string>> holds,
            int stance, IEnumerable<string> held, IReadOnlyDictionary<string, int> holdStances = null, IEnumerable<string> allClips = null, IEnumerable<string> known = null)
        {
            var other = new Dictionary<string, int>();
            if (stances == null || (allClips != null && !stances.ContainsKey(stance))) return other;
            var holding = new HashSet<string>(held ?? Enumerable.Empty<string>());
            var shown = new HashSet<string>();
            if (stances.TryGetValue(stance, out var own)) shown.UnionWith(own);
            if (holds != null) foreach (var hold in holds) if (holding.Contains(hold.Key)) shown.UnionWith(hold.Value);

            foreach (var under in stances.OrderBy(s => s.Key))
            {
                if (under.Key == stance) continue;
                foreach (var clip in under.Value) if (!shown.Contains(clip) && !other.ContainsKey(clip)) other[clip] = under.Key;
            }
            if (holds != null)
            {
                foreach (var hold in holds.OrderBy(h => h.Key, System.StringComparer.Ordinal))
                {
                    var with = holdStances != null && holdStances.TryGetValue(hold.Key, out var s) ? s : -1;
                    foreach (var clip in hold.Value) if (!shown.Contains(clip) && !other.ContainsKey(clip)) other[clip] = with;
                }
            }
            // Its own stance swept, the others not yet: what is not known its own waits behind the filter.
            if (allClips != null)
            {
                var told = new HashSet<string>(known ?? Enumerable.Empty<string>());
                foreach (var clip in allClips) if (!shown.Contains(clip) && !told.Contains(clip) && !other.ContainsKey(clip)) other[clip] = -1;
            }
            return other;
        }
    }
}
