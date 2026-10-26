using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>Animation clips: playing them, and what each plays of an attack, of the game's own actions, by its name or heard around it.</summary>
    internal static partial class Previews
    {
        private static readonly Dictionary<Attack, ItemDrop.ItemData.SharedData> WeaponOf = new Dictionary<Attack, ItemDrop.ItemData.SharedData>();
        private static AnimationClip _startedClip;

        /// <summary>A clip an effect list's button started, whose parts light that button too.</summary>
        private static (AnimationClip Clip, object Key) _litWith;

        /// <summary>What a creature's clips play: of its attacks, and what the game plays with its own actions.</summary>
        private sealed class ClipPlays
        {
            public Dictionary<string, ClipAttack> Attacks = new Dictionary<string, ClipAttack>();
            public Dictionary<string, object> Actions = new Dictionary<string, object>();
            public Dictionary<string, object> Around = new Dictionary<string, object>();

            /// <summary>The flying effect of a born flyer its animator has no flying switch for, kept going in every clip.</summary>
            public EffectList Flying;

            /// <summary>What clips play that were paired by their names alone (<see cref="ClipByName"/>).</summary>
            public Dictionary<string, object> ByName = new Dictionary<string, object>();

            /// <summary>What each clip is, in a few words, for its chip: the attack it plays, or what the creature does in it.</summary>
            public Dictionary<string, string> Tags = new Dictionary<string, string>();

            /// <summary>The prefab of the item whose attack each attack clip plays, by clip name, for what the strict rules keep back of it.</summary>
            public Dictionary<string, string> AttackItems = new Dictionary<string, string>();

            /// <summary>Whether each clip plays any sound or effect, as worked out when first asked.</summary>
            public Dictionary<string, bool> Sounding = new Dictionary<string, bool>();

            /// <summary>False while the animator is still being watched.</summary>
            public bool Ready = true;

            /// <summary>Whether <see cref="Sounding"/> is known for every clip, worked out a little each frame.</summary>
            public bool AllSounding;

            /// <summary>The triggers the attacks of what it carries now are started by (<see cref="ClipLeftovers"/>).</summary>
            public List<string> Pulled = new List<string>();

            /// <summary>The switches what it carries now names, to draw, reel or reload (<see cref="HeldClips"/>).</summary>
            public List<string> Holding = new List<string>();

            /// <summary>The clips it never plays (<see cref="ClipLeftovers.Unplayed"/>); null while not worked out yet.</summary>
            public HashSet<string> Unplayed;

            /// <summary>The clips it plays only with other gear in hand, each with the stance that gear is held in (<see cref="HeldClips"/>); set with <see cref="Unplayed"/>.</summary>
            public Dictionary<string, int> OtherGear;
        }

        /// <summary>
        /// Whether what the stage copy's clips play is still being worked out, over the next
        /// frames; till then the panel says so rather than show the part found so far.
        /// </summary>
        public static bool ClipsSorting
        {
            get
            {
                var plays = StagePlays(out _);
                return plays != null && (!plays.Ready || !plays.AllSounding || plays.Unplayed == null);
            }
        }

        /// <summary>What the stage copy's clips play, as far as known, and the ears that play it; null for a copy without.</summary>
        private static ClipPlays StagePlays(out AnimationEars ears)
        {
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            ears = animator != null ? animator.GetComponent<AnimationEars>() : null;
            return ears != null ? PlaysOf(ears.Prefab, Stage.Subject, wait: false) : null;
        }

        /// <summary>
        /// Works out, a few milliseconds a frame, whether each of the stage copy's clips plays
        /// anything, so the panel never works out a person's hundreds of clips all at once.
        /// </summary>
        private static void SortSomeClips()
        {
            var plays = StagePlays(out _);
            if (plays == null || !plays.Ready || plays.AllSounding) return;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (var clip in Clips())
            {
                if (plays.Sounding.ContainsKey(clip.name)) continue;
                if (watch.ElapsedMilliseconds >= SortingBudgetMs) return;
                ClipSounds(clip);
            }
            plays.AllSounding = true;
        }

        /// <summary>
        /// Works out, once what each clip plays is known, which clips the stage copy never plays and
        /// which it plays only with other gear in hand: where each of its animator's attack triggers
        /// leads, and what it plays under each stance and with each switch an item names, each seen
        /// on a hidden copy over the next frames and kept for its prefab whatever it carries; and
        /// what it carries now. A person plays any attack once it holds the item, so what its other
        /// attacks play is other gear's, not never played.
        /// </summary>
        private static void SortLeftovers()
        {
            var plays = StagePlays(out var ears);
            if (plays == null || !plays.Ready || plays.Unplayed != null) return;
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            if (animator == null || animator.runtimeAnimatorController == null) return;
            var key = ears.Prefab.name + "|" + animator.runtimeAnimatorController.name;
            if (!LeadsOf.TryGetValue(key, out var leads))
            {
                var asked = ClipLeftovers.Candidates(animator.parameters.Where(p => p.type == AnimatorControllerParameterType.Trigger).Select(p => p.name));
                var seen = asked.Count > 0 ? TriggerProbe.ClipsOf(ears.Prefab.name, animator, asked, wait: false) : null;
                if (seen != null && seen.Busy) return;
                leads = new Dictionary<string, IReadOnlyList<string>>();
                if (seen != null) foreach (var trigger in asked) if (seen.Triggers.TryGetValue(trigger, out var clips)) leads[trigger] = clips;
                LeadsOf[key] = leads;
            }
            var gear = GameGear();
            var swept = TriggerProbe.StancesOf(ears.Prefab.name, animator, Stances, gear.Holds, wait: false);
            if (swept.Busy) return;

            var other = plays.Tags.Keys.Concat(plays.Actions.Keys).Concat(plays.Around.Keys).Concat(plays.ByName.Keys).Concat(plays.Attacks.Keys).ToList();
            var held = HeldClips.OtherGear(swept.Stances, swept.Holds, StanceNow(animator), plays.Holding, gear.HoldStances);
            var left = ClipLeftovers.UnplayedBy(leads, plays.Pulled, other.Where(c => !held.ContainsKey(c)));
            var unplayed = new HashSet<string>(System.StringComparer.Ordinal);
            var person = ears.Prefab.GetComponent<Player>() != null;
            foreach (var clip in left)
            {
                if (held.ContainsKey(clip.Key)) continue;
                if (person) held[clip.Key] = gear.AttackStances.TryGetValue(clip.Value, out var stance) ? stance : -1;
                else unplayed.Add(clip.Key);
            }
            // What it plays now, as its own attacks and actions, is never other gear's.
            foreach (var clip in plays.Attacks.Keys.Concat(plays.Actions.Keys)) held.Remove(clip);
            plays.OtherGear = held;
            plays.Unplayed = unplayed;
            if (unplayed.Count > 0) Log.Note($"Scry leaves out {Numbers.Count(unplayed.Count)} clips {ears.Prefab.name} never plays, as nothing it carries starts them: {string.Join(", ", unplayed.OrderBy(c => c, System.StringComparer.Ordinal))}.");
            if (held.Count > 0) Log.Note($"Scry leaves out {Numbers.Count(held.Count)} clips {ears.Prefab.name} plays only with other gear in hand.");
        }

        /// <summary>Where each attack trigger of a prefab's animator leads, by prefab and controller, seen once (<see cref="SortLeftovers"/>).</summary>
        private static readonly Dictionary<string, Dictionary<string, IReadOnlyList<string>>> LeadsOf = new Dictionary<string, Dictionary<string, IReadOnlyList<string>>>();

        /// <summary>The stance a copy's animator stands in, as the game numbers stances (<c>ItemDrop.ItemData.AnimationState</c>).</summary>
        private static int StanceNow(Animator animator)
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == "statei" && parameter.type == AnimatorControllerParameterType.Int) return animator.GetInteger("statei");
            }
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == "statef" && parameter.type == AnimatorControllerParameterType.Float) return Mathf.RoundToInt(animator.GetFloat("statef"));
            }
            return 0;
        }

        /// <summary>Every stance the game has, by its number, to sweep a copy's animator through.</summary>
        private static readonly IReadOnlyList<int> Stances = System.Enum.GetValues(typeof(ItemDrop.ItemData.AnimationState)).Cast<int>().ToList();

        /// <summary>The switches an item's attacks name to draw, charge or reload (<c>Attack.m_drawAnimationState</c>, <c>m_chargeAnimationBool</c>, <c>m_reloadAnimation</c>).</summary>
        private static IEnumerable<string> Switches(GameObject item)
        {
            var shared = item != null ? item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared : null;
            if (shared == null) yield break;
            foreach (var attack in new[] { shared.m_attack, shared.m_secondaryAttack })
            {
                if (attack == null) continue;
                foreach (var name in new[] { attack.m_drawAnimationState, attack.m_chargeAnimationBool, attack.m_reloadAnimation }) if (!string.IsNullOrEmpty(name)) yield return name;
            }
        }

        /// <summary>
        /// What every item of the game starts its attacks by and switches on, with the stance it is
        /// held in, worked out once from the game's item list: which gear a clip it does not hold
        /// now goes with.
        /// </summary>
        private static (List<(string Switch, int Stance)> Holds, Dictionary<string, int> HoldStances, Dictionary<string, int> AttackStances) GameGear()
        {
            if (_gear.Holds != null) return _gear;
            var holds = new List<(string, int)>();
            var holdStances = new Dictionary<string, int>(System.StringComparer.Ordinal);
            var attackStances = new Dictionary<string, int>(System.StringComparer.Ordinal);
            var items = ObjectDB.instance != null ? ObjectDB.instance.m_items : new List<GameObject>();
            foreach (var item in items)
            {
                var shared = item != null ? item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared : null;
                if (shared == null) continue;
                var stance = (int)shared.m_animationState;
                foreach (var switched in Switches(item))
                {
                    if (holdStances.ContainsKey(switched)) continue;
                    holdStances[switched] = stance;
                    holds.Add((switched, stance));
                }
                foreach (var attack in new[] { shared.m_attack, shared.m_secondaryAttack })
                {
                    if (attack == null || string.IsNullOrEmpty(attack.m_attackAnimation)) continue;
                    var pulled = ClipAttacks.StartTriggers(attack.m_attackAnimation, attack.m_attackChainLevels, attack.m_attackRandomAnimations, _ => true)
                        .Concat(ClipAttacks.ComboSteps(attack.m_attackAnimation, attack.m_attackChainLevels, _ => true).Select(step => step.Trigger));
                    foreach (var trigger in pulled) if (!attackStances.ContainsKey(trigger)) attackStances[trigger] = stance;
                }
            }
            // Not kept while the game's item list is not there yet.
            if (ObjectDB.instance != null) _gear = (holds, holdStances, attackStances);
            return (holds, holdStances, attackStances);
        }

        private static (List<(string Switch, int Stance)> Holds, Dictionary<string, int> HoldStances, Dictionary<string, int> AttackStances) _gear;

        /// <summary>The clips of the stage copy it plays only with other gear in hand, each with the stance that gear is held in; null while not worked out, or for a copy without.</summary>
        public static IReadOnlyDictionary<string, int> ClipsOtherGear() => StagePlays(out _)?.OtherGear;

        /// <summary>The clips of the stage copy it never plays (<see cref="ClipLeftovers"/>); null while not worked out, or for a copy without.</summary>
        public static HashSet<string> ClipsUnplayed() => StagePlays(out _)?.Unplayed;

        /// <summary>How long working out the clips may take each frame.</summary>
        private const long SortingBudgetMs = 4;

        /// <summary>
        /// Whether a clip of the stage copy plays any sound or effect: of its own, with its attack
        /// or the game's actions, found by name, or heard around it. Worked out once per clip.
        /// </summary>
        public static bool ClipSounds(AnimationClip clip)
        {
            if (clip == null) return false;
            var plays = StagePlays(out var ears);
            if (plays == null || !plays.Ready) return false;
            if (!plays.Sounding.TryGetValue(clip.name, out var sounds))
            {
                sounds = ears.Members(clip).Count > 0 || ears.ByNameMembers(clip).Count > 0 || ears.AroundMembers(clip).Count > 0;
                plays.Sounding[clip.name] = sounds;
            }
            return sounds;
        }

        private static readonly Dictionary<string, string> NoTags = new Dictionary<string, string>();

        /// <summary>What each clip of the stage copy is, in a few words, by clip name: the attack it plays or what the creature does in it.</summary>
        public static IReadOnlyDictionary<string, string> ClipTags()
        {
            var ears = ClipPlayer.AnimatorOf(Stage.Subject).OrNull()?.GetComponent<AnimationEars>();
            var plays = ears != null ? PlaysOf(ears.Prefab, Stage.Subject, wait: false) : null;
            return plays != null ? plays.Tags : NoTags;
        }

        /// <summary>The item whose attack each attack clip of the stage copy plays, by clip name, by the item's prefab.</summary>
        public static IReadOnlyDictionary<string, string> ClipAttackItems()
        {
            var ears = ClipPlayer.AnimatorOf(Stage.Subject).OrNull()?.GetComponent<AnimationEars>();
            var plays = ears != null ? PlaysOf(ears.Prefab, Stage.Subject, wait: false) : null;
            return plays != null ? plays.AttackItems : NoTags;
        }

        /// <summary>
        /// The stage copy's clips an effect list goes with: those that play it (as the game does
        /// with its own actions, or an item's attack), those it was found by name to go with, and
        /// those it is heard around, each said which.
        /// </summary>
        public static List<(AnimationClip Clip, string How)> ClipsOfList(EffectList list)
        {
            var found = new List<(AnimationClip, string)>();
            var animator = list != null ? ClipPlayer.AnimatorOf(Stage.Subject) : null;
            var ears = animator != null ? animator.GetComponent<AnimationEars>() : null;
            var plays = ears != null ? PlaysOf(ears.Prefab, Stage.Subject) : null;
            if (plays == null) return found;

            // Asked on every event the panel draws while the list is the one played last.
            if (ReferenceEquals(list, _ofList) && ReferenceEquals(plays, _ofListPlays) && ReferenceEquals(Stage.Subject, _ofListCopy)) return _ofListClips;

            void Add(string name, string how)
            {
                var clip = ClipNamed(name);
                if (clip != null && !found.Exists(f => f.Item1 == clip)) found.Add((clip, how));
            }
            if (AttackOf.TryGetValue(list, out var attack) && AttackClipOf(attack) is AnimationClip swing) Add(swing.name, "");
            foreach (var pair in plays.Actions) if (pair.Value == list) Add(pair.Key, "");
            foreach (var pair in plays.ByName) if (pair.Value == list) Add(pair.Key, "by name");
            foreach (var pair in plays.Around) if (pair.Value == list) Add(pair.Key, "heard around");
            _ofList = list;
            _ofListPlays = plays;
            _ofListCopy = Stage.Subject;
            _ofListClips = found;
            return found;
        }

        private static EffectList _ofList;
        private static ClipPlays _ofListPlays;
        private static GameObject _ofListCopy;
        private static List<(AnimationClip Clip, string How)> _ofListClips;

        /// <summary>The stage copy's clip of a name, or null.</summary>
        public static AnimationClip ClipNamed(string name)
        {
            if (name == null) return null;
            Clips();
            return _clipByName.TryGetValue(name, out var clip) ? clip : null;
        }

        private static readonly Dictionary<string, ClipPlays> ClipPlaysCache = new Dictionary<string, ClipPlays>();

        /// <summary>
        /// What a creature's clip plays of an attack (<see cref="ClipAttacks"/>), its
        /// <see cref="ClipAttack.Key"/> the <see cref="Attack"/>: of the attacks its animator can
        /// start, those of what it has now first, then those of all it may carry. Null for a clip
        /// no attack plays.
        /// </summary>
        public static ClipAttack AttackOfClip(GameObject prefab, GameObject copy, string clip)
        {
            var plays = string.IsNullOrEmpty(clip) ? null : PlaysOf(prefab, copy);
            return plays != null && plays.Attacks.TryGetValue(clip, out var part) ? part : null;
        }

        /// <summary>
        /// What the game plays as it moves a creature's animator into a clip by one of its own
        /// actions (<see cref="ClipActions"/>): a jump's effects with the clip the jump leads to,
        /// waking's with the wake-up, going to sleep's, being alerted's, eating's, even an empty
        /// list, which the game then plays as nothing. Null for none.
        /// </summary>
        public static EffectList ListOfClip(GameObject prefab, GameObject copy, string clip) => ListOfClip(prefab, copy, clip, out _);

        /// <summary>
        /// As <see cref="ListOfClip(GameObject, GameObject, string)"/>, and whether the game keeps
        /// it going for as long as the creature is there (in water, flying) rather than playing it
        /// once.
        /// </summary>
        public static EffectList ListOfClip(GameObject prefab, GameObject copy, string clip, out bool lasting)
        {
            lasting = false;
            var plays = string.IsNullOrEmpty(clip) ? null : PlaysOf(prefab, copy);
            if (plays == null) return null;
            var character = prefab.GetComponent<Character>();
            if (!plays.Actions.TryGetValue(clip, out var list))
            {
                // A born flyer the animator has no flying switch for flies whatever it plays, and
                // the game keeps its flying effect going all the while (Character.IsFlying).
                if (plays.Flying == null) return null;
                lasting = true;
                return plays.Flying;
            }
            lasting = character != null && (list == character.m_waterEffects || list == character.m_flyingContinuousEffect);
            return (EffectList)list;
        }

        /// <summary>
        /// What is heard around a creature's clip, though the game does not play it with the clip
        /// (<see cref="ClipAround"/>): with waking and a spawn roar, what it calls out once
        /// alerted; with the clips it idles in, its idle sound. Null for none.
        /// </summary>
        public static EffectList AroundOfClip(GameObject prefab, GameObject copy, string clip)
        {
            var plays = string.IsNullOrEmpty(clip) ? null : PlaysOf(prefab, copy);
            return plays != null && plays.Around.TryGetValue(clip, out var list) ? (EffectList)list : null;
        }

        /// <summary>
        /// What a clip plays that it was paired with by its name alone, where the animator could
        /// not be seen to go there (<see cref="ClipByName"/>), and whether the game keeps it
        /// going. Null for none.
        /// </summary>
        public static EffectList ByNameOfClip(GameObject prefab, GameObject copy, string clip, out bool lasting)
        {
            lasting = false;
            var plays = string.IsNullOrEmpty(clip) ? null : PlaysOf(prefab, copy);
            if (plays == null || !plays.ByName.TryGetValue(clip, out var found)) return null;
            lasting = found == prefab.GetComponent<Character>().OrNull()?.m_waterEffects;
            return (EffectList)found;
        }

        /// <summary>
        /// The clip an item's attack plays first on the person trying it on, as the person's
        /// animator plays it for the attack's trigger in the stance the item gives; null for none.
        /// Asked without waiting, it is null while the animator is still watched, rather than
        /// watching the rest of it in this one frame.
        /// </summary>
        private static AnimationClip AttackClipOf(Attack attack, bool wait = true, int step = 0)
        {
            var ears = ClipPlayer.AnimatorOf(Stage.Subject).OrNull()?.GetComponent<AnimationEars>();
            var plays = ears != null ? PlaysOf(ears.Prefab, Stage.Subject, wait) : null;
            if (plays == null) return null;
            foreach (var part in plays.Attacks)
            {
                if (!part.Value.Begins) continue;
                if (step == 0 ? part.Value.Key == attack : part.Value.Key is ComboStep at && at.Attack == attack && at.Step == step) return ClipNamed(part.Key);
            }
            return null;
        }

        /// <summary>
        /// The stage copy's clip that begins an attack of an item, by the item's name: one its
        /// creature carries, or a weapon the person on the stage wears, its first attack or its
        /// second; null where it has none of that name or the clip is not known yet.
        /// </summary>
        public static AnimationClip ClipOfAttackItem(string item, bool secondary = false, int step = 0)
        {
            // Asked for each line of the table on every event the panel draws: found once for the copy.
            if (!ReferenceEquals(_attackClipsOf, Stage.Subject))
            {
                _attackClipsOf = Stage.Subject;
                AttackClips.Clear();
            }
            var key = item + (secondary ? "#2" : "") + (step > 0 ? "#" + Stored.Count(step) : "");
            if (AttackClips.TryGetValue(key, out var known)) return known;
            if (EntryKeys.IsDeathAttack(item))
            {
                var dies = DeathAttackClip();
                if (dies != null) AttackClips[key] = dies;
                return dies;
            }
            var attack = AttackOfItem(item, secondary);
            // Asked for every line of a table each time the panel draws: never made to watch the
            // rest of the animator in one frame (a troll's took 380 ms); known once watched.
            var clip = attack != null ? AttackClipOf(attack, wait: false, step) : null;
            // Not kept until found: the clips are worked out a while after the copy is shown.
            if (clip != null) AttackClips[key] = clip;
            return clip;
        }

        /// <summary>An item's attack, its first or its second, by the item's name: one the stage copy's creature carries, or a weapon the person on the stage wears; null where it has none.</summary>
        private static Attack AttackOfItem(string item, bool secondary)
        {
            var prefab = ClipPlayer.AnimatorOf(Stage.Subject).OrNull()?.GetComponent<AnimationEars>().OrNull()?.Prefab;
            var worn = Looks.IsWorn(_entry) && _entry.Source is GameObject source ? Looks.WornWith(source) : null;
            var carried = worn != null ? worn.Find(i => i != null && i.name == item) : prefab != null ? Relations.CarriedItems(prefab).Find(i => i.name == item) : null;
            var shared = carried != null ? carried.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared : null;
            return secondary ? shared?.m_secondaryAttack : shared?.m_attack;
        }

        /// <summary>
        /// The trigger the game starts an item's attack by (<c>Attack.Start</c>), for an attack that
        /// plays no clip on the stage copy: the first of them its animator has, else the first the
        /// game pulls, and whether the animator has it. Null where the attack or its trigger is not known.
        /// </summary>
        public static string AttackTriggerOf(string item, bool secondary, out bool inAnimator)
        {
            inAnimator = false;
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            var attack = animator != null && !EntryKeys.IsDeathAttack(item) ? AttackOfItem(item, secondary) : null;
            if (attack == null) return null;
            var pulled = ClipAttacks.StartTriggers(attack.m_attackAnimation, attack.m_attackChainLevels, attack.m_attackRandomAnimations, _ => true);
            if (pulled.Count == 0) return null;
            var had = pulled.Find(t => HasTrigger(animator, t));
            inAnimator = had != null;
            return had ?? pulled[0];
        }

        /// <summary>
        /// Plays a weapon's attack once the person wearing it is on the stage and its clip is
        /// known, after a click on its attacks table while it is shown on its own put it on the
        /// person; given up when something else is selected or after a while.
        /// </summary>
        public static void PlayAttackWhenKnown(string item, bool secondary, int step = 0)
        {
            _attackAsked = (item, secondary, step, _entry, Time.unscaledTime + AttackWait);
        }

        /// <summary>An attack asked for before its clip was known, which step of it, for whom and until when; null item for none.</summary>
        private static (string Item, bool Secondary, int Step, Entry For, float Until) _attackAsked;

        private const float AttackWait = 20f;

        /// <summary>Plays the attack asked for once its clip is known (<see cref="PlayAttackWhenKnown"/>).</summary>
        private static void PlayAttackAsked()
        {
            if (_attackAsked.Item == null) return;
            if (!ReferenceEquals(_attackAsked.For, _entry) || Time.unscaledTime > _attackAsked.Until)
            {
                _attackAsked = default;
                return;
            }
            var clip = ClipOfAttackItem(_attackAsked.Item, _attackAsked.Secondary, _attackAsked.Step);
            if (clip == null) return;
            _attackAsked = default;
            PlayClip(clip);
            LastClip = clip;
        }

        /// <summary>The copy whose attacks' clips are kept below, and those clips by the item each is made with.</summary>
        private static GameObject _attackClipsOf;
        private static readonly Dictionary<string, AnimationClip> AttackClips = new Dictionary<string, AnimationClip>();

        /// <summary>
        /// Whether what the stage copy's clips play is known (<see cref="ClipsKnown"/>): an attack
        /// with no clip of its own then plays nothing, and its name is no link.
        /// </summary>
        public static bool StageClipsKnown()
        {
            var ears = ClipPlayer.AnimatorOf(Stage.Subject).OrNull()?.GetComponent<AnimationEars>();
            return ears != null && ClipsKnown(ears.Prefab, Stage.Subject);
        }

        /// <summary>
        /// Whether what a copy's clips play is known, without waiting for it: false while its
        /// animator is still being watched, when a clip started now has to wait for its attack and
        /// lists rather than stall the game until the watching is done.
        /// </summary>
        public static bool ClipsKnown(GameObject prefab, GameObject copy)
        {
            var plays = PlaysOf(prefab, copy, wait: false);
            return plays == null || plays.Ready;
        }

        /// <summary>What clips play while their creature's animator is still being watched: nothing yet.</summary>
        private static readonly ClipPlays NotYet = new ClipPlays { Ready = false };

        /// <summary>
        /// What each of a creature's clips plays. The animator is watched over the next frames; till
        /// then <see cref="NotYet"/>, unless <paramref name="wait"/>, when it is finished at once.
        /// </summary>
        private static ClipPlays PlaysOf(GameObject prefab, GameObject copy, bool wait = true)
        {
            if (prefab == null) return null;

            // Asked many times a frame by the panel (for every clip, a person's hundreds), so the
            // answer is kept for the frame.
            if (_playsFrame == Time.frameCount && _playsPrefab == prefab && _playsCopy == copy && _plays != null && (!wait || _plays.Ready)) return _plays;
            var found = Find(prefab, copy, wait);
            _playsFrame = Time.frameCount;
            _playsPrefab = prefab;
            _playsCopy = copy;
            _plays = found;
            return found;
        }

        private static int _playsFrame = -1;
        private static GameObject _playsPrefab;
        private static GameObject _playsCopy;
        private static ClipPlays _plays;

        /// <summary>The clips a body's own water and jump effects play with, by their names.</summary>
        private static readonly string[] WaterClips = { "swim", "tread" };
        private static readonly string[] JumpClips = { "jump" };

        /// <summary>
        /// What a prefab's clips play: worked out once for what it carries now (its look's gear, or
        /// what the person tries on), from the attacks of what it carries, the game's own actions,
        /// what is heard around it, clips found by their names, and a tag for each; none yet while
        /// its animator is still watched.
        /// </summary>
        private static ClipPlays Find(GameObject prefab, GameObject copy, bool wait)
        {
            // What it has now: a creature what its look carries; the person trying an item on
            // what it wears, the item first.
            var worn = Looks.IsWorn(_entry) && !ReferenceEquals(_entry.Source, prefab) ? Looks.WornWith((GameObject)_entry.Source) : null;
            var carried = worn ?? (_entry != null && ReferenceEquals(_entry.Source, prefab) ? CarriedNow(_entry) : null);
            var cacheKey = prefab.name + "|" + (carried == null ? "" : string.Join(",", carried.Select(c => c.name)));
            if (ClipPlaysCache.TryGetValue(cacheKey, out var plays)) return plays;

            // While its animator is still watched, nothing below can be answered yet; asked each
            // frame meanwhile, it is not worked out again only to be thrown away.
            if (!wait && TriggerProbe.IsBusy(prefab.name)) return NotYet;

            plays = new ClipPlays();
            ClipPlaysCache[cacheKey] = plays;
            var animator = ClipPlayer.AnimatorOf(copy);
            if (animator == null) return plays;

            var items = worn ?? Relations.CarriedItems(prefab);
            if (carried != null) items = items.Where(carried.Contains).Concat(items.Where(i => !carried.Contains(i))).ToList();
            // Holding nothing that attacks, it fights with its bare hands (Humanoid.GetCurrentWeapon).
            var bare = prefab.GetComponent<Humanoid>().OrNull()?.m_unarmedWeapon.OrNull()?.gameObject;
            if (bare != null && !items.Any(Attacks)) items = items.Concat(new[] { bare }).ToList();
            var names = new Dictionary<Attack, string>();
            var itemsOf = new Dictionary<Attack, string>();
            var attacks = AttacksOf(prefab, animator, items, names, itemsOf);
            var actions = GameLists(prefab).ToList();
            if (attacks.Count == 0 && actions.Count == 0 && prefab.GetComponent<BaseAI>() == null) return plays;
            var clips = animator.runtimeAnimatorController.animationClips.Where(c => c != null).ToList();
            var striking = clips.Where(c => c.events.Any(e => e.functionName == "Hit" || e.functionName == "OnAttackTrigger")).Select(c => c.name);
            var seen = TriggerProbe.ClipsOf(prefab.name, animator, attacks.Select(a => a.Item1), wait);
            if (seen.Busy)
            {
                ClipPlaysCache.Remove(cacheKey);
                return NotYet;
            }
            plays.Attacks = ClipAttacks.Match(attacks, seen.Triggers, clips.Select(c => c.name), striking);
            plays.Pulled = attacks.Select(a => a.Item1).Distinct().ToList();
            plays.Holding = items.SelectMany(Switches).Distinct().ToList();
            plays.Actions = ClipActions.Match(actions, seen.Actions, plays.Attacks.Keys);
            var body = prefab.GetComponent<Character>();
            plays.Around = AroundOf(prefab, seen, plays);
            plays.ByName = ByNameOf(body, seen, clips, plays);
            if (body != null && body.m_flying && EffectSlots.ShowsAny(body.m_flyingContinuousEffect) && !animator.parameters.Any(p => p.name == "flying")) plays.Flying = body.m_flyingContinuousEffect;
            TagClips(plays, names, itemsOf, seen);
            TagDeathAttack(prefab, body, plays);
            if (body != null && FirstTime("lasting:" + prefab.name)) TellLasting(prefab, body);
            return plays;
        }

        /// <summary>Whether an item has an attack the game starts by an animation (<c>Attack.Start</c>).</summary>
        private static bool Attacks(GameObject item)
        {
            var shared = item != null ? item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared : null;
            return shared != null && (!string.IsNullOrEmpty(shared.m_attack?.m_attackAnimation) || !string.IsNullOrEmpty(shared.m_secondaryAttack?.m_attackAnimation));
        }

        /// <summary>
        /// The attacks of what a prefab carries that its animator has a trigger for, each with the
        /// triggers that start it, and after them every attack's later combo steps, so a start keeps
        /// its own clip; each attack's name put in <paramref name="names"/> and its item's prefab in <paramref name="itemsOf"/>.
        /// </summary>
        private static List<(string, object)> AttacksOf(GameObject prefab, Animator animator, List<GameObject> items, Dictionary<Attack, string> names, Dictionary<Attack, string> itemsOf)
        {
            var attacks = new List<(string, object)>();
            var steps = new List<(string, object)>();
            foreach (var item in items)
            {
                var shared = item.GetComponent<ItemDrop>().OrNull()?.m_itemData?.m_shared;
                if (shared == null) continue;
                foreach (var attack in new[] { shared.m_attack, shared.m_secondaryAttack })
                {
                    var triggers = attack != null ? TriggersOf(animator, attack) : null;
                    if (triggers == null || triggers.Count == 0) continue;
                    WeaponOf[attack] = shared;
                    foreach (var trigger in triggers) attacks.Add((trigger, attack));
                    // A chain's later steps after every attack's start, so a start keeps its own clip.
                    foreach (var (step, trigger) in ClipAttacks.ComboSteps(attack.m_attackAnimation, attack.m_attackChainLevels, t => HasTrigger(animator, t)))
                    {
                        steps.Add((trigger, new ComboStep { Attack = attack, Step = step }));
                    }
                    var name = (CatalogBuilder.GameName(item) ?? WeaponChoices.Readable(item.name, prefab.name)).ToLowerInvariant();
                    names[attack] = ClipWords.Attack(name, attack == shared.m_secondaryAttack);
                    itemsOf[attack] = item.name;
                }
            }
            attacks.AddRange(steps);
            return attacks;
        }

        /// <summary>
        /// What is heard next or now and then: waking, a troll is silent and roars once it spots you
        /// (BaseAI.SetAlerted); awake, it makes its idle sound now and then (BaseAI.DoIdleSound),
        /// whatever it plays; staggered, it was hit, again and again (Character.AddStaggerDamage).
        /// </summary>
        private static Dictionary<string, object> AroundOf(GameObject prefab, Probed seen, ClipPlays plays)
        {
            var ai = prefab.GetComponent<BaseAI>();
            var alerted = ai != null && EffectSlots.ShowsAny(ai.m_alertedEffects) ? ai.m_alertedEffects : null;
            var around = new List<(string, object)>();
            if (alerted != null)
            {
                around.Add(("wake", alerted));
                around.Add(("spawn", alerted));
            }

            var hit = prefab.GetComponent<Character>().OrNull()?.m_hitEffects;
            if (hit != null && EffectSlots.ShowsAny(hit)) around.Add(("stagger", hit));
            var idleSound = ai != null && EffectSlots.ShowsAny(ai.m_idleSound) ? ai.m_idleSound : null;
            return ClipAround.Match(around, seen.Actions, seen.Idle, idleSound, plays.Attacks.Keys);
        }

        /// <summary>Where the animator could not be seen to swim or jump, the clips named so.</summary>
        private static Dictionary<string, object> ByNameOf(Character body, Probed seen, List<AnimationClip> clips, ClipPlays plays)
        {
            var named = new List<(string, string[], object)>();
            if (body != null && EffectSlots.ShowsAny(body.m_waterEffects)) named.Add(("water", WaterClips, body.m_waterEffects));
            if (body != null && EffectSlots.ShowsAny(body.m_jumpEffects)) named.Add(("jump", JumpClips, body.m_jumpEffects));
            var tried = new Dictionary<string, IReadOnlyList<string>>();
            IReadOnlyList<string> Saw(string action) => seen.Actions.TryGetValue(action, out var s) ? s : System.Array.Empty<string>();
            tried["water"] = Saw("water").Concat(Saw("swim")).ToList();
            tried["jump"] = Saw("jump");
            return ClipByName.Match(named, tried, clips.Select(c => c.name), plays.Attacks.Keys.Concat(plays.Actions.Keys));
        }

        /// <summary>What each clip is: the attack it plays, what the animator was seen to do there, what it was named for, or idling.</summary>
        private static void TagClips(ClipPlays plays, Dictionary<Attack, string> names, Dictionary<Attack, string> itemsOf, Probed seen)
        {
            foreach (var part in plays.Attacks)
            {
                var attack = part.Value.Key as Attack ?? (part.Value.Key as ComboStep)?.Attack as Attack;
                if (attack == null || !names.TryGetValue(attack, out var name)) continue;
                plays.Tags[part.Key] = part.Value.Key is ComboStep step ? ClipWords.Swing(name, step.Step) : name;
                if (itemsOf.TryGetValue(attack, out var item)) plays.AttackItems[part.Key] = item;
            }
            foreach (var pair in seen.Actions)
            {
                if (pair.Value.Count > 0 && ClipWords.Action(pair.Key, out var word) && !plays.Tags.ContainsKey(pair.Value[0])) plays.Tags[pair.Value[0]] = word;
            }
            foreach (var pair in plays.ByName)
            {
                if (plays.Tags.ContainsKey(pair.Key)) continue;
                plays.Tags[pair.Key] = ClipWords.ByName(pair.Key);
            }
            foreach (var clip in seen.Idle) if (!plays.Tags.ContainsKey(clip)) plays.Tags[clip] = ClipWords.Idles;
        }

        /// <summary>
        /// The clip a creature dies in, where its death hurts what stands near (<see cref="Facts.DeathAttackOf"/>):
        /// one of its attacks too, named by what its death does, and kept back as an attack's clip is.
        /// </summary>
        private static void TagDeathAttack(GameObject prefab, Character body, ClipPlays plays)
        {
            if (body == null || Facts.DeathAttackOf(prefab, out var blast) == null) return;
            var name = (CatalogBuilder.GameName(blast) ?? Naming.Readable(blast.name)).ToLowerInvariant();
            foreach (var pair in plays.Actions)
            {
                if (!ReferenceEquals(pair.Value, body.m_deathEffects)) continue;
                plays.Tags[pair.Key] = ClipWords.DeathAttack(name);
                plays.AttackItems[pair.Key] = EntryKeys.DeathAttack;
            }
        }

        /// <summary>The stage copy's clip it dies in, where its death is one of its attacks (<see cref="TagDeathAttack"/>); null where none is, or not known yet.</summary>
        private static AnimationClip DeathAttackClip()
        {
            var plays = StagePlays(out _);
            if (plays == null || !plays.Ready) return null;
            foreach (var pair in plays.AttackItems) if (EntryKeys.IsDeathAttack(pair.Value)) return ClipNamed(pair.Key);
            return null;
        }

        /// <summary>
        /// The game's own actions on a creature's animator that come with an effect list, as its
        /// code plays them together: a jump (<c>Character.ForceJump</c>), eating
        /// (<c>MonsterAI.UpdateConsumeItem</c>), going to sleep and waking (<c>MonsterAI.Sleep</c>,
        /// <c>Wakeup</c>), being alerted (<c>BaseAI.SetAlerted</c>). Named as the probe names them.
        /// </summary>
        private static IEnumerable<(string, object)> GameLists(GameObject prefab)
        {
            var character = prefab.GetComponent<Character>();
            var ai = prefab.GetComponent<BaseAI>();
            var monster = ai as MonsterAI;
            if (character.OrNull()?.m_jumpEffects != null) yield return ("jump", character.m_jumpEffects);
            if (character is Humanoid humanoid && humanoid.m_consumeItemEffects != null) yield return ("consume", humanoid.m_consumeItemEffects);
            if (monster.OrNull()?.m_sleepEffects != null) yield return ("sleep", monster.m_sleepEffects);
            if (monster.OrNull()?.m_wakeupEffects != null) yield return ("wake", monster.m_wakeupEffects);
            if (ai.OrNull()?.m_alertedEffects != null) yield return ("alert", ai.m_alertedEffects);

            // In water or flying, the game keeps these going for as long (Character.UpdateContinousEffects);
            // dying, it plays its death (Character.OnDeath).
            if (character.OrNull()?.m_waterEffects != null)
            {
                yield return ("water", character.m_waterEffects);
                yield return ("swim", character.m_waterEffects);
            }
            if (character.OrNull()?.m_flyingContinuousEffect != null) yield return ("fly", character.m_flyingContinuousEffect);
            if (character.OrNull()?.m_deathEffects != null) yield return ("dead", character.m_deathEffects);
        }

        /// <summary>What a creature keeps going in water and in flight, and its own scale, said once per prefab.</summary>
        [Diagnostic]
        private static void TellLasting(GameObject prefab, Character body)
        {
            string Of(EffectList list) => EffectSlots.ShowsAny(list) ? string.Join(", ", EffectSlots.NamesPlayed(list)) : "nothing";
            Log.Note($"Scry: {prefab.name} keeps going in water {Of(body.m_waterEffects)}; flying {Of(body.m_flyingContinuousEffect)}{(body.m_flying ? ", and it flies from birth" : "")}; its own scale {Numbers.Amount(prefab.transform.localScale.x, 2)}.");
        }

        /// <summary>The triggers an attack starts by on this animator, as <c>Attack.Start</c> pulls them; none when it has none.</summary>
        private static List<string> TriggersOf(Animator animator, Attack attack) =>
            ClipAttacks.StartTriggers(attack.m_attackAnimation, attack.m_attackChainLevels, attack.m_attackRandomAnimations, t => HasTrigger(animator, t));

        private static bool HasTrigger(Animator animator, string name)
        {
            foreach (var parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == name) return true;
            }
            return false;
        }

        /// <summary>What the stage copy's animation clip plays of itself, by prefab name.</summary>
        public static List<string> ClipMembers(AnimationClip clip)
        {
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            var ears = animator != null ? animator.GetComponent<AnimationEars>() : null;
            return ears != null ? ears.Members(clip) : new List<string>();
        }

        /// <summary>What the stage copy's animation clip plays that it was paired with by its name alone, by prefab name.</summary>
        public static List<string> ClipByNameMembers(AnimationClip clip)
        {
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            var ears = animator != null ? animator.GetComponent<AnimationEars>() : null;
            return ears != null ? ears.ByNameMembers(clip) : new List<string>();
        }

        /// <summary>What is heard around the stage copy's animation clip, by prefab name.</summary>
        public static List<string> ClipAroundMembers(AnimationClip clip)
        {
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            var ears = animator != null ? animator.GetComponent<AnimationEars>() : null;
            return ears != null ? ears.AroundMembers(clip) : new List<string>();
        }

        /// <summary>
        /// The stage copy's animation clips, each name once, by name. Kept for as long as the
        /// copy's animator and its controller stay the same; not to be changed by the caller.
        /// </summary>
        public static List<AnimationClip> Clips()
        {
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            var controller = animator != null ? animator.runtimeAnimatorController : null;
            if (controller == null)
            {
                _clipByName.Clear();
                _clipsOf = null;
                _clipNames = new List<string>();
                return NoClips;
            }
            if (ReferenceEquals(controller, _clipsOf) && _clips != null) return _clips;

            var all = controller.animationClips;
            var names = new List<string>(all.Length);
            var clips = new List<AnimationClip>(all.Length);
            var seen = new HashSet<string>();
            foreach (var clip in all)
            {
                if (clip == null) continue;
                var name = clip.name;
                if (!seen.Add(name)) continue;
                names.Add(name);
                clips.Add(clip);
            }
            var keys = names.ToArray();
            var sorted = clips.ToArray();
            System.Array.Sort(keys, sorted, System.StringComparer.OrdinalIgnoreCase);
            _clipsOf = controller;
            _clips = new List<AnimationClip>(sorted);
            _clipNames = new List<string>(keys);
            _clipByName.Clear();
            for (var i = 0; i < keys.Length; i++) _clipByName[keys[i]] = sorted[i];
            return _clips;
        }

        /// <summary>
        /// The names of <see cref="Clips"/>, in the same order: a clip's name is made anew each time
        /// it is asked for, so the panel takes them from here.
        /// </summary>
        public static List<string> ClipNames()
        {
            Clips();
            return _clipNames;
        }

        private static List<string> _clipNames = new List<string>();

        private static readonly Dictionary<string, AnimationClip> _clipByName = new Dictionary<string, AnimationClip>();

        private static readonly List<AnimationClip> NoClips = new List<AnimationClip>();
        private static RuntimeAnimatorController _clipsOf;
        private static List<AnimationClip> _clips;

        /// <summary>
        /// The clip the stage copy's animator plays on its own right now, when Scry plays none:
        /// the strongest of the state it is in or moving to. Null when there is none.
        /// </summary>
        public static AnimationClip AnimatorClipNow()
        {
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null || PlayingClip() != null) return null;
            var infos = animator.IsInTransition(0) ? animator.GetNextAnimatorClipInfo(0) : animator.GetCurrentAnimatorClipInfo(0);
            AnimationClip strongest = null;
            var weight = -1f;
            foreach (var info in infos)
            {
                if (info.clip == null || info.weight <= weight) continue;
                strongest = info.clip;
                weight = info.weight;
            }
            return strongest;
        }

        /// <summary>The clip playing on the stage copy, or null.</summary>
        public static AnimationClip PlayingClip()
        {
            var player = Stage.Subject != null ? Stage.Subject.GetComponent<ClipPlayer>() : null;
            return player != null ? player.Clip : null;
        }

        /// <summary>
        /// Plays a clip on the stage copy and the copy in the world. Started by an effect list's
        /// button (an item's attack), what it plays lights that button too.
        /// </summary>
        public static void PlayClip(AnimationClip clip, object litWith = null)
        {
            _litWith = (clip, litWith);
            var speed = _explorer != null ? _explorer.Modifiers.AnimationSpeed : 1f;
            ClipPlayer.Play(Stage.Subject, clip, Repeat, speed);
            ClipPlayer.Play(_world, clip, Repeat, speed);
            _startedClip = clip;
        }

        private static string _clipOnShow;

        /// <summary>Plays the clip of that name once the next selection is shown, as when going to a creature from one of its animation's sounds.</summary>
        public static void PlayClipOnShow(string name) => _clipOnShow = name;

        /// <summary>Plays the clip asked for by name as the selection showed, once (<see cref="PlayClipOnShow"/>).</summary>
        private static void PlayClipAsked()
        {
            if (_clipOnShow == null) return;
            var clip = ClipNamed(_clipOnShow);
            _clipOnShow = null;
            if (clip != null) PlayClip(clip);
        }

        /// <summary>Forgets the clip started last, so what a list's button starts next can be told.</summary>
        private static void ForgetStartedClip() => _startedClip = null;

        /// <summary>Lets go of what was found out about the clips of the world left's prefabs.</summary>
        private static void ForgetClips()
        {
            ClipPlaysCache.Clear();
            LeadsOf.Clear();
            _gear = default;
            WeaponOf.Clear();
            _clipsOf = null;
            _clips = null;
            _ofList = null;
            _ofListPlays = null;
            _ofListCopy = null;
            _ofListClips = null;
            _plays = null;
            _playsPrefab = null;
            _playsCopy = null;
            _attackClipsOf = null;
            AttackClips.Clear();
            _attackAsked = default;
        }

        /// <summary>Where the clip on the stage is, and how long it is.</summary>
        public static bool ClipPosition(out float time, out float length) => ClipPlayer.Position(Stage.Subject, out time, out length);

        public static bool ClipPaused => ClipPlayer.Paused(Stage.Subject);

        /// <summary>Moves the clip to a time, on the stage and in the world together.</summary>
        public static void SeekClip(float time)
        {
            ClipPlayer.Seek(Stage.Subject, time);
            ClipPlayer.Seek(_world, time);
        }

        public static void PauseClip(bool pause)
        {
            ClipPlayer.Pause(Stage.Subject, pause);
            ClipPlayer.Pause(_world, pause);
        }

        public static void StopClip()
        {
            ClipPlayer.Stop(Stage.Subject);
            ClipPlayer.Stop(_world);
        }

        /// <summary>Makes the clip playing on the stage and in the world play again as it ends, or not.</summary>
        private static void LoopPlayingClip(bool loop)
        {
            ClipPlayer.SetLoop(Stage.Subject, loop);
            ClipPlayer.SetLoop(_world, loop);
        }
    }
}
