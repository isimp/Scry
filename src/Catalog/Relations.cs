using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// How prefabs point at one another beyond their effect lists, read into a <see cref="LinkBook"/>:
    /// the sounds and effects a creature's animations name, its footsteps, the items it carries,
    /// any prefab a field names (a projectile an attack fires, what a projectile spawns on hit, a
    /// plant's grown version, a spawner's creature), armour sets, weapons and their ammo, and the
    /// status effects items and prefabs give.
    /// </summary>
    internal static class Relations
    {
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

        private static readonly Dictionary<RuntimeAnimatorController, List<(string Clip, GameObject Thing)>> ByController =
            new Dictionary<RuntimeAnimatorController, List<(string, GameObject)>>();
        private static readonly Dictionary<Type, FieldInfo[]> PrefabFields = new Dictionary<Type, FieldInfo[]>();

        public static LinkBook Gather(List<GameObject> prefabs)
        {
            var book = new LinkBook();
            var sets = new List<(string, string)>();
            var ammo = new List<(string, string, bool)>();
            ByController.Clear();

            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;
                try
                {
                    Animations(prefab, book);
                    Steps(prefab, book);
                    Carried(prefab, book);
                    Fields(prefab, book);

                    var shared = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                    if (shared != null)
                    {
                        sets.Add((prefab.name, shared.m_setName));
                        var type = shared.m_itemType;
                        ammo.Add((prefab.name, shared.m_ammoType,
                            type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable));
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogDebug($"Scry could not read the links of {prefab.name}: {ex.Message}");
                }
            }

            book.AddSets(sets);
            book.AddAmmo(ammo);
            foreach (var giver in Knowledge.Givers()) book.Add(giver.Prefab, StatusEffects, "se:" + giver.Effect, GivenBy, giver.How);
            return book;
        }

        /// <summary>What a creature's clips name themselves to play or hold, by clip.</summary>
        private static void Animations(GameObject prefab, LinkBook book)
        {
            foreach (var animator in prefab.GetComponentsInChildren<Animator>(true))
            {
                var controller = animator.runtimeAnimatorController;
                if (controller == null) continue;
                if (!ByController.TryGetValue(controller, out var named))
                {
                    named = new List<(string, GameObject)>();
                    foreach (var clip in controller.animationClips)
                    {
                        if (clip == null) continue;
                        foreach (var e in clip.events)
                        {
                            if ((e.functionName == "Effect" || e.functionName == "Attach") && e.objectReferenceParameter is GameObject thing && thing != null)
                            {
                                named.Add((clip.name, thing));
                            }
                        }
                    }
                    ByController[controller] = named;
                }
                foreach (var (clip, thing) in named) book.Add(prefab.name, AnimationSounds, thing.name, PlayedByAnimation, clip);
            }
        }

        /// <summary>A creature's footsteps, by how it moves and what it walks on.</summary>
        private static void Steps(GameObject prefab, LinkBook book)
        {
            var step = prefab.GetComponentInChildren<FootStep>(true);
            if (step?.m_effects == null) return;
            foreach (var effect in step.m_effects)
            {
                if (effect?.m_effectPrefabs == null) continue;
                var note = Gait(effect.m_motionType) + " on " + Ground(effect.m_material);
                foreach (var thing in effect.m_effectPrefabs) if (thing != null) book.Add(prefab.name, Footsteps, thing.name, FootstepOf, note);
            }
        }

        private static string Gait(FootStep.MotionType motion)
        {
            var names = Enum.GetValues(typeof(FootStep.MotionType)).Cast<FootStep.MotionType>()
                .Where(m => (motion & m) != 0).Select(m => m.ToString().ToLowerInvariant()).ToList();
            return names.Count > 0 ? string.Join("/", names) : "any gait";
        }

        private static string Ground(FootStep.GroundMaterial material)
        {
            if ((material & FootStep.GroundMaterial.Everything) == FootStep.GroundMaterial.Everything) return "any ground";
            var names = Enum.GetValues(typeof(FootStep.GroundMaterial)).Cast<FootStep.GroundMaterial>()
                .Where(m => m != FootStep.GroundMaterial.None && m != FootStep.GroundMaterial.Everything && (material & m) != 0)
                .Select(m => m == FootStep.GroundMaterial.Default ? "plain ground" : m == FootStep.GroundMaterial.GenericGround ? "ground" : m.ToString().ToLowerInvariant())
                .ToList();
            return names.Count > 0 ? string.Join("/", names) : "nothing";
        }

        /// <summary>Everything a humanoid may be handed when it spawns.</summary>
        public static List<GameObject> CarriedItems(GameObject prefab)
        {
            var items = new List<GameObject>();
            var humanoid = prefab != null ? prefab.GetComponent<Humanoid>() : null;
            if (humanoid == null) return items;
            if (humanoid.m_defaultItems != null) items.AddRange(humanoid.m_defaultItems);
            if (humanoid.m_randomWeapon != null) items.AddRange(humanoid.m_randomWeapon);
            if (humanoid.m_randomArmor != null) items.AddRange(humanoid.m_randomArmor);
            if (humanoid.m_randomShield != null) items.AddRange(humanoid.m_randomShield);
            if (humanoid.m_randomSets != null) foreach (var set in humanoid.m_randomSets) if (set?.m_items != null) items.AddRange(set.m_items);
            if (humanoid.m_randomItems != null) foreach (var random in humanoid.m_randomItems) if (random?.m_prefab != null) items.Add(random.m_prefab);
            return items.Where(i => i != null).Distinct().ToList();
        }

        private static void Carried(GameObject prefab, LinkBook book)
        {
            foreach (var item in CarriedItems(prefab)) book.Add(prefab.name, Carries, item.name, CarriedBy);
        }

        /// <summary>
        /// Any other prefab a field of the prefab names: directly, in a list, or one level down in
        /// the game's small data classes (a spawner's spawn data, an item's attack). Parts of the
        /// prefab itself, drop tables (told as drops), what it carries and what it leaves behind are
        /// linked elsewhere and left out here.
        /// </summary>
        private static void Fields(GameObject prefab, LinkBook book)
        {
            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || component is CharacterDrop || component is Humanoid
                    || component is TreeBase || component is TreeLog || component is Destructible) continue;
                Named(component, prefab, book, 0);

                var shared = (component as ItemDrop)?.m_itemData?.m_shared;
                if (shared == null) continue;
                Named(shared, prefab, book, 1);
                if (shared.m_attack != null) Named(shared.m_attack, prefab, book, 1);
                if (shared.m_secondaryAttack != null) Named(shared.m_secondaryAttack, prefab, book, 1);
            }
        }

        private static void Named(object owner, GameObject prefab, LinkBook book, int depth, string outer = null)
        {
            foreach (var field in FieldsOf(owner.GetType()))
            {
                object value;
                try { value = field.GetValue(owner); }
                catch { continue; }
                if (value == null) continue;

                var label = outer ?? Naming.FieldLabel(field.Name);
                if (value is GameObject single)
                {
                    Link(single, prefab, book, label);
                }
                else if (value is IEnumerable list && !(value is string))
                {
                    foreach (var item in list)
                    {
                        if (item is GameObject each) Link(each, prefab, book, label);
                        else if (item != null && depth < 1 && IsData(item.GetType())) Named(item, prefab, book, depth + 1, label);
                    }
                }
                else if (depth < 1 && IsData(value.GetType()))
                {
                    Named(value, prefab, book, depth + 1, label);
                }
            }
        }

        private static void Link(GameObject target, GameObject prefab, LinkBook book, string label)
        {
            // Only other prefabs: a part of this one has a parent, and is not a thing of its own.
            if (target == null || target == prefab || target.transform.parent != null) return;
            book.Add(prefab.name, Spawns, target.name, SpawnedBy, label.ToLowerInvariant());
        }

        /// <summary>The game's own small serializable data classes, which hold prefab names one level down.</summary>
        private static bool IsData(Type type)
        {
            return type.IsClass && !typeof(UnityEngine.Object).IsAssignableFrom(type) && type.IsSerializable
                   && type.Namespace == null && type != typeof(DropTable) && type != typeof(EffectList);
        }

        /// <summary>The fields that can hold a prefab: one, a list of them, or data holding them.</summary>
        private static FieldInfo[] FieldsOf(Type type)
        {
            if (PrefabFields.TryGetValue(type, out var known)) return known;
            var found = new List<FieldInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var t = type; t != null && t != typeof(object) && t != typeof(MonoBehaviour) && t != typeof(Component); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                {
                    var ft = field.FieldType;
                    if (field.IsNotSerialized || field.GetCustomAttribute<NonSerializedAttribute>() != null) continue;
                    if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;
                    if (ft == typeof(GameObject) || ft == typeof(GameObject[]) || ft == typeof(List<GameObject>)) found.Add(field);
                    else if (ft.IsArray && IsData(ft.GetElementType())) found.Add(field);
                    else if (ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>) && IsData(ft.GetGenericArguments()[0])) found.Add(field);
                    else if (IsData(ft)) found.Add(field);
                }
            }
            known = found.ToArray();
            PrefabFields[type] = known;
            return known;
        }
    }
}
