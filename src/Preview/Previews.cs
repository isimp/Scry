using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Keeps the previews in step with what is selected, and carries out what the panel's buttons
    /// ask for: showing a copy in the world, playing sounds and effects, flying projectiles and
    /// showing a status effect's visuals on you.
    ///
    /// Everything made here is a <see cref="Ghost"/>: local, unsaved and unseen by anyone else.
    /// Whatever plays in the world is also on a timer, so nothing lingers if it never ends itself.
    /// </summary>
    internal static class Previews
    {
        private const float EffectSeconds = 10f;


        private struct Timed
        {
            public GameObject Thing;
            public float Until;
        }

        private static Explorer _explorer;
        private static int _selectionVersion = -1;
        private static int _modifierVersion = -1;
        private static Entry _entry;
        private static int _builtLevel = 1;
        private static Wear _builtWear;
        private static float _replayAt = -1f;
        private static bool _stageStale;

        private static GameObject _world;
        private static readonly List<GameObject> Pinned = new List<GameObject>();
        private static Vector3 _spot;
        private static Quaternion _facing = Quaternion.identity;

        private static readonly List<Timed> Played = new List<Timed>();
        private static GameObject _sound;
        private static float _soundWaitUntil;
        private static bool _soundPaused;
        private static Entry _soundEntry;
        private static readonly List<GameObject> StatusVisuals = new List<GameObject>();
        private static StatusEffect _status;

        /// <summary>Whether the selected model is also shown in the world, where you were looking.</summary>
        public static bool InWorld { get; private set; }

        /// <summary>Plays an effect on the stage again as soon as it ends.</summary>
        public static bool LoopEffects = true;

        /// <summary>Plays a sound again as soon as it ends.</summary>
        public static bool LoopSounds;

        public static float ProjectileSpeed = 40f;

        public static bool SoundPlaying => _sound != null;
        public static bool StatusShowing => _status != null && StatusVisuals.Count > 0;
        public static int PinnedCount => Pinned.Count;
        public static bool AnythingInWorld => _world != null || Pinned.Count > 0 || Played.Count > 0 || _sound != null || StatusVisuals.Count > 0;

        /// <summary>How many things Scry has out in the world or playing, for the Clear button.</summary>
        public static int OutCount => (_world != null ? 1 : 0) + Pinned.Count + Played.Count + (_sound != null ? 1 : 0) + StatusVisuals.Count;

        public static void Update(Explorer explorer)
        {
            Expire();

            if (explorer == null) return;

            if (explorer != _explorer)
            {
                _explorer = explorer;
                _selectionVersion = -1;
            }

            var modifiers = explorer.Modifiers;
            if (explorer.SelectionVersion != _selectionVersion)
            {
                _selectionVersion = explorer.SelectionVersion;
                _modifierVersion = modifiers.Version;
                _stageStale = false;
                Selected(explorer.Selected, modifiers);
            }
            else if (modifiers.Version != _modifierVersion)
            {
                _modifierVersion = modifiers.Version;
                Modified(modifiers);
            }

            if (_stageStale)
            {
                _stageStale = false;
                Stage.Show(_entry, modifiers);
            }

            Repeat(modifiers);
        }

        /// <summary>
        /// Takes the stage copy down while the panel is closed, so a looping effect does not go on
        /// sounding in your ears. It is put back when the panel opens again.
        /// </summary>
        public static void Suspend()
        {
            Stage.ClearSubject();
            _stageStale = true;
        }

        private static void Selected(Entry entry, Modifiers modifiers)
        {
            _entry = entry;
            _builtLevel = modifiers.Level;
            _builtWear = modifiers.Wear;
            _replayAt = -1f;

            StopSound();
            Stage.Show(entry, modifiers);
            if (InWorld) RebuildWorld(modifiers);

            if (entry != null && entry.Kind == Kind.Sound && Plugin.PlayOnSelect) PlaySound(entry);
        }

        private static void Modified(Modifiers modifiers)
        {
            if (modifiers.Level != _builtLevel || modifiers.Wear != _builtWear)
            {
                _builtLevel = modifiers.Level;
                _builtWear = modifiers.Wear;
                Stage.Show(_entry, modifiers);
                if (InWorld) RebuildWorld(modifiers);
                return;
            }

            Stage.Apply(modifiers);
            ApplyLive(_world, modifiers);
        }

        /// <summary>Loops a finished effect on the stage, and a finished sound, while the panel is open.</summary>
        private static void Repeat(Modifiers modifiers)
        {
            if (_entry == null) return;

            if (LoopEffects && _entry.Kind == Kind.Effect && Stage.Finished)
            {
                if (_replayAt < 0f) _replayAt = Time.unscaledTime + 0.4f;
                else if (Time.unscaledTime >= _replayAt)
                {
                    _replayAt = -1f;
                    Stage.Show(_entry, modifiers);
                }
            }

            // A sound stopped by hand is not replayed; one that ran out is.
            // A chosen variant repeats as itself; a random play picks again each time, as in the game.
            if (LoopSounds && _entry.Kind == Kind.Sound && _soundEntry == _entry && _sound == null) PlaySound(_entry, _soundChosen);
        }

        // ----- The copy in the world -----

        public static bool IsModel(Entry entry)
        {
            return entry != null && !entry.Empty && entry.Source is GameObject
                   && entry.Kind != Kind.Sound && entry.Kind != Kind.Effect && entry.Kind != Kind.StatusEffect;
        }

        /// <summary>Shows the selection in the world where you are looking, or takes it away again.</summary>
        public static void ToggleWorld()
        {
            if (InWorld)
            {
                InWorld = false;
                Destroy(ref _world);
                return;
            }

            InWorld = true;
            Aim();
            if (_explorer != null) RebuildWorld(_explorer.Modifiers);
        }

        /// <summary>Moves the copy in the world to where you are looking now.</summary>
        public static void PlaceHere()
        {
            Aim();
            if (_world == null) return;
            _world.transform.position = _spot;
            _world.transform.rotation = _facing;
        }

        /// <summary>Leaves the copy in the world standing, so another can be shown beside it.</summary>
        public static void Pin()
        {
            if (_world == null) return;
            Pinned.Add(_world);
            _world = null;
            InWorld = false;
        }

        /// <summary>Removes everything Scry has put in the world.</summary>
        public static void ClearWorld()
        {
            InWorld = false;
            Destroy(ref _world);
            foreach (var pinned in Pinned) if (pinned != null) Object.Destroy(pinned);
            Pinned.Clear();
            foreach (var played in Played) if (played.Thing != null) Object.Destroy(played.Thing);
            Played.Clear();
            StopSound();
            StopStatus(false);
        }

        private static void RebuildWorld(Modifiers modifiers)
        {
            Destroy(ref _world);
            if (!IsModel(_entry)) return;

            var prefab = (GameObject)_entry.Source;
            _world = Ghost.Make(prefab, null, _spot, _facing);
            if (_world == null) return;

            if (_entry.Kind == Kind.Creature) Looks.ApplyLevel(prefab, _world, modifiers.Level);
            if (modifiers.WearAvailable) Looks.ApplyWear(prefab, _world, modifiers.Wear);
            _world.AddComponent<Keep>().BaseScale = _world.transform.localScale;
            ApplyLive(_world, modifiers);
        }

        private static void ApplyLive(GameObject copy, Modifiers modifiers)
        {
            if (copy == null) return;
            var keep = copy.GetComponent<Keep>();
            if (keep != null) copy.transform.localScale = keep.BaseScale * modifiers.Scale;
            foreach (var animator in copy.GetComponentsInChildren<Animator>(true)) animator.speed = modifiers.AnimationSpeed;
            ClipPlayer.SetSpeed(copy, modifiers.AnimationSpeed);
        }

        /// <summary>
        /// Where you are looking: the first ground or building along the camera's view, or a few
        /// metres in front of you when there is none. Copies there face you.
        /// </summary>
        private static void Aim()
        {
            var player = Player.m_localPlayer;
            if (player == null) return;

            var view = GameCamera.instance != null ? GameCamera.instance.transform : player.transform;
            var mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");

            if (Physics.Raycast(view.position, view.forward, out var hit, 60f, mask, QueryTriggerInteraction.Ignore))
            {
                _spot = hit.point;
            }
            else
            {
                _spot = player.transform.position + player.transform.forward * 4f;
                if (ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(_spot, out var ground)) _spot.y = ground;
            }

            var toPlayer = player.transform.position - _spot;
            toPlayer.y = 0f;
            _facing = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer) : Quaternion.identity;
        }

        // ----- Sounds -----

        /// <summary>
        /// Plays a sound at your ears. It follows the camera, so it stays with you. Without a clip
        /// the game picks one of the sound's variants at random, as it does in play; with one,
        /// exactly that variant plays, still with the sound's own volume, pitch and mix.
        /// </summary>
        public static void PlaySound(Entry entry, AudioClip only = null)
        {
            StopSound();
            if (!(entry?.Source is GameObject prefab)) return;

            var ears = GameCamera.instance != null ? GameCamera.instance.transform : Player.m_localPlayer?.transform;
            if (ears == null) return;

            _sound = Ghost.Make(prefab, ears, ears.position, ears.rotation);
            if (_sound == null) return;

            _soundEntry = entry;
            _soundChosen = only;
            _soundPaused = false;
            _soundWaitUntil = Time.unscaledTime + MaxDelay(prefab) + 0.5f;
            if (only != null) Narrow(_sound, only);
        }

        /// <summary>
        /// Leaves the copy only the chosen clip to pick from. The game's sound script picks its clip
        /// when it starts, a frame after the copy wakes, so narrowing its list here is enough. A
        /// plain audio source has already started, so it is restarted with the clip.
        /// </summary>
        private static void Narrow(GameObject copy, AudioClip clip)
        {
            var scripted = false;
            foreach (var sfx in copy.GetComponentsInChildren<ZSFX>(true))
            {
                if (sfx.m_audioClips == null || System.Array.IndexOf(sfx.m_audioClips, clip) < 0)
                {
                    // Another part of the same sound; left quiet so only the chosen variant is heard.
                    sfx.m_playOnAwake = false;
                    continue;
                }
                sfx.m_audioClips = new[] { clip };
                scripted = true;
            }
            if (scripted) return;

            foreach (var source in copy.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip != clip) continue;
                source.Stop();
                source.clip = clip;
                source.Play();
                return;
            }
        }

        private static float MaxDelay(GameObject prefab)
        {
            var delay = 0f;
            foreach (var sfx in prefab.GetComponentsInChildren<ZSFX>(true)) delay = Mathf.Max(delay, sfx.m_maxDelay);
            return delay;
        }

        public static void StopSound()
        {
            _soundEntry = null;
            _soundPaused = false;
            _soundChosen = null;
            Destroy(ref _sound);
        }

        private static AudioClip _soundChosen;

        /// <summary>
        /// The clip the playing sound is actually playing, whether chosen or picked at random, so
        /// the panel can show which variant was heard. Null when nothing plays.
        /// </summary>
        public static AudioClip SoundClipNow()
        {
            if (_sound == null) return null;
            foreach (var source in _sound.GetComponentsInChildren<AudioSource>())
            {
                if (source.isPlaying && source.clip != null) return source.clip;
            }
            return _soundChosen;
        }

        /// <summary>Every clip a sound can play, each once, in the order the sound lists them.</summary>
        public static List<AudioClip> SoundVariants(GameObject prefab)
        {
            var clips = new List<AudioClip>();
            if (prefab == null) return clips;

            foreach (var sfx in prefab.GetComponentsInChildren<ZSFX>(true))
            {
                if (sfx.m_audioClips == null) continue;
                foreach (var clip in sfx.m_audioClips) if (clip != null && !clips.Contains(clip)) clips.Add(clip);
            }
            foreach (var source in prefab.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip != null && !clips.Contains(source.clip)) clips.Add(source.clip);
            }
            return clips;
        }

        /// <summary>
        /// Whether the sound copy is still worth keeping: something on it plays or is paused, or it
        /// is still inside the random delay some sounds wait before starting.
        /// </summary>
        private static bool SoundAlive(float now)
        {
            if (_sound == null) return false;
            if (_soundPaused) return true;
            foreach (var source in _sound.GetComponentsInChildren<AudioSource>())
            {
                if (source.isPlaying) return true;
            }
            return now < _soundWaitUntil;
        }

        /// <summary>The audio source on the playing sound that holds its clip.</summary>
        private static AudioSource SoundSource()
        {
            if (_sound == null) return null;
            AudioSource holding = null;
            foreach (var source in _sound.GetComponentsInChildren<AudioSource>())
            {
                if (source.clip == null) continue;
                if (source.isPlaying) return source;
                if (holding == null) holding = source;
            }
            return holding;
        }

        /// <summary>Where the playing sound is and how long its clip is, for the panel's timeline.</summary>
        public static bool SoundPosition(out float time, out float length)
        {
            time = 0f;
            length = 0f;
            var source = SoundSource();
            if (source == null || source.clip == null || (!source.isPlaying && !_soundPaused)) return false;
            time = source.time;
            length = source.clip.length;
            return length > 0f;
        }

        /// <summary>Jumps the playing sound to a point in its clip.</summary>
        public static void SeekSound(float time)
        {
            var source = SoundSource();
            if (source == null || source.clip == null) return;
            source.time = Mathf.Clamp(time, 0f, Mathf.Max(0f, source.clip.length - 0.05f));
        }

        public static bool SoundPaused => _soundPaused;

        public static void PauseSound(bool pause)
        {
            var source = SoundSource();
            if (source == null) return;
            if (pause) source.Pause();
            else source.UnPause();
            _soundPaused = pause;
        }

        // ----- Effects -----

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
        }

        /// <summary>Plays every prefab of an effect list at a point, as the game would on a hit.</summary>
        public static void PlayList(EffectList list, Vector3 position, Quaternion rotation)
        {
            if (list?.m_effectPrefabs == null) return;
            foreach (var data in list.m_effectPrefabs)
            {
                if (data == null || !data.m_enabled || data.m_prefab == null) continue;
                Remember(Ghost.Make(data.m_prefab, null, position, rotation), EffectSeconds);
            }
        }

        // ----- Projectiles -----

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

        // ----- Status effects -----

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

        /// <summary>Plays one of a status effect's lists on you once.</summary>
        public static void PlayOnYou(EffectList list)
        {
            foreach (var copy in OnYou(list)) Remember(copy, EffectSeconds);
        }

        private static bool HasAny(EffectList list)
        {
            if (list?.m_effectPrefabs == null) return false;
            foreach (var data in list.m_effectPrefabs)
            {
                if (data != null && data.m_enabled && data.m_prefab != null) return true;
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
                if (data == null || !data.m_enabled || data.m_prefab == null) continue;

                var anchor = player.transform;
                if (!string.IsNullOrEmpty(data.m_childTransform))
                {
                    var child = Utils.FindChild(anchor, data.m_childTransform);
                    if (child != null) anchor = child;
                }

                var parent = data.m_attach || data.m_follow ? anchor : null;
                var copy = Ghost.Make(data.m_prefab, parent, anchor.position, anchor.rotation);
                if (copy != null) made.Add(copy);
            }
            return made;
        }

        // ----- Animation -----

        /// <summary>Plays the clip again as soon as it ends.</summary>
        public static bool LoopClips;

        /// <summary>The animation clips the stage copy's animator has, by name.</summary>
        public static List<AnimationClip> Clips()
        {
            var clips = new List<AnimationClip>();
            var animator = ClipPlayer.AnimatorOf(Stage.Subject);
            if (animator == null) return clips;

            var seen = new HashSet<string>();
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && seen.Add(clip.name)) clips.Add(clip);
            }
            clips.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));
            return clips;
        }

        /// <summary>The clip playing on the stage copy, or null.</summary>
        public static AnimationClip PlayingClip()
        {
            var player = Stage.Subject != null ? Stage.Subject.GetComponent<ClipPlayer>() : null;
            return player != null ? player.Clip : null;
        }

        public static void PlayClip(AnimationClip clip)
        {
            var speed = _explorer != null ? _explorer.Modifiers.AnimationSpeed : 1f;
            ClipPlayer.Play(Stage.Subject, clip, LoopClips, speed);
            ClipPlayer.Play(_world, clip, LoopClips, speed);
        }

        public static void StopClip()
        {
            ClipPlayer.Stop(Stage.Subject);
            ClipPlayer.Stop(_world);
        }

        public static void ToggleLoopClips()
        {
            LoopClips = !LoopClips;
            ClipPlayer.SetLoop(Stage.Subject, LoopClips);
            ClipPlayer.SetLoop(_world, LoopClips);
        }

        /// <summary>Restarts what is on the stage, for effects that have played out.</summary>
        public static void Replay()
        {
            if (_explorer != null) Stage.Show(_entry, _explorer.Modifiers);
        }

        // ----- Housekeeping -----

        private static void Remember(GameObject thing, float seconds)
        {
            if (thing != null) Played.Add(new Timed { Thing = thing, Until = Time.unscaledTime + seconds });
        }

        private static void Expire()
        {
            var now = Time.unscaledTime;
            for (var i = Played.Count - 1; i >= 0; i--)
            {
                var played = Played[i];
                if (played.Thing != null && now < played.Until) continue;
                if (played.Thing != null) Object.Destroy(played.Thing);
                Played.RemoveAt(i);
            }

            if (_sound != null && !SoundAlive(now)) Destroy(ref _sound);
            for (var i = StatusVisuals.Count - 1; i >= 0; i--) if (StatusVisuals[i] == null) StatusVisuals.RemoveAt(i);
            Pinned.RemoveAll(p => p == null);
        }

        private static void Destroy(ref GameObject thing)
        {
            if (thing != null) Object.Destroy(thing);
            thing = null;
        }
    }
}
