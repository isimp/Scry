using UnityEngine;

namespace Scry
{
    /// <summary>
    /// How loud Scry's previews play, as a share of how loud the game plays the same sound. An
    /// audio source's own volume stops at the game's full volume, so the share is applied to the
    /// sound itself as Unity plays it (<c>OnAudioFilterRead</c>), which can go above it. Only the
    /// copies Scry makes carry it; the game's own sounds are left as they are.
    /// </summary>
    internal static class Loudness
    {
        /// <summary>The share now, read by the audio thread; 1 is the game's own loudness.</summary>
        public static volatile float Gain = 1f;

        /// <summary>
        /// Puts the gain on every sound of a copy that does not carry it yet. The gain is taken from
        /// the selection first: a copy may be made and heard in the same moment the selection
        /// changes, before the next frame would, and must never start at the last one's loudness.
        /// </summary>
        public static void Add(GameObject copy)
        {
            var explorer = Session.IsOpen ? Session.Explorer : null;
            Gain = explorer != null ? explorer.Modifiers.Volume : 1f;
            if (copy == null) return;
            foreach (var source in copy.GetComponentsInChildren<AudioSource>(true))
            {
                if (source != null && source.GetComponent<PreviewGain>() == null) source.gameObject.AddComponent<PreviewGain>();
            }
        }
    }

    /// <summary>
    /// Scales one copy's sound by <see cref="Loudness.Gain"/> as it plays. Above the game's own
    /// loudness a loud sound may clip; the samples are held within full scale so it distorts
    /// rather than cracks.
    /// </summary>
    internal sealed class PreviewGain : MonoBehaviour
    {
        private void OnAudioFilterRead(float[] data, int channels)
        {
            var gain = Loudness.Gain;
            if (gain == 1f) return;
            for (var i = 0; i < data.Length; i++)
            {
                var sample = data[i] * gain;
                data[i] = sample > 1f ? 1f : sample < -1f ? -1f : sample;
            }
        }
    }
}
