using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// With <see cref="Settings.LogPreviews"/> on, watches a copy let fall for a few seconds and
    /// tells in the log where it was made, how high and how fast it went and where it came to
    /// rest, measured from the foot of the copy it came from, with what it has to land with and
    /// what else it touched on the way (another copy, the ground, a building or a tree of the world).
    /// </summary>
    [Diagnostic]
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
        private Collider[] _own;
        private int _meets;
        private readonly List<string> _touched = new List<string>();
        private static readonly Collider[] Near = new Collider[16];

        public static void Start(GameObject piece, GameObject from, bool onStage)
        {
            if (!Settings.LogPreviews || piece == null || from == null) return;
            var watch = piece.AddComponent<FallWatch>();
            watch._from = from.name;
            watch._onStage = onStage;
            watch._foot = from.transform.position;
            watch._start = piece.transform.position;
            watch._highest = watch._start.y;
            // The game's time, as the fall it watches is the game's physics.
            watch._until = Time.time + Seconds;
            watch._body = piece.GetComponentInChildren<Rigidbody>();

            var colliders = piece.GetComponentsInChildren<Collider>(true);
            watch._own = colliders;
            if (colliders.Length > 0)
            {
                var layer = colliders[0].gameObject.layer;
                for (var i = 0; i < 32; i++) if (!Physics.GetIgnoreLayerCollision(layer, i)) watch._meets |= 1 << i;
            }
            // Where each collider reaches, from the foot, and where the ground is beneath it, since
            // a copy made reaching into the ground is pushed out of it slowly.
            var foot = watch._foot.y;
            string Reach(Collider c)
            {
                var b = c.bounds;
                var ground = !onStage && ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(b.center) : float.NaN;
                return $" from {Numbers.Fixed(b.min.y - foot, 1)} to {Numbers.Fixed(b.max.y - foot, 1)} m, {Numbers.Fixed(b.size.x, 1)} by {Numbers.Fixed(b.size.z, 1)} m across"
                       + (float.IsNaN(ground) ? "" : $", ground beneath at {Numbers.Fixed(ground - foot, 1)} m");
            }
            var told = string.Join(", ", colliders.Select(c =>
                c.GetType().Name + (c is MeshCollider mesh && !mesh.convex ? " not convex" : "") + (c.enabled ? "" : " off") + " on " + LayerMask.LayerToName(c.gameObject.layer) + "/" + Numbers.Count(c.gameObject.layer) + Reach(c)));
            var body = watch._body;
            Log.Note($"Scry lets {piece.name} fall {(onStage ? "on the stage" : "in the world")} from {from.name}: made {Around(watch._start - watch._foot)} of its foot"
                + (body != null ? $", body mass {Numbers.Amount(body.mass, 1)}{(body.isKinematic ? " kinematic" : "")}{(body.useGravity ? "" : " without gravity")}, pushed apart at most {Numbers.Amount(body.maxDepenetrationVelocity, 1)} m/s, moving {Numbers.Amount(body.linearVelocity.magnitude, 1)} m/s" : ", no body")
                + $"; {Numbers.Count(colliders.Length)} colliders: {(colliders.Length > 0 ? told : "none")}.");
        }

        private void FixedUpdate()
        {
            if (_body == null) return;
            _highest = Mathf.Max(_highest, _body.worldCenterOfMass.y);
            _fastest = Mathf.Max(_fastest, _body.linearVelocity.magnitude);
            Touching();
        }

        /// <summary>Where a touch's note says by how much, cut off to tell one touch from another.</summary>
        private static readonly string[] ByWhat = { " by " };

        /// <summary>Notes each collider not its own that one of its colliders overlaps, by name, layer and what it belongs to.</summary>
        private void Touching()
        {
            if (_own == null || _touched.Count >= 6) return;
            foreach (var mine in _own)
            {
                if (mine == null || !mine.enabled) continue;
                var bounds = mine.bounds;
                var count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * 0.05f, Near, Quaternion.identity, _meets, QueryTriggerInteraction.Ignore);
                for (var i = 0; i < count; i++)
                {
                    var other = Near[i];
                    if (other == null || System.Array.IndexOf(_own, other) >= 0) continue;
                    var inside = Physics.ComputePenetration(mine, mine.transform.position, mine.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out var away, out var depth);
                    var told = $"{other.name} ({other.GetType().Name} on {LayerMask.LayerToName(other.gameObject.layer)}/{Numbers.Count(other.gameObject.layer)}) of {other.transform.root.name}";
                    if (inside) told += $" by {Numbers.Fixed(depth, 2)} m, pushed {(away.y > 0.7f ? "up" : away.y < -0.7f ? "down" : "aside")} at {Numbers.Fixed(Time.time - (_until - Seconds), 2)} s";
                    if (!_touched.Exists(t => t.StartsWith(told.Split(ByWhat, System.StringSplitOptions.None)[0], System.StringComparison.Ordinal))) _touched.Add(told);
                    if (_touched.Count >= 6) return;
                }
            }
        }

        private void Update()
        {
            if (Time.time < _until) return;
            // Gone at the frame's end whatever the note does, so a watch that fails is not tried again each frame.
            Destroy(this);
            Guard.Run(Feature.LetFall, "noting a watched fall", () =>
            {
                var at = _body != null ? _body.transform.position : transform.position;
                var ground = _onStage ? float.NaN : ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(at) : float.NaN;
                Log.Note($"Scry watched {name} from {_from} {(_onStage ? "on the stage" : "in the world")} for {Numbers.Amount(Seconds, 0)} s: highest {Numbers.Fixed(_highest - _foot.y, 1)} m above its foot, fastest {Numbers.Amount(_fastest, 1)} m/s, now {Around(at - _foot)} of it"
                    + (float.IsNaN(ground) ? "" : $", {Numbers.Fixed(at.y - ground, 1)} m above the ground there")
                    + $"; touched {(_touched.Count > 0 ? string.Join(", ", _touched) : "nothing")}.");
            });
        }

        private static string Around(Vector3 offset)
        {
            var aside = new Vector2(offset.x, offset.z).magnitude;
            return $"{Numbers.Fixed(offset.y, 1)} m {(offset.y >= 0f ? "above" : "below")} and {Numbers.Fixed(aside, 1)} m aside";
        }
    }
}
