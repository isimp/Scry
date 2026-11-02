using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What of the cloth physics the game ships (MagicaCloth 2) worn cloth is set up with, as
    /// <c>VisEquipment.SetupCloth</c> sets it up, found by name once for each cloth type, so Scry
    /// needs no reference to its library.
    /// </summary>
    internal sealed class ClothLibrary
    {
        public readonly MethodInfo Initialize;
        public readonly MethodInfo DisableAutoBuild;
        public readonly MethodInfo ReplaceTransform;
        public readonly MethodInfo BuildAndRun;
        private readonly FieldInfo _data;
        private readonly FieldInfo _constraint;
        private readonly FieldInfo _colliderList;

        private ClothLibrary(Type cloth)
        {
            Initialize = cloth.GetMethod("Initialize", Type.EmptyTypes);
            DisableAutoBuild = cloth.GetMethod("DisableAutoBuild", Type.EmptyTypes);
            ReplaceTransform = cloth.GetMethod("ReplaceTransform", new[] { typeof(Dictionary<string, Transform>) });
            BuildAndRun = cloth.GetMethod("BuildAndRun", Type.EmptyTypes);
            _data = TypeFields.Named(cloth, "serializeData");
            _constraint = _data != null ? TypeFields.Named(_data.FieldType, "colliderCollisionConstraint") : null;
            _colliderList = _constraint != null ? TypeFields.Named(_constraint.FieldType, "colliderList") : null;
        }

        private static readonly Dictionary<Type, ClothLibrary> ByType = new Dictionary<Type, ClothLibrary>();

        /// <summary>What a cloth type has of it, found once.</summary>
        public static ClothLibrary Of(Type cloth)
        {
            if (!ByType.TryGetValue(cloth, out var found)) ByType[cloth] = found = new ClothLibrary(cloth);
            return found;
        }

        /// <summary>The shapes a cloth rests against, its own list, which the cloth reads as it builds; null where it cannot be read.</summary>
        public IList CollidersOf(object cloth)
        {
            if (_data == null || _constraint == null || _colliderList == null) return null;
            var data = TypeFields.Value(_data, cloth);
            var constraint = data != null ? TypeFields.Value(_constraint, data) : null;
            return constraint != null ? TypeFields.Value(_colliderList, constraint) as IList : null;
        }

        /// <summary>Each of it by name, and whether the cloth type has it.</summary>
        public IEnumerable<(string Name, bool Found)> Members()
        {
            yield return ("MagicaCloth.Initialize", Initialize != null);
            yield return ("MagicaCloth.DisableAutoBuild", DisableAutoBuild != null);
            yield return ("MagicaCloth.ReplaceTransform", ReplaceTransform != null);
            yield return ("MagicaCloth.BuildAndRun", BuildAndRun != null);
            yield return ("MagicaCloth.serializeData", _data != null);
            yield return ("ClothSerializeData.colliderCollisionConstraint", _constraint != null);
            yield return ("ColliderCollisionConstraint.SerializeData.colliderList", _colliderList != null && typeof(IList).IsAssignableFrom(_colliderList.FieldType));
        }
    }
}
