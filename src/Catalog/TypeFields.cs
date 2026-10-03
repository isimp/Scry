using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The fields of a type that hold something Scry reads (a drop table, an effect list, a prefab,
    /// a status effect, fuel, a conversion), found once per type and kept. A type and its bases
    /// are looked through down to Unity's own, which declare none of the game's fields. A mod's
    /// type whose fields cannot be read has none, remembered as such, and is told once with the
    /// rest left out (<see cref="Faults.Skip"/>).
    /// </summary>
    internal static class TypeFields
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>Every instance field a type and its bases declare, down to Unity's own types.</summary>
        public static IEnumerable<FieldInfo> Of(Type type)
        {
            for (var t = type; t != null && t != typeof(object) && t != typeof(MonoBehaviour) && t != typeof(Component) && t != typeof(ScriptableObject); t = t.BaseType)
            {
                foreach (var field in t.GetFields(Declared)) yield return field;
            }
        }

        /// <summary>The fields of a type that match, found once and kept in the cache given.</summary>
        public static FieldInfo[] Matching(Dictionary<Type, FieldInfo[]> cache, Type type, Func<FieldInfo, bool> match) =>
            Picked(cache, type, field => match(field) ? new[] { field } : Array.Empty<FieldInfo>());

        /// <summary>
        /// A field's value on what holds it: true with the value when it could be read, false with
        /// none when it could not (a mod's field whose type will not load), the field left out as
        /// any of a type's fields that cannot be read. Nothing is made for the call, as every
        /// field of every prefab is read this way while the catalog is.
        /// </summary>
        public static bool Read(FieldInfo field, object owner, out object value)
        {
            Reading.Field = field;
            Reading.Owner = owner;
            Reading.Value = null;
            var failure = Steps.Run(ReadOne, Reading, null);
            value = Reading.Value;
            Reading.Field = null;
            Reading.Owner = null;
            Reading.Value = null;
            if (failure != null) Faults.Skip("reading of a type's fields", field.DeclaringType?.Name + "." + field.Name, failure);
            return failure == null;
        }

        private sealed class FieldRead
        {
            public FieldInfo Field;
            public object Owner;
            public object Value;
        }

        private static readonly FieldRead Reading = new FieldRead();
        private static readonly Action<FieldRead> ReadOne = read => read.Value = read.Field.GetValue(read.Owner);

        /// <summary>What each field of a type gives, if anything, found once and kept in the cache given.</summary>
        public static T[] Picked<T>(Dictionary<Type, T[]> cache, Type type, Func<FieldInfo, IEnumerable<T>> pick)
        {
            if (cache.TryGetValue(type, out var known)) return known;
            if (!Guard.Each("reading of a type's fields", type.Name, () => Of(type).SelectMany(pick).ToArray(), out known)) known = Array.Empty<T>();
            cache[type] = known;
            return known;
        }
    }
}
