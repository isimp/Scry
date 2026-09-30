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
        /// The reading of the catalog, a piece at a time, each piece followed by what is being read
        /// next. Nothing is shown until the end: an entry's kind, its users, its origin, what it
        /// leaves behind and its links are only known once everything is read. Each prefab is
        /// looked through once, and what it tells is read from that for its own details, where it
        /// comes from and its links; one that cannot be read loses only its own part.
        /// </summary>
        internal static IEnumerator<string> Steps(CatalogJob job)
        {
            CatalogTiming.Reset();
            var collections = GC.CollectionCount(0);

            var started = CatalogTiming.Start();
            try { Compatibility.Check(); }
            catch (Exception ex) { Faults.Tell("the startup check", ex); }
            CatalogTiming.Add("startup check", started);
            yield return "Reading the prefabs";

            started = CatalogTiming.Start();
            var scene = ZNetScene.instance;
            var registered = new Dictionary<string, Found>(StringComparer.Ordinal);
            var effects = new Dictionary<string, Found>(StringComparer.Ordinal);
            EffectLinks.Clear();
            Localized.Clear();
            foreach (var list in new[] { scene.m_prefabs, scene.m_nonNetViewPrefabs })
            {
                foreach (var prefab in list)
                {
                    if (prefab == null || registered.ContainsKey(prefab.name)) continue;
                    registered[prefab.name] = new Found { Prefab = prefab };
                }
            }
            var prefabs = registered.Values.Select(f => f.Prefab).ToList();
            CatalogTiming.Add("setup", started);
            yield return "Reading recipes";

            started = CatalogTiming.Start();
            Knowledge.Begin();
            Relations.Begin(registered.Keys);
            IndexRecipes();
            CatalogTiming.Add("setup", started);
            yield return $"Reading prefabs: 0 of {registered.Count:N0}";

            // A prefab at a time: some are large, and a few read together could take a frame's share.
            var components = new List<Component>();
            var leftovers = new List<Leftover>();
            var read = 0;
            var progress = "";
            foreach (var pair in registered)
            {
                job.Piece = pair.Key;
                ReadPrefab(pair.Key, pair.Value, effects, components, leftovers);
                if (++read % 16 == 1) progress = $"Reading prefabs: {read:N0} of {registered.Count:N0}";
                yield return progress;
            }
            components.Clear();

            // Where things live and which mod added them, before any entry is made.
            foreach (var step in Knowledge.Finish(prefabs)) yield return "Reading " + step;

            started = CatalogTiming.Start();
            var entries = new List<Entry>();
            var db = ObjectDB.instance;
            if (db != null)
            {
                foreach (var effect in db.m_StatusEffects)
                {
                    if (effect == null) continue;
                    try
                    {
                        var origin = Origins.StatusEffects.Of(effect.name);
                        var shown = Localize(effect.m_name);
                        Gather(effect, "status effect " + effect.name, origin, effects, "se:" + effect.name, shown.Length > 0 ? shown : effect.name);
                        Relations.ReadStatusEffect(effect);

                        entries.Add(new Entry
                        {
                            Name = effect.name,
                            DisplayName = shown,
                            Kind = Kind.StatusEffect,
                            Origin = origin,
                            Source = effect,
                            Icon = effect.m_icon,
                            Components = new[] { effect.GetType().Name },
                            ModName = Knowledge.ModName(effect.name),
                        });
                    }
                    catch (Exception ex)
                    {
                        Failed("status effect", effect.name, ex);
                    }
                }
            }
            CatalogTiming.Add("status effects", started);

            started = CatalogTiming.Start();
            Raids(entries);
            CatalogTiming.Add("raids", started);
            yield return "Reading the interface's sounds";

            started = CatalogTiming.Start();
            GatherInterface(effects);
            CatalogTiming.Add("interface", started);
            yield return "Reading effects";

            // Effects can point at further effects (a hit effect with an area of its own), so the
            // walk continues until nothing new turns up. Then what a prefab's helpers play is read
            // as the prefab's (the spawn effects of what a staff's projectile leaves to raise its
            // summon, the snow a shovel moves), and what that adds is walked in turn. A helper that
            // turned out to be an effect of its own is read only as that.
            var described = new HashSet<string>(StringComparer.Ordinal);
            var walked = 0;
            var helpersRead = false;
            bool grew;
            do
            {
                grew = false;
                foreach (var found in new List<Found>(effects.Values))
                {
                    var name = found.Prefab.name;
                    if (registered.ContainsKey(name) || !described.Add(name)) continue;
                    started = CatalogTiming.Start();
                    components.Clear();
                    Describe(found, name, Provenance.Combine(found.UserOrigins), effects, components);
                    CatalogTiming.Add("describe effects", started);
                    grew = true;
                    if (++walked % 4 == 0) yield return $"Reading effects: {walked:N0}";
                }

                if (!grew && !helpersRead)
                {
                    helpersRead = true;
                    var helpers = new List<Relations.Helper>();
                    Relations.TakeHelpers(helpers);
                    for (var i = 0; i < helpers.Count; i++)
                    {
                        started = CatalogTiming.Start();
                        GatherHelper(helpers[i], registered, effects, components);
                        CatalogTiming.Add("helpers", started);
                        if (i % 8 == 7) yield return $"Reading what prefabs spawn: {i + 1:N0} of {helpers.Count:N0}";
                    }
                    grew = true;
                }
            }
            while (grew);
            components.Clear();

            var made = 0;
            var total = registered.Count + effects.Count;
            foreach (var pair in registered)
            {
                MakeEntry(entries, pair.Key, pair.Value, effects, true);
                if (++made % 16 == 0) yield return $"Making entries: {made:N0} of about {total:N0}";
            }
            foreach (var pair in effects)
            {
                if (registered.ContainsKey(pair.Key)) continue;
                MakeEntry(entries, pair.Key, pair.Value, effects, false);
                if (++made % 16 == 0) yield return $"Making entries: {made:N0} of about {total:N0}";
            }
            yield return "Pairing leftovers";

            started = CatalogTiming.Start();
            try
            {
                Leftovers.Pair(entries, leftovers);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Scry could not pair what things leave behind, and leaves them unpaired: {ex}");
            }
            CatalogTiming.Add("leftovers", started);
            yield return "Linking entries";

            started = CatalogTiming.Start();
            LinkBook book = null;
            try
            {
                book = Relations.Finish(prefabs);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Scry could not link entries, and shows them without links: {ex}");
            }
            CatalogTiming.Add("links finished", started);
            yield return "Linking entries";

            started = CatalogTiming.Start();
            IEnumerator<int> linking = null;
            try
            {
                if (book != null) linking = book.ApplyInSteps(entries, 400).GetEnumerator();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Scry could not link entries, and shows them without links: {ex}");
            }
            CatalogTiming.Add("links applied", started);
            while (linking != null)
            {
                started = CatalogTiming.Start();
                var more = false;
                try
                {
                    more = linking.MoveNext();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"Scry could not link all entries, and shows some without links: {ex}");
                }
                CatalogTiming.Add("links applied", started);
                if (!more) break;
                yield return $"Linking entries: {linking.Current:N0}";
            }

            yield return "Grouping entries";
            IEnumerator<int> grouping = null;
            try
            {
                grouping = Grouping.Apply(entries, 400).GetEnumerator();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Scry could not group entries, and lists them ungrouped: {ex}");
            }
            while (grouping != null)
            {
                started = CatalogTiming.Start();
                var more = false;
                try
                {
                    more = grouping.MoveNext();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"Scry could not group all entries, and lists some ungrouped: {ex}");
                }
                CatalogTiming.Add("grouping", started);
                if (!more) break;
                yield return $"Grouping entries: {grouping.Current:N0}";
            }

            // Locations and rooms take their names from creatures' (a Fuling camp), so they come last.
            started = CatalogTiming.Start();
            try
            {
                PlaceEntries.Add(entries);
            }
            catch (Exception ex)
            {
                Faults.Tell("the locations and dungeon rooms", ex);
            }
            CatalogTiming.Add("locations", started);

            job.Entries = entries;
            Plugin.Note($"Scry's catalog, by part (ms): {CatalogTiming.Report()}; {GC.CollectionCount(0) - collections} garbage collections meanwhile.");
            Faults.TellSkipped();
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
            if (user.StartsWith("se:", StringComparison.Ordinal)) return;

            components.Clear();
            try
            {
                prefab.GetComponentsInChildren(true, components);
            }
            catch (Exception ex)
            {
                Faults.Skip("effects of what prefabs spawn", prefab.name, ex);
                return;
            }
            var origin = Origins.Prefabs.Of(user);
            foreach (var component in components)
            {
                if (component == null) continue;
                try
                {
                    Gather(component, user, origin, effects, user, user, helper.Part);
                }
                catch (Exception ex)
                {
                    Faults.Skip("effects of what prefabs spawn", prefab.name, ex);
                }
            }
        }

        private static void MakeEntry(List<Entry> entries, string name, Found found, Dictionary<string, Found> effects, bool registeredOrigin)
        {
            var started = CatalogTiming.Start();
            try
            {
                entries.Add(ToEntry(name, found, effects, registeredOrigin));
            }
            catch (Exception ex)
            {
                Failed("entry", name, ex);
            }
            CatalogTiming.Add("entries", started);
        }

        private static readonly HashSet<string> FailedKinds = new HashSet<string>();

        /// <summary>
        /// Lets go of what was read for the catalog of a world that was left: it points at that
        /// world's prefabs, effect lists and recipes, which would otherwise be kept in memory.
        /// </summary>
        public static void Forget()
        {
            _recipes = null;
            FailedKinds.Clear();
        }

        /// <summary>Something left out of the catalog, told once for each kind of failure.</summary>
        private static void Failed(string what, string name, Exception ex)
        {
            // An entry left out for a game change is told as that; anything else once for its kind.
            if (Trouble.IsGameChange(ex))
            {
                Faults.Skip(what + " entries", name, ex);
                return;
            }
            if (FailedKinds.Add(what + "|" + ex.GetType().Name + "|" + ex.Message))
            {
                Plugin.Log.LogWarning($"Scry leaves the {what} {name} out of its catalog (said once for this kind of failure): {ex}");
            }
        }

        /// <summary>
        /// Every raid the world has switched on (<c>RandEventSystem.m_events</c>), each an entry of
        /// its own though it is no prefab, named by the message the game shows when it starts.
        /// </summary>
        private static void Raids(List<Entry> entries)
        {
            var events = RandEventSystem.instance?.m_events;
            if (events == null) return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raid in events)
            {
                if (raid == null || string.IsNullOrEmpty(raid.m_name) || !raid.m_enabled || !seen.Add(raid.m_name)) continue;
                try
                {
                    var shown = Localize(raid.m_startMessage);
                    entries.Add(new Entry
                    {
                        Name = raid.m_name,
                        DisplayName = shown.Length > 0 ? shown : Naming.FieldLabel(raid.m_name),
                        Kind = Kind.Raid,
                        Origin = Origins.Raids.Of(raid.m_name),
                        Source = raid,
                        Components = new[] { raid.GetType().Name },
                        Biomes = Knowledge.BiomeKeys(raid.m_biome),
                    });
                }
                catch (Exception ex)
                {
                    Failed("raid", raid.m_name, ex);
                }
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
                PlayedIn = asEffect != null ? asEffect.Fields.ToArray() : new string[0],
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

            // Something none of the other kinds is grouped by what it is there for.
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
                entry.LooksFrom(() =>
                {
                    try
                    {
                        var names = Variants.Of(prefab, out var look);
                        return (names, look);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning($"Scry could not work out the looks of {name}, and offers none: {ex.Message}");
                        throw;
                    }
                });
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
                var station = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(making.Station) : null;
                var shown = station != null ? Localize(station.GetComponent<Piece>()?.m_name) : "";
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

            try
            {
                found.Prefab.GetComponentsInChildren(true, components);
            }
            catch (Exception ex)
            {
                components.Clear();
                Faults.Skip("effect lists", owner, ex);
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
                    Faults.Skip("effect lists", owner, ex);
                }
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
        private static void FindLeftovers(GameObject prefab, List<Leftover> found)
        {
            void Add(GameObject thing, GameObject owner, string role)
            {
                if (thing != null && owner != null) found.Add(new Leftover(thing.name, owner.name, role));
            }
            void Thrown(EffectList list, GameObject owner, string role, bool ragdolls)
            {
                if (list?.m_effectPrefabs == null) return;
                foreach (var data in list.m_effectPrefabs)
                {
                    var thing = data?.m_prefab;
                    if (thing == null) continue;
                    if (ragdolls && thing.GetComponent<Ragdoll>() != null) Add(thing, owner, "ragdoll");
                    else if (Ghost.IsDebris(thing)) Add(thing, owner, role);
                }
            }

            if (prefab == null) return;
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
                Faults.Skip("what things leave behind", prefab.name, ex);
            }
        }

        /// <summary>
        /// Tokens translated so far, kept from one reading of the catalog to the next: the same
        /// names come up again and again, and the game keeps only its last hundred.
        /// </summary>
        private static readonly Dictionary<string, string> Localized = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>The game's text for a name token, or nothing when it has none.</summary>
        public static string Localize(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            if (Localized.TryGetValue(token, out var known)) return known;

            string text;
            try
            {
                text = Naming.Plain(Localization.instance != null ? Localization.instance.Localize(token) : token).Trim();
                if (string.IsNullOrEmpty(text) || text.StartsWith("[", StringComparison.Ordinal) || text.StartsWith("$", StringComparison.Ordinal)) text = "";
            }
            catch
            {
                text = "";
            }
            if (Localized.Count > 20000) Localized.Clear();
            Localized[token] = text;
            return text;
        }
    }
}
