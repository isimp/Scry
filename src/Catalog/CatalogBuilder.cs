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
    internal static partial class CatalogBuilder
    {
        /// <summary>Leaving a world forgets what is kept here of it (<see cref="WorldCaches"/>).</summary>
        static CatalogBuilder() => WorldCaches.Register(nameof(CatalogBuilder), Forget);

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

            /// <summary>The effect lists that play it, by field name; a status effect's marked "se:", the interface's "ui:".</summary>
            public readonly HashSet<string> Fields = new HashSet<string>(StringComparer.Ordinal);
        }

        private static readonly Dictionary<Type, FieldInfo[]> EffectFieldsByType = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Reads the whole catalog at once, as the job would over frames.</summary>
        public static List<Entry> Build()
        {
            var job = new CatalogJob();
            while (!job.Advance(double.MaxValue)) { }
            if (job.Entries == null) throw new InvalidOperationException(job.Failure ?? "the catalog could not be read");
            return job.Entries;
        }

        /// <summary>
        /// One registered prefab: its components looked through once, for its own details, what
        /// it tells of where things come from, and its links.
        /// </summary>
        private static void ReadPrefab(string name, Found found, Dictionary<string, Found> effects, List<Component> components, List<Leftover> leftovers)
        {
            components.Clear();
            var started = CatalogTiming.Start();
            Describe(found, name, Origins.Prefabs.Of(name), effects, components);
            CatalogTiming.Add("describe", started);
            if (components.Count == 0) return;
            Knowledge.Read(found.Prefab, components);
            started = CatalogTiming.Start();
            Relations.Read(found.Prefab, components);
            CatalogTiming.Add("links", started);
            started = CatalogTiming.Start();
            FindLeftovers(found.Prefab, leftovers);
            CatalogTiming.Add("leftovers", started);
        }

        /// <summary>A helper's effect lists, read as its user's under the name of the field that led to it.</summary>
        private static void GatherHelper(Relations.Helper helper, Dictionary<string, Found> registered, Dictionary<string, Found> effects, List<Component> components)
        {
            var prefab = helper.Prefab;
            if (prefab == null || registered.ContainsKey(prefab.name) || effects.ContainsKey(prefab.name)) return;

            // Only prefabs are followed so far; a status effect's helpers would need its shown name.
            var user = helper.User;
            if (user.StartsWith(EntryKeys.StatusEffect, StringComparison.Ordinal)) return;

            components.Clear();
            if (!Guard.Each(Feature.EffectLists, "effects of what prefabs spawn", prefab.name, () => prefab.GetComponentsInChildren(true, components))) return;
            var origin = Origins.Prefabs.Of(user);
            foreach (var component in components)
            {
                if (component == null) continue;
                Guard.Each(Feature.EffectLists, "effects of what prefabs spawn", prefab.name, () =>
                {
                    Gather(component, user, origin, effects, user, user, helper.Part);
                });
            }
        }

        private static void MakeEntry(List<Entry> entries, string name, Found found, Dictionary<string, Found> effects, bool registeredOrigin)
        {
            var started = CatalogTiming.Start();
            Guard.Each(Feature.Catalog, "entries", name, () =>
            {
                entries.Add(ToEntry(name, found, effects, registeredOrigin));
            });
            CatalogTiming.Add("entries", started);
        }

        /// <summary>
        /// Lets go of what was read for the catalog of a world that was left: it points at that
        /// world's prefabs, effect lists and recipes, which would otherwise be kept in memory.
        /// </summary>
        public static void Forget()
        {
            _recipes = null;
        }

        /// <summary>
        /// Every raid the world has switched on (<c>RandEventSystem.m_events</c>), each an entry of
        /// its own though it is no prefab, named by the message the game shows when it starts; a
        /// boss's own event, which has none, by the boss it is the fight of.
        /// </summary>
        private static void Raids(List<Entry> entries)
        {
            var events = RandEventSystem.instance.OrNull()?.m_events;
            if (events == null) return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raid in events)
            {
                if (raid == null || string.IsNullOrEmpty(raid.m_name) || !raid.m_enabled || !seen.Add(raid.m_name)) continue;
                Guard.Each(Feature.Raids, "raid entries", raid.m_name, () =>
                {
                    var shown = Localize(raid.m_startMessage);
                    var boss = Knowledge.BossOfEvent(raid.m_name);
                    if (shown.Length == 0 && boss != null)
                    {
                        var bossName = Localize(boss.GetComponent<Character>().OrNull()?.m_name);
                        shown = RaidWords.Fighting(bossName.Length > 0 ? bossName : boss.name);
                    }
                    entries.Add(new Entry
                    {
                        Name = raid.m_name,
                        DisplayName = shown.Length > 0 ? shown : Naming.FieldLabel(raid.m_name),
                        Kind = Kind.Raid,
                        Origin = Origins.Raids.Of(raid.m_name),
                        Source = raid,
                        Components = new[] { raid.GetType().Name },
                        Biomes = RaidGrouping.Biomes(RaidGrouping.Role(raid.m_random, raid.m_standaloneInterval, boss != null), Knowledge.BiomeKeys(raid.m_biome), Knowledge.EveryBiomeKey),
                    });
                });
            }
        }

        /// <summary>What a biome's and a mod's entries are made of, as their details list it.</summary>
        private static readonly string[] BiomeComponents = { "BiomeEnvSetup" };
        private static readonly string[] ModComponents = { "BaseUnityPlugin" };

        /// <summary>
        /// Every biome the world's weather is set up for (<c>EnvMan.m_biomes</c>), an entry of its
        /// own though it is no prefab: its weathers with their weights and its music by the time
        /// of day, several setups of one biome taken together. Listed in the order players meet
        /// them; what lives and stands there comes from the other entries' biomes.
        /// </summary>
        private static void Biomes(List<Entry> entries)
        {
            var setups = EnvMan.instance != null ? EnvMan.instance.m_biomes : null;
            if (setups == null) return;
            var byName = new Dictionary<string, BiomeSource>(StringComparer.Ordinal);
            foreach (var setup in setups)
            {
                if (setup == null) continue;
                foreach (var key in Knowledge.BiomeKeys(setup.m_biome))
                {
                    if (!byName.TryGetValue(key, out var source)) byName[key] = source = new BiomeSource { Name = key };
                    if (setup.m_environments != null) foreach (var env in setup.m_environments) if (env != null) source.Weathers.Add((env.m_environment, env.m_weight));
                    if (source.Morning.Length == 0) source.Morning = setup.m_musicMorning ?? "";
                    if (source.Day.Length == 0) source.Day = setup.m_musicDay ?? "";
                    if (source.Evening.Length == 0) source.Evening = setup.m_musicEvening ?? "";
                    if (source.Night.Length == 0) source.Night = setup.m_musicNight ?? "";
                }
            }
            foreach (var source in byName.Values)
            {
                entries.Add(new Entry
                {
                    Name = source.Name,
                    DisplayName = Knowledge.BiomeName(source.Name),
                    Kind = Kind.Biome,
                    Origin = Origin.Vanilla,
                    Source = source,
                    Biomes = new[] { source.Name },
                    Group = "Biomes",
                    GroupOrder = 1,
                    GroupRank = BiomeWords.Rank(source.Name),
                    Components = BiomeComponents,
                });
            }
        }

        /// <summary>
        /// Every mod loaded (<c>Chainloader.PluginInfos</c>), an entry of its own though it is no
        /// prefab: its page tells what it adds and which of the game's rules it hooks into, what
        /// its package says of it (<see cref="ModFolders"/>), and how it ties to the other mods
        /// (<see cref="ModLinks"/>). It is listed by whether it adds to the game, only hooks into
        /// its drops or spawning, or neither.
        /// </summary>
        private static void Mods(List<Entry> entries)
        {
            var adding = new HashSet<string>(entries.Where(e => e.ModName.Length > 0).Select(e => e.ModName), StringComparer.Ordinal);
            var mods = new List<(Entry Entry, ModFacts Facts)>();
            foreach (var info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
            {
                var name = info?.Metadata?.Name;
                if (string.IsNullOrEmpty(name)) continue;
                Guard.Each(Feature.ModPages, "mod entries", name, () =>
                {
                    if (!Guard.Each(Feature.ModPages, "mods' folders", name, () => System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(info.Location) ?? ""), out var folder)) folder = "";
                    var source = new ModSource { Name = name, Version = info.Metadata.Version?.ToString() ?? "", Guid = info.Metadata.GUID ?? "", Folder = folder };
                    var facts = new ModFacts { Name = name, Guid = source.Guid };
                    if (info.Dependencies != null)
                    {
                        foreach (var dependency in info.Dependencies)
                        {
                            if (dependency == null) continue;
                            if ((dependency.Flags & BepInEx.BepInDependency.DependencyFlags.HardDependency) != 0) facts.Hard.Add(dependency.DependencyGUID);
                            else facts.Soft.Add(dependency.DependencyGUID);
                        }
                    }
                    if (info.Incompatibilities != null) foreach (var refused in info.Incompatibilities) if (refused != null) facts.Incompatible.Add(refused.IncompatibilityGUID);

                    var package = ModFolders.PackageFolder(info.Location);
                    var manifest = package != null ? ModFolders.Manifest(package) : null;
                    if (manifest != null)
                    {
                        facts.Package = System.IO.Path.GetFileName(package);
                        facts.PackageDeps.AddRange(manifest.Dependencies);
                        source.Description = manifest.Description;
                        source.Author = ModManifest.Author(facts.Package, manifest.Name) ?? "";
                        source.Website = manifest.Website;
                        source.ReadmePath = ModFolders.FileIn(package, "README.md");
                        source.IconPath = ModFolders.FileIn(package, "icon.png");
                    }

                    var group = Groups.Mod(adding.Contains(name), ModHooks.Rules(name).Count > 0);
                    var entry = new Entry
                    {
                        Name = name,
                        DisplayName = name,
                        Kind = Kind.Mod,
                        Origin = Origin.Mod,
                        ModName = name,
                        Source = source,
                        Group = group.Name,
                        GroupOrder = group.Order,
                        Components = ModComponents,
                    };
                    entries.Add(entry);
                    mods.Add((entry, facts));
                });
            }

            var links = ModLinks.Of(mods.Select(m => m.Facts));
            foreach (var (entry, facts) in mods)
            {
                if (links.TryGetValue(facts.Name, out var relations)) ((ModSource)entry.Source).Relations = relations;
            }
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
                Group = Kinds.GroupLabel(Kinds.GroupOf(found.Traits)),
                GroupOrder = (int)Kinds.GroupOf(found.Traits),
                PlayedIn = asEffect != null ? asEffect.Fields.ToArray() : Array.Empty<string>(),
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
                ModClue = Knowledge.ModClue(name),
            };

            // Something none of the other kinds is grouped by what it is there for.
            if (entry.Kind == Kind.Spawner)
            {
                var spawns = Groups.Spawner(found.Traits);
                entry.Group = spawns.Name;
                entry.GroupOrder = spawns.Order;
            }
            if (entry.Kind == Kind.Other)
            {
                var role = Groups.Role(found.Traits);
                entry.Group = role.Name;
                entry.GroupOrder = role.Order;
            }

            if (found.Prefab != null)
            {
                // Its looks are read off the prefab when it is first selected, not for every prefab here.
                var prefab = found.Prefab;
                entry.LooksFrom(() => Guard.Each(Feature.Looks, "looks", name, () =>
                {
                    var names = PrefabLooks.Of(prefab, out var look);
                    return (names, look);
                }, out var read) ? read : (Array.Empty<string>(), 0));
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
        /// a recipe without one, a piece's build station, and any station that turns something
        /// into it (a smelter, a fermenter), as the details tell it "Made in".
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

            foreach (var making in Knowledge.MadeOf(prefab.name))
            {
                if (uses.Exists(u => u.Name == making.Station)) continue;
                var station = GamePrefabs.Named(making.Station);
                var shown = station != null ? Localize(station.GetComponent<Piece>().OrNull()?.m_name) : "";
                uses.Add(new StationUse(making.Station, shown, 1));
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
        private static void Describe(Found found, string owner, Origin ownerOrigin, Dictionary<string, Found> effects, List<Component> components)
        {
            var traits = new PrefabTraits();
            found.Traits = traits;

            if (!Guard.Each(Feature.EffectLists, "effect lists", owner, () => found.Prefab.GetComponentsInChildren(true, components)))
            {
                components.Clear();
                return;
            }

            foreach (var component in components)
            {
                // A missing script shows up as a null component.
                if (component == null) continue;

                Guard.Each(Feature.EffectLists, "effect lists", owner, () =>
                {
                    Note(component, found, traits);
                    found.Components.Add(component.GetType().Name);
                    Gather(component, owner, ownerOrigin, effects, owner, owner);

                    // A creature's attacks are its own: their sounds and effects are played by it.
                    if (component is Humanoid)
                    {
                        foreach (var item in Relations.CarriedItems(found.Prefab))
                        {
                            var carried = item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
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
                });
            }
        }

        private static void Note(Component component, Found found, PrefabTraits traits)
        {
            if (component is Interactable) traits.IsUsable = true;
            switch (component)
            {
                case OfferingBowl _:
                    traits.IsAltar = true;
                    break;
                case global::Ragdoll _:
                    traits.HasRagdoll = true;
                    break;
                case Container _:
                    traits.HasContainer = true;
                    break;
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
                    // A piece a mod added to one of the game's own prefabs to make it buildable is
                    // switched off on the prefab (MoreVanillaBuildPrefabs does so; none of the
                    // game's own 25,802 pieces in its bundles is), and leaves the prefab what it was.
                    if (piece.enabled) traits.HasPiece = true;
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
                case TreeBase _:
                    traits.HasResource = true;
                    traits.IsTree = true;
                    break;
                case TreeLog _:
                    traits.HasResource = true;
                    traits.IsLog = true;
                    break;
                case MineRock _:
                case MineRock5 _:
                    traits.HasResource = true;
                    traits.IsMined = true;
                    break;
                case Pickable _:
                case PickableItem _:
                    traits.HasResource = true;
                    traits.IsPicked = true;
                    break;
                case Plant _:
                    traits.HasResource = true;
                    traits.HasPlant = true;
                    break;
                case Destructible breaks:
                    traits.HasDestructible = true;
                    if (Knowledge.MinedInside(breaks.m_spawnWhenDestroyed) != null) traits.BreaksIntoResource = true;
                    break;
                case DropOnDestroyed dropping:
                    if (dropping.m_dropWhenDestroyed?.m_drops != null && dropping.m_dropWhenDestroyed.m_drops.Count > 0) traits.HasDrops = true;
                    break;
                case SpawnArea _:
                    traits.HasSpawner = true;
                    traits.IsNest = true;
                    break;
                case CreatureSpawner _:
                    traits.HasSpawner = true;
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
            var token = item != null ? item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared?.m_name : null;
            if (string.IsNullOrEmpty(token) || !token.StartsWith("$", StringComparison.Ordinal)) return null;
            var shown = Localize(token);
            return shown.Length > 0 && !shown.StartsWith("$", StringComparison.Ordinal) ? shown : null;
        }

        /// <summary>What an item a creature carries is called: its shown name, or its prefab name when it has none.</summary>
        internal static string AttackName(GameObject item)
        {
            var shared = item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
            var shown = shared != null ? Localize(shared.m_name) : "";
            return shown.Length > 0 && !shown.StartsWith("$", StringComparison.Ordinal) ? shown : item.name;
        }

        /// <summary>
        /// What each prefab leaves behind, as the game leaves it: a creature's ragdoll and the
        /// remains its death throws, a tree's log and stump, a log's halves, a rock's broken
        /// version, and the debris anything throws when destroyed.
        /// </summary>
        private static void FindLeftovers(GameObject prefab, List<Leftover> found)
        {
            void Add(GameObject thing, GameObject owner, string role)
            {
                if (thing != null && owner != null) found.Add(new Leftover(thing.name, owner.name, role));
            }
            void Thrown(EffectList list, GameObject owner, string role, bool ragdolls)
            {
                foreach (var slot in EffectSlots.Of(list))
                {
                    if (!EffectSlots.Plays(slot)) continue;
                    var thing = slot.m_prefab;
                    if (ragdolls && thing.GetComponent<Ragdoll>() != null) Add(thing, owner, "ragdoll");
                    else if (PrefabShapes.IsDebris(thing)) Add(thing, owner, role);
                }
            }

            if (prefab == null) return;
            Guard.Each(Feature.WhatThingsLeaveWhenBroken, "what things leave behind", prefab.name, () =>
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

                Thrown(prefab.GetComponent<WearNTear>().OrNull()?.m_destroyedEffect, prefab, "debris", false);
                Thrown(prefab.GetComponent<MineRock>().OrNull()?.m_destroyedEffect, prefab, "debris", false);
                Thrown(prefab.GetComponent<MineRock5>().OrNull()?.m_destroyedEffect, prefab, "debris", false);
            });
        }

        /// <summary>
        /// Tokens translated so far in a reading of the catalog, from its start: the same names
        /// come up again and again, and the game keeps only its last hundred.
        /// </summary>
        private static readonly Dictionary<string, string> Localized = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Forgets the tokens translated, as a reading of the catalog starts.</summary>
        private static void ForgetTranslations() => Localized.Clear();

        /// <summary>The game's text for a name token, or nothing when it has none.</summary>
        public static string Localize(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            if (Localized.TryGetValue(token, out var known)) return known;

            if (!Guard.Each(Feature.GameNames, "names in the game's language", token, () => Naming.Plain(Localization.instance != null ? Localization.instance.Localize(token) : token).Trim(), out var text)
                || string.IsNullOrEmpty(text) || text.StartsWith("[", StringComparison.Ordinal) || text.StartsWith("$", StringComparison.Ordinal))
            {
                text = "";
            }
            if (Localized.Count > 20000) Localized.Clear();
            Localized[token] = text;
            return text;
        }
    }
}
