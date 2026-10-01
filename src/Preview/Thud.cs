using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The sound and dust of a falling copy striking the ground, as the game plays them through
    /// the prefab's <c>ImpactEffect</c>: its hit effect where it strikes, when it strikes faster
    /// than the effect's least speed, and again no sooner than its interval. The copy keeps none
    /// of the game's scripts, so this stands in for that one; it deals no damage and only plays.
    /// On the stage the effect is the stage's, heard as if beside you; in the world it plays where
    /// the copy strikes. The layers the game's effect answers to are not asked, as the stage's
    /// ground is on a layer of Scry's own.
    /// </summary>
    internal sealed class Thud : MonoBehaviour
    {
        private EffectList _hit;
        private float _least;
        private float _interval;
        private float _next;
        private bool _onStage;

        /// <summary>How many strikes have played, for the self-test.</summary>
        public static int Played { get; private set; }

        /// <summary>Has a falling copy play its prefab's impact, if the prefab has one.</summary>
        public static void Add(GameObject prefab, GameObject copy, bool onStage)
        {
            if (prefab == null || copy == null) return;
            var impact = prefab.GetComponentInChildren<ImpactEffect>(true);
            if (impact == null || impact.m_hitEffect == null || !impact.m_hitEffect.HasEffects()) return;
            var body = copy.GetComponentInChildren<Rigidbody>(true);
            if (body == null) return;
            var thud = body.gameObject.AddComponent<Thud>();
            thud._hit = impact.m_hitEffect;
            thud._least = impact.m_minVelocity;
            thud._interval = Mathf.Max(0.1f, impact.m_interval);
            thud._onStage = onStage;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_hit == null || collision.contactCount == 0 || Time.unscaledTime < _next) return;
            if (collision.relativeVelocity.magnitude < _least) return;
            _next = Time.unscaledTime + _interval;
            var point = collision.GetContact(0).point;
            Played++;
            if (_onStage) Stage.PlayList(_hit, null, null, point);
            else Previews.PlayList(_hit, point, transform.rotation);
        }
    }
}
