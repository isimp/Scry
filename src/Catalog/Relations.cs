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
        public const string Items = "Items";
        public const string ItemOf = "Item of";

        /// <summary>The ways an item gives a status effect that its own facts already tell, with a link.</summary>
        private static readonly HashSet<string> ToldByItems = new HashSet<string> { "equip", "set", "consume", "attack" };

        private static readonly Dictionary<RuntimeAnimatorController, List<(string Clip, GameObject Thing)>> ByController =
            new Dictionary<RuntimeAnimatorController, List<(string, GameObject)>>();
        private static readonly Dictionary<Type, FieldInfo[]> PrefabFields = new Dictionary<Type, FieldInfo[]>();

        private static LinkBook _book = new LinkBook();
        private static readonly List<(string, string, string, bool)> Sets = new List<(string, string, string, bool)>();
        private static readonly List<(string, string, bool)> Ammo = new List<(string, string, bool)>();
        private static readonly HashSet<string> Made = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The registered prefabs' names: a prefab a field names that is none of them is followed as a helper of its user.</summary>
        private static readonly HashSet<string> Registered = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The helpers followed while reading, for their effect lists to be read as their users'.</summary>
        private static readonly List<Helper> Helpers = new List<Helper>();

        /// <summary>The helpers followed for the prefab being read, so each is followed once for it.</summary>
        private static readonly HashSet<GameObject> Following = new HashSet<GameObject>();

        /// <summary>
        /// A prefab no entry stands for, used by one that has one: what a summoning staff's
        /// projectile leaves to raise the troll, the snow a shovel moves. What it spawns and plays
        /// is its user's, under the name of the field that led to it.
        /// </summary>
        public struct Helper
        {
            public string User;
            public GameObject Prefab;
            public string Part;
        }

        /// <summary>A creature's gear, linked as what it carries, not as what it spawns.</summary>
        private static readonly HashSet<string> GearFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "m_defaultItems", "m_randomWeapon", "m_randomArmor", "m_randomShield", "m_randomSets", "m_randomItems",
        };

        /// <summary>Lets go of the links read, for a world that was left.</summary>
        public static void Forget()
        {
            _book = new LinkBook();
            Sets.Clear();
            Ammo.Clear();
            ByController.Clear();
            Made.Clear();
            Registered.Clear();
            Helpers.Clear();
            Following.Clear();
        }

        /// <summary>Starts reading the links again, for the catalog of the current world, whose registered prefabs are named.</summary>
        public static void Begin(IEnumerable<string> registered)
        {
            _book = new LinkBook();
            Sets.Clear();
            Ammo.Clear();
            ByController.Clear();
            Made.Clear();
            Registered.Clear();
            Registered.UnionWith(registered);
            Helpers.Clear();
            Following.Clear();
            if (ObjectDB.instance != null)
            {
                foreach (var recipe in ObjectDB.instance.m_recipes) if (recipe?.m_item != null) Made.Add(recipe.m_item.gameObject.name);
            }
        }

        /// <summary>One prefab's links, read from its components, which are looked through once for the whole catalog.</summary>
        public static void Read(GameObject prefab, List<Component> components)
        {
            if (prefab == null) return;
            Following.Clear();

            // Each kind of link on its own: one that fails, on a mod's odd prefab or because an
            // update changed what it reads, costs only that kind.
            void Part(string links, Action read)
            {
                Guard.Each(links, prefab.name, () => read());
            }
            Part("links of animations", () => Animations(prefab, _book));
            Part("links of footsteps", () => Steps(prefab, _book));
            Part("links of carried items", () => Carried(prefab, _book));
            Part("links of named prefabs and items", () => Fields(prefab, components, _book));
            Part("links of station upgrades", () => Upgrade(prefab, _book));
            Part("links of status effects damage causes", () =>
            {
                var aoe = prefab.GetComponent<Aoe>();
                if (aoe != null) Damage(prefab, aoe.m_damage, _book, StatusEffects);
                var damages = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                // An item tells what its damage causes in its own facts; the effect names the item.
                if (damages != null) Damage(prefab, damages.m_damages, _book, null);
            });
            Part("links of sets and ammo", () =>
            {
                var shared = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                if (shared == null) return;
                Sets.Add((prefab.name, shared.m_setName, CatalogBuilder.Localize(shared.m_name), Made.Contains(prefab.name)));
                var type = shared.m_itemType;
                Ammo.Add((prefab.name, shared.m_ammoType,
                    type == ItemDrop.ItemData.ItemType.Ammo || type == ItemDrop.ItemData.ItemType.AmmoNonEquipable));
            });
        }

        /// <summary>The prefabs a status effect's fields name, such as the demister's ball of light.</summary>
        public static void ReadStatusEffect(StatusEffect effect)
        {
            if (effect == null) return;
            Following.Clear();
            Guard.Each("links of status effects", effect.name, () =>
            {
                Named(effect, "se:" + effect.name, null, _book, 0);
            });
        }

        /// <summary>Hands over the helpers followed so far, and forgets them.</summary>
        public static void TakeHelpers(List<Helper> into)
        {
            into.AddRange(Helpers);
            Helpers.Clear();
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
            var prefab = ZNetScene.instance != null ? GamePrefabs.Named(name) : prefabs.Find(p => p != null && p.name == name);
            return prefab != null && prefab.GetComponent<ItemDrop>() != null;
        }

        /// <summary>The status effects a prefab's damage puts on what it hits.</summary>
        private static void Damage(GameObject prefab, HitData.DamageTypes damage, LinkBook book, string group)
        {
            foreach (var (type, effect) in CombatWords.DamageEffects)
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
            foreach (var animator in played != null ? new[] { played } : Array.Empty<Animator>())
            {
                var controller = animator.runtimeAnimatorController;
                if (controller == null) continue;
                if (!ByController.TryGetValue(controller, out var named))
                {
                    // Every clip's events are read here once anyway; what a copy's ears need of
                    // them is kept too, so showing a person need not read its hundreds of clips.
                    named = new List<(string, GameObject)>();
                    var events = 0;
                    var unknown = new SortedSet<string>(StringComparer.Ordinal);
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
            foreach (var item in CarriedItems(prefab))
            {
                book.Add(prefab.name, Carries, item.name, CarriedBy);

                // What its attacks fire or spawn is the creature's too. A creature's own attack
                // items (a troll's throw) are in the game's item list but not the scene's, so they
                // are not in the catalog, and a link through them would be lost.
                var shared = item.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                if (shared == null) continue;
                var part = CatalogBuilder.AttackName(item);
                if (shared.m_attack != null) Named(shared.m_attack, prefab.name, prefab, book, 1, part: part);
                if (shared.m_secondaryAttack != null) Named(shared.m_secondaryAttack, prefab.name, prefab, book, 1, part: part);
            }
        }

        /// <summary>
        /// Any other prefab a field of the prefab names: directly, in a list, or one level down in
        /// the game's small data classes (a spawner's spawn data, an item's attack). Parts of the
        /// prefab itself, drop tables (told as drops), what a creature carries and what anything
        /// leaves behind are linked elsewhere and left out here.
        /// </summary>
        private static void Fields(GameObject prefab, List<Component> components, LinkBook book)
        {
            foreach (var component in components)
            {
                if (Skipped(component)) continue;
                Named(component, prefab.name, prefab, book, 0);

                var shared = (component as ItemDrop)?.m_itemData?.m_shared;
                if (shared == null) continue;
                Named(shared, prefab.name, prefab, book, 1);
                if (shared.m_attack != null) Named(shared.m_attack, prefab.name, prefab, book, 1);
                if (shared.m_secondaryAttack != null) Named(shared.m_secondaryAttack, prefab.name, prefab, book, 1);

                // Ammo holds the projectile the weapon firing it shoots (Attack.FireProjectileBurst
                // takes the ammo's), whatever the ammo's own attack is: an arrow's is a swing.
                var ammo = shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo || shared.m_itemType == ItemDrop.ItemData.ItemType.AmmoNonEquipable;
                if (ammo && shared.m_attack?.m_attackProjectile != null) Link(shared.m_attack.m_attackProjectile, prefab.name, prefab, book, Naming.FieldLabel("m_attackProjectile"), null);
            }
        }

        /// <summary>Parts whose prefabs are linked elsewhere: drops, what is left behind, a tree's log and stump.</summary>
        private static bool Skipped(Component component)
        {
            return component == null || component is Transform || component is CharacterDrop
                   || component is TreeBase || component is TreeLog || component is Destructible;
        }

        /// <summary>
        /// Fields whose items the facts already tell, as uses or sources, and are not linked again:
        /// what a piece is built from, what a creature eats, what a trader sells, what a station
        /// turns into what or burns, what a producer makes, and what an altar takes.
        /// </summary>
        private static bool ItemsToldElsewhere(object owner, FieldInfo field)
        {
            if (owner is Piece && field.Name == "m_resources") return true;
            if (owner is MonsterAI && field.Name == "m_consumeItems") return true;
            if (owner is CookingStation && field.Name == "m_overCookedItem") return true;
            if (owner is Trader || owner is Beehive || owner is SapCollector || owner is Incinerator || owner is OfferingBowl) return true;
            // A plant's grown version is told as "Grows into" on it and "Grows from" on what it grows
            // into; what a bush gives, as "Picked" on it and "Picked from" on the item.
            if (owner is Plant && field.Name == "m_grownPrefabs") return true;
            if (owner is Pickable && field.Name == "m_itemPrefab") return true;
            return owner is Component && Knowledge.IsToldAsUse(owner.GetType(), field);
        }

        /// <summary>
        /// The prefabs the fields of one object name, linked from its user (by key: a prefab's name,
        /// or "se:" and a status effect's); <paramref name="self"/> is the user's own prefab, if any.
        /// </summary>
        private static void Named(object owner, string key, GameObject self, LinkBook book, int depth, string outer = null, string part = null)
        {
            foreach (var field in FieldsOf(owner.GetType()))
            {
                if (owner is Humanoid && GearFields.Contains(field.Name)) continue;
                var told = depth == 0 && ItemsToldElsewhere(owner, field);
                if (told && (owner is Plant || owner is Pickable)) continue;
                if (!TypeFields.Read(field, owner, out var value) || value == null) continue;

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
                    if (Links(single, self)) Link(single, key, self, book, Label(), part);
                }
                else if (value is ItemDrop thing)
                {
                    if (!told && thing != null && Links(thing.gameObject, self)) LinkItem(thing.gameObject, key, book, Label());
                }
                else if (value is IEnumerable list && !(value is string))
                {
                    foreach (var item in list)
                    {
                        if (item is GameObject each)
                        {
                            if (Links(each, self)) Link(each, key, self, book, Label(), part);
                        }
                        else if (item is ItemDrop listed)
                        {
                            if (!told && listed != null && Links(listed.gameObject, self)) LinkItem(listed.gameObject, key, book, Label());
                        }
                        else if (item != null && depth < 1 && !told && IsData(item.GetType())) Named(item, key, self, book, depth + 1, Label(), part);
                    }
                }
                else if (depth < 1 && !told && IsData(value.GetType()))
                {
                    Named(value, key, self, book, depth + 1, Label(), part);
                }
            }
        }

        /// <summary>
        /// The prefabs the fields of one object name, directly, in a list, or one level down in the
        /// game's small data classes, without linking them: for a location, which is no entry.
        /// </summary>
        public static void PrefabsNamedBy(object owner, List<GameObject> into, int depth = 0)
        {
            foreach (var field in FieldsOf(owner.GetType()))
            {
                if (!TypeFields.Read(field, owner, out var value) || value == null) continue;
                if (value is GameObject single)
                {
                    if (single != null) into.Add(single);
                }
                else if (value is IEnumerable list && !(value is string))
                {
                    foreach (var item in list)
                    {
                        if (item is GameObject each)
                        {
                            if (each != null) into.Add(each);
                        }
                        else if (item != null && depth < 1 && IsData(item.GetType())) PrefabsNamedBy(item, into, depth + 1);
                    }
                }
                else if (depth < 1 && IsData(value.GetType()))
                {
                    PrefabsNamedBy(value, into, depth + 1);
                }
            }
        }

        /// <summary>Only other prefabs are linked: a part of this one has a parent, and is not a thing of its own.</summary>
        private static bool Links(GameObject target, GameObject self) => target != null && target != self && target.transform.parent == null;

        /// <summary>An item a field names, such as a creature's saddle, a door's key or a fish's bait: linked as an item, not as spawned.</summary>
        private static void LinkItem(GameObject item, string key, LinkBook book, string label)
        {
            book.Add(key, Items, item.name, ItemOf, label.ToLowerInvariant());
        }

        private static void Link(GameObject target, string key, GameObject self, LinkBook book, string label, string part)
        {
            if (!Links(target, self)) return;
            book.Add(key, Spawns, target.name, SpawnedBy, label.ToLowerInvariant());
            Follow(target, key, self, book, part ?? label);
        }

        /// <summary>
        /// Follows a prefab no entry stands for into what its own fields name, linked from its
        /// user, and keeps it for its effect lists to be read as the user's (<see cref="Helper"/>).
        /// A few dozen at most for one user, each once, so a chain of them ends.
        /// </summary>
        private static void Follow(GameObject target, string key, GameObject self, LinkBook book, string part)
        {
            if (Registered.Count == 0 || Registered.Contains(target.name) || Following.Count >= 64 || !Following.Add(target)) return;
            Helpers.Add(new Helper { User = key, Prefab = target, Part = part });

            var parts = new List<Component>();
            target.GetComponentsInChildren(true, parts);
            foreach (var component in parts)
            {
                if (Skipped(component)) continue;
                Guard.Each("links of what prefabs spawn", target.name, () =>
                {
                    Named(component, key, self, book, 0, null, part);
                });
            }
        }

        /// <summary>
        /// The game's own small serializable data, classes or structs (a turret's ammo, a fire
        /// pit's fireworks), which hold prefab names one level down.
        /// </summary>
        private static bool IsData(Type type)
        {
            var shape = type.IsClass ? !typeof(UnityEngine.Object).IsAssignableFrom(type) : type.IsValueType && !type.IsPrimitive && !type.IsEnum;
            return shape && type.IsSerializable && type.Namespace == null && type != typeof(DropTable) && type != typeof(EffectList);
        }

        /// <summary>The fields that can hold a prefab or an item: one, a list of them, or data holding them.</summary>
        private static FieldInfo[] FieldsOf(Type type) => TypeFields.Matching(PrefabFields, type, field =>
        {
            var ft = field.FieldType;
            if (field.IsNotSerialized || field.GetCustomAttribute<NonSerializedAttribute>() != null) return false;
            if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) return false;
            return ft == typeof(GameObject) || ft == typeof(GameObject[]) || ft == typeof(List<GameObject>)
                   || ft == typeof(ItemDrop) || ft == typeof(ItemDrop[]) || ft == typeof(List<ItemDrop>)
                   || ft.IsArray && IsData(ft.GetElementType())
                   || ft.IsGenericType && ft.GetGenericTypeDefinition() == typeof(List<>) && IsData(ft.GetGenericArguments()[0])
                   || IsData(ft);
        });
    }
}
