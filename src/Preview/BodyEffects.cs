using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A status effect's effects put on a body where the game puts them on a character (its start
    /// effects through <c>EffectList.Create</c>, with the character as base): each at its named
    /// part, else at the body's centre; turned as the body, as the part where the slot inherits its
    /// rotation, or at random where it says so; twice the body's radius in size where the slot
    /// scales; hung on the part, keeping its size in the world, where it attaches or follows; and
    /// only the slots for the body's model (male, female) and those for any.
    /// </summary>
    internal static class BodyEffects
    {
        /// <summary>Puts each effect of a list on a body; the copies made.</summary>
        /// <param name="list">The effects to put on.</param>
        /// <param name="body">The character's root, whose parts are looked up by name.</param>
        /// <param name="centre">The body's centre in the world, where an effect naming no part stands.</param>
        /// <param name="radius">The body's radius, which an effect that scales takes twice.</param>
        /// <param name="model">The body's model, which picks among slots for one; -1 for every slot.</param>
        /// <param name="loose">Where an effect that neither attaches nor follows is hung to be cleared with the rest; none to leave it standing in the world.</param>
        /// <param name="layer">The layer the copies are drawn on; -1 for their own.</param>
        public static List<GameObject> Put(EffectList list, Transform body, Vector3 centre, float radius, int model, Transform loose = null, int layer = -1)
        {
            var made = new List<GameObject>();
            if (body == null) return made;
            foreach (var data in EffectSlots.Of(list))
            {
                if (!EffectSlots.Shows(data)) continue;
                if (model >= 0 && data.m_variant >= 0 && data.m_variant != model) continue;

                var part = body;
                var position = centre;
                if (!string.IsNullOrEmpty(data.m_childTransform))
                {
                    var child = Utils.FindChild(body, data.m_childTransform);
                    if (child != null)
                    {
                        part = child;
                        position = child.position;
                    }
                }
                var rotation = data.m_inheritParentRotation ? part.rotation : body.rotation;
                if (data.m_randomRotation) rotation = Random.rotation;

                var copy = Ghost.Make(data.m_prefab, null, position, rotation, layer);
                if (copy == null) continue;
                if (data.m_scale) copy.transform.localScale = Vector3.one * (radius * 2f);
                // Hung keeping its place, turn and size in the world, as the game hangs it.
                var on = data.m_attach || data.m_follow ? part : loose;
                if (on != null) copy.transform.SetParent(on, true);
                made.Add(copy);
            }
            return made;
        }
    }
}
