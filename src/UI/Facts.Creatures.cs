using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Scry
{
    /// <summary>A creature's facts: its health, attacks, resistances, drops, senses and taming.</summary>
    internal sealed partial class Facts
    {
        // ----- Creatures -----

        /// <summary>How many stars a creature can have, for what they add.</summary>
        private int _stars = 2;

        private void Creature(GameObject prefab, Character character)
        {
            Add("Health", Number(character.m_health));
            Add("Faction", Groups.FactionName(character.m_faction.ToString()));
            if (character.m_boss) Add("Boss", "yes");
            if (!string.IsNullOrEmpty(character.m_bossEvent) && Knowledge.BossOfEvent(character.m_bossEvent) == prefab)
            {
                Add("Its fight", "music and weather while its health bar shows", EntryKeys.For(Kind.Raid, character.m_bossEvent));
            }

            // What stars add: its health once more for each, and half as much again to each hit.
            if (!character.m_boss)
            {
                var health = CombatWords.StarHealth(character.m_health, _stars);
                if (health != null) Add("Health with stars", health);
                var hits = CombatWords.StarDamage(_stars);
                if (hits != null) Add("Damage with stars", hits);
            }

            Part("resistances", () => Resists(character.m_damageModifiers));
            Part("weak spots", () => WeakSpots(character));
            Part("attacks", () => Attacks(prefab));
            // Whether it can be tamed, fought or looted is worth telling when it cannot.
            var person = character is Player;
            if (!person && !Pairs.Any(pair => pair.Key.StartsWith("Attack: ", StringComparison.Ordinal)))
            {
                // One that hunts yet carries no attack Scry can read may still have one in code.
                if (prefab.GetComponent<MonsterAI>() != null) AddUnsure("Attacks", "none Scry can see", "It hunts, yet carries no attack item Scry can read; code of the game or a mod may still give it one");
                else Add("Attacks", "none");
            }
            Part("behaviour", () => Behaviour(prefab, character));
            if (character.m_boss) Part("summoning", () => SummonedBy(prefab));
            var key = character.m_defeatSetGlobalKey;
            if (!string.IsNullOrEmpty(key) && Knowledge.Unlocks.Any(key)) Part("after it falls", () => AfterItFalls(key));

            if (prefab.GetComponent<Tameable>() == null && !person) Add("Tameable", "no");
            if (prefab.GetComponent<Tameable>() != null)
            {
                Add("Tameable", "yes");
                var ai = prefab.GetComponent<MonsterAI>();
                var food = ai?.m_consumeItems?.Where(i => i != null).ToList();
                if (food != null && food.Count > 0)
                {
                    var eats = new Row { Title = "Eats" };
                    foreach (var item in food) eats.Items.Add(new Ingredient { Icon = Icon(item.gameObject), Name = ItemName(item.gameObject), Amount = "", Prefab = item.gameObject.name });
                    Rows.Add(eats);
                }
            }

            var drops = prefab.GetComponent<CharacterDrop>();
            if (drops != null && drops.m_drops.Count > 0)
            {
                var row = new Row { Title = "Drops" };
                foreach (var drop in drops.m_drops)
                {
                    if (drop?.m_prefab == null) continue;
                    var amount = DropWords.CreatureAmount(drop.m_amountMin, drop.m_amountMax, drop.m_onePerPlayer);
                    if (drop.m_chance < 1f) amount += $" ({Mathf.RoundToInt(drop.m_chance * 100f)}%)";
                    row.Items.Add(new Ingredient { Icon = Icon(drop.m_prefab), Name = ItemName(drop.m_prefab), Amount = amount, Prefab = drop.m_prefab.name });
                }
                // A boss's trophy gives its Forsaken power on its boss stone.
                foreach (var drop in drops.m_drops)
                {
                    var power = drop?.m_prefab != null ? Knowledge.PowerOf(drop.m_prefab.name).Power : null;
                    var effect = power != null && ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(power.GetStableHashCode()) : null;
                    if (effect != null) Add("Forsaken power", EffectName(effect), "se:" + power);
                }
                if (row.Items.Count > 0)
                {
                    Rows.Add(row);
                    _drops = true;
                    // A boss is not spawned with stars; any other creature's drops grow with them.
                    if (!character.m_boss && drops.m_drops.Any(d => d?.m_prefab != null && d.m_levelMultiplier && !d.m_onePerPlayer))
                    {
                        var same = drops.m_drops.Where(d => d?.m_prefab != null && !d.m_levelMultiplier).Select(d => ItemName(d.m_prefab)).Distinct().ToList();
                        Add("Drops with stars", DropWords.StarDrops(_stars, same));
                    }
                }
            }

            // Mods that hook into what creatures drop, or where they spawn, can change either
            // beyond what the prefab says (ModHooks); a person has neither.
            if (character is Player) return;

            // What it was seen to drop as you played, whichever mod put it there (DropWatch).
            var kills = DropWatch.Seen.Kills(prefab.name);
            if (kills > 0)
            {
                var seen = new Row { Title = SeenWords.Title(kills), Unsure = UnsureWords.Seen };
                foreach (var drop in DropWatch.Seen.Of(prefab.name)) seen.Items.Add(Chip(drop.Item, SeenWords.Chip(drop, kills)));
                if (seen.Items.Count > 0) Rows.Add(seen);
                else AddUnsure(SeenWords.Title(kills), "nothing", UnsureWords.Seen);
            }
            if (drops == null || !drops.m_drops.Any(d => d?.m_prefab != null)) Add("Drops", "nothing");
            Hooked(HookedRule.Drops);
            if (Knowledge.IsPlacedByWorld(prefab.name) || Knowledge.WhereLines(prefab.name).Count > 0) Hooked(HookedRule.Spawns);
        }

        /// <summary>
        /// Each attack it has, from the items it may carry (a creature fights with items of its
        /// own, as a person does): what a hit does, how, and from how near and far and how often
        /// its AI uses it (<c>m_aiAttackRange</c>, <c>m_aiAttackRangeMin</c>, <c>m_aiAttackInterval</c>).
        /// Each goes to its item.
        /// </summary>
        private void Attacks(GameObject prefab)
        {
            foreach (var item in Relations.CarriedItems(prefab))
            {
                var shared = item.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                var attack = shared?.m_attack;
                if (attack == null) continue;
                var name = ItemName(item);
                var damage = Damages(shared.m_damages);
                var key = "Attack: " + name;
                if (Pairs.Any(p => p.Key == key)) continue;
                Add(key, CombatWords.Attack(damage, attack.m_attackType.ToString(), shared.m_aiAttackRangeMin, shared.m_aiAttackRange, shared.m_aiAttackInterval));
                Links[key] = item.name;
            }
        }

        /// <summary>How it moves, sees and hears, what it fears, when it flees, and how long it takes to tame.</summary>
        private void Behaviour(GameObject prefab, Character character)
        {
            var moves = new List<string>();
            if (character.m_flying) moves.Add($"flies {Number(character.m_flySlowSpeed)}–{Number(character.m_flyFastSpeed)} m/s");
            else moves.Add($"walks {Number(character.m_walkSpeed)} m/s, runs {Number(character.m_runSpeed)} m/s");
            if (character.m_canSwim) moves.Add($"swims {Number(character.m_swimSpeed)} m/s");
            Add("Moves", string.Join(", ", moves));

            var ai = prefab.GetComponent<BaseAI>();
            if (ai != null)
            {
                Add("Sees", CombatWords.Sight(ai.m_viewRange, ai.m_viewAngle));
                if (ai.m_hearRange < 9000f) Add("Hears", $"{Number(ai.m_hearRange)} m");
                if (ai.m_afraidOfFire) Add("Fire", "afraid of it");
                else if (ai.m_avoidFire) Add("Fire", "keeps away from it");
                // AnimalAI.UpdateAI only ever flees from what it senses.
                if (ai is AnimalAI) Add("Fights", "never: it flees from what it senses");
                if (ai.m_passiveAggresive) Add("With Passive enemies", CombatWords.PassiveEnemies(flees: ai is AnimalAI));
                if (ai is MonsterAI monster)
                {
                    Add("Turns on you", CombatWords.Alerted(monster.m_alertRange));
                    Add("Gives up chasing", CombatWords.Chase(monster.m_maxChaseDistance));
                    if (monster.m_fleeIfLowHealth > 0f) Add("Flees", $"below {Mathf.RoundToInt(monster.m_fleeIfLowHealth * 100f)}% health, right after being hurt");
                    if (!monster.m_attackPlayerObjects) Add("Leaves alone", "what players build");
                }
            }

            var tame = prefab.GetComponent<Tameable>();
            if (tame != null)
            {
                Add("Takes to tame", Minutes(tame.m_tamingTime));
                Add("Stays fed", Minutes(tame.m_fedDuration));
                Part("riding", () => Riding(tame));
                var breed = prefab.GetComponent<Procreation>();
                if (breed != null) Part("breeding", () => Breeding(breed));
                Hooked(HookedRule.Taming);
            }

            var grow = prefab.GetComponent<Growup>();
            if (grow != null) Part("growing up", () => GrowingUp(grow));
        }

        /// <summary>
        /// What its defeat opens, through the world key it sets (<c>Character.m_defeatSetGlobalKey</c>):
        /// raids that may then come or stop, what then spawns or stops spawning, and what traders
        /// then sell, a row for each, each thing a chip going to it.
        /// </summary>
        private void AfterItFalls(string key)
        {
            var catalog = Session.Explorer?.Catalog;
            foreach (Unlock kind in Enum.GetValues(typeof(Unlock)))
            {
                var targets = Knowledge.Unlocks.Of(key, kind);
                if (targets.Count == 0) continue;
                var row = new Row { Title = UnlockWords.Title(kind, targets.Count) };
                foreach (var target in targets)
                {
                    if (kind == Unlock.RaidStarts || kind == Unlock.RaidEnds)
                    {
                        var raid = catalog?.FirstOrDefault(e => e.Key == target);
                        if (raid != null) row.Items.Add(EntryChip(raid));
                    }
                    else row.Items.Add(Chip(target, ""));
                }
                if (row.Items.Count > 0) Rows.Add(row);
            }
        }

        /// <summary>What a saddle lets it be ridden with, and its stamina while ridden (<c>Sadle</c>).</summary>
        private void Riding(Tameable tame)
        {
            if (tame.m_saddleItem != null) Add("Ridden with", ItemName(tame.m_saddleItem.gameObject), tame.m_saddleItem.gameObject.name);
            var saddle = tame.m_saddle;
            if (saddle == null) return;
            Add("Stamina when ridden", RideWords.Stamina(saddle.m_maxStamina, saddle.m_staminaRegen, saddle.m_staminaRegenHungry));
            Add("Riding drains", RideWords.Drains(saddle.m_runStaminaDrain, saddle.m_swimStaminaDrain));
        }

        /// <summary>How a tame one breeds (<c>Procreation.Procreate</c>), and its young.</summary>
        private void Breeding(Procreation breed)
        {
            var partner = breed.m_seperatePartner != null ? AnyName(breed.m_seperatePartner, breed.m_seperatePartner.name) : null;
            Add("Breeds when", BreedWords.Needs(breed.m_partnerCheckRange, partner, breed.m_noPartnerOffspring != null));
            Add("Love", BreedWords.Love(breed.m_updateInterval, breed.m_pregnancyChance, breed.m_requiredLovePoints));
            Add("Pregnant for", Naming.Duration(breed.m_pregnancyDuration));
            Add("Stops breeding", BreedWords.Crowd(breed.m_maxCreatures, breed.m_totalCheckRange));
            if (breed.m_offspring != null) Rows.Add(new Row { Title = "Has young, " + BreedWords.Stars(breed.m_minOffspringLevel), Items = { Chip(breed.m_offspring.name, "") } });
            if (breed.m_noPartnerOffspring != null) Rows.Add(new Row { Title = "With no partner near, has", Items = { Chip(breed.m_noPartnerOffspring.name, "") } });
        }

        /// <summary>What a young one grows up into, and when (<c>Growup</c>).</summary>
        private void GrowingUp(Growup grow)
        {
            Add("Grows up in", Naming.Duration(grow.m_growTime));
            var grown = Knowledge.GrownOf(grow);
            if (grown.Count == 0) return;
            var row = new Row { Title = (grown.Count > 1 ? "Grows into one of these" : "Grows into") + (grow.m_inheritTame ? ", staying tame" : "") };
            if (grown.Count > 1)
            {
                var shares = BreedWords.Shares(grow.m_altGrownPrefabs.Where(a => a?.m_prefab != null).GroupBy(a => a.m_prefab).Select(g => g.Sum(a => a.m_weight)).ToArray());
                for (var i = 0; i < grown.Count; i++) row.Items.Add(Chip(grown[i].name, shares[i] + "%"));
            }
            else row.Items.Add(Chip(grown[0].name, ""));
            Rows.Add(row);
        }

        /// <summary>What it resists or is weak to, as a grid of every damage type, as a creature's, a piece's or a resource's are told.</summary>
        private void Resists(HitData.DamageModifiers mods, string title = "Damage it takes")
        {
            Rows.Add(new Row { Title = title, Cells = Cells(mods) });
        }

        /// <summary>Every damage type in the game's order with the share of it taken (<see cref="ResistWords"/>).</summary>
        private static List<ResistCell> Cells(HitData.DamageModifiers mods)
        {
            var degrees = new[] { mods.m_blunt, mods.m_slash, mods.m_pierce, mods.m_chop, mods.m_pickaxe, mods.m_fire, mods.m_frost, mods.m_lightning, mods.m_poison, mods.m_spirit };
            return degrees.Select((degree, i) => ResistWords.Cell(ResistWords.Types[i], (Degree)(int)degree)).ToList();
        }

        /// <summary>
        /// Each damage modifier that is not plain, grouped by degree from very weak to immune, as
        /// the game orders them, not by the words' spelling.
        /// </summary>
        private static List<(string Words, string[] Types)> ByDegree(HitData.DamageModifiers mods)
        {
            var groups = new SortedDictionary<int, (string Words, List<string> Types)>();
            foreach (var field in typeof(HitData.DamageModifiers).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType != typeof(HitData.DamageModifier) || field.Name == "m_nonPlayer") continue;
                var modifier = (HitData.DamageModifier)field.GetValue(mods);
                if (modifier == HitData.DamageModifier.Normal) continue;

                var order = Array.IndexOf(Degrees, modifier);
                if (order < 0) order = Degrees.Length + (int)modifier;
                if (!groups.TryGetValue(order, out var group)) groups[order] = group = (ModifierWords(modifier), new List<string>());
                group.Types.Add(Naming.FieldLabel(field.Name).ToLowerInvariant());
            }
            return groups.Values.Select(g => (g.Words, g.Types.ToArray())).ToList();
        }

        /// <summary>
        /// Its weak spots: parts whose own resistances take the place of the body's for a hit
        /// that lands there (<c>Character.GetDamageModifiers</c>), a troll's head for one. Each
        /// part is told once, however many of it there are.
        /// </summary>
        private void WeakSpots(Character character)
        {
            if (character is Player) return;
            if (character.m_weakSpots == null || !character.m_weakSpots.Any(s => s != null))
            {
                Add("Weak spots", "none");
                return;
            }
            foreach (var spot in character.m_weakSpots)
            {
                if (spot == null) continue;
                var key = "Hit on the " + CombatWords.PartName(spot.gameObject.name);
                if (Pairs.Any(p => p.Key == key)) continue;
                Add(key, CombatWords.Resistances(ByDegree(spot.m_damageModifiers)));
            }
        }
    }
}
