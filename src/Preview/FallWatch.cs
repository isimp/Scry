using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// With <see cref="Plugin.LogPreviews"/> on, watches a copy let fall for a few seconds and
    /// tells in the log where it was made, how high and how fast it went and where it came to
    /// rest, measured from the foot of the copy it came from, with what it has to land with.
    /// </summary>
    internal sealed class FallWatch : MonoBehaviour
    {
        private const float Seconds = 3f;

        private string _from;
        private bool _onStage;
        private Vector3 _foot;
        private Vector3 _start;
        private float _until;
        private float _highest;
        private float _fastest;
        private Rigidbody _body;

        public static void Start(GameObject piece, GameObject from, bool onStage)
        {
            if (!Plugin.LogPreviews || piece == null || from == null) return;
            var watch = piece.AddComponent<FallWatch>();
            watch._from = from.name;
            watch._onStage = onStage;
            watch._foot = from.transform.position;
            watch._start = piece.transform.position;
            watch._highest = watch._start.y;
            watch._until = Time.time + Seconds;
            watch._body = piece.GetComponentInChildren<Rigidbody>();

            var colliders = piece.GetComponentsInChildren<Collider>(true);
            var told = string.Join(", ", colliders.Select(c =>
                c.GetType().Name + (c is MeshCollider mesh && !mesh.convex ? " not convex" : "") + (c.enabled ? "" : " off") + " on " + LayerMask.LayerToName(c.gameObject.layer) + "/" + c.gameObject.layer));
            var body = watch._body;
            Plugin.Note($"Scry lets {piece.name} fall {(onStage ? "on the stage" : "in the world")} from {from.name}: made {Around(watch._start - watch._foot)} of its foot"
                + (body != null ? $", body mass {body.mass:0.#}{(body.isKinematic ? " kinematic" : "")}{(body.useGravity ? "" : " without gravity")}, pushed apart at most {body.maxDepenetrationVelocity:0.#} m/s, moving {body.linearVelocity.magnitude:0.#} m/s" : ", no body")
                + $"; {colliders.Length} colliders: {(colliders.Length > 0 ? told : "none")}.");
        }

        private void FixedUpdate()
        {
            if (_body == null) return;
            _highest = Mathf.Max(_highest, _body.worldCenterOfMass.y);
            _fastest = Mathf.Max(_fastest, _body.linearVelocity.magnitude);
        }

        private void Update()
        {
            if (Time.time < _until) return;
            var at = _body != null ? _body.transform.position : transform.position;
            var ground = _onStage ? float.NaN : ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(at) : float.NaN;
            Plugin.Note($"Scry watched {name} from {_from} {(_onStage ? "on the stage" : "in the world")} for {Seconds:0} s: highest {_highest - _foot.y:0.0} m above its foot, fastest {_fastest:0.#} m/s, now {Around(at - _foot)} of it"
                + (float.IsNaN(ground) ? "" : $", {at.y - ground:0.0} m above the ground there") + ".");
            Destroy(this);
        }

        private static string Around(Vector3 offset)
        {
            var aside = new Vector2(offset.x, offset.z).magnitude;
            return $"{offset.y:0.0} m {(offset.y >= 0f ? "above" : "below")} and {aside:0.0} m aside";
        }
    }
}
