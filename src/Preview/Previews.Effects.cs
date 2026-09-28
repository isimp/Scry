using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>Effect lists played on the stage and in the world, with what the game does alongside: ragdolls, breaking, the animator's own switches; a prefab's lists, status effects and projectiles.</summary>
    internal static partial class Previews
    {
        /// <summary>Plays an effect where you are looking, or on you.</summary>
        public static void PlayEffect(Entry entry, bool onYou)
        {
            if (!(entry?.Source is GameObject prefab)) return;
            var player = Player.m_localPlayer;
            if (player == null) return;

            GameObject copy;
            if (onYou)
            {
                copy = Ghost.Make(prefab, player.transform, player.transform.position, player.transform.rotation);
            }
            else
            {
                Aim();
                copy = Ghost.Make(prefab, null, _spot, _facing);
            }
            Remember(copy, EffectSeconds);
            Started((onYou ? "on you:" : "there:") + prefab.name, new[] { copy });
        }

        /// <summary>
        /// Plays every prefab of a list at a point in the world. Muted, it is only seen: while the
        /// stage shows the same, its sound is heard from there once rather than twice.
        /// </summary>
        public static List<GameObject> PlayList(EffectList list, Vector3 position, Quaternion rotation, bool muted = false, float size = 1f)
        {
            var made = new List<GameObject>();
            if (list?.m_effectPrefabs == null) return made;
            foreach (var data in list.m_effectPrefabs)
            {
                if (data == null || !data.m_enabled || data.m_prefab == null) continue;
                GameObject copy;
                if (Ghost.IsDebris(data.m_prefab))
                {
                    if (!Falling.Ready(Stage.Layer)) continue;
                    copy = Falling.Debris(data.m_prefab, null, position, rotation, -1, Stage.Layer);
                }
                else if (Ghost.IsWholeModel(data.m_prefab)) continue;
                else
                {
                    copy = Ghost.Make(data.m_prefab, null, position, rotation);
                    Ghost.Magnify(copy, size);
                }

                if (copy == null) continue;
                if (muted) foreach (var source in copy.GetComponentsInChildren<AudioSource>(true)) source.mute = true;
                Remember(copy, EffectSeconds);
                made.Add(copy);
            }
            return made;
        }

        /// <summary>
        /// Whether the selection stands in the world too. Its sounds are then heard from there,
        /// where they are, and the stage's copies are only seen, so nothing sounds twice.
        /// </summary>
        public static bool WorldHeard => _world != null;

        /// <summary>
        /// Plays a list a sound or effect is part of, whole: an effect's on the stage around it,
        /// restarted to play along, and a sound's where you are looking.
        /// </summary>
        public static void PlayWhole(Entry entry, EffectList list)
        {
            if (entry == null || list == null) return;
            Stop(list);
            var things = new List<GameObject>();
            if (entry.Kind == Kind.Effect && Stage.IsStaged(entry))
            {
                Replay();
                foreach (var made in Stage.PlayList(list, null, entry.Name)) things.Add(made.Item2);
            }
            else
            {
                Aim();
                things.AddRange(PlayList(list, _spot + Vector3.up * 0.5f, _facing));
            }
            Started(list, things);
        }

        /// <summary>Flies a projectile from in front of you towards where you are looking.</summary>
        public static void Fire(Entry entry)
        {
            if (!(entry?.Source is GameObject prefab)) return;
            var player = Player.m_localPlayer;
            if (player == null) return;

            Aim();
            var start = (player.m_eye != null ? player.m_eye.position : player.transform.position + Vector3.up * 1.6f)
                        + player.transform.forward * 0.8f;
            var direction = _spot - start;
            if (direction.sqrMagnitude < 0.01f) direction = player.transform.forward;
            direction.Normalize();

            var copy = Ghost.Make(prefab, null, start, Quaternion.LookRotation(direction));
            if (copy == null) return;

            var projectile = prefab.GetComponentInChildren<Projectile>(true);
            var flight = copy.AddComponent<Flight>();
            flight.Velocity = direction * ProjectileSpeed;
            flight.Gravity = projectile != null ? projectile.m_gravity : 0f;
            flight.Lifetime = projectile != null && projectile.m_ttl > 0f ? Mathf.Min(projectile.m_ttl, 12f) : 4f;
            flight.Burst = projectile?.m_hitEffects;
            Remember(copy, flight.Lifetime + 1f);
        }

        /// <summary>
        /// Shows a status effect's start visuals and sounds on you. Only the look: the effect itself
        /// is never applied, so nothing about your character changes.
        /// </summary>
        public static void ShowStatus(Entry entry)
        {
            StopStatus(false);
            if (!(entry?.Source is StatusEffect effect)) return;
            if (Player.m_localPlayer == null) return;

            _status = effect;
            StatusVisuals.AddRange(OnYou(effect.m_startEffects));
        }

        /// <summary>Takes a status effect's visuals off you, playing its stop effects if asked.</summary>
        public static void StopStatus(bool playStop)
        {
            foreach (var visual in StatusVisuals) if (visual != null) Object.Destroy(visual);
            StatusVisuals.Clear();

            if (playStop && _status != null && Player.m_localPlayer != null)
            {
                foreach (var copy in OnYou(_status.m_stopEffects)) Remember(copy, EffectSeconds);
            }
            _status = null;
        }

        /// <summary>
        /// Every effect list a status effect carries that has something in it, by a plain name:
        /// its start and stop, and whatever else its kind adds, such as a tick or a break.
        /// </summary>
        public static List<KeyValuePair<string, EffectList>> StatusLists(StatusEffect effect)
        {
            var lists = new List<KeyValuePair<string, EffectList>>();
            if (effect == null) return lists;

            foreach (var field in CatalogBuilder.EffectFields(effect.GetType()))
            {
                if (!(field.GetValue(effect) is EffectList list) || !HasAny(list)) continue;
                lists.Add(new KeyValuePair<string, EffectList>(Naming.EffectListLabel(field.Name), list));
            }
            return lists;
        }

        /// <summary>The attack each of a creature's attack lists belongs to, for the swing that goes with it.</summary>
        private static readonly Dictionary<EffectList, Attack> AttackOf = new Dictionary<EffectList, Attack>();

        /// <summary>The prefabs an effect list plays, each once.</summary>
        private static IEnumerable<string> Members(EffectList list) =>
            (list?.m_effectPrefabs ?? new EffectList.EffectData[0]).Where(d => d?.m_prefab != null).Select(d => d.m_prefab.name).Distinct();

        /// <summary>A weapon's own lists that play with a swing; its block, equip and the like do not.</summary>
        private static readonly HashSet<string> SwingLists = new HashSet<string>
        {
            "m_hitEffect", "m_hitTerrainEffect", "m_startEffect", "m_holdStartEffect", "m_triggerEffect", "m_trailStartEffect",
        };

        /// <summary>
        /// Every effect list anywhere on a prefab, as <see cref="PrefabLists(GameObject)"/>, but of
        /// the items a creature carries only those it has now: the weapon in its hand, its shield
        /// and armour. A slap is not in the hand that holds a log.
        /// </summary>
        public static List<KeyValuePair<string, EffectList>> PrefabLists(GameObject prefab, ICollection<GameObject> carried)
        {
            _carriedOnly = carried;
            try
            {
                return PrefabLists(prefab);
            }
            finally
            {
                _carriedOnly = null;
            }
        }

        private static ICollection<GameObject> _carriedOnly;

        /// <summary>What a creature has on now, in the look shown: what it holds and wears.</summary>
        public static List<GameObject> CarriedNow(Entry entry)
        {
            var prefab = entry?.Source as GameObject;
            var modifiers = _explorer?.Modifiers;
            if (prefab == null || modifiers == null || !modifiers.LookAvailable || !Variants.IsGear(prefab)) return null;

            // It carries its weapons whether they are drawn or not, and uses any of them, so what
            // it has is all it carries in the look, even while it is shown without gear. Asked on
            // every event the panel draws, so kept while the prefab and look stay the same.
            if (ReferenceEquals(prefab, _carriedPrefab) && modifiers.Look == _carriedLook && _carried != null) return _carried;
            _carried = Gear.Inventory(prefab, modifiers.Look);
            _carriedPrefab = prefab;
            _carriedLook = modifiers.Look;
            return _carried;
        }

        private static GameObject _carriedPrefab;
        private static int _carriedLook;
        private static List<GameObject> _carried;

        /// <summary>
        /// Every effect list anywhere on a prefab that has something in it, each once, named after
        /// the part it belongs to: a creature's hits and death, a piece's placing and breaking, an
        /// item's attacks.
        /// </summary>
        public static List<KeyValuePair<string, EffectList>> PrefabLists(GameObject prefab)
        {
            var lists = new List<KeyValuePair<string, EffectList>>();
            if (prefab == null) return lists;
            var seen = new HashSet<EffectList>();
            var found = new List<KeyValuePair<string, KeyValuePair<string, EffectList>>>();

            // Named by what they are for ("Death", "Hit"); the part they belong to is only added
            // where two would read the same. An item's attacks always say which attack.
            void Collect(object owner, string part, bool alwaysSayPart)
            {
                foreach (var on in CatalogBuilder.ListsOn(owner))
                {
                    var list = on.List;
                    if (!HasAny(list) || !seen.Add(list)) continue;
                    var label = on.Label;
                    if (alwaysSayPart) label = part + ": " + label.ToLowerInvariant();
                    found.Add(new KeyValuePair<string, KeyValuePair<string, EffectList>>(part, new KeyValuePair<string, EffectList>(label, list)));
                }
            }

            // An item's own lists swing its attack, on the person trying it on.
            var own = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            if (own != null)
            {
                foreach (var attack in new[] { own.m_attack, own.m_secondaryAttack })
                {
                    if (attack == null) continue;
                    foreach (var field in CatalogBuilder.EffectFields(typeof(Attack)))
                    {
                        if (field.GetValue(attack) is EffectList list) AttackOf[list] = attack;
                    }
                }
                if (own.m_attack != null)
                {
                    foreach (var field in CatalogBuilder.EffectFields(typeof(ItemDrop.ItemData.SharedData)))
                    {
                        if (field.GetValue(own) is EffectList list && SwingLists.Contains(field.Name)) AttackOf[list] = own.m_attack;
                    }
                }
            }

            // A creature's attacks play with its clips (AttackOfClip), not as lists of their own;
            // its weapons' other lists (a block) are one chip per list that plays alike.
            var others = new List<(string Label, string Owner, EffectList List)>();
            foreach (var item in Relations.CarriedItems(prefab))
            {
                if (_carriedOnly != null && !_carriedOnly.Contains(item)) continue;
                var carried = item.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                if (carried == null) continue;
                var shown = CatalogBuilder.GameName(item);
                foreach (var field in CatalogBuilder.EffectFields(typeof(ItemDrop.ItemData.SharedData)))
                {
                    if (SwingLists.Contains(field.Name) || !(field.GetValue(carried) is EffectList list) || !HasAny(list)) continue;
                    others.Add((Naming.EffectListLabel(field.Name), shown ?? WeaponChoices.Readable(item.name, prefab.name), list));
                }
            }
            foreach (var group in others.GroupBy(o => o.Label + "|" + string.Join(",", Members(o.List).OrderBy(m => m))))
            {
                var first = group.First();
                if (!seen.Add(first.List)) continue;
                var clash = others.Any(o => o.Label == first.Label && !group.Contains(o));
                var label = clash ? first.Owner + ": " + first.Label.ToLowerInvariant() : first.Label;
                found.Add(new KeyValuePair<string, KeyValuePair<string, EffectList>>(label, new KeyValuePair<string, EffectList>(label, first.List)));
            }

            foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                try
                {
                    Collect(component, component.GetType().Name, false);
                    var shared = (component as ItemDrop)?.m_itemData?.m_shared;
                    if (shared == null) continue;
                    Collect(shared, "Item", false);
                    if (shared.m_attack != null) Collect(shared.m_attack, "Attack", true);
                    if (shared.m_secondaryAttack != null) Collect(shared.m_secondaryAttack, "Second attack", true);
                }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogDebug($"Scry could not read the effects on {prefab.name}: {ex.Message}");
                }
            }

            var counts = new Dictionary<string, int>();
            foreach (var item in found) counts[item.Value.Key] = counts.TryGetValue(item.Value.Key, out var n) ? n + 1 : 1;
            foreach (var item in found)
            {
                var label = counts[item.Value.Key] > 1 ? $"{item.Value.Key} ({Naming.FieldLabel(item.Key).ToLowerInvariant()})" : item.Value.Key;
                lists.Add(new KeyValuePair<string, EffectList>(label, item.Value.Value));
            }
            return lists;
        }

        /// <summary>
        /// Plays an effect list on one copy: on the stage when it is the stage copy, heard as if
        /// beside you, otherwise where the copy stands in the world. At a part of it when given.
        /// </summary>
        public static List<GameObject> PlayOnCopy(GameObject copy, EffectList list, Transform at)
        {
            if (copy == null || list == null) return new List<GameObject>();
            if (copy == Stage.Subject) return Stage.PlayList(list, at).ConvertAll(m => m.Item2);
            return PlayList(list, at != null ? at.position : copy.transform.position + Vector3.up * 0.5f, copy.transform.rotation, size: SizeOf(copy));
        }

        /// <summary>How many times its size the selection is shown, on the stage or in the world; what it plays is shown so too.</summary>
        public static float SizeOf(GameObject copy)
        {
            return (copy == Stage.Subject || copy == _world) && _explorer != null ? _explorer.Modifiers.Scale : 1f;
        }

        /// <summary>
        /// Plays an effect list, with what goes with it: a creature that leaves a ragdoll falls as
        /// one, a piece that breaks into parts breaks, and otherwise a creature that has an
        /// animation for it plays that.
        /// </summary>
        public static void PlayEffectList(string label, EffectList list)
        {
            var prefab = _entry?.Source as GameObject;
            var things = new List<GameObject>();
            Stop(list);
            _startedClip = null;
            var heard = $"{_entry?.Name}'s \"{label}\"";
            Listen.Start(heard, 3f);
            _heard = heard;

            // What the game does with the list besides playing it (ChipPlan). The clips it could
            // go with are looked for only when nothing else comes first, since finding them may
            // have to wait for the animator to be watched.
            var character = prefab != null ? prefab.GetComponent<Character>() : null;
            var ragdoll = _entry != null && _entry.Kind == Kind.Creature ? Falling.RagdollIn(list) : null;
            var facts = new ChipFacts(RoleOf(prefab, list), AnimatorParameters())
            {
                Flying = character != null && character.m_flying,
                HasRagdoll = ragdoll != null,
                IsDestroyed = Falling.IsDestroyedList(prefab, list),
            };
            var plan = ChipPlan.For(facts);
            AnimationClip swing = null, own = null;
            if (plan.Step == ChipStep.List)
            {
                swing = AttackOf.TryGetValue(list, out var attack) ? AttackClipOf(attack) : null;
                own = ClipsOfList(list).Find(c => c.How.Length == 0).Clip;
                facts.HasAttackClip = swing != null;
                facts.HasOwnClip = own != null;
                plan = ChipPlan.For(facts);
            }
            if (facts.Role != ListRole.Other && plan.Step != ChipStep.Trigger && plan.Step != ChipStep.Switch && plan.Step != ChipStep.ShakeTrunk && plan.Step != ChipStep.Ragdoll)
            {
                var animator = ClipPlayer.AnimatorOf(Stage.Subject);
                Listen.Note(heard, $"the animator has nothing the game sets for {facts.Role.ToString().ToLowerInvariant()}, so the game animates nothing here either; its triggers are {(animator != null ? Triggers(animator) : "none")}");
            }

            switch (plan.Step)
            {
                case ChipStep.Ragdoll:
                    var modifiers = _explorer?.Modifiers;
                    var level = modifiers != null ? modifiers.Level : 1;
                    var gear = modifiers != null && modifiers.LookAvailable ? Variants.GearOf(prefab, modifiers.Look) : new List<GameObject>();
                    things.Add(Stage.Fall(ragdoll, prefab, level, gear));
                    things.Add(FallInWorld(ragdoll, prefab, level, gear));
                    break;
                case ChipStep.Destroy:
                    Tell(prefab, list);
                    things.Add(Stage.Destroy(prefab, list));
                    things.Add(DestroyInWorld(prefab, list));
                    break;
                case ChipStep.ShakeTrunk:
                    ShakeTrunk(prefab);
                    break;
                case ChipStep.Trigger:
                    Trigger(plan.Parameter);
                    break;
                case ChipStep.Switch:
                    Switch(plan.Parameter, true);
                    var name = plan.Parameter;
                    if (plan.UndoOnStop) Undo[list] = () => SwitchQuiet(name, false);
                    if (plan.OffAfter > 0f) LaterOn.Add((Time.unscaledTime + plan.OffAfter, () => SwitchQuiet(name, false)));
                    break;
                case ChipStep.AttackClip:
                    // An item's attack, played whole by the clip the person swings it in, as a
                    // creature's attacks play by theirs; lit under the list pressed.
                    Listen.Note(heard, "played by the clip " + swing.name);
                    PlayClip(swing, litWith: list);
                    break;
                case ChipStep.OwnClip:
                    // A list the game plays as it moves the animator into a clip, seen by the probe
                    // (not one only heard around it: a hit does not play the stagger).
                    Listen.Note(heard, "plays with the clip " + own.name);
                    PlayClip(own);
                    break;
            }
            if (plan.PlaysList) things.AddRange(PlayEffectList(list));
            if (_startedClip != null) ClipOf[list] = _startedClip;
            _heard = null;

            Listen.Add(heard, things);
            if (WorldHeard) Listen.Note(heard, "a copy stands in the world, so the stage's are muted");
            if (plan.PlaysList && !things.Exists(Perceptible)) TellEmpty(label, list, things);
            Started(list, things);
        }

        /// <summary>The effect list being played, as the log names it, for notes on what was set.</summary>
        private static string _heard;

        /// <summary>Plays a list at a point on a copy, on the stage or in the world, as the stage's or the world's.</summary>
        public static List<GameObject> PlayOnCopyAt(GameObject copy, EffectList list, Vector3 point)
        {
            if (copy == null || list == null) return new List<GameObject>();
            if (copy == Stage.Subject) return Stage.PlayList(list, null, null, point).ConvertAll(m => m.Item2);
            return PlayList(list, point, copy.transform.rotation, size: SizeOf(copy));
        }

        private static readonly List<(float At, System.Action Act)> LaterOn = new List<(float, System.Action)>();
        private static readonly Dictionary<object, System.Action> Undo = new Dictionary<object, System.Action>();

        /// <summary>
        /// What an effect list is to its prefab, where the game does more with it than play it: a
        /// tree's hit (it shakes, TreeBase.Shake), a jump, a death, being alerted, waking, eating
        /// or blocking (each setting the animator as the game sets it).
        /// </summary>
        private static ListRole RoleOf(GameObject prefab, EffectList list)
        {
            if (prefab == null || list == null) return ListRole.Other;
            var tree = prefab.GetComponent<TreeBase>();
            if (tree != null && tree.m_trunk != null && list == tree.m_hitEffect) return ListRole.TreeHit;

            var character = prefab.GetComponent<Character>();
            var ai = prefab.GetComponent<BaseAI>();
            var humanoid = character as Humanoid;
            if (character != null && list == character.m_jumpEffects) return ListRole.Jump;
            if (character != null && list == character.m_deathEffects) return ListRole.Death;
            if (ai != null && list == ai.m_alertedEffects) return ListRole.Alerted;
            if (ai is MonsterAI monster && list == monster.m_wakeupEffects) return ListRole.Wakeup;
            if (humanoid != null && list == humanoid.m_consumeItemEffects) return ListRole.Consume;
            if (humanoid != null && (list == humanoid.m_perfectBlockEffect || IsBlock(prefab, list))) return ListRole.Block;
            return ListRole.Other;
        }

        /// <summary>The trigger and switch names the stage copy's and the world copy's animators have.</summary>
        private static string[] AnimatorParameters()
        {
            var names = new HashSet<string>();
            foreach (var copy in new[] { Stage.Subject, _world })
            {
                var animator = ClipPlayer.AnimatorOf(copy);
                if (animator == null) continue;
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.type == AnimatorControllerParameterType.Trigger || parameter.type == AnimatorControllerParameterType.Bool) names.Add(parameter.name);
                }
            }
            return names.ToArray();
        }

        /// <summary>A tree struck shakes its trunk a second (TreeBase.Shake), on the stage and in the world.</summary>
        private static void ShakeTrunk(GameObject prefab)
        {
            var tree = prefab.GetComponent<TreeBase>();
            foreach (var copy in new[] { Stage.Subject, _world })
            {
                var trunk = copy != null ? Looks.Twin(prefab.transform, copy.transform, tree.m_trunk.transform) : null;
                if (trunk != null) TrunkShake.Start(trunk);
            }
            Listen.Note(_heard, "shook the trunk");
        }

        /// <summary>Whether the list is what an item the creature carries, or the item itself, plays when blocking.</summary>
        private static bool IsBlock(GameObject prefab, EffectList list)
        {
            foreach (var item in Relations.CarriedItems(prefab))
            {
                var shared = item.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
                if (shared != null && shared.m_blockEffect == list) return true;
            }
            return false;
        }

        /// <summary>Sets a trigger on the stage copy's and the world copy's animators, where they have it.</summary>
        private static bool Trigger(string name)
        {
            var set = TriggerQuiet(name);
            if (_heard != null && !set)
            {
                var animator = ClipPlayer.AnimatorOf(Stage.Subject);
                Listen.Note(_heard, $"the animator has no {name} trigger, so the game animates nothing here either; its triggers are {(animator != null ? Triggers(animator) : "none")}");
            }
            else Listen.Note(_heard, $"set the animator's {name} trigger");
            return set;
        }

        private static bool TriggerQuiet(string name)
        {
            var set = false;
            foreach (var copy in new[] { Stage.Subject, _world })
            {
                var animator = ClipPlayer.AnimatorOf(copy);
                if (animator == null || !Has(animator, name, AnimatorControllerParameterType.Trigger)) continue;
                ClipPlayer.Stop(copy);
                animator.SetTrigger(name);
                set = true;
            }
            return set;
        }

        /// <summary>Sets a switch on the stage copy's and the world copy's animators, where they have it.</summary>
        private static bool Switch(string name, bool on)
        {
            var set = SwitchQuiet(name, on);
            if (_heard != null) Listen.Note(_heard, set ? $"set the animator's {name} switch {(on ? "on" : "off")}" : $"the animator has no {name} switch, so the game animates nothing here either");
            return set;
        }

        private static bool SwitchQuiet(string name, bool on)
        {
            var set = false;
            foreach (var copy in new[] { Stage.Subject, _world })
            {
                var animator = ClipPlayer.AnimatorOf(copy);
                if (animator == null || !Has(animator, name, AnimatorControllerParameterType.Bool)) continue;
                ClipPlayer.Stop(copy);
                animator.SetBool(name, on);
                set = true;
            }
            return set;
        }

        private static bool Has(Animator animator, string name, AnimatorControllerParameterType type)
        {
            foreach (var parameter in animator.parameters) if (parameter.type == type && parameter.name == name) return true;
            return false;
        }

        /// <summary>The animator's triggers by name, for the log.</summary>
        private static string Triggers(Animator animator)
        {
            var names = new List<string>();
            foreach (var parameter in animator.parameters) if (parameter.type == AnimatorControllerParameterType.Trigger) names.Add(parameter.name);
            return names.Count > 0 ? string.Join(", ", names) : "none";
        }

        private static readonly HashSet<EffectList> ToldEmpty = new HashSet<EffectList>();

        /// <summary>Whether a copy can be seen or heard: it draws, glows, sounds, or stands in for a fallen copy.</summary>
        private static bool Perceptible(GameObject thing)
        {
            return thing != null && (thing.GetComponentInChildren<Renderer>(true) != null || thing.GetComponentInChildren<AudioSource>(true) != null
                                     || thing.GetComponentInChildren<Light>(true) != null || thing.GetComponent<Standin>() != null);
        }

        /// <summary>Says once per list why playing it showed nothing, for finding out what it holds.</summary>
        private static void TellEmpty(string label, EffectList list, List<GameObject> made)
        {
            if (list?.m_effectPrefabs == null || !ToldEmpty.Add(list)) return;
            var parts = new List<string>();
            foreach (var data in list.m_effectPrefabs)
            {
                if (data?.m_prefab == null) { parts.Add("an empty slot"); continue; }
                var copy = made.Find(m => m != null && m.name == data.m_prefab.name);
                var kept = string.Join(" ", data.m_prefab.GetComponentsInChildren<Component>(true).Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name).Distinct().Take(12));
                var why = !data.m_enabled ? "switched off"
                    : copy != null ? "copied, but it has nothing that draws or sounds once its scripts are off: " + kept
                    : Ghost.IsWholeModel(data.m_prefab) && !Ghost.IsDebris(data.m_prefab) ? "a whole model, left out"
                    : "could not be copied";
                parts.Add($"{data.m_prefab.name} ({why})");
            }
            Plugin.Note($"Scry played nothing to see or hear of {_entry?.Name}'s \"{label}\": {(parts.Count > 0 ? string.Join("; ", parts) : "it is empty")}.");
        }

        /// <summary>The ragdoll a creature leaves when it dies, when it has one.</summary>
        public static global::Ragdoll RagdollOf(Entry entry)
        {
            if (entry == null || entry.Kind != Kind.Creature || !(entry.Source is GameObject prefab)) return null;
            return Falling.RagdollIn(prefab.GetComponent<Character>()?.m_deathEffects);
        }

        /// <summary>Whether an entry is something the game leaves to physics, a log or an item, which can be let fall.</summary>
        public static bool CanLetFall(Entry entry)
        {
            if (!(entry?.Source is GameObject prefab) || Looks.IsWorn(entry)) return false;
            return (prefab.GetComponent<TreeLog>() != null || prefab.GetComponent<ItemDrop>() != null) && prefab.GetComponent<Rigidbody>() != null;
        }

        /// <summary>Lets a log or an item fall and roll or tumble, on the stage and in the world.</summary>
        public static void LetFall()
        {
            if (!CanLetFall(_entry)) return;
            Stop("let fall");
            var prefab = (GameObject)_entry.Source;
            Started("let fall", new[] { Stage.LetFall(prefab), LetFallInWorld(prefab) });
        }

        /// <summary>Lets the creature fall as its ragdoll, on the stage and in the world, without its death effects.</summary>
        public static void Ragdoll()
        {
            var ragdoll = RagdollOf(_entry);
            if (ragdoll == null) return;
            Stop("ragdoll");
            var prefab = (GameObject)_entry.Source;
            var modifiers = _explorer?.Modifiers;
            var level = modifiers != null ? modifiers.Level : 1;
            var gear = modifiers != null && modifiers.LookAvailable ? Variants.GearOf(prefab, modifiers.Look) : new List<GameObject>();
            Started("ragdoll", new[] { Stage.Fall(ragdoll, prefab, level, gear), FallInWorld(ragdoll, prefab, level, gear) });
        }

        /// <summary>Plays an effect list on the stage copy, and on the copy in the world when there is one.</summary>
        public static List<GameObject> PlayEffectList(EffectList list)
        {
            var things = new List<GameObject>();
            foreach (var made in Stage.PlayList(list)) things.Add(made.Item2);
            if (_world != null) things.AddRange(PlayList(list, _world.transform.position + Vector3.up * 0.5f, _world.transform.rotation, size: SizeOf(_world)));
            return things;
        }

        private static readonly HashSet<string> Told = new HashSet<string>();

        /// <summary>Says once per prefab what destroying it leaves behind, for finding out why nothing falls.</summary>
        private static void Tell(GameObject prefab, EffectList list)
        {
            if (prefab == null || !Told.Add(prefab.name)) return;
            var debris = new List<string>();
            if (list?.m_effectPrefabs != null)
            {
                foreach (var data in list.m_effectPrefabs) if (data?.m_prefab != null && Ghost.IsDebris(data.m_prefab)) debris.Add(data.m_prefab.name);
            }
            var tree = prefab.GetComponent<TreeBase>();
            Plugin.Note($"Scry destroys {prefab.name}: {(Falling.Breaks(prefab, list) ? "breaks into its own parts" : "no parts of its own")}, "
                               + $"{(tree != null && tree.m_logPrefab != null ? "fells its log " + tree.m_logPrefab.name : "no log")}, "
                               + $"debris {(debris.Count > 0 ? string.Join(", ", debris) : "none")}.");
        }

        /// <summary>Plays one of a status effect's lists on you once.</summary>
        public static void PlayOnYou(EffectList list)
        {
            var made = OnYou(list);
            foreach (var copy in made) Remember(copy, EffectSeconds);
            Started(list, made);
        }

        private static bool HasAny(EffectList list)
        {
            if (list?.m_effectPrefabs == null) return false;
            foreach (var data in list.m_effectPrefabs)
            {
                if (data != null && data.m_enabled && data.m_prefab != null && !Ghost.IsWholeModel(data.m_prefab)) return true;
            }
            return false;
        }

        /// <summary>
        /// Copies of an effect list's prefabs placed on you the way the game places them: on the
        /// named part of the body when there is one, and attached when the list says so.
        /// </summary>
        private static List<GameObject> OnYou(EffectList list)
        {
            var made = new List<GameObject>();
            var player = Player.m_localPlayer;
            if (player == null || list?.m_effectPrefabs == null) return made;

            foreach (var data in list.m_effectPrefabs)
            {
                if (data == null || !data.m_enabled || data.m_prefab == null || Ghost.IsWholeModel(data.m_prefab)) continue;

                var anchor = player.transform;
                if (!string.IsNullOrEmpty(data.m_childTransform))
                {
                    var child = Utils.FindChild(anchor, data.m_childTransform);
                    if (child != null) anchor = child;
                }

                var parent = data.m_attach || data.m_follow ? anchor : null;
                var copy = Ghost.MakeOn(data.m_prefab, parent, anchor.position, anchor.rotation);
                if (copy != null) made.Add(copy);
            }
            return made;
        }
    }
}
