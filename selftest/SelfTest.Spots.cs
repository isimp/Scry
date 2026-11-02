using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's look at the spots the character's sight keeps for the map: which of the
    /// game's prefabs count as worth finding again and by what kind, the things near the character
    /// kept where the sight's line reaches them, a thing destroyed taking its own spot away while
    /// a deposit struck into its broken form keeps it, and what keeping spots costs the sight a
    /// frame and the file on disk.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>How near a findable thing stands for the part to expect its spot kept, well inside the reach of the smallest.</summary>
        private const float SpotNear = 15f;

        /// <summary>The spot a destroyed thing took away, put back after the part where the sight did not.</summary>
        private static (string Kind, float X, float Z)? _spotTakenAway;

        private static IEnumerator SpotsKept(Probe p)
        {
            var player = Player.m_localPlayer;
            var world = CompendiumMap.World;
            if (player == null || ZNetScene.instance == null || ZDOMan.instance == null || world == null) p.Skip("there is no world or character");
            SpotKindsOfTheGame(p);
            var watched = ZDOMan.instance.m_onZDODestroyed?.GetInvocationList().Any(d => d.Method.DeclaringType == typeof(CompendiumSight)) ?? false;
            p.Check(watched, "things the game destroys are watched, to take their spots away");
            if (!Settings.RecordSeenSpots || Settings.SeenOnMapReach <= 0f) p.Skip("keeping where things were seen is off, or the world lets the map keep none");
            yield return Until(() => SeenSpotsKeeper.Ready, 10);
            if (!p.Check(SeenSpotsKeeper.Ready, "the character's spots are read")) yield break;

            // A real round of the sight, as play runs it, then what it kept near the character.
            var lookUp = CompendiumKeeper.CatchUpFor(X.Entries);
            var faults = Faults.Count;
            var failed = Steps.Run(() => CompendiumSight.LookAtAll(player, lookUp, new CompendiumBook()), null);
            p.Check(failed == null, "a round of the sight, spots kept, finds no fault", failed?.Message);
            var near = FindableNear(player.transform.position, SpotNear);
            var spots = SeenSpotsKeeper.Spots;
            var kept = near.Where(t => spots.Has(world, t.Kind, t.At.x, t.At.z)).ToList();
            p.Note($"{Numbers.Count(near.Count)} things worth finding again stand within {Numbers.Metres(SpotNear, 0)}, {Numbers.Count(kept.Count)} of them kept as spots: {string.Join(", ", near.Select(t => t.Kind).Distinct().Take(8))}");
            var plain = near.Where(t => PlainlyInSight(t.Thing)).ToList();
            if (plain.Count == 0) p.Untried("a thing worth finding again kept where the sight reaches it", "stand facing an ore deposit, flint or a berry bush, nothing between", "deposit");
            else p.Check(plain.All(t => spots.Has(world, t.Kind, t.At.x, t.At.z)), $"the {Numbers.Count(plain.Count)} of them plainly in sight are all kept",
                string.Join(", ", plain.Where(t => !spots.Has(world, t.Kind, t.At.x, t.At.z)).Select(t => $"{t.Kind} at {Numbers.Metres(Vector3.Distance(t.At, player.transform.position), 1)}")));
            // A spot kept nearby whose thing is gone was destroyed out of the character's reach (by
            // another player, or before the spots were read): noted, as nothing looks for it yet.
            var standing = FindableNear(player.transform.position, SpotNear + SeenSpots.SameSpot * 2f);
            var empty = spots.Near(world, player.transform.position.x, player.transform.position.z, SpotNear)
                .Where(s => !s.Kind.StartsWith(EntryKeys.Location, StringComparison.Ordinal) &&!standing.Any(t => t.Kind == s.Kind && Mathf.Abs(t.At.x - s.X) <= SeenSpots.SameSpot && Mathf.Abs(t.At.z - s.Z) <= SeenSpots.SameSpot)).ToList();
            if (empty.Count > 0) p.Note($"{Numbers.Count(empty.Count)} spots kept near the character have no thing standing there: {string.Join(", ", empty.Take(5).Select(s => $"{s.Kind} at {Numbers.Point(s.X, s.Z, 0)}"))}");

            // A thing destroyed takes its own spot away; a whole deposit struck becomes its broken form and keeps it.
            var gone = ZDOMan.instance.m_onZDODestroyed?.GetInvocationList().FirstOrDefault(d => d.Method.DeclaringType == typeof(CompendiumSight)) as Action<ZDO>;
            var whole = kept.FirstOrDefault(t => SpotKinds.Replaced(t.Zdo.GetPrefab()));
            var lone = kept.FirstOrDefault(t => !SpotKinds.Replaced(t.Zdo.GetPrefab()));
            if (gone != null && whole.Thing != null)
            {
                gone(whole.Zdo);
                p.Check(spots.Has(world, whole.Kind, whole.At.x, whole.At.z), $"{whole.Kind} struck into its broken form keeps its spot");
            }
            if (gone != null && lone.Thing != null)
            {
                _spotTakenAway = (lone.Kind, lone.At.x, lone.At.z);
                gone(lone.Zdo);
                p.Check(!spots.Has(world, lone.Kind, lone.At.x, lone.At.z), $"{lone.Kind} destroyed takes its spot away");
                // It still stands, only told destroyed: its spot put back.
                SpotPutBack();
            }
            if (whole.Thing == null && lone.Thing == null) p.Untried("a thing destroyed taking its spot away", "stand facing an ore deposit, flint or a berry bush, nothing between", "deposit");
            p.Check(Faults.Count == faults, "nothing told a fault meanwhile", Faults.Latest);
            yield return null;

            // What keeping spots costs a round of the sight: rounds with it, then without.
            var scratch = new CompendiumBook();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            for (var round = 0; round < 10; round++) CompendiumSight.LookAtAll(player, lookUp, scratch);
            var withMs = clock.Elapsed.TotalMilliseconds / 10;
            Settings.RecordSeenSpots = false;
            try
            {
                clock.Restart();
                for (var round = 0; round < 10; round++) CompendiumSight.LookAtAll(player, lookUp, scratch);
            }
            finally
            {
                Settings.RecordSeenSpots = true;
            }
            var withoutMs = clock.Elapsed.TotalMilliseconds / 10;
            var added = Math.Max(0, withMs - withoutMs) / 60;
            p.Check(added <= 0.2, $"keeping spots adds at most 0.2 ms a frame to the sight, a round spread over a second's frames",
                $"{Numbers.Amount(added, 3)} ms a frame; a round takes {Numbers.Amount(withMs, 3)} ms with spots kept, {Numbers.Amount(withoutMs, 3)} ms without, over {Numbers.Count(CompendiumSight.ThingCount)} things");
            p.Note($"a round of the sight takes {Numbers.Amount(withMs, 3)} ms with spots kept, {Numbers.Amount(withoutMs, 3)} ms without; {Numbers.Amount(added, 3)} ms a frame added");

            // How many spots the world keeps and how large the file is they are written to.
            SeenSpotsKeeper.Save(now: true);
            var path = CompendiumKeeper.CharacterFile != null ? SeenSpotsKeeper.PathOf(CompendiumKeeper.CharacterFile) : null;
            yield return Until(() => path != null && File.Exists(path), 10);
            var bytes = path != null && File.Exists(path) ? new FileInfo(path).Length : 0;
            p.Note($"this world keeps {Numbers.Count(spots.CountIn(world))} spots of at most {Numbers.Count(SeenSpots.MostAWorld)}, every world {Numbers.Count(spots.Count)}; the file holds {Numbers.Count(bytes)} bytes");
        }

        /// <summary>Puts back the spot a destroyed thing took away, where the sight did not keep it again.</summary>
        private static void SpotPutBack()
        {
            if (_spotTakenAway is (string kind, float x, float z)) SeenSpotsKeeper.Saw(kind, x, z);
            _spotTakenAway = null;
        }

        /// <summary>Which of the game's prefabs count as worth finding again, and by what kind: deposits by the whole one, a Wishbone's finds not until opened, what lies everywhere and crops grown from planting never.</summary>
        private static void SpotKindsOfTheGame(Probe p)
        {
            foreach (var (name, findable, how) in new[]
            {
                ("rock4_copper", true, "a copper deposit"), ("MineRock_Tin", true, "a tin deposit"), ("MineRock_Obsidian", true, "an obsidian deposit"), ("Pickable_Flint", true, "flint"),
                ("RaspberryBush", true, "a raspberry bush"), ("Pickable_Mushroom", true, "a mushroom"), ("silvervein", false, "a buried silver vein, which a Wishbone finds"),
                ("VineGreen", false, "a vine grown from a planted sapling"), ("Pickable_Carrot", false, "a carrot grown from a planted seed"),
                ("Pickable_SeedCarrot", true, "a wild seed carrot"), ("Pickable_Stone", false, "a stone"), ("Pickable_Branch", false, "a branch"), ("Beech1", false, "a beech"), ("rock1", false, "a plain rock"),
            })
            {
                var prefab = GamePrefabs.Named(name);
                if (prefab == null) { p.Note($"no {name} in the game"); continue; }
                var kind = SpotKinds.Of(name.GetStableHashCode());
                p.Check((kind != null) == findable, $"{how} ({name}) is {(findable ? "" : "not ")}worth finding again", kind ?? "none");
            }
            // A broken form is kept by the whole deposit it comes of; only the whole, struck, is replaced where it stands.
            foreach (var name in new[] { "rock4_copper", "silvervein" })
            {
                var broken = GamePrefabs.Named(name).OrNull()?.GetComponent<Destructible>().OrNull()?.m_spawnWhenDestroyed;
                if (broken == null) { p.Note($"{name} has no broken form"); continue; }
                p.Check(SpotKinds.Of(broken.name.GetStableHashCode()) == name, $"{broken.name} is kept as the {name} it comes of", SpotKinds.Of(broken.name.GetStableHashCode()) ?? "none");
                p.Check(!SpotKinds.Replaced(broken.name.GetStableHashCode()), $"and {broken.name} destroyed is gone, not replaced");
            }
            p.Check(SpotKinds.Replaced("rock4_copper".GetStableHashCode()), "a whole copper deposit struck is replaced by its broken form where it stands");
            p.Check(!SpotKinds.Replaced("MineRock_Tin".GetStableHashCode()), "a tin deposit mined away is gone");
        }

        /// <summary>The things worth finding again loaded within a reach of a point, with the kind each is kept by.</summary>
        private static List<(ZNetView Thing, ZDO Zdo, string Kind, Vector3 At)> FindableNear(Vector3 point, float reach)
        {
            var found = new List<(ZNetView, ZDO, string, Vector3)>();
            var seen = new HashSet<ZNetView>();
            foreach (var part in UnityEngine.Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None).Cast<Component>()
                .Concat(UnityEngine.Object.FindObjectsByType<MineRock5>(FindObjectsSortMode.None))
                .Concat(UnityEngine.Object.FindObjectsByType<MineRock>(FindObjectsSortMode.None))
                .Concat(UnityEngine.Object.FindObjectsByType<Destructible>(FindObjectsSortMode.None)))
            {
                var thing = part.GetComponentInParent<ZNetView>();
                var zdo = thing != null ? thing.GetZDO() : null;
                if (zdo == null || !seen.Add(thing) || (thing.transform.position - point).sqrMagnitude > reach * reach) continue;
                var kind = SpotKinds.Of(zdo.GetPrefab());
                if (kind != null) found.Add((thing, zdo, kind, thing.transform.position));
            }
            return found;
        }

        /// <summary>Whether a thing stands plainly in the camera's sight: well inside its view, and a line to its middle stopped by nothing but itself, leaves included.</summary>
        private static bool PlainlyInSight(ZNetView thing)
        {
            var view = GameCamera.instance != null ? GameCamera.instance.transform : null;
            var collider = thing.GetComponentInChildren<Collider>();
            if (view == null || collider == null) return false;
            var middle = collider.bounds.center;
            if (Vector3.Angle(view.forward, middle - view.position) > 30f) return false;
            var mask = LayerMask.GetMask(SightSolid) | LayerMask.GetMask("viewblock");
            return !Physics.Linecast(view.position, middle, out var hit, mask) || hit.collider.transform.IsChildOf(thing.transform);
        }
    }
}
