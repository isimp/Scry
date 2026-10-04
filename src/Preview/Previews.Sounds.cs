using System.Collections.Generic;
using System.Linq;
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

            var ears = GameCamera.instance != null ? GameCamera.instance.transform : Player.m_localPlayer.OrNull()?.transform;
            if (ears == null) return;
            TheSound.Play(entry, prefab, only, ears);
        }

        /// <summary>The sound playing at your ears.</summary>
        private static readonly SoundPreview TheSound = new SoundPreview();

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
                    return MusicWords.Stopped;
                }
                return first != null ? NamedMusic(entry, first) : MusicWords.None;
            }
            if (!(entry?.Source is PlaceSource place)) return null;
            if (MusicPreview.PlayingFor == entry)
            {
                StopSound();
                return MusicWords.Stopped;
            }
            if (place.Contents == null) return MusicWords.NotLoaded;
            if (place.Contents.Music.Count == 0) return MusicWords.None;
            StopSound();
            var played = MusicPreview.Play(entry, PlaceAssets.Asset(place), place.Contents.Music);
            return played != null ? MusicWords.Playing(MusicWords.Name(played), MusicStop.Enter) : MusicWords.NotFound;
        }

        /// <summary>A piece of the game's music by its name, played for an entry, or stopped when it plays already.</summary>
        public static string NamedMusic(Entry entry, string name)
        {
            if (MusicPreview.PlayingFor == entry && MusicPreview.Playing == name)
            {
                StopSound();
                return MusicWords.Stopped;
            }
            StopSound();
            var played = MusicPreview.Play(entry, null, new List<PlaceMusic> { new PlaceMusic { Name = name, When = MusicWhen.Inside } });
            return played != null ? MusicWords.Playing(MusicWords.Name(played), MusicStop.Click) : MusicWords.NotFound;
        }

        /// <summary>The music a raid or a boss's fight forces (<c>RandomEvent.m_forceMusic</c>), from the game's music list, or stopped when it plays already.</summary>
        private static string EventMusic(Entry entry, RandomEvent raid)
        {
            if (MusicPreview.PlayingFor == entry)
            {
                StopSound();
                return MusicWords.Stopped;
            }
            if (string.IsNullOrEmpty(raid.m_forceMusic)) return MusicWords.None;
            StopSound();
            var played = MusicPreview.Play(entry, null, new List<PlaceMusic> { new PlaceMusic { Name = raid.m_forceMusic, When = MusicWhen.Inside } });
            return played != null ? MusicWords.Playing(MusicWords.Name(played), MusicStop.ClickOrEnter) : MusicWords.NotFound;
        }

        public static void StopSound()
        {
            MusicPreview.Stop();
            TheSound.Stop();
        }

        /// <summary>
        /// The clip the playing sound is actually playing, whether chosen or picked at random, so
        /// the panel can show which variant was heard. Null when nothing plays.
        /// </summary>
        public static AudioClip SoundClipNow() => TheSound.ClipNow();

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

        /// <summary>The playing sound's source and how its clip loads, for the self-test to tell.</summary>
        public static string SoundSourceTold() => TheSound.SourceTold();

        /// <summary>Where the playing sound is and how long its clip is, for the panel's timeline.</summary>
        public static bool SoundPosition(out float time, out float length) => TheSound.Position(out time, out length);

        /// <summary>Jumps the playing sound to a point in its clip.</summary>
        public static void SeekSound(float time) => TheSound.SeekTo(time);

        /// <summary>Where the sound was each frame while a point sought while paused was being settled, for the self-test to tell.</summary>
        public static readonly List<string> SeekTrail = new List<string>();

        /// <summary>Each frame: a point sought while paused is watched until it holds (<see cref="SoundPreview.Settle"/>).</summary>
        public static void SettleSeek() => TheSound.Settle(SeekTrail);

        public static bool SoundPaused => TheSound.Paused;

        public static void PauseSound(bool pause) => TheSound.Pause(pause, SeekTrail);
    }
}
