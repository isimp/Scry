using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A tree's trunk shaking a second when struck, as <c>TreeBase.ShakeAnimation</c> shakes it:
    /// quick turns either way that die away.
    /// </summary>
    internal sealed class TrunkShake : MonoBehaviour
    {
        private Quaternion _rest;
        private float _from;

        public static void Start(Transform trunk)
        {
            var shake = trunk.GetComponent<TrunkShake>();
            if (shake == null)
            {
                shake = trunk.gameObject.AddComponent<TrunkShake>();
                shake._rest = trunk.localRotation;
            }
            shake._from = Time.time;
        }

        private void Update()
        {
            var time = Time.time;
            if (time - _from >= 1f)
            {
                transform.localRotation = _rest;
                Destroy(this);
                return;
            }
            var left = 1f - Mathf.Clamp01((time - _from) / 1f);
            var turn = left * left * left * 1.5f;
            transform.localRotation = _rest * Quaternion.Euler(Mathf.Sin(time * 40f) * turn, 0f, Mathf.Cos(time * 0.9f * 40f) * turn);
        }
    }
}
