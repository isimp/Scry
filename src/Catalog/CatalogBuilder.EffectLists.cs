using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Scry
{
    /// <summary>The effect lists on every prefab and part, and what each plays.</summary>
    internal static partial class CatalogBuilder
    {
        /// <summary>
        /// The interface is part of the scene rather than a prefab, so its sounds are only
        /// reachable through the live objects. Walking from each one's root covers the whole of it.
        /// </summary>
        private static void GatherInterface(Dictionary<string, Found> effects)
        {
            var roots = new HashSet<Transform>();
            AddRoot(InventoryGui.instance, roots);
            AddRoot(Hud.instance, roots);
            AddRoot(StoreGui.instance, roots);
            AddRoot(Minimap.instance, roots);
            AddRoot(MessageHud.instance, roots);
            AddRoot(Menu.instance, roots);
            AddRoot(Chat.instance, roots);

            foreach (var root in roots)
            {
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    try
                    {
                        Gather(component, Provenance.Interface, Origin.Vanilla, effects, null, Provenance.Interface);
                    }
                    catch (Exception ex)
                    {
                        Faults.Skip("interface sounds", Provenance.Interface, ex);
                    }
                }
            }
        }

        private static void AddRoot(Component part, HashSet<Transform> roots)
        {
            if (part != null) roots.Add(part.transform.root);
        }

        /// <summary>
        /// Records whatever the effect lists on one object point at, and who uses it: by name for
        /// the details, and as a list with its purpose for playing it whole. An attack's lists say
        /// which attack they are for.
        /// </summary>
        private static void Gather(object owner, string ownerName, Origin ownerOrigin, Dictionary<string, Found> effects,
            string ownerKey, string shown, string part = null)
        {
            foreach (var on in ListsOn(owner))
            {
                var list = on.List;
                if (list.m_effectPrefabs == null || list.m_effectPrefabs.Length == 0) continue;

                var label = on.Label;
                if (part != null) label = part + ": " + label.ToLowerInvariant();
                EffectLinks.Note(list, shown, ownerKey, label);

                foreach (var data in list.m_effectPrefabs)
                {
                    var prefab = data?.m_prefab;
                    if (prefab == null) continue;

                    if (!effects.TryGetValue(prefab.name, out var found))
                    {
                        found = new Found { Prefab = prefab };
                        effects[prefab.name] = found;
                    }

                    if (found.Users.Add(ownerName)) found.UserOrigins.Add(ownerOrigin);
                    found.Fields.Add((ownerName == Provenance.Interface ? "ui:" : ownerName.StartsWith("status effect ", StringComparison.Ordinal) ? "se:" : "") + on.Field);
                }
            }
        }

        /// <summary>An effect list on an object: the field it is in ("m_projectileHitEffects.m_effect" when inside the field's game data), and its label.</summary>
        internal struct ListOn
        {
            public string Field;
            public string Label;
            public EffectList List;
        }

        /// <summary>
        /// Every effect list on an object: in its own fields, and one level down in the game's data
        /// its fields hold, one or a list of them (a fire pit's fireworks, an archery target's hit
        /// by each kind of projectile). An attack's lists are read with the attack.
        /// </summary>
        internal static IEnumerable<ListOn> ListsOn(object owner)
        {
            foreach (var (field, name, label) in OwnLists(owner.GetType()))
            {
                if (field.GetValue(owner) is EffectList list) yield return new ListOn { Field = name, Label = label, List = list };
            }
            foreach (var (outer, inner) in NestedLists(owner.GetType()))
            {
                var value = outer.GetValue(owner);
                if (value == null) continue;
                if (value is IEnumerable many)
                {
                    // One of several says what it is for: a firework, a kind of projectile.
                    foreach (var item in many)
                    {
                        if (item == null) continue;
                        var of = WhatFor(item);
                        foreach (var (field, name, label) in inner)
                        {
                            if (field.GetValue(item) is EffectList list)
                            {
                                yield return new ListOn { Field = name, Label = of.Length > 0 ? Naming.NestedListLabel(outer.Name, field.Name, of) : label, List = list };
                            }
                        }
                    }
                }
                else
                {
                    foreach (var (field, name, label) in inner)
                    {
                        if (field.GetValue(value) is EffectList list) yield return new ListOn { Field = name, Label = label, List = list };
                    }
                }
            }
        }

        private static readonly Dictionary<Type, FieldInfo> NamingFields = new Dictionary<Type, FieldInfo>();

        /// <summary>
        /// What one entry of game data is for, by its first field that says so: an item (by its
        /// shown name), a prefab, or a kind (an enum, such as a projectile type); empty if none.
        /// </summary>
        private static string WhatFor(object item)
        {
            var type = item.GetType();
            if (!NamingFields.TryGetValue(type, out var naming))
            {
                naming = null;
                try
                {
                    foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (field.IsNotSerialized || (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)) continue;
                        if (field.FieldType.IsEnum || typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                        {
                            naming = field;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Faults.Skip("reading of a type's fields", type.Name, ex);
                }
                NamingFields[type] = naming;
            }
            if (naming == null) return "";

            var value = naming.GetValue(item);
            switch (value)
            {
                case ItemDrop drop when drop != null:
                    var shown = Naming.Plain(Localize(drop.m_itemData?.m_shared?.m_name ?? ""));
                    return shown.Length > 0 && !shown.StartsWith("$", StringComparison.Ordinal) ? shown : drop.gameObject.name;
                case Component component when component != null:
                    return component.gameObject.name;
                case UnityEngine.Object thing when thing != null:
                    return thing.name;
                case Enum kind:
                    // A kind of nothing (a projectile type of None) says nothing; flags each say theirs.
                    if (Convert.ToInt64(kind) == 0) return "";
                    return string.Join(", ", kind.ToString().Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).Select(Naming.FieldLabel));
                default:
                    return "";
            }
        }

        private static readonly Dictionary<Type, (FieldInfo, string, string)[]> OwnListsByType = new Dictionary<Type, (FieldInfo, string, string)[]>();
        private static readonly Dictionary<Type, (FieldInfo, (FieldInfo, string, string)[])[]> NestedListsByType = new Dictionary<Type, (FieldInfo, (FieldInfo, string, string)[])[]>();

        private static (FieldInfo Field, string Name, string Label)[] OwnLists(Type type)
        {
            if (OwnListsByType.TryGetValue(type, out var known)) return known;
            known = EffectFields(type).Select(f => (f, f.Name, Naming.EffectListLabel(f.Name))).ToArray();
            OwnListsByType[type] = known;
            return known;
        }

        /// <summary>The fields of a type that hold game data with effect lists in it, each with those lists' fields, names and labels.</summary>
        private static (FieldInfo Outer, (FieldInfo Field, string Name, string Label)[] Inner)[] NestedLists(Type type)
        {
            if (NestedListsByType.TryGetValue(type, out var known)) return known;
            var found = new List<(FieldInfo, (FieldInfo, string, string)[])>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            try
            {
                for (var t = type; t != null && t != typeof(object) && t != typeof(MonoBehaviour) && t != typeof(Component) && t != typeof(ScriptableObject); t = t.BaseType)
                {
                    foreach (var outer in t.GetFields(flags))
                    {
                        if (outer.IsNotSerialized || (!outer.IsPublic && outer.GetCustomAttribute<SerializeField>() == null)) continue;
                        var ft = outer.FieldType;
                        var element = ft.IsArray ? ft.GetElementType()
                            : ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>) ? ft.GetGenericArguments()[0]
                            : ft;
                        if (!HoldsLists(element)) continue;
                        var inner = EffectFields(element).Select(f => (f, outer.Name + "." + f.Name, Naming.NestedListLabel(outer.Name, f.Name))).ToArray();
                        if (inner.Length > 0) found.Add((outer, inner));
                    }
                }
            }
            catch (Exception ex)
            {
                Faults.Skip("reading of a type's fields", type.Name, ex);
                found.Clear();
            }
            known = found.ToArray();
            NestedListsByType[type] = known;
            return known;
        }

        /// <summary>
        /// The game's own small data, a class or a struct, that may hold effect lists of its own;
        /// an attack and an item's data are read on their own, as are the lists themselves.
        /// </summary>
        private static bool HoldsLists(Type type)
        {
            if (type == null || type.IsPrimitive || type.IsEnum || type == typeof(string) || !type.IsSerializable || type.Namespace != null) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
            return type != typeof(EffectList) && type != typeof(DropTable) && type != typeof(Attack)
                   && type != typeof(ItemDrop.ItemData) && type != typeof(ItemDrop.ItemData.SharedData);
        }

        /// <summary>The EffectList fields on a type and its bases, remembered per type; none for a type whose fields cannot be read.</summary>
        internal static FieldInfo[] EffectFields(Type type)
        {
            if (EffectFieldsByType.TryGetValue(type, out var known)) return known;

            var found = new List<FieldInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            try
            {
                for (var t = type; t != null && t != typeof(object); t = t.BaseType)
                {
                    foreach (var field in t.GetFields(flags))
                    {
                        if (field.FieldType == typeof(EffectList)) found.Add(field);
                    }
                }
            }
            catch (Exception ex)
            {
                Faults.Skip("reading of a type's fields", type.Name, ex);
                found.Clear();
            }

            known = found.ToArray();
            EffectFieldsByType[type] = known;
            return known;
        }
    }
}
