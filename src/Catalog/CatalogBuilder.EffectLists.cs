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
                    Guard.Each("interface sounds", Provenance.Interface, () =>
                    {
                        Gather(component, Provenance.Interface, Origin.Vanilla, effects, null, Provenance.Interface);
                    });
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
                if (EffectSlots.Of(list).Length == 0) continue;

                var label = on.Label;
                if (part != null) label = Naming.OfPart(part, label);
                EffectLinks.Note(list, shown, ownerKey, label);

                foreach (var slot in EffectSlots.Of(list))
                {
                    if (!EffectSlots.Plays(slot)) continue;
                    var prefab = slot.m_prefab;

                    if (!effects.TryGetValue(prefab.name, out var found))
                    {
                        found = new Found { Prefab = prefab };
                        effects[prefab.name] = found;
                    }

                    if (found.Users.Add(ownerName)) found.UserOrigins.Add(ownerOrigin);
                    found.Fields.Add(Groups.FieldOf(on.Field, ownerName == Provenance.Interface, Provenance.IsStatusEffect(ownerName)));
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
                if (TypeFields.Value(field, owner) is EffectList list) yield return new ListOn { Field = name, Label = label, List = list };
            }
            foreach (var (outer, inner) in NestedLists(owner.GetType()))
            {
                var value = TypeFields.Value(outer, owner);
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
                            if (TypeFields.Value(field, item) is EffectList list)
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
                        if (TypeFields.Value(field, value) is EffectList list) yield return new ListOn { Field = name, Label = label, List = list };
                    }
                }
            }
        }

        private static readonly Dictionary<Type, FieldInfo[]> NamingFields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>
        /// What one entry of game data is for, by its first field that says so: an item (by its
        /// shown name), a prefab, or a kind (an enum, such as a projectile type); empty if none.
        /// </summary>
        private static string WhatFor(object item)
        {
            var type = item.GetType();
            var namings = TypeFields.Matching(NamingFields, type, field =>
                !field.IsNotSerialized && (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                && (field.FieldType.IsEnum || typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType)));
            var naming = namings.Length > 0 ? namings[0] : null;
            if (naming == null) return "";

            var value = TypeFields.Value(naming, item);
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
                    if (Convert.ToInt64(kind, System.Globalization.CultureInfo.InvariantCulture) == 0) return "";
                    return FactWords.Choice(kind) ?? "";
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
        private static (FieldInfo Outer, (FieldInfo Field, string Name, string Label)[] Inner)[] NestedLists(Type type) =>
            TypeFields.Picked(NestedListsByType, type, outer =>
            {
                if (outer.IsNotSerialized || (!outer.IsPublic && outer.GetCustomAttribute<SerializeField>() == null)) return Array.Empty<(FieldInfo, (FieldInfo, string, string)[])>();
                var ft = outer.FieldType;
                var element = ft.IsArray ? ft.GetElementType()
                    : ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>) ? ft.GetGenericArguments()[0]
                    : ft;
                if (!HoldsLists(element)) return Array.Empty<(FieldInfo, (FieldInfo, string, string)[])>();
                var inner = EffectFields(element).Select(f => (f, Naming.MemberPath(outer.Name, f.Name), Naming.NestedListLabel(outer.Name, f.Name))).ToArray();
                return inner.Length > 0 ? new[] { (outer, inner) } : Array.Empty<(FieldInfo, (FieldInfo, string, string)[])>();
            });

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
        internal static FieldInfo[] EffectFields(Type type) => TypeFields.Matching(EffectFieldsByType, type, field => field.FieldType == typeof(EffectList));
    }
}
