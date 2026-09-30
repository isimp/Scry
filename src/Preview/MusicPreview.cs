using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A location's or room's music, played at your ears through the game's music mix at your
    /// music volume, once through. The game's own music is muted meanwhile rather than stopped,
    /// so it goes on where it is and is heard again when this ends, is stopped, or another
    /// entry is selected. A source of the place's own comes from its loaded model; a piece named
    /// for stepping inside or for its weather comes from the game's music list (<c>MusicMan.m_music</c>).
    /// </summary>
    internal static class MusicPreview
    {
        /// <summary>Leaving a world stops it (<see cref="WorldCaches"/>).</summary>
        static MusicPreview() => WorldCaches.Register(nameof(MusicPreview), Stop);

        private static GameObject _player;
        private static AudioSource _source;
        private static readonly List<AudioSource> Muted = new List<AudioSource>();

        /// <summary>The entry whose music plays, or null.</summary>
        public static Entry PlayingFor { get; private set; }

        /// <summary>The name of the music playing, or null.</summary>
        public static string Playing { get; private set; }

        /// <summary>Whether its clip is sounding now.</summary>
        public static bool Sounding => _source != null && _source.isPlaying;

        /// <summary>Plays the first of a place's music that can be found; the name played, or null when none could be.</summary>
        public static string Play(Entry entry, GameObject model, IReadOnlyList<PlaceMusic> music)
        {
            Stop();
            if (music == null) return null;
            foreach (var tune in music)
            {
                if (!Find(tune, model, out var clip, out var volume)) continue;
                Start(clip, volume);
                PlayingFor = entry;
                Playing = tune.Name;
                return tune.Name;
            }
            return null;
        }

        /// <summary>Stops the music and gives the game's back.</summary>
        public static void Stop()
        {
            if (_source != null) _source.Stop();
            foreach (var source in Muted) if (source != null) source.mute = false;
            Muted.Clear();
            PlayingFor = null;
            Playing = null;
        }

        /// <summary>Gives the game's music back once this has played through.</summary>
        public static void Update()
        {
            if (Playing != null && !Sounding) Stop();
        }

        private static bool Find(PlaceMusic tune, GameObject model, out AudioClip clip, out float volume)
        {
            clip = null;
            volume = 1f;
            if (tune.When == MusicWhen.Near)
            {
                if (model == null) return false;
                foreach (var near in model.GetComponentsInChildren<MusicLocation>(true))
                {
                    var own = near != null ? near.GetComponent<AudioSource>() : null;
                    if (own == null || own.clip == null || PlaceReader.PrefabName(near.gameObject.name) != tune.Name) continue;
                    clip = own.clip;
                    volume = own.volume;
                    return true;
                }
                return false;
            }

            var named = MusicMan.instance != null ? MusicMan.instance.m_music : null;
            var piece = named?.Find(m => m != null && m.m_enabled && m.m_name == tune.Name && m.m_clips != null && m.m_clips.Length > 0);
            if (piece == null) return false;
            clip = piece.m_clips[Random.Range(0, piece.m_clips.Length)];
            volume = piece.m_volume;
            return clip != null;
        }

        private static void Start(AudioClip clip, float volume)
        {
            if (_source == null)
            {
                _player = new GameObject("Scry music");
                Object.DontDestroyOnLoad(_player);
                _source = _player.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.loop = false;
                _source.spatialBlend = 0f;
                _source.priority = 0;
                _source.bypassReverbZones = true;
            }
            if (MusicMan.instance != null)
            {
                _source.outputAudioMixerGroup = MusicMan.instance.m_musicMixer;
                foreach (var game in MusicMan.instance.GetComponentsInChildren<AudioSource>(true))
                {
                    if (game == null || game.mute) continue;
                    game.mute = true;
                    Muted.Add(game);
                }
            }
            _source.clip = clip;
            _source.volume = volume * MusicMan.m_masterMusicVolume;
            _source.time = 0f;
            _source.Play();
        }
    }
}
