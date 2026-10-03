using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The sound playing at your ears (<see cref="Previews.PlaySound"/>): its copy, which follows
    /// the camera, the variant chosen, whether it is paused, the source it plays through, a point
    /// sought (<see cref="SeekWatch"/>), and whether its end is Scry's to decide. It keeps all of it
    /// and lets go of it itself.
    /// </summary>
    internal sealed class SoundPreview
    {
        /// <summary>The copy playing; null when none plays.</summary>
        public GameObject Copy;

        /// <summary>The entry played, and the variant chosen, kept after the copy has gone for playing again.</summary>
        public Entry Entry { get; private set; }
        public AudioClip Chosen { get; private set; }

        public bool Paused { get; private set; }

        private float _waitUntil;
        private AudioSource _held;
        private bool _takenOver;
        private readonly SeekWatch _seek = new SeekWatch();

        private static AccessTools.FieldRef<ZSFX, float> _fadeOutTimer;
        private static bool _fadeOutTimerTried;

        /// <summary>
        /// Plays a sound at the ears given. Without a clip the game picks one of the sound's
        /// variants at random, as it does in play; with one, exactly that variant plays, still with
        /// the sound's own volume, pitch and mix.
        /// </summary>
        public void Play(Entry entry, GameObject prefab, AudioClip only, Transform ears)
        {
            Copy = Ghost.Make(prefab, ears, ears.position, ears.rotation);
            if (Copy == null) return;

            Entry = entry;
            Chosen = only;
            Paused = false;
            _seek.Forget();
            _held = null;
            _takenOver = false;
            _waitUntil = Time.unscaledTime + MaxDelay(prefab) + 0.5f;
            if (only != null) Narrow(Copy, only);
            StartUnscripted(Copy, prefab);
        }

        /// <summary>Stops it and lets go of all of it.</summary>
        public void Stop()
        {
            Entry = null;
            Paused = false;
            _seek.Forget();
            _held = null;
            Chosen = null;
            if (Copy != null) Object.Destroy(Copy);
            Copy = null;
        }

        /// <summary>Takes the copy down once it has played out, keeping what was played for playing again.</summary>
        public void Drop()
        {
            if (Copy != null) Object.Destroy(Copy);
            Copy = null;
        }

        /// <summary>Lets go of the entry played, for a world that was left.</summary>
        public void ForgetEntry() => Entry = null;

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

        /// <summary>
        /// The clip it is actually playing, whether chosen or picked at random, so the panel can
        /// show which variant was heard. Null when nothing plays.
        /// </summary>
        public AudioClip ClipNow()
        {
            if (Copy == null) return null;
            foreach (var source in Copy.GetComponentsInChildren<AudioSource>())
            {
                if (source.isPlaying && source.clip != null) return source.clip;
            }
            return Chosen;
        }

        /// <summary>
        /// Whether the copy is still worth keeping: something on it plays or is paused, or it is
        /// still inside the random delay some sounds wait before starting.
        /// </summary>
        public bool Alive(float now)
        {
            if (Copy == null) return false;
            if (Paused) return true;
            foreach (var source in Copy.GetComponentsInChildren<AudioSource>())
            {
                if (source.isPlaying) return true;
            }
            return now < _waitUntil;
        }

        /// <summary>
        /// The source it plays through: the one found playing, held on to while it is paused or
        /// sought, so a prefab with several sources is paused, sought and resumed on the same one.
        /// </summary>
        private AudioSource Source()
        {
            if (Copy == null) return null;
            if (_held != null && _held.clip != null && _held.transform.IsChildOf(Copy.transform)) return _held;
            AudioSource holding = null;
            foreach (var source in Copy.GetComponentsInChildren<AudioSource>())
            {
                if (source.clip == null) continue;
                if (source.isPlaying) return _held = source;
                if (holding == null) holding = source;
            }
            return holding;
        }

        /// <summary>Its source and how its clip loads, for the self-test to tell.</summary>
        public string SourceTold()
        {
            var source = Source();
            return source == null ? "none" : $"{source.name}, {source.clip.name} ({source.clip.loadType}), {Numbers.Count(Copy.GetComponentsInChildren<AudioSource>().Length)} sources";
        }

        /// <summary>Where it is and how long its clip is, for the panel's timeline.</summary>
        public bool Position(out float time, out float length)
        {
            time = 0f;
            length = 0f;
            var source = Source();
            if (source == null || source.clip == null || (!source.isPlaying && !Paused)) return false;
            time = Paused && _seek.WhilePaused.HasValue ? _seek.WhilePaused.Value : source.time;
            length = source.clip.length;
            return length > 0f;
        }

        /// <summary>Jumps to a point in its clip; a point set while paused is kept and played from on going on, as a streamed clip (music) can lose it otherwise.</summary>
        public void SeekTo(float time)
        {
            var source = Source();
            if (source == null || source.clip == null) return;
            TakeOver();
            var at = SeekWatch.Clamp(time, source.clip.length);
            source.time = at;
            _seek.Sought(at, Paused);
        }

        /// <summary>
        /// Each frame: a point sought while paused is watched until the sound has played on past
        /// it, and set again, a tenth of a second apart at the most, while it is before it: a
        /// streamed clip (music) reads back the point at once, yet can start from its beginning.
        /// Where the sound was is noted in the trail given, for the self-test.
        /// </summary>
        public void Settle(List<string> trail)
        {
            if (_seek.Watching == null) return;
            var point = _seek.Watching.Value;
            var source = Source();
            if (source == null || source.clip == null)
            {
                _seek.StopWatching();
                return;
            }
            var step = _seek.Step(source.time, source.isPlaying, Time.unscaledTime);
            if (step != SeekStep.Expired && trail.Count < 60) trail.Add($"{Numbers.Fixed(source.time, 2)} s, sample {Numbers.Count(source.timeSamples)}{(source.isPlaying ? "" : ", not playing")}");
            if (step == SeekStep.SetAgain) SetPoint(source, point);
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

        /// <summary>Pauses it, or goes on, from the point sought while paused where there is one; the trail given starts again then.</summary>
        public void Pause(bool pause, List<string> trail)
        {
            var source = Source();
            if (source == null) return;

            // Pausing a sound that has just ended, as a click can land just after, would keep it
            // paused at its end for good, with nothing left to resume; it is let end instead.
            if (pause && !source.isPlaying) return;
            TakeOver();
            if (pause) source.Pause();
            else if (_seek.GoOn(Time.unscaledTime) is float at)
            {
                // Played again from the point sought, set again once playing and each frame after
                // until the clip is there: a streamed clip (music) can start over from its beginning.
                source.Stop();
                SetPoint(source, at);
                source.Play();
                SetPoint(source, at);
                trail.Clear();
            }
            else source.UnPause();
            _held = source;
            Paused = pause;
        }

        /// <summary>
        /// Once the sound is skipped through or paused, its end is Scry's to decide. The game ends
        /// sounds by time played rather than by where the clip is: a timed destruction on the
        /// prefab removes the copy after a fixed while, and the sound script can fade out and stop
        /// after a delay. Both are called off, so the sound runs until its clip really ends, is
        /// stopped, or is cleared.
        /// </summary>
        private void TakeOver()
        {
            if (Copy == null || _takenOver) return;
            _takenOver = true;

            foreach (var timer in Copy.GetComponentsInChildren<TimedDestruction>(true))
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

            foreach (var sfx in Copy.GetComponentsInChildren<ZSFX>(true))
            {
                sfx.m_fadeOutOnAwake = false;
                if (_fadeOutTimer != null) _fadeOutTimer(sfx) = -1f;
            }
        }
    }
}
