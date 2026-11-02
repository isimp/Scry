using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// How cloth worn on a body is set up as the game puts it on (<c>VisEquipment.SetupCloth</c>):
    /// it rests against the wearer's shapes before its own, and the bones it was made on are
    /// swapped for the wearer's of the same name.
    /// </summary>
    internal static class WornCloth
    {
        /// <summary>The shapes worn cloth rests against: the wearer's first, then the cloth's own; neither list is changed.</summary>
        public static List<T> Colliders<T>(IEnumerable<T> wearer, IEnumerable<T> own)
        {
            var all = new List<T>(wearer);
            all.AddRange(own);
            return all;
        }

        /// <summary>The wearer's bones by name, the first of a name kept; a missing or nameless bone is left out.</summary>
        public static Dictionary<string, T> BoneMap<T>(IEnumerable<T> bones, Func<T, string> nameOf) where T : class
        {
            var map = new Dictionary<string, T>();
            foreach (var bone in bones)
            {
                var name = bone != null ? nameOf(bone) : null;
                if (name != null && !map.ContainsKey(name)) map.Add(name, bone);
            }
            return map;
        }
    }
}
