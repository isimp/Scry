using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>Sounds played at your ears: one variant or the game's pick, paused, sought and taken over from the game's own script.</summary>
    internal static partial class Previews
    {
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
            _seekWhilePaused = null;
            _seekOnGoing = null;
            _held = null;
            _soundTakenOver = false;
            _soundWaitUntil = Time.unscaledTime + MaxDelay(prefab) + 0.5f;
            if (only != null) Narrow(_sound, only);
            StartUnscripted(_sound, prefab);
        }

        /// <summary>
        /// Plays the music of the location or room shown, or stops it when it plays already, and
        /// says what it did. It plays once its model has loaded and been read (<see cref="MusicPreview"/>).
        /// </summary>
        public static string PlacesMusic(Entry entry)
        {
            if (entry?.Source is RandomEvent raid) return EventMusic(entry, raid);
            if (entry?.Source is BiomeSource biome)
            {
                var first = BiomeWords.Music(biome.Morning, biome.Evening, biome.Day, biome.Night).Select(m => m.Music).OrderBy(m => m == biome.Day ? 0 : 1).FirstOrDefault();
                if (MusicPreview.PlayingFor == entry)
                {
                    StopSound();
                    return "Stopped its music.";
                }
                return first != null ? NamedMusic(entry, first) : "It has no music of its own.";
            }
            if (!(entry?.Source is PlaceSource place)) return null;
            if (MusicPreview.PlayingFor == entry)
            {
                StopSound();
                return "Stopped its music.";
            }
            if (place.Contents == null) return "Its music is known once its model has loaded.";
            if (place.Contents.Music.Count == 0) return "It has no music of its own.";
            StopSound();
            var played = MusicPreview.Play(entry, PlaceAssets.Asset(place), place.Contents.Music);
            return played != null ? $"Playing {played}; Enter again stops it." : "Its music could not be found.";
        }

        /// <summary>A piece of the game's music by its name, played for an entry, or stopped when it plays already.</summary>
        public static string NamedMusic(Entry entry, string name)
        {
            if (MusicPreview.PlayingFor == entry && MusicPreview.Playing == name)
            {
                StopSound();
                return "Stopped its music.";
            }
            StopSound();
            var played = MusicPreview.Play(entry, null, new List<PlaceMusic> { new PlaceMusic { Name = name, When = MusicWhen.Inside } });
            return played != null ? $"Playing {Naming.FieldLabel(played)}; click it again to stop it." : "Its music could not be found.";
        }

        /// <summary>The music a raid or a boss's fight forces (<c>RandomEvent.m_forceMusic</c>), from the game's music list, or stopped when it plays already.</summary>
        private static string EventMusic(Entry entry, RandomEvent raid)
        {
            if (MusicPreview.PlayingFor == entry)
            {
                StopSound();
                return "Stopped its music.";
            }
            if (string.IsNullOrEmpty(raid.m_forceMusic)) return "It has no music of its own.";
            StopSound();
            var played = MusicPreview.Play(entry, null, new List<PlaceMusic> { new PlaceMusic { Name = raid.m_forceMusic, When = MusicWhen.Inside } });
            return played != null ? $"Playing {Naming.FieldLabel(played)}; click it or press Enter again to stop it." : "Its music could not be found.";
        }

        /// <summary>
        /// Starts audio that the game would start from a script the copy no longer has. Location
        /// music is the main case: its source does not play on its own, and <c>MusicLocation</c>
        /// starts it when you come near, at your music volume. Sounds the game's sound script plays
        /// are left to it.
        /// </summary>
        private static void StartUnscripted(GameObject copy, GameObject prefab)
        {
            if (copy.GetComponentInChildren<ZSFX>(true) != null) return;

            var music = prefab.GetComponentInChildren<MusicLocation>(true) != null;
            foreach (var source in copy.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip == null || source.isPlaying) continue;
                if (music) source.volume *= MusicMan.m_masterMusicVolume;
                source.time = 0f;
                source.Play();
            }
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
            MusicPreview.Stop();
            _soundEntry = null;
            _soundPaused = false;
            _seekWhilePaused = null;
            _seekOnGoing = null;
            _held = null;
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
        /// <summary>
        /// The source the sound plays through: the one found playing, held on to while it is
        /// paused or sought, so a prefab with several sources is paused, sought and resumed on
        /// the same one.
        /// </summary>
        private static AudioSource SoundSource()
        {
            if (_sound == null) return null;
            if (_held != null && _held.clip != null && _held.transform.IsChildOf(_sound.transform)) return _held;
            AudioSource holding = null;
            foreach (var source in _sound.GetComponentsInChildren<AudioSource>())
            {
                if (source.clip == null) continue;
                if (source.isPlaying) return _held = source;
                if (holding == null) holding = source;
            }
            return holding;
        }

        private static AudioSource _held;

        /// <summary>The playing sound's source and how its clip loads, for the self-test to tell.</summary>
        public static string SoundSourceTold()
        {
            var source = SoundSource();
            return source == null ? "none" : $"{source.name}, {source.clip.name} ({source.clip.loadType}), {_sound.GetComponentsInChildren<AudioSource>().Length} sources";
        }

        /// <summary>Where the playing sound is and how long its clip is, for the panel's timeline.</summary>
        public static bool SoundPosition(out float time, out float length)
        {
            time = 0f;
            length = 0f;
            var source = SoundSource();
            if (source == null || source.clip == null || (!source.isPlaying && !_soundPaused)) return false;
            time = _soundPaused && _seekWhilePaused.HasValue ? _seekWhilePaused.Value : source.time;
            length = source.clip.length;
            return length > 0f;
        }

        /// <summary>Jumps the playing sound to a point in its clip.</summary>
        public static void SeekSound(float time)
        {
            var source = SoundSource();
            if (source == null || source.clip == null) return;
            TakeOverSound();
            var at = Mathf.Clamp(time, 0f, Mathf.Max(0f, source.clip.length - 0.05f));
            source.time = at;
            // A point set while paused is kept and played from on going on, as a streamed clip
            // (music) can lose it otherwise.
            _seekWhilePaused = _soundPaused ? at : (float?)null;
        }

        private static float? _seekWhilePaused;

        /// <summary>A point sought while paused, set again as the sound goes on until the clip is there, for two seconds at most.</summary>
        private static float? _seekOnGoing;
        private static float _seekOnGoingUntil;
        private static float _seekAgainAt;

        /// <summary>How long a streamed clip is given to get to a point set before it is set again.</summary>
        private const float SeekAgainAfter = 0.1f;

        /// <summary>How far past the point sought the sound must have played for the point to have held.</summary>
        private const float HeldPast = 0.3f;

        /// <summary>Where the sound was each frame while a point sought while paused was being settled, for the self-test to tell.</summary>
        public static readonly List<string> SeekTrail = new List<string>();

        /// <summary>
        /// Each frame: a point sought while paused is watched until the sound has played on past
        /// it, and set again, a tenth of a second apart at the most, while it is before it: a
        /// streamed clip (music) reads back the point at once, yet can start from its beginning.
        /// </summary>
        public static void SettleSeek()
        {
            if (_seekOnGoing == null) return;
            var source = SoundSource();
            if (source == null || source.clip == null || Time.unscaledTime > _seekOnGoingUntil)
            {
                _seekOnGoing = null;
                return;
            }
            if (SeekTrail.Count < 60) SeekTrail.Add($"{source.time:0.00} s, sample {source.timeSamples}{(source.isPlaying ? "" : ", not playing")}");
            if (!source.isPlaying) return;
            if (source.time >= _seekOnGoing.Value + HeldPast)
            {
                _seekOnGoing = null;
                return;
            }
            if (source.time + 0.1f >= _seekOnGoing.Value || Time.unscaledTime < _seekAgainAt) return;
            SetPoint(source, _seekOnGoing.Value);
            _seekAgainAt = Time.unscaledTime + SeekAgainAfter;
        }

        /// <summary>Sets where a source plays from, by its samples as well as its time: a streamed clip may keep only the one.</summary>
        private static void SetPoint(AudioSource source, float time)
        {
            source.time = time;
            if (source.clip != null && source.clip.frequency > 0)
            {
                source.timeSamples = Mathf.Clamp(Mathf.RoundToInt(time * source.clip.frequency), 0, Mathf.Max(0, source.clip.samples - 1));
            }
        }

        public static bool SoundPaused => _soundPaused;

        public static void PauseSound(bool pause)
        {
            var source = SoundSource();
            if (source == null) return;

            // Pausing a sound that has just ended, as a click can land just after, would keep it
            // paused at its end for good, with nothing left to resume; it is let end instead.
            if (pause && !source.isPlaying) return;
            TakeOverSound();
            if (pause) source.Pause();
            else if (_seekWhilePaused.HasValue)
            {
                // Played again from the point sought, set again once playing and each frame after
                // until the clip is there: a streamed clip (music) can start over from its beginning.
                source.Stop();
                SetPoint(source, _seekWhilePaused.Value);
                source.Play();
                SetPoint(source, _seekWhilePaused.Value);
                _seekOnGoing = _seekWhilePaused.Value;
                _seekOnGoingUntil = Time.unscaledTime + 2f;
                _seekAgainAt = Time.unscaledTime + SeekAgainAfter;
                _seekWhilePaused = null;
                SeekTrail.Clear();
            }
            else source.UnPause();
            _held = source;
            _soundPaused = pause;
        }

        private static bool _soundTakenOver;
        private static AccessTools.FieldRef<ZSFX, float> _fadeOutTimer;
        private static bool _fadeOutTimerTried;

        /// <summary>
        /// Once the sound is skipped through or paused, its end is Scry's to decide. The game ends
        /// sounds by time played rather than by where the clip is: a timed destruction on the
        /// prefab removes the copy after a fixed while, and the sound script can fade out and stop
        /// after a delay. Both are called off, so the sound runs until its clip really ends, is
        /// stopped, or is cleared.
        /// </summary>
        private static void TakeOverSound()
        {
            if (_sound == null || _soundTakenOver) return;
            _soundTakenOver = true;

            foreach (var timer in _sound.GetComponentsInChildren<TimedDestruction>(true))
            {
                timer.CancelInvoke();
                Object.Destroy(timer);
            }

            if (!_fadeOutTimerTried)
            {
                _fadeOutTimerTried = true;
                try
                {
                    _fadeOutTimer = AccessTools.FieldRefAccess<ZSFX, float>("m_fadeOutTimer");
                }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogDebug($"Scry cannot reach the sound fade timer: {ex.Message}");
                }
            }

            foreach (var sfx in _sound.GetComponentsInChildren<ZSFX>(true))
            {
                sfx.m_fadeOutOnAwake = false;
                if (_fadeOutTimer != null) _fadeOutTimer(sfx) = -1f;
            }
        }
    }
}
