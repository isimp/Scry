using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Flies a projectile copy along an arc and bursts it where it lands. Its own script is gone
    /// from the copy, so this moves it instead: the same gravity, pointed along its path, stopped
    /// by the first piece of ground or building in the way. The ray only looks; nothing is hit.
    /// </summary>
    internal sealed class Flight : MonoBehaviour
    {
        public Vector3 Velocity;
        public float Gravity;
        public float Lifetime = 4f;
        public EffectList Burst;

        /// <summary>Flying on the stage: it lands on the stage's ground and bursts there.</summary>
        public bool OnStage;

        private float _age;

        private static int _mask = -1;

        private static int Mask
        {
            get
            {
                if (_mask == -1) _mask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain", "vehicle");
                return _mask;
            }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            _age += dt;

            Velocity += Vector3.down * Gravity * dt;
            var from = transform.position;
            var step = Velocity * dt;

            var mask = OnStage ? 1 << Stage.Layer : Mask;
            if (step.sqrMagnitude > 0f && Physics.Raycast(from, step.normalized, out var hit, step.magnitude, mask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point;
                if (OnStage) Stage.PlayList(Burst, null, null, hit.point);
                else Previews.PlayList(Burst, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(gameObject);
                return;
            }

            transform.position = from + step;
            if (Velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(Velocity);

            if (_age > Lifetime) Destroy(gameObject);
        }
    }
}
