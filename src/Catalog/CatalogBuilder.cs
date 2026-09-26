using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Reads every prefab and status effect the game has into the catalog.
    ///
    /// The scene's registered prefabs are only part of it. Most sounds and many visual effects are
    /// never registered: they hang off the effect lists of the items, pieces, creatures, status
    /// effects and interface that play them, so those lists are followed as well, and whatever
    /// they reach is catalogued with the names of the prefabs that use it.
    ///
    /// Effects that belong only to locations, such as boss altars and runestones, stay out:
    /// locations are loaded from their bundles on demand, and walking them would force the loads.
    /// </summary>
    internal static class CatalogBuilder
    {
        private sealed class Found
        {
            public GameObject Prefab;
            public PrefabTraits Traits;
            public string Token;
            public object Icon;
            public int ExtraLevels;
            public bool HasWear;
            public readonly HashSet<string> Users = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<Origin> UserOrigins = new List<Origin>();
            public readonly HashSet<string> Components = new HashSet<string>(StringComparer.Ordinal);
        }

        private static readonly Dictionary<Type, FieldInfo[]> EffectFieldsByType = new Dictionary<Type, FieldInfo[]>();

        public static List<Entry> Build()
        {
            Compatibility.Check();
            var scene = ZNetScene.instance;
            var registered = new Dictionary<string, Found>(StringComparer.Ordinal);
            EffectLinks.Clear();
            var effects = new Dictionary<string, Found>(StringComparer.Ordinal);

            foreach (var list in new[] { scene.m_prefabs, scene.m_nonNetViewPrefabs })
            {
                foreach (var prefab in list)
                {
                    if (prefab == null || registered.ContainsKey(prefab.name)) continue;
                    registered[prefab.name] = new Found { Prefab = prefab };
                }
            }

            foreach (var pair in registered)
            {
                Describe(pair.Value, pair.Key, Origins.Prefabs.Of(pair.Key), effects);
            }

            // Where things live and which mod added them, before any entry is made.
            Knowledge.Gather(registered.Values.Select(f => f.Prefab));
            IndexRecipes();

            var entries = new List<Entry>();

            var db = ObjectDB.instance;
            if (db != null)
            {
                foreach (var effect in db.m_StatusEffects)
                {
                    if (effect == null) continue;
                    var origin = Origins.StatusEffects.Of(effect.name);
                    var shown = Localize(effect.m_name);
                    Gather(effect, "status effect " + effect.name, origin, effects, "se:" + effect.name, shown.Length > 0 ? shown : effect.name);

                    entries.Add(new Entry
                    {
                        Name = effect.name,
                        DisplayName = Localize(effect.m_name),
                        Kind = Kind.StatusEffect,
                        Origin = origin,
                        Source = effect,
                        Icon = effect.m_icon,
                        Components = new[] { effect.GetType().Name },
                        ModName = Knowledge.ModName(effect.name),
                    });
                }
            }

            GatherInterface(effects);

            // Effects can point at further effects (a hit effect with an area of its own), so the
            // walk continues until nothing new turns up.
            var described = new HashSet<string>(StringComparer.Ordinal);
            bool grew;
            do
            {
                grew = false;
                foreach (var found in new List<Found>(effects.Values))
                {
                    var name = found.Prefab.name;
                    if (registered.ContainsKey(name) || !described.Add(name)) continue;
                    Describe(found, name, Provenance.Combine(found.UserOrigins), effects);
                    grew = true;
                }
            }
            while (grew);

            foreach (var pair in registered) entries.Add(ToEntry(pair.Key, pair.Value, effects, registeredOrigin: true));
            foreach (var pair in effects)
            {
                if (!registered.ContainsKey(pair.Key)) entries.Add(ToEntry(pair.Key, pair.Value, effects, registeredOrigin: false));
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            Leftovers.Pair(entries, FindLeftovers(registered.Values.Select(f => f.Prefab)));
            if (watch.ElapsedMilliseconds >= 50) Plugin.Note($"Scry paired leftovers in {watch.ElapsedMilliseconds} ms.");

            watch.Restart();
            Relations.Gather(registered.Values.Select(f => f.Prefab).ToList()).Apply(entries);
            if (watch.ElapsedMilliseconds >= 50) Plugin.Note($"Scry read links in {watch.ElapsedMilliseconds} ms.");

            return entries;
        }

        private static Entry ToEntry(string name, Found found, Dictionary<string, Found> effects, bool registeredOrigin)
        {
            effects.TryGetValue(name, out var asEffect);
            if (asEffect != null) found.Traits.FromEffectList = true;

            var entry = new Entry
            {
                Name = name,
                DisplayName = Localize(found.Token),
                Kind = Kinds.Of(found.Traits),
                Empty = Kinds.IsEmpty(found.Traits),
                ExtraLevels = found.ExtraLevels,
                HasWear = found.HasWear,
                Source = found.Prefab,
                Icon = found.Icon,
                Origin = registeredOrigin ? Origins.Prefabs.Of(name) : Provenance.Combine(found.UserOrigins),
                Registered = registeredOrigin,
                Components = found.Components.ToArray(),
                Biomes = Knowledge.Biomes(name),
                ModName = Knowledge.ModName(name),
            };

            if (found.Prefab != null)
            {
                entry.Looks = Variants.Of(found.Prefab, out var look);
                entry.DefaultLook = look;
                entry.Stations = StationsOf(found.Prefab);
            }

            if (asEffect != null)
            {
                entry.UsedBy.AddRange(asEffect.Users);
                entry.UsedBy.Sort(StringComparer.OrdinalIgnoreCase);
            }

            return entry;
        }

        private static Dictionary<string, List<Recipe>> _recipes;

        /// <summary>
        /// Where it is made: each recipe's crafting station and the level it needs, "by hand" for
        /// a recipe without one, and a piece's build station.
        /// </summary>
        private static StationUse[] StationsOf(GameObject prefab)
        {
            var uses = new List<StationUse>();

            if (prefab.GetComponent<ItemDrop>() != null && _recipes != null && _recipes.TryGetValue(prefab.name, out var recipes))
            {
                foreach (var recipe in recipes)
                {
                    uses.Add(recipe.m_craftingStation != null
                        ? new StationUse(recipe.m_craftingStation.gameObject.name, Localize(recipe.m_craftingStation.m_name), Math.Max(1, recipe.m_minStationLevel))
                        : new StationUse("hand", "By hand", 1));
                }
            }

            var piece = prefab.GetComponent<Piece>();
            if (piece != null && piece.m_craftingStation != null)
            {
                uses.Add(new StationUse(piece.m_craftingStation.gameObject.name, Localize(piece.m_craftingStation.m_name), 1));
            }

            return uses.ToArray();
        }

        /// <summary>Every enabled recipe by the prefab name of what it makes.</summary>
        private static void IndexRecipes()
        {
            _recipes = new Dictionary<string, List<Recipe>>(StringComparer.Ordinal);
            var db = ObjectDB.instance;
            if (db == null) return;
            foreach (var recipe in db.m_recipes)
            {
                if (recipe == null || !recipe.m_enabled || recipe.m_item == null) continue;
                var name = recipe.m_item.gameObject.name;
                if (!_recipes.TryGetValue(name, out var list)) _recipes[name] = list = new List<Recipe>();
                list.Add(recipe);
            }
        }

        /// <summary>Reads a prefab's components into its traits, and follows its effect lists.</summary>
        private static void Describe(Found found, string owner, Origin ownerOrigin, Dictionary<string, Found> effects)
        {
            var traits = new PrefabTraits();
            found.Traits = traits;

            Component[] components;
            try
            {
                components = found.Prefab.GetComponentsInChildren<Component>(true);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"Scry could not read {owner}: {ex.Message}");
                return;
            }

            foreach (var component in components)
            {
                // A missing script shows up as a null component.
                if (component == null) continue;

                try
                {
                    Note(component, found, traits);
                    found.Components.Add(component.GetType().Name);
                    Gather(component, owner, ownerOrigin, effects, owner, owner);

                    // A creature's attacks are its own: their sounds and effects are played by it.
                    if (component is Humanoid)
                    {
                        foreach (var item in Relations.CarriedItems(found.Prefab))
                        {
                            var carried = item.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                            if (carried == null) continue;
                            var part = AttackName(item);
                            Gather(carried, owner, ownerOrigin, effects, owner, owner, part);
                            if (carried.m_attack != null) Gather(carried.m_attack, owner, ownerOrigin, effects, owner, owner, part);
                            if (carried.m_secondaryAttack != null) Gather(carried.m_secondaryAttack, owner, ownerOrigin, effects, owner, owner, part + " (second)");
                        }
                    }

                    if (component is ItemDrop drop && drop.m_itemData?.m_shared != null)
                    {
                        var shared = drop.m_itemData.m_shared;
                        Gather(shared, owner, ownerOrigin, effects, owner, owner);
                        if (shared.m_attack != null) Gather(shared.m_attack, owner, ownerOrigin, effects, owner, owner, "Attack");
                        if (shared.m_secondaryAttack != null) Gather(shared.m_secondaryAttack, owner, ownerOrigin, effects, owner, owner, "Second attack");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogDebug($"Scry skipped a part of {owner}: {ex.Message}");
                }
            }
        }

        private static void Note(Component component, Found found, PrefabTraits traits)
        {
            switch (component)
            {
                case Character character:
                    traits.HasCharacter = true;
                    found.Token = found.Token ?? character.m_name;
                    break;
                case ItemDrop drop:
                    traits.HasItemDrop = true;
                    var shared = drop.m_itemData?.m_shared;
                    if (shared != null)
                    {
                        found.Token = found.Token ?? shared.m_name;
                        if (found.Icon == null && shared.m_icons != null && shared.m_icons.Length > 0) found.Icon = shared.m_icons[0];
                    }
                    break;
                case Piece piece:
                    traits.HasPiece = true;
                    found.Token = found.Token ?? piece.m_name;
                    found.Icon = found.Icon ?? piece.m_icon;
                    break;
                case Projectile _:
                    traits.HasProjectile = true;
                    break;
                case ParticleSystem _:
                    traits.HasParticles = true;
                    break;
                case ParticleSystemRenderer _:
                    break;
                case Renderer _:
                    traits.HasRenderer = true;
                    break;
                case Light _:
                    traits.HasLight = true;
                    break;
                case AudioSource _:
                case ZSFX _:
                    traits.HasAudio = true;
                    break;
                case Collider collider:
                    if (!collider.isTrigger) traits.HasSolidCollider = true;
                    break;
                case LevelEffects levels:
                    found.ExtraLevels = Math.Max(found.ExtraLevels, levels.m_levelSetups?.Count ?? 0);
                    break;
                case WearNTear wear:
                    found.HasWear |= (wear.m_worn != null && wear.m_worn != wear.m_new)
                                     || (wear.m_broken != null && wear.m_broken != wear.m_new);
                    break;
            }
        }

        /// <summary>
        /// The name the game shows for an item, when it has one of its own: a translated name. A
        /// name that is no translation key ("slap", "fireballattack") is only a maker's label, and
        /// gives none.
        /// </summary>
        internal static string GameName(GameObject item)
        {
            var token = item != null ? item.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_name : null;
            if (string.IsNullOrEmpty(token) || !token.StartsWith("$", StringComparison.Ordinal)) return null;
            var shown = Localize(token);
            return shown.Length > 0 && !shown.StartsWith("$", StringComparison.Ordinal) ? shown : null;
        }

        /// <summary>What an item a creature carries is called: its shown name, or its prefab name when it has none.</summary>
        internal static string AttackName(GameObject item)
        {
            var shared = item.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            var shown = shared != null ? Localize(shared.m_name) : "";
            return shown.Length > 0 && !shown.StartsWith("$", StringComparison.Ordinal) ? shown : item.name;
        }

        /// <summary>
        /// What each prefab leaves behind, as the game leaves it: a creature's ragdoll and the
        /// remains its death throws, a tree's log and stump, a log's halves, a rock's broken
        /// version, and the debris anything throws when destroyed.
        /// </summary>
        private static List<Leftover> FindLeftovers(IEnumerable<GameObject> prefabs)
        {
            var found = new List<Leftover>();
            void Add(GameObject thing, GameObject owner, string role)
            {
                if (thing != null && owner != null) found.Add(new Leftover(thing.name, owner.name, role));
            }
            void Thrown(EffectList list, GameObject owner, string role, bool ragdolls)
            {
                if (list?.m_effectPrefabs == null) return;
                foreach (var data in list.m_effectPrefabs)
                {
                    var prefab = data?.m_prefab;
                    if (prefab == null) continue;
                    if (ragdolls && prefab.GetComponent<Ragdoll>() != null) Add(prefab, owner, "ragdoll");
                    else if (Ghost.IsDebris(prefab)) Add(prefab, owner, role);
                }
            }

            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;
                try
                {
                    var character = prefab.GetComponent<Character>();
                    if (character != null) Thrown(character.m_deathEffects, prefab, "remains", true);

                    var tree = prefab.GetComponent<TreeBase>();
                    if (tree != null)
                    {
                        Add(tree.m_logPrefab, prefab, "log");
                        Add(tree.m_stubPrefab, prefab, "stump");
                        Thrown(tree.m_destroyedEffect, prefab, "debris", false);
                    }

                    var log = prefab.GetComponent<TreeLog>();
                    if (log != null)
                    {
                        Add(log.m_subLogPrefab, prefab, "half");
                        Thrown(log.m_destroyedEffect, prefab, "debris", false);
                    }

                    var destructible = prefab.GetComponent<Destructible>();
                    if (destructible != null)
                    {
                        Add(destructible.m_spawnWhenDestroyed, prefab, "broken");
                        Thrown(destructible.m_destroyedEffect, prefab, "debris", false);
                    }

                    Thrown(prefab.GetComponent<WearNTear>()?.m_destroyedEffect, prefab, "debris", false);
                    Thrown(prefab.GetComponent<MineRock>()?.m_destroyedEffect, prefab, "debris", false);
                    Thrown(prefab.GetComponent<MineRock5>()?.m_destroyedEffect, prefab, "debris", false);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogDebug($"Scry could not read what {prefab.name} leaves behind: {ex.Message}");
                }
            }
            return found;
        }

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
                        Plugin.Log.LogDebug($"Scry skipped part of the interface: {ex.Message}");
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
            foreach (var field in EffectFields(owner.GetType()))
            {
                if (!(field.GetValue(owner) is EffectList list) || list.m_effectPrefabs == null) continue;

                var label = Naming.EffectListLabel(field.Name);
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
                }
            }
        }

        /// <summary>The EffectList fields on a type and its bases, remembered per type.</summary>
        internal static FieldInfo[] EffectFields(Type type)
        {
            if (EffectFieldsByType.TryGetValue(type, out var known)) return known;

            var found = new List<FieldInfo>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var field in t.GetFields(flags))
                {
                    if (field.FieldType == typeof(EffectList)) found.Add(field);
                }
            }

            known = found.ToArray();
            EffectFieldsByType[type] = known;
            return known;
        }

        /// <summary>The game's text for a name token, or nothing when it has none.</summary>
        public static string Localize(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";

            try
            {
                var text = Naming.Plain(Localization.instance != null ? Localization.instance.Localize(token) : token).Trim();
                if (string.IsNullOrEmpty(text) || text.StartsWith("[", StringComparison.Ordinal) || text.StartsWith("$", StringComparison.Ordinal)) return "";
                return text;
            }
            catch
            {
                return "";
            }
        }
    }
}
