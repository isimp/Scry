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
            Part("behaviour", () => Behaviour(prefab, character));
            if (character.m_boss) Part("summoning", () => SummonedBy(prefab));

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
            Add(ModHookWords.Label(HookedRule.Drops), ModHookWords.Note(HookedRule.Drops, ModHooks.Mods(HookedRule.Drops)));
            if (Knowledge.IsPlacedByWorld(prefab.name) || Knowledge.WhereLines(prefab.name).Count > 0)
            {
                Add(ModHookWords.Label(HookedRule.Spawns), ModHookWords.Note(HookedRule.Spawns, ModHooks.Mods(HookedRule.Spawns)));
            }
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
                if (ai.m_passiveAggresive) Add("Fights", "only once attacked");
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
            }
        }

        /// <summary>What it resists or is weak to, a row for each degree, as a creature's or a resource's are told.</summary>
        private void Resists(HitData.DamageModifiers mods)
        {
            foreach (var group in ByDegree(mods)) Add(group.Words, string.Join(", ", group.Types));
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
            if (character.m_weakSpots == null) return;
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
