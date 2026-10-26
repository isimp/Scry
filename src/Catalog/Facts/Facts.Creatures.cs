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

        /// <summary>Whether its stars are the game's two as a guess: a mod's creature no source in the world names.</summary>
        private bool _starsUnsure;

        /// <summary>What its stars were worked out from (<see cref="CreatureStars.Stamp"/>).</summary>
        private int _starsOf;

        /// <summary>How many stars the creature can have, for what the stage still lacks of it under the strict rules; none for a boss, which has no stars.</summary>
        public int Stars => Boss ? 0 : _stars;

        /// <summary>Whether the creature is a boss (<c>Character.m_boss</c>).</summary>
        public bool Boss { get; private set; }

        /// <summary>The gear the stage can dress the creature in, by prefab and name: what it carries that shows when worn (<see cref="PrefabGear.Shows"/>).</summary>
        public readonly List<(string Prefab, string Name)> Gear = new List<(string, string)>();

        /// <summary>
        /// The drop's flag that stars leave its amount alone, read by name: the game has it since
        /// 1.0.20 and sets it on every creature's trophy; null in a game before it.
        /// </summary>
        internal static FieldInfo AmountStaysField => TypeFields.Named(typeof(CharacterDrop.Drop), "m_levelMultiplierNotAmount");

        /// <summary>What stars do to a creature's drop (<see cref="DropWords.WithStars"/>).</summary>
        internal static StarDrop WithStars(CharacterDrop.Drop drop)
        {
            var field = AmountStaysField;
            var amountStays = field != null && TypeFields.Value(field, drop) is bool stays && stays;
            return DropWords.WithStars(drop.m_levelMultiplier, amountStays, drop.m_onePerPlayer, drop.m_chance);
        }

        private void Creature(GameObject prefab, Character character)
        {
            Add("Health", Numbers.Amount(character.m_health));
            Proofs["Health"] = EntryKeys.Did(Deed.Killed);
            Add("Faction", Groups.FactionName(character.m_faction.ToString()));
            Boss = character.m_boss;
            if (character.m_boss) Add("Boss", "yes");
            if (!string.IsNullOrEmpty(character.m_bossEvent) && Knowledge.BossOfEvent(character.m_bossEvent) == prefab)
            {
                Add("Boss fight", "music and weather while its health bar shows", EntryKeys.For(Kind.Raid, character.m_bossEvent));
            }

            if (!character.m_boss) StarsTable(prefab, character);

            Part("resistances", () => Rows.Add(new Row { Title = "Damage it takes", HitReveals = true, Cells = ResistWords.ForCreature(Cells(character.m_damageModifiers)) }));
            Part("weak spots", () => WeakSpots(character));
            Part("gear", () => GearShown(prefab));
            Part("attacks", () => Attacks(prefab));
            Part("what to bring", () => Bring(prefab, character));
            // Whether it can be tamed, fought or looted is worth telling when it cannot.
            var person = character is Player;
            if (!person && !Rows.Any(row => row.Title == CombatWords.AttacksTitle))
            {
                // One that hunts yet carries no attack Scry can read may still have one in code.
                if (prefab.GetComponent<MonsterAI>() != null) AddUnsure("Attacks", "none Scry can see", UnsureWords.NoAttackItem);
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
                var food = ai.OrNull()?.m_consumeItems?.Where(i => i != null).ToList();
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
                var row = new Row { Title = "Drops", Tally = Tally.Fell, TallyOf = prefab.name };
                // What only it drops first, then what a trader pays for, then the rest, each the least likely first.
                var here = new[] { prefab.name };
                var told = drops.m_drops.Where(d => d?.m_prefab != null).Select(d => (Drop: d, Notable: Notable(d.m_prefab.name, here)));
                foreach (var (drop, notable) in ContentOrder.LootFirst(told, d => d.Drop.m_chance, d => d.Notable.Only, d => d.Notable.Worth))
                {
                    var amount = DropWords.CreatureDrop(drop.m_amountMin, drop.m_amountMax, drop.m_onePerPlayer, drop.m_chance);
                    row.Items.Add(Marked(new Ingredient { Icon = Icon(drop.m_prefab), Name = ItemName(drop.m_prefab), Amount = amount, Prefab = drop.m_prefab.name }, notable));
                }
                // A boss's trophy gives its Forsaken power on its boss stone.
                foreach (var drop in drops.m_drops)
                {
                    var power = drop?.m_prefab != null ? Knowledge.PowerOf(drop.m_prefab.name).Power : null;
                    var effect = power != null && ObjectDB.instance != null ? ObjectDB.instance.GetStatusEffect(power.GetStableHashCode()) : null;
                    // What its fall opens: named once the power was met, as a line only naming it.
                    if (effect != null) AddListed("Forsaken power", EffectName(effect), EntryKeys.For(Kind.StatusEffect, power));
                }
                if (row.Items.Count > 0)
                {
                    Rows.Add(row);
                    _drops = true;
                    // A boss is not spawned with stars; any other creature's drops grow with them.
                    if (!character.m_boss && drops.m_drops.Any(d => d?.m_prefab != null && WithStars(d) != StarDrop.Same))
                    {
                        var same = drops.m_drops.Where(d => d?.m_prefab != null && WithStars(d) == StarDrop.Same).Select(d => ItemName(d.m_prefab)).Distinct().ToList();
                        Add("Drops with stars", DropWords.StarDrops(_stars, same));
                        Proofs["Drops with stars"] = EntryKeys.Did(Deed.StarredFell);
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
                var seen = new Row { Title = SeenWords.Title(kills), Unsure = UnsureWords.Seen, AllPlay = true };
                foreach (var drop in DropWatch.Seen.Of(prefab.name)) seen.Items.Add(Chip(drop.Item, SeenWords.Chip(drop, kills)));
                if (seen.Items.Count > 0) Rows.Add(seen);
                else
                {
                    AddUnsure(SeenWords.Title(kills), "nothing", UnsureWords.Seen);
                    AllPlay.Add(SeenWords.Title(kills));
                }
            }
            if (drops == null || !drops.m_drops.Any(d => d?.m_prefab != null)) Add("Drops", "nothing");
            Hooked(HookedRule.Drops);
            if (Knowledge.IsPlacedByWorld(prefab.name) || Knowledge.WhereLines(prefab.name).Count > 0) Hooked(HookedRule.Spawns);
        }

        /// <summary>
        /// What stars add (<see cref="CombatWords.StarLines"/>): its health once more for each,
        /// half as much again to each hit, its drops doubled, and a line of chances for each drop
        /// that only gets likelier; under the strict rules kept back by the cell.
        /// </summary>
        private void StarsTable(GameObject prefab, Character character)
        {
            // Nothing in the world gives it a level above one: said so, no table.
            if (_stars <= 0)
            {
                Add(CombatWords.StarsTitle, CombatWords.NeverStars);
                return;
            }
            var stars = new Row { Title = CombatWords.StarsTitle, Columns = CombatWords.StarColumns(_stars), TallyOf = prefab.name, Unsure = _starsUnsure ? UnsureWords.ModStars : null };
            var told = (prefab.GetComponent<CharacterDrop>().OrNull()?.m_drops ?? new List<CharacterDrop.Drop>()).Where(d => d?.m_prefab != null).ToList();
            var grow = told.Any(d => WithStars(d) == StarDrop.Grows);
            foreach (var line in CombatWords.StarLines(character.m_health, _stars, grow)) stars.Lines.Add((line, null));
            // Its health without stars shows when its health does; each starred cell, of its
            // health, its damage and its drops, once one with that many stars was seen to fall;
            // without stars what it hits and drops is the creature itself, nothing to find out.
            for (var line = 0; line < stars.Lines.Count; line++)
            {
                if (line == 0) stars.CellProofs[(line, 1)] = EntryKeys.Did(Deed.Killed);
                for (var star = 1; star <= _stars; star++) stars.CellProofs[(line, star + 1)] = EntryKeys.StarsFell(star);
            }
            // A drop that only gets likelier, such as every trophy, has a line of its chances,
            // which the strict rules tell as the character's own counts at each count of stars.
            foreach (var drop in told.Where(d => WithStars(d) == StarDrop.Likelier).OrderBy(d => d.m_chance))
            {
                var line = CombatWords.StarChance(ItemName(drop.m_prefab), drop.m_chance, _stars);
                if (line == null) continue;
                stars.StarItems[stars.Lines.Count] = drop.m_prefab.name;
                stars.Lines.Add((line, EntryKeys.For(Kind.Item, drop.m_prefab.name)));
            }
            if (stars.Lines.Count > 0) Rows.Add(stars);
        }

        /// <summary>
        /// Each attack it has, from the items it may carry (a creature fights with items of its
        /// own, as a person does): what a hit deals as it is rolled each hit, how, and from how near and far and how often
        /// its AI uses it (<c>m_aiAttackRange</c>, <c>m_aiAttackRangeMin</c>, <c>m_aiAttackInterval</c>),
        /// a line of its attacks table each, its name playing the attack on the stage.
        /// </summary>
        private void Attacks(GameObject prefab)
        {
            // Each attack with the projectile it fires and what it puts on you, each a link.
            var attacks = new List<(string[] Cells, string Item, GameObject Fires, StatusEffect OnHit, (string Type, float Amount)[] Damage)>();
            var rolls = prefab.GetComponent<Player>() == null;
            foreach (var item in Relations.CarriedItems(prefab))
            {
                var shared = item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
                var attack = shared?.m_attack;
                // What it wears, its armor and shield, is no attack of its.
                if (attack == null || !CombatWords.IsCreatureAttack(shared.m_itemType.ToString(), attack.m_attackAnimation)) continue;
                // The game names some attacks in its own shorthand ("slap", "LOG"), not by a name to show,
                // and some by the creature's own name; one of those that fires something goes by what it
                // fires ("fireballattack" is a troll's rock throw, a greydwarf's "Greydwarf" its stone throw).
                var coded = !string.IsNullOrEmpty(shared.m_name) && !shared.m_name.StartsWith("$", StringComparison.Ordinal);
                var fires = attack.m_attackProjectile;
                var name = coded ? Naming.Readable(shared.m_name) : ItemName(item);
                if (fires != null && (coded || string.Equals(name, AnyName(prefab, prefab.name), StringComparison.OrdinalIgnoreCase)))
                    name = CombatWords.FiredName(AnyName(fires, fires.name)) ?? name;
                if (attacks.Any(a => a.Cells[0] == name)) continue;
                // Rolled each hit as anything but a player rolls one (Character.GetRandomSkillFactor); a player rolls by its skill.
                var dealt = rolls ? CombatWords.CreatureDamage(DamageFigures(shared.m_damages)) ?? "" : Damages(shared.m_damages);
                var cells = CombatWords.AttackCells(name, dealt, attack.m_attackType.ToString(), shared.m_aiAttackRangeMin, shared.m_aiAttackRange, shared.m_aiAttackInterval,
                    fires != null ? AnyName(fires, fires.name) : null);
                attacks.Add((cells, item.name, fires, shared.m_attackStatusEffect, DamageFigures(shared.m_damages)));
            }
            var onHit = attacks.Any(a => a.OnHit != null);
            var table = new Row { Title = CombatWords.AttacksTitle, Columns = CombatWords.AttackColumnsFor(onHit), AttackCells = onHit ? new[] { 1, 3 } : new[] { 1 } };
            // A summon hits harder by its summoner's skill (SpawnAbility.Spawn), rolled as anything
            // but a player rolls a hit (Character.GetRandomSkillFactor): faint, as summoned at the character's level.
            var summoned = SkillReading.SummonedBy(prefab.name);
            var summoner = summoned != null ? YourAt(summoned.Value.Skill) : null;
            foreach (var (cells, item, fires, effect, damage) in attacks)
            {
                var line = table.Lines.Count;
                table.Lines.Add((onHit ? CombatWords.OnHitCells(cells, effect != null ? EffectName(effect) : null) : cells, EntryKeys.PlayAttack(item)));
                if (summoner != null)
                {
                    var strength = SkillMath.SummonStrength(summoner.Value.Level, summoned.Value.PerLevel);
                    table.Yours[line] = new List<(string[], string, string)> { YourLine(SkillWords.SummonedLine(SkillWords.SummonedAt(summoner.Value.At), damage, strength), summoner.Value) };
                }
                if (fires != null) table.CellLinks[(line, 2)] = fires.name;
                if (effect != null) table.CellLinks[(line, 3)] = EntryKeys.For(Kind.StatusEffect, effect.name);
            }
            if (table.Lines.Count > 0) Rows.Add(table);
        }

        /// <summary>
        /// What to bring against it, each a search: weapons dealing what it is weak to, gear
        /// resisting the elements its attacks deal, each with what shows it under the strict rules.
        /// </summary>
        private void Bring(GameObject prefab, Character character)
        {
            var weak = SearchFight.Taking(ResistWords.ForCreature(Cells(character.m_damageModifiers)), Tone.Weak);
            var row = new Row { Title = SearchFight.BringTitle };
            foreach (var (text, query, shownBy) in SearchFight.Bring(weak, AttacksOf(prefab)))
            {
                row.Items.Add(new Ingredient { Name = text, Amount = "", Prefab = EntryKeys.Search(query), ShownBy = shownBy });
            }
            if (row.Items.Count > 0) Rows.Add(row);
        }

        /// <summary>A creature's attacks, each by its item and the damage types it deals (<see cref="Entry.Attacks"/>): what it carries that is an attack, not what it wears.</summary>
        internal static (string Item, string[] Types)[] AttacksOf(GameObject prefab)
        {
            var attacks = new List<(string, string[])>();
            foreach (var item in Relations.CarriedItems(prefab))
            {
                var shared = item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
                if (shared?.m_attack == null || !CombatWords.IsCreatureAttack(shared.m_itemType.ToString(), shared.m_attack.m_attackAnimation)) continue;
                attacks.Add((item.name, SearchFight.Dealt(DamageFigures(shared.m_damages))));
            }
            return attacks.ToArray();
        }

        /// <summary>How it moves, sees and hears, what it fears, when it flees, and how long it takes to tame.</summary>
        private void Behaviour(GameObject prefab, Character character)
        {
            Add("Moves", CombatWords.Moves(character.m_flying, character.m_flySlowSpeed, character.m_flyFastSpeed, character.m_walkSpeed, character.m_runSpeed,
                character.m_canSwim, character.m_swimSpeed));
            // The player runs faster at its Run (Player.GetRunSpeedFactor).
            if (character is Player) YoursAt("Moves", Skills.SkillType.Run, level => SkillWords.Runs(character.m_runSpeed * SkillMath.RunSpeedShare(level)));

            var ai = prefab.GetComponent<BaseAI>();
            if (ai != null)
            {
                Add("Sees", CombatWords.Sight(ai.m_viewRange, ai.m_viewAngle));
                // How near it sees the character sneaking at its Sneak, from the dark to full light
                // (Player.UpdateStealth, read by BaseAI.CanSeeTarget).
                YoursAt("Sees", Skills.SkillType.Sneak, level => SkillWords.Sneaking(ai.m_viewRange * SkillMath.Stealth(level, 0f), ai.m_viewRange * SkillMath.Stealth(level, 1f)));
                Add("Hears", CombatWords.Hears(ai.m_hearRange));
                if (ai.m_afraidOfFire) Add("Fire", "afraid of it");
                else if (ai.m_avoidFire) Add("Fire", "keeps away from it");
                // AnimalAI.UpdateAI only ever flees from what it senses.
                if (ai is AnimalAI) Add("Fights", "never: it flees from what it senses");
                if (ai.m_passiveAggresive) Add("When enemies are passive", CombatWords.PassiveEnemies(flees: ai is AnimalAI));
                if (ai is MonsterAI monster)
                {
                    Add("Turns on you", CombatWords.Alerted(monster.m_alertRange));
                    // MonsterAI.UpdateAI takes the character's stealth the same way.
                    if (monster.m_alertRange > 0f && monster.m_alertRange < 9000f)
                    {
                        YoursAt("Turns on you", Skills.SkillType.Sneak, level => SkillWords.Sneaking(monster.m_alertRange * SkillMath.Stealth(level, 0f), monster.m_alertRange * SkillMath.Stealth(level, 1f)));
                    }
                    Add("Gives up chasing", CombatWords.Chase(monster.m_maxChaseDistance));
                    Add("Flees", CombatWords.Flees(monster.m_fleeIfLowHealth));
                    if (!monster.m_attackPlayerObjects) Add("Leaves alone", "what players build");
                }
            }

            var tame = prefab.GetComponent<Tameable>();
            if (tame != null)
            {
                Add("Tames in", Numbers.Duration(tame.m_tamingTime));
                // A summon's hits train its summoner's skill, by its share of a rise (Character.RaiseSkill).
                if (tame.m_levelUpOwnerSkill != Skills.SkillType.None && tame.m_levelUpFactor > 0f)
                {
                    var page = EntryKeys.For(Kind.Skill, tame.m_levelUpOwnerSkill.ToString());
                    Add("Trains", SkillWords.TrainsOwner(SkillName(tame.m_levelUpOwnerSkill), tame.m_levelUpFactor), EntryOf(page) != null ? page : null);
                }
                Add("Fed for", Numbers.Duration(tame.m_fedDuration));
                Proofs["Tames in"] = Proofs["Fed for"] = EntryKeys.Did(Deed.Tamed);
                // Riding hinges on the saddle, breeding on the young it brings: neither is told
                // before the character met it.
                Part("riding", () => Riding(tame, character), tame.m_saddleItem != null ? tame.m_saddleItem.gameObject.name : null);
                var breed = prefab.GetComponent<Procreation>();
                if (breed != null) Part("breeding", () => Breeding(breed), breed.m_offspring != null ? breed.m_offspring.name : null);
                Hooked(HookedRule.Taming);
            }

            var grow = prefab.GetComponent<Growup>();
            if (grow != null) Part("growing up", () => GrowingUp(grow), grow.m_grownPrefab != null ? grow.m_grownPrefab.name : null);
        }

        /// <summary>
        /// What its defeat opens, through the world key it sets (<c>Character.m_defeatSetGlobalKey</c>):
        /// raids that may then come or stop, what then spawns or stops spawning, and what traders
        /// then sell, a row for each, each thing a chip going to it.
        /// </summary>
        private void AfterItFalls(string key)
        {
            foreach (Unlock kind in Enum.GetValues(typeof(Unlock)))
            {
                var targets = Knowledge.Unlocks.Of(key, kind);
                if (targets.Count == 0) continue;
                // The row's title tells what the fall opens, so while none of it was met it is left out whole.
                var row = new Row { Title = UnlockWords.Title(kind, targets.Count), UnmetLeftOut = true };
                foreach (var target in targets)
                {
                    if (kind == Unlock.RaidStarts || kind == Unlock.RaidEnds)
                    {
                        var raid = EntryOf(target);
                        if (raid != null) row.Items.Add(EntryChip(raid));
                    }
                    else row.Items.Add(Chip(target, ""));
                }
                if (row.Items.Count > 0) Rows.Add(row);
            }
        }

        /// <summary>
        /// What a saddle lets it be ridden with, and its stamina while ridden (<c>Sadle</c>); its
        /// drain less and its running faster by the rider's Riding (<c>Sadle.UpdateRiding</c>,
        /// <c>Character.GetRunSpeedFactor</c>).
        /// </summary>
        private void Riding(Tameable tame, Character character)
        {
            if (tame.m_saddleItem != null) Add("Ridden with", ItemName(tame.m_saddleItem.gameObject), tame.m_saddleItem.gameObject.name);
            var saddle = tame.m_saddle;
            if (saddle == null) return;
            Add("Stamina when ridden", RideWords.Stamina(saddle.m_maxStamina, saddle.m_staminaRegen, saddle.m_staminaRegenHungry));
            Add("Riding drains", RideWords.Drains(saddle.m_runStaminaDrain, saddle.m_swimStaminaDrain));
            // The game shows none of these numbers: under the strict rules they wait for a ride.
            Proofs["Stamina when ridden"] = Proofs["Riding drains"] = EntryKeys.Did(Deed.Ridden);
            YoursAt("Riding drains", Skills.SkillType.Ride, level => RideWords.Drains(saddle.m_runStaminaDrain * SkillMath.MountDrainShare(level), saddle.m_swimStaminaDrain * SkillMath.MountDrainShare(level)));
            if (character.m_runSpeed > 0f)
            {
                Add("Runs ridden", SkillWords.Span(SkillWords.Speed(character.m_runSpeed), SkillWords.Speed(character.m_runSpeed * SkillMath.MountSpeedShare(SkillMath.Top))));
                Proofs["Runs ridden"] = EntryKeys.Did(Deed.Ridden);
                YoursAt("Runs ridden", Skills.SkillType.Ride, level => SkillWords.Speed(character.m_runSpeed * SkillMath.MountSpeedShare(level)));
            }
        }

        /// <summary>How a tame one breeds (<c>Procreation.Procreate</c>), and its young.</summary>
        private void Breeding(Procreation breed)
        {
            var partner = breed.m_seperatePartner != null ? AnyName(breed.m_seperatePartner, breed.m_seperatePartner.name) : null;
            Add("Breeds when", BreedWords.Needs(breed.m_partnerCheckRange, partner, breed.m_noPartnerOffspring != null));
            Add("Love", BreedWords.Love(breed.m_updateInterval, breed.m_pregnancyChance, breed.m_requiredLovePoints));
            Add("Pregnant for", Numbers.Duration(breed.m_pregnancyDuration));
            Add("Stops breeding", BreedWords.Crowd(breed.m_maxCreatures, breed.m_totalCheckRange));
            // The game shows none of these numbers: under the strict rules they wait for a birth seen.
            Proofs["Breeds when"] = Proofs["Love"] = Proofs["Pregnant for"] = Proofs["Stops breeding"] = EntryKeys.Did(Deed.Born);
            if (breed.m_offspring != null) Rows.Add(new Row { Title = BreedWords.Young(breed.m_minOffspringLevel), Items = { Chip(breed.m_offspring.name, "") } });
            if (breed.m_noPartnerOffspring != null) Rows.Add(new Row { Title = "With no partner near, has", Items = { Chip(breed.m_noPartnerOffspring.name, "") } });
        }

        /// <summary>What a young one grows up into, and when (<c>Growup</c>).</summary>
        private void GrowingUp(Growup grow)
        {
            Add("Grows up in", Numbers.Duration(grow.m_growTime));
            Proofs["Grows up in"] = EntryKeys.Did(Deed.GrewUp);
            var grown = Knowledge.GrownOf(grow);
            if (grown.Count == 0) return;
            var row = new Row { Title = BreedWords.GrowsInto(grown.Count > 1, grow.m_inheritTame) };
            if (grown.Count > 1)
            {
                var shares = BreedWords.Shares(grow.m_altGrownPrefabs.Where(a => a?.m_prefab != null).GroupBy(a => a.m_prefab).Select(g => g.Sum(a => a.m_weight)).ToArray());
                for (var i = 0; i < grown.Count; i++) row.Items.Add(Chip(grown[i].name, shares[i]));
            }
            else row.Items.Add(Chip(grown[0].name, ""));
            Rows.Add(row);
        }

        /// <summary>What it resists or is weak to, as a grid of every damage type, as a creature's, a piece's or a resource's are told.</summary>
        private void Resists(HitData.DamageModifiers mods, string title = "Damage it takes", bool hitReveals = false)
        {
            Rows.Add(new Row { Title = title, HitReveals = hitReveals, Cells = Cells(mods) });
        }

        /// <summary>Every damage type in the game's order with the share of it taken (<see cref="ResistWords"/>); the search reads the same cells.</summary>
        internal static List<ResistCell> Cells(HitData.DamageModifiers mods)
        {
            var degrees = new[] { mods.m_blunt, mods.m_slash, mods.m_pierce, mods.m_chop, mods.m_pickaxe, mods.m_fire, mods.m_frost, mods.m_lightning, mods.m_poison, mods.m_spirit };
            return degrees.Select((degree, i) => ResistWords.Cell(ResistWords.Types[i], DegreeOf(degree))).ToList();
        }

        /// <summary>The damage modifier fields of each type, found once per type.</summary>
        private static readonly Dictionary<Type, FieldInfo[]> ModifierFields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>
        /// Each damage modifier that is not plain, grouped by degree from very weak to immune, as
        /// the game orders them, not by the words' spelling.
        /// </summary>
        private static List<(string Words, string[] Types)> ByDegree(HitData.DamageModifiers mods)
        {
            var modifiers = new List<(string, Degree)>();
            foreach (var field in TypeFields.Matching(ModifierFields, typeof(HitData.DamageModifiers), f => f.IsPublic && f.FieldType == typeof(HitData.DamageModifier) && f.Name != "m_nonPlayer"))
            {
                var modifier = TypeFields.Value(field, mods) is HitData.DamageModifier read ? read : HitData.DamageModifier.Normal;
                modifiers.Add((Naming.FieldLabel(field.Name).ToLowerInvariant(), DegreeOf(modifier)));
            }
            return ResistWords.ByDegree(modifiers);
        }

        /// <summary>What the creature carries that shows when worn, for the stage's looks and what sight still lacks of them.</summary>
        private void GearShown(GameObject prefab)
        {
            foreach (var item in Relations.CarriedItems(prefab))
            {
                if (item != null && PrefabGear.Shows(item) && !Gear.Exists(g => g.Prefab == item.name)) Gear.Add((item.name, ItemName(item)));
            }
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
                var key = CombatWords.WeakSpot(spot.gameObject.name);
                if (Pairs.Any(p => p.Key == key)) continue;
                Add(key, CombatWords.Resistances(ByDegree(spot.m_damageModifiers)));
                Proofs[key] = EntryKeys.WeakSpot(spot.gameObject.name);
            }
        }
    }
}
