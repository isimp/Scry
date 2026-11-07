using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's look at why a magmafish swimming in lava is never seen.</summary>
    internal static partial class SelfTest
    {
        /// <summary>The magmafish's prefab, as the game names it.</summary>
        private const string Magmafish = "Fish11";

        /// <summary>
        /// Each magmafish loaded near the character, measured: how high it swims against the
        /// ground under it, whether the game counts that ground as lava, and what the eye's line
        /// to it meets on the layers Scry's sight counts as solid. One near enough to be seen is
        /// checked to have a clear line from the eye, as Scry's sight needs to see any thing
        /// (<c>CompendiumSight.Clear</c>); a fail there names what stands between.
        /// </summary>
        private static IEnumerator MagmafishInSight(Probe p)
        {
            var player = Player.m_localPlayer;
            var zones = ZoneSystem.instance;
            if (player == null || zones == null)
            {
                p.Skip("no character in a world");
                yield break;
            }
            var eye = GameCamera.instance != null ? GameCamera.instance.transform.position : player.GetEyePoint();
            var fish = Object.FindObjectsByType<Fish>(FindObjectsSortMode.None)
                .Where(f => f != null && f.name.StartsWith(Magmafish, System.StringComparison.Ordinal))
                .OrderBy(f => (f.transform.position - eye).sqrMagnitude).Take(6).ToList();
            if (fish.Count == 0)
            {
                p.Untried("a magmafish in lava, measured against the ground and the eye's line to it", "stand by lava in the Ashlands with magmafish about", "magmafish");
                yield break;
            }
            var solid = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
            var reach = CompendiumKeeper.Book.World.SeenReach + 5f;
            foreach (var one in fish)
            {
                var at = one.transform.position;
                var away = Vector3.Distance(eye, at);
                var ground = zones.GetGroundHeight(at);
                var blocked = Physics.Linecast(eye, at, out var hit, solid);
                var stop = blocked ? Vector3.Distance(hit.point, at) : 0f;
                var met = blocked ? $"stops {Numbers.Fixed(stop, 1)} m short on {LayerMask.LayerToName(hit.collider.gameObject.layer)} ({hit.collider.name})" : "is clear";
                p.Note($"{one.name} {Numbers.Fixed(away, 1)} m away swims {Numbers.Fixed(at.y - ground, 2)} m over the ground (lava there: {zones.IsLava(at)}); the eye's line {met}");
                if (away <= reach) p.Check(!blocked, "a magmafish near enough to be seen has a clear line from the eye, as any thing seen needs", met);
            }
            if (fish.All(f => Vector3.Distance(eye, f.transform.position) > reach))
                p.Untried("a magmafish near enough to be seen", $"come within {Numbers.Fixed(reach, 0)} m of one", "magmafish");
            yield return null;
        }
    }
}
