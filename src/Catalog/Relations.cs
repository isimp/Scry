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
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static Relations() => WorldCaches.Register(nameof(Relations), Forget);

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
        public const string Upgrades = "Upgrades";
        public const string UpgradeOf = "Upgrade of";

        /// <summary>The status effect each kind of damage puts on what it hits, as <c>Character</c> adds them.</summary>
        private static readonly (string Damage, string Effect)[] DamageEffects =
        {
            ("fire", "Burning"), ("frost", "Frost"), ("lightning", "Lightning"), ("poison", "Poison"), ("spirit", "Spirit"),
        };

        /// <summary>The ways an item gives a status effect that its own facts already tell, with a link.</summary>
        private static readonly HashSet<string> ToldByItems = new HashSet<string> { "equip", "set", "consume", "attack" };

        private static readonly Dictionary<RuntimeAnimatorController, List<(string Clip, GameObject Thing)>> ByController =
            new Dictionary<RuntimeAnimatorController, List<(string, GameObject)>>();
        private static readonly Dictionary<Type, FieldInfo[]> PrefabFields = new Dictionary<Type, FieldInfo[]>();

        private static LinkBook _book = new LinkBook();
        private static readonly List<(string, string, string, bool)> Sets = new List<(string, string, string, bool)>();
        private static readonly List<(string, string, bool)> Ammo = new List<(string, string, bool)>();
        private static readonly HashSet<string> Made = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Lets go of the links read, for a world that was left.</summary>
        public static void Forget()
        {
            _book = new LinkBook();
            Sets.Clear();
            Ammo.Clear();
            ByController.Clear();
            Made.Clear();
        }

        /// <summary>Starts reading the links again, for the catalog of the current world.</summary>
        public static void Begin()
        {
            _book = new LinkBook();
            Sets.Clear();
            Ammo.Clear();
            ByController.Clear();
            Made.Clear();
            if (ObjectDB.instance != null)
            {
                foreach (var recipe in ObjectDB.instance.m_recipes) if (recipe?.m_item != null) Made.Add(recipe.m_item.gameObject.name);
            }
        }

        /// <summary>One prefab's links, read from its components, which are looked through once for the whole catalog.</summary>
        public static void Read(GameObject prefab, List<Component> components)
        {
            if (prefab == null) return;
            try
            {
                Animations(prefab, _book);
                Steps(prefab, _book);
                Carried(prefab, _book);
                Fields(prefab, components, _book);
                Upgrade(prefab, _book);

                var aoe = prefab.GetComponent<Aoe>();
                if (aoe != null) Damage(prefab, aoe.m_damage, _book, StatusEffects);

                var shared = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                if (shared != null)
                {
                    // An item tells what its damage causes in its own facts; the effect names the item.
                    Damage(prefab, shared.m_damages, _book, null);
                    Sets.Add((prefab.name, shared.m_setName, CatalogBuilder.Localize(shared.m_name), Made.Contains(prefab.name)));
                    var type = shared.m_itemType;
                    Ammo.Add((prefab.name, shared.m_ammoType,
                        type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable));
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Scry could not read the links of {prefab.name}: {ex.Message}");
            }
        }

        /// <summary>The links read, with sets, weapons' ammo and what gives each status effect put in once every prefab is read.</summary>
        public static LinkBook Finish(List<GameObject> prefabs)
        {
            var book = _book;
            _book = new LinkBook();
            book.AddSets(Sets);
            book.AddAmmo(Ammo);
            foreach (var giver in Knowledge.Givers())
            {
                // What an item gives when worn, used, as a set or on hit is in its own facts already.
                var item = ToldByItems.Contains(giver.How) && IsItem(giver.Prefab, prefabs);
                book.Add(giver.Prefab, item ? null : StatusEffects, "se:" + giver.Effect, GivenBy, giver.How);
            }
            Sets.Clear();
            Ammo.Clear();
            return book;
        }

        private static bool IsItem(string name, List<GameObject> prefabs)
        {
            var prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(name) : prefabs.Find(p => p != null && p.name == name);
            return prefab != null && prefab.GetComponent<ItemDrop>() != null;
        }

        /// <summary>The status effects a prefab's damage puts on what it hits.</summary>
        private static void Damage(GameObject prefab, HitData.DamageTypes damage, LinkBook book, string group)
        {
            foreach (var (type, effect) in DamageEffects)
            {
                var amount = type == "fire" ? damage.m_fire : type == "frost" ? damage.m_frost : type == "lightning" ? damage.m_lightning
                    : type == "poison" ? damage.m_poison : damage.m_spirit;
                if (amount > 0f) book.Add(prefab.name, group, "se:" + effect, GivenBy, type + " damage");
            }
        }

        /// <summary>A crafting station's upgrades: the pieces that raise its level while built near it.</summary>
        private static void Upgrade(GameObject prefab, LinkBook book)
        {
            var extension = prefab.GetComponent<StationExtension>();
            var station = extension != null ? extension.m_craftingStation : null;
            if (station != null) book.Add(station.transform.root.name, Upgrades, prefab.name, UpgradeOf);
        }

        /// <summary>What a creature's clips name themselves to play or hold, by clip.</summary>
        private static void Animations(GameObject prefab, LinkBook book)
        {
            // Only the animator the creature plays by, not an old one left switched off beside it.
            var played = ClipPlayer.AnimatorOf(prefab);
            foreach (var animator in played != null ? new[] { played } : new Animator[0])
            {
                var controller = animator.runtimeAnimatorController;
                if (controller == null) continue;
                if (!ByController.TryGetValue(controller, out var named))
                {
                    // Every clip's events are read here once anyway; what a copy's ears need of
                    // them is kept too, so showing a person need not read its hundreds of clips.
                    named = new List<(string, GameObject)>();
                    var events = 0;
                    var unknown = new SortedSet<string>();
                    foreach (var clip in controller.animationClips)
                    {
                        if (clip == null) continue;
                        foreach (var e in clip.events)
                        {
                            events++;
                            if (!AnimationEars.Answers(e.functionName)) unknown.Add(e.functionName);
                            if ((e.functionName == "Effect" || e.functionName == "Attach") && e.objectReferenceParameter is GameObject thing && thing != null)
                            {
                                named.Add((clip.name, thing));
                            }
                        }
                    }
                    ByController[controller] = named;
                    AnimationEars.Remember(controller, events, unknown);
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
        private static void Fields(GameObject prefab, List<Component> components, LinkBook book)
        {
            foreach (var component in components)
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

                // An attack uses only what its kind uses (Attack.OnAttackTrigger): a projectile
                // only when it throws or shoots, what it spawns only when it swings or hits an area.
                if (owner is Attack attack)
                {
                    var type = attack.m_attackType;
                    if (field.Name == "m_attackProjectile" && type != Attack.AttackType.Projectile) continue;
                    if (field.Name == "m_spawnOnTrigger" && type != Attack.AttackType.Horizontal && type != Attack.AttackType.Vertical && type != Attack.AttackType.Area) continue;
                }

                // Named only when a link is made or looked for further down: most fields name nothing.
                var label = outer;
                string Label() => label = label ?? Naming.FieldLabel(field.Name);
                if (value is GameObject single)
                {
                    if (Links(single, prefab)) Link(single, prefab, book, Label());
                }
                else if (value is IEnumerable list && !(value is string))
                {
                    foreach (var item in list)
                    {
                        if (item is GameObject each)
                        {
                            if (Links(each, prefab)) Link(each, prefab, book, Label());
                        }
                        else if (item != null && depth < 1 && IsData(item.GetType())) Named(item, prefab, book, depth + 1, Label());
                    }
                }
                else if (depth < 1 && IsData(value.GetType()))
                {
                    Named(value, prefab, book, depth + 1, Label());
                }
            }
        }

        /// <summary>Only other prefabs are linked: a part of this one has a parent, and is not a thing of its own.</summary>
        private static bool Links(GameObject target, GameObject prefab) => target != null && target != prefab && target.transform.parent == null;

        private static void Link(GameObject target, GameObject prefab, LinkBook book, string label)
        {
            if (!Links(target, prefab)) return;
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
            try
            {
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
            }
            catch (Exception ex)
            {
                // A mod's type whose fields cannot be read links nothing, remembered as such.
                Plugin.Log.LogDebug($"Scry could not read the fields of {type.Name}: {ex.Message}");
                found.Clear();
            }
            known = found.ToArray();
            PrefabFields[type] = known;
            return known;
        }
    }
}
