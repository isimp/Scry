using System;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// What near the character lets an untried row be tried (<see cref="Probe.Untried"/>), by a
    /// key each row names: a kind of thing loaded within <see cref="NeedReach"/>. Only a hint: a
    /// row may still want more of the thing than being near (a chest the world filled, a bush
    /// picked before), and says so again when run. Rows nothing near frees (a tool in hand, a
    /// kill to come) name no key.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>How near a thing must be loaded to count as near the character, in metres.</summary>
        private const float NeedReach = 40f;

        /// <summary>
        /// Each key's look, by one thing the ball round the character touches and where the
        /// character stands: a place by the game's own look at where it stands, none touched.
        /// </summary>
        private static readonly Dictionary<string, Func<Collider, Vector3, bool>> Needs = new Dictionary<string, Func<Collider, Vector3, bool>>(StringComparer.Ordinal)
        {
            ["altar"] = (c, at) => Is<OfferingBowl>(c, at),
            ["beehive"] = (c, at) => Is<Beehive>(c, at),
            ["breakable"] = (c, at) => Is<Destructible>(c, at) || Is<TreeBase>(c, at) || Is<MineRock5>(c, at),
            ["breeding"] = (c, at) => Is<Procreation>(c, at, p => p.GetComponent<Character>().OrNull()?.IsTamed() == true),
            ["chest"] = (c, at) => Is<Container>(c, at, b => b.GetComponent<Piece>() == null),
            ["cooking"] = (c, at) => Is<CookingStation>(c, at),
            ["armed"] = (c, at) => Is<Humanoid>(c, at, h => !h.IsPlayer()),
            ["creature"] = (c, at) => Is<Character>(c, at, x => !x.IsPlayer()),
            ["pickable"] = (c, at) => Is<Pickable>(c, at),
            ["piece"] = (c, at) => Is<WearNTear>(c, at),
            ["place"] = null,
            ["plant"] = (c, at) => Is<Plant>(c, at),
            ["rock"] = (c, at) => Is<MineRock5>(c, at),
            ["runestone"] = (c, at) => Is<RuneStone>(c, at),
            ["spawner"] = (c, at) => Is<SpawnArea>(c, at),
            ["tame"] = (c, at) => Is<Tameable>(c, at, x => x.GetComponent<Character>().OrNull()?.IsTamed() == true),
            ["tameable"] = (c, at) => Is<Tameable>(c, at, x => x.GetComponent<Character>().OrNull()?.IsTamed() == false),
            ["treasure"] = (c, at) => Is<PickableItem>(c, at),
            ["tree"] = (c, at) => Is<TreeBase>(c, at),
            ["upgrades"] = (c, at) => Is<StationExtension>(c, at),
            ["vegvisir"] = (c, at) => Is<Vegvisir>(c, at),
        };

        /// <summary>Each key's thing in words, as the chat names it once one is near.</summary>
        private static readonly Dictionary<string, string> NeedWords = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["altar"] = "an altar",
            ["beehive"] = "a beehive",
            ["breakable"] = "something that breaks",
            ["breeding"] = "a tame creature that breeds",
            ["chest"] = "a chest the world filled",
            ["cooking"] = "a cooking station",
            ["armed"] = "an armed creature",
            ["creature"] = "a creature",
            ["pickable"] = "something to pick",
            ["piece"] = "a built piece",
            ["place"] = "a place",
            ["plant"] = "a plant",
            ["rock"] = "a rock in pieces",
            ["runestone"] = "a rune stone",
            ["spawner"] = "a spawner",
            ["tame"] = "a tame creature",
            ["tameable"] = "a creature to tame",
            ["treasure"] = "treasure",
            ["tree"] = "a tree",
            ["upgrades"] = "a station's upgrade",
            ["vegvisir"] = "a Vegvisir",
        };

        /// <summary>The keys of what is loaded near the character now, each looked at whole at once, as a run by what is near asks; none outside a world.</summary>
        private static HashSet<string> NeedsNear()
        {
            var near = new HashSet<string>(StringComparer.Ordinal);
            var player = Player.m_localPlayer;
            if (player == null) return near;
            var at = player.transform.position;
            foreach (var need in Needs) if (NearWhole(need.Key, at)) near.Add(need.Key);
            return near;
        }

        /// <summary>What was found near the character between runs, by key, one key looked at a second (<see cref="LookNear"/>).</summary>
        private static readonly HashSet<string> NearNow = new HashSet<string>(StringComparer.Ordinal);
        private static int _nearTurn;
        private static float _nearAt = -10f;

        /// <summary>The key being looked for between runs, where the character stood, how many things the ball touched and the next to look at; none while no look goes on.</summary>
        private static string _looking;
        private static Vector3 _lookAt;
        private static int _touched, _nextTouched;

        /// <summary>How many of the things the ball touched a frame looks at between runs: in a base thousands are, and a look at all at once held a frame for some milliseconds.</summary>
        private const int TouchedAFrame = 256;

        /// <summary>
        /// Between runs, the next of the keys the untried rows name looked for near the character,
        /// one begun a second and worked through <see cref="TouchedAFrame"/> things a frame, so a
        /// world full of things costs no frame much; keys no row names any more are forgotten.
        /// </summary>
        private static void LookNear(IReadOnlyList<string> wanted)
        {
            NearNow.IntersectWith(wanted);
            var player = Player.m_localPlayer;
            if (_looking != null)
            {
                if (player == null || !Needs.ContainsKey(_looking)) _looking = null;
                else LookOn();
                return;
            }
            if (wanted.Count == 0 || player == null || Time.unscaledTime - _nearAt < 1f) return;
            _nearAt = Time.unscaledTime;
            var need = wanted[_nearTurn++ % wanted.Count];
            if (!Needs.TryGetValue(need, out var touches)) return;
            var at = player.transform.position;
            if (touches == null)
            {
                KeepFound(need, NearWhole(need, at));
                return;
            }
            _looking = need;
            _lookAt = at;
            _touched = Physics.OverlapSphereNonAlloc(at, NeedReach, Touching, ~0, QueryTriggerInteraction.Collide);
            _nextTouched = 0;
            LookOn();
        }

        /// <summary>The look going on worked through one frame's share of what the ball touched: found, or done with none, or left for the next frame.</summary>
        private static void LookOn()
        {
            var touches = Needs[_looking];
            var end = Math.Min(_touched, _nextTouched + TouchedAFrame);
            var found = Steps.Run(() => AnyTouched(touches, _lookAt, _nextTouched, end), out var any, null) == null && any;
            _nextTouched = end;
            if (!found && _nextTouched < _touched) return;
            KeepFound(_looking, found);
            _looking = null;
        }

        /// <summary>Keeps whether a key's thing was found near the character now.</summary>
        private static void KeepFound(string need, bool found)
        {
            if (found) NearNow.Add(need);
            else NearNow.Remove(need);
        }

        /// <summary>Whether any of the things the ball touched, from one place among them to another, is what a key looks for.</summary>
        private static bool AnyTouched(Func<Collider, Vector3, bool> touches, Vector3 at, int from, int to)
        {
            for (var i = from; i < to; i++)
            {
                if (Touching[i] != null && touches(Touching[i], at)) return true;
            }
            return false;
        }

        /// <summary>Whether a key's thing is near, looked at whole in one go: a place by where the character stands, else every thing the ball touches.</summary>
        private static bool NearWhole(string need, Vector3 at)
        {
            if (!Needs.TryGetValue(need, out var touches)) return false;
            if (touches == null) return Steps.Run(() => Location.GetLocation(at) != null, out var inPlace, null) == null && inPlace;
            var count = Physics.OverlapSphereNonAlloc(at, NeedReach, Touching, ~0, QueryTriggerInteraction.Collide);
            return Steps.Run(() => AnyTouched(touches, at, 0, count), out var any, null) == null && any;
        }

        /// <summary>
        /// Whether a thing the ball touched is of a kind and within <see cref="NeedReach"/> of the
        /// character: the ball, as the physics keeps what touches it, triggers among them, stands
        /// in for a look through the whole scene, which in a big world held a frame for some 30 ms
        /// every few seconds while the self-test waited between runs.
        /// </summary>
        private static bool Is<T>(Collider touched, Vector3 at, Func<T, bool> fits = null) where T : Component
        {
            var thing = touched.GetComponentInParent<T>();
            return thing != null && (thing.transform.position - at).sqrMagnitude <= NeedReach * NeedReach && (fits == null || fits(thing));
        }

        /// <summary>
        /// Every look for something near the character timed where it stands, as the self-test
        /// looks between runs: the ball and each frame's share of what it touched, the slowest
        /// frame's share of any key and how long it took.
        /// </summary>
        private static (string Need, double Ms) SlowestNearLook(Vector3 at)
        {
            var slowest = (Need: "none", Ms: 0.0);
            double Since(long started) => (System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            foreach (var need in Needs)
            {
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                if (need.Value == null)
                {
                    NearWhole(need.Key, at);
                    if (Since(started) > slowest.Ms) slowest = (need.Key, Since(started));
                    continue;
                }
                var count = Physics.OverlapSphereNonAlloc(at, NeedReach, Touching, ~0, QueryTriggerInteraction.Collide);
                for (var from = 0; from < count; from += TouchedAFrame)
                {
                    if (from > 0) started = System.Diagnostics.Stopwatch.GetTimestamp();
                    var to = Math.Min(count, from + TouchedAFrame);
                    var any = Steps.Run(() => AnyTouched(need.Value, at, from, to), out var hit, null) == null && hit;
                    if (Since(started) > slowest.Ms) slowest = (need.Key, Since(started));
                    if (any) break;
                }
            }
            return slowest;
        }

        /// <summary>What touches the ball looked through, kept for every look so none makes anything; a crowd past it is left out.</summary>
        private static readonly Collider[] Touching = new Collider[4096];
    }
}
