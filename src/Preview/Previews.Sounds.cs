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
            _soundTakenOver = false;
            _soundWaitUntil = Time.unscaledTime + MaxDelay(prefab) + 0.5f;
            if (only != null) Narrow(_sound, only);
            StartUnscripted(_sound, prefab);
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
            TakeOverSound();
            source.time = Mathf.Clamp(time, 0f, Mathf.Max(0f, source.clip.length - 0.05f));
        }

        public static bool SoundPaused => _soundPaused;

        public static void PauseSound(bool pause)
        {
            var source = SoundSource();
            if (source == null) return;
            TakeOverSound();
            if (pause) source.Pause();
            else source.UnPause();
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
