using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Scry
{
    /// <summary>
    /// The self-test's look at what showing things on the large map would stand on: the map's own
    /// sizes, which of the world's placement rules put resources down and how deep, the prefabs a
    /// Wishbone finds, how a sight line meets a deposit near the character, and how a square drawn
    /// on the map's pin layer sits over explored ground and over fog. It notes and pictures what it
    /// finds rather than judging it, and writes the whole of it to <see cref="MapChecksFile"/>.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>Where the whole look is written, beside the self-test's results.</summary>
        public static string MapChecksFile => Path.Combine(BepInEx.Paths.BepInExRootPath, "Scry-mapchecks.log");

        /// <summary>The squares laid on the large map's pin layer for the picture, taken away after it.</summary>
        private static readonly List<GameObject> MapCheckSquares = new List<GameObject>();

        /// <summary>The layers a sight line stops at, as the compendium's sight has them.</summary>
        private static readonly string[] SightSolid = { "Default", "static_solid", "Default_small", "piece", "terrain" };

        private static IEnumerator MapChecks(Probe p)
        {
            var map = Minimap.instance;
            var zones = ZoneSystem.instance;
            var player = Player.m_localPlayer;
            if (map == null || zones == null || player == null || ZNetScene.instance == null)
            {
                p.Skip("there is no map, world or character");
                yield break;
            }
            var log = new StringBuilder();
            MapSizes(p, map, log);
            var byRules = PlacementRules(p, zones, log);
            InsidePlaces(p, byRules, log);
            WishboneFinds(p, log);
            DepositSight(p, player, log);
            File.WriteAllText(MapChecksFile, log.ToString());
            p.Note("the whole look is written to " + MapChecksFile);
            yield return LayerOverFog(p, map, player);
            MapChecksAsTheyWere();
        }

        /// <summary>The large map's texture, the metres a pixel of it stands for, and how far around the character it uncovers.</summary>
        private static void MapSizes(Probe p, Minimap map, StringBuilder log)
        {
            var line = $"map: {Numbers.Count(map.m_textureSize)} pixels a side, {Numbers.Fixed(map.m_pixelSize, 1)} m a pixel, so {Numbers.Count((int)(map.m_textureSize * map.m_pixelSize))} m across; uncovers {Numbers.Fixed(map.m_exploreRadius, 1)} m around you";
            p.Note(line);
            log.AppendLine(line);
        }

        /// <summary>
        /// Every enabled placement rule of the world's vegetation with what its prefab is made of
        /// (a deposit, a pickable, a tree), how deep it is set, and whether it carries a beacon a
        /// Wishbone finds; then how many kinds of location the world places, and how many in all.
        /// Returns the resources the rules place.
        /// </summary>
        private static HashSet<string> PlacementRules(Probe p, ZoneSystem zones, StringBuilder log)
        {
            log.AppendLine().AppendLine("placement rules (name, prefab, made of, biome, how many a zone, group, set below the ground, beacon):");
            var resources = new List<string>();
            var buried = new List<string>();
            foreach (var rule in zones.m_vegetation)
            {
                if (rule == null || !rule.m_enable || rule.m_prefab == null) continue;
                var prefab = rule.m_prefab;
                var made = MadeOf(prefab);
                var beacon = prefab.GetComponentInChildren<Beacon>(true);
                log.AppendLine($"  {rule.m_name}\t{prefab.name}\t{made}\t{rule.m_biome}\t{Numbers.Fixed(rule.m_min, 1)}-{Numbers.Fixed(rule.m_max, 1)}\t{Numbers.Count(rule.m_groupSizeMin)}-{Numbers.Count(rule.m_groupSizeMax)}\t{Numbers.Fixed(rule.m_groundOffset, 2)} m\t{(beacon != null ? Numbers.Fixed(beacon.m_range, 0) + " m" : "none")}");
                if (made.Length > 0) resources.Add(prefab.name);
                if (beacon != null || rule.m_groundOffset < -0.5f) buried.Add($"{prefab.name} ({Numbers.Fixed(rule.m_groundOffset, 2)} m{(beacon != null ? ", beacon" : "")})");
            }
            p.Note($"{Numbers.Count(resources.Distinct().Count())} kinds of resource are placed by the world's rules: {string.Join(", ", resources.Distinct().OrderBy(n => n, System.StringComparer.Ordinal))}");
            p.Note(buried.Count > 0 ? "set below the ground or found by a Wishbone: " + string.Join(", ", buried.Distinct()) : "no rule sets a resource below the ground or with a beacon");
            var places = zones.m_locations.Where(l => l != null && l.m_enable).ToList();
            var line = $"{Numbers.Count(places.Count)} kinds of location placed, {Numbers.Count(places.Sum(l => l.m_quantity))} in all at most";
            p.Note(line);
            log.AppendLine().AppendLine(line);
            foreach (var place in places) log.AppendLine($"  {place.m_prefabName}\t{place.m_biome}\t{Numbers.Count(place.m_quantity)}");
            return new HashSet<string>(resources);
        }

        /// <summary>
        /// The resources standing inside places as their parts (<see cref="PlaceContents.Parts"/>,
        /// as reading the locations kept them), each place with those it holds, and the kinds no
        /// placement rule puts down, which only a place brings.
        /// </summary>
        private static void InsidePlaces(Probe p, HashSet<string> byRules, StringBuilder log)
        {
            var places = X.Catalog.Where(e => e.Kind == Kind.Location && e.Source is PlaceSource place && place.Contents != null).ToList();
            if (places.Count == 0)
            {
                p.Note("no place's contents are read yet: read the locations to see which resources stand inside places");
                return;
            }
            log.AppendLine().AppendLine("resources standing inside places (place, the resources it holds):");
            var onlyInside = new SortedSet<string>(System.StringComparer.Ordinal);
            foreach (var entry in places)
            {
                var place = (PlaceSource)entry.Source;
                var held = place.Contents.Parts.Select(part => part.Prefab).Distinct()
                    .Where(prefab => GamePrefabs.Named(prefab) is GameObject found && MadeOf(found).Length > 0).ToList();
                if (held.Count == 0) continue;
                log.AppendLine($"  {place.Prefab}\t{string.Join(", ", held)}");
                foreach (var prefab in held) if (!byRules.Contains(prefab)) onlyInside.Add(prefab);
            }
            p.Note(onlyInside.Count > 0 ? "resources only places bring, no rule placing them: " + string.Join(", ", onlyInside) : "every resource standing inside a place is also placed by the world's rules");
        }

        /// <summary>What a prefab a player gathers is made of, by the game's parts; empty for anything else.</summary>
        private static string MadeOf(GameObject prefab)
        {
            var parts = new List<string>();
            if (prefab.GetComponent<MineRock5>() != null) parts.Add("MineRock5");
            if (prefab.GetComponent<MineRock>() != null) parts.Add("MineRock");
            if (prefab.GetComponent<Destructible>() != null) parts.Add("Destructible");
            if (prefab.GetComponent<Pickable>() != null) parts.Add("Pickable");
            if (prefab.GetComponent<TreeBase>() != null) parts.Add("TreeBase");
            return string.Join("+", parts);
        }

        /// <summary>Every prefab of the game's that carries a beacon a Wishbone finds, with how far it reaches; whether an opened silver vein still does.</summary>
        private static void WishboneFinds(Probe p, StringBuilder log)
        {
            var found = new List<string>();
            foreach (var prefab in ZNetScene.instance.m_prefabs)
            {
                var beacon = prefab != null ? prefab.GetComponentInChildren<Beacon>(true) : null;
                if (beacon != null) found.Add($"{prefab.name} {Numbers.Fixed(beacon.m_range, 0)} m");
            }
            var line = $"{Numbers.Count(found.Count)} prefabs a Wishbone finds: {string.Join(", ", found.OrderBy(n => n, System.StringComparer.Ordinal))}";
            p.Note(line);
            log.AppendLine().AppendLine(line);
            var opened = GamePrefabs.Named("silvervein_frac");
            var still = opened != null && opened.GetComponentInChildren<Beacon>(true) != null;
            var said = opened == null ? "there is no silvervein_frac" : still ? "an opened silver vein (silvervein_frac) still carries a beacon" : "an opened silver vein (silvervein_frac) carries no beacon";
            p.Note(said);
            log.AppendLine(said);
        }

        /// <summary>
        /// The deposits, pickables and trees loaded within 60 m, the nearest few: the layers of their
        /// colliders, and whether a sight line from the camera to their middle, stopping where the
        /// compendium's sight stops, meets the thing itself, something else, or nothing; and for
        /// each beacon loaded, how deep it lies under the ground and whether that line reaches it.
        /// </summary>
        private static void DepositSight(Probe p, Player player, StringBuilder log)
        {
            var eye = GameCamera.instance != null ? GameCamera.instance.transform.position : player.GetEyePoint();
            var mask = LayerMask.GetMask(SightSolid);
            var near = new List<Component>();
            near.AddRange(Object.FindObjectsByType<MineRock5>(FindObjectsSortMode.None));
            near.AddRange(Object.FindObjectsByType<MineRock>(FindObjectsSortMode.None));
            near.AddRange(Object.FindObjectsByType<Destructible>(FindObjectsSortMode.None));
            near.AddRange(Object.FindObjectsByType<Pickable>(FindObjectsSortMode.None));
            near.AddRange(Object.FindObjectsByType<TreeBase>(FindObjectsSortMode.None));
            var nearest = near.Where(c => Vector3.Distance(c.transform.position, player.transform.position) < 60f)
                .OrderBy(c => Vector3.Distance(c.transform.position, player.transform.position)).Take(8).ToList();
            log.AppendLine().AppendLine("loaded within 60 m (prefab, kind, metres away, collider layers, a sight line to its middle):");
            var meets = new Dictionary<string, int>();
            foreach (var thing in nearest)
            {
                var said = SightTo(eye, thing.transform, mask);
                meets[said] = meets.TryGetValue(said, out var n) ? n + 1 : 1;
                var layers = thing.GetComponentsInChildren<Collider>(true).Select(c => LayerMask.LayerToName(c.gameObject.layer)).Distinct();
                log.AppendLine($"  {Utils.GetPrefabName(thing.gameObject)}\t{thing.GetType().Name}\t{Numbers.Fixed(Vector3.Distance(thing.transform.position, player.transform.position), 1)}\t{string.Join(",", layers)}\t{said}");
            }
            p.Note(nearest.Count == 0 ? "nothing to gather is loaded within 60 m: stand near deposits or bushes to see how a sight line meets them"
                : $"sight lines to the {Numbers.Count(nearest.Count)} things to gather nearest: " + string.Join(", ", meets.Select(m => $"{Numbers.Count(m.Value)} {m.Key}")));

            var beacons = Object.FindObjectsByType<Beacon>(FindObjectsSortMode.None).ToList();
            log.AppendLine().AppendLine($"beacons loaded: {Numbers.Count(beacons.Count)} (prefab, metres away, top under the ground, a sight line to it):");
            foreach (var beacon in beacons.Where(b => b != null))
            {
                var root = Networked(beacon.transform);
                var top = RenderedBounds(root.gameObject).max.y;
                var ground = ZoneSystem.instance.GetGroundHeight(root.position);
                log.AppendLine($"  {Utils.GetPrefabName(root.gameObject)}\t{Numbers.Fixed(Vector3.Distance(root.position, player.transform.position), 1)}\t{Numbers.Fixed(ground - top, 2)} m\t{SightTo(eye, root, mask)}");
            }
            if (beacons.Count > 0) p.Note($"{Numbers.Count(beacons.Count)} beacons loaded near you, their depths in the log");
            else p.Note("no beacon is loaded near you: stand in the Mountains near silver to see how deep a vein lies");
        }

        /// <summary>Whether a sight line from the eye to a thing's middle meets the thing itself, something else (named), or nothing.</summary>
        private static string SightTo(Vector3 eye, Transform thing, int mask)
        {
            var middle = RenderedBounds(thing.gameObject).center;
            if (!Physics.Linecast(eye, middle, out var hit, mask)) return "reaches it, meeting nothing";
            if (hit.collider.transform.IsChildOf(Networked(thing))) return "meets the thing itself";
            return "stopped by " + LayerMask.LayerToName(hit.collider.gameObject.layer);
        }

        /// <summary>The thing a part belongs to as the game networks it, or the part itself where none: a wall of the place it stands in is no part of it.</summary>
        private static Transform Networked(Transform part)
        {
            var view = part.GetComponentInParent<ZNetView>();
            return view != null ? view.transform : part;
        }

        /// <summary>What a thing's drawn renderers cover, or its position where it has none: a deposit hides its parts' own renderers and draws them as one.</summary>
        private static Bounds RenderedBounds(GameObject thing)
        {
            var renderers = thing.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToList();
            if (renderers.Count == 0) return new Bounds(thing.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        /// <summary>
        /// The large map opened, a 512 m square laid on its pin layer over the character's own
        /// ground and up to three more further out in view, pictured, so the picture shows whether
        /// the fog covers the layer or the layer stands over the fog.
        /// </summary>
        private static IEnumerator LayerOverFog(Probe p, Minimap map, Player player)
        {
            if (Game.m_noMap)
            {
                p.Note("this world has no map, so no squares are laid on it");
                yield break;
            }
            Session.Hide();
            map.SetMapMode(Minimap.MapMode.Large);
            yield return new Wait(1f);
            var image = map.m_mapImageLarge;
            var window = image.uvRect;
            var area = image.rectTransform.rect;
            // Squares further and further out along one way that keeps them in view, so some fall
            // in fog; the picture tells which.
            var at = player.transform.position;
            var side = area.width * (512f / (map.m_textureSize * map.m_pixelSize)) / window.width;
            Square(map, at, side, window, area);
            var laid = 0;
            for (var turn = 0; turn < 8 && laid == 0; turn++)
            {
                var way = Quaternion.Euler(0f, turn * 45f, 0f) * Vector3.forward;
                foreach (var reach in new[] { 700f, 1300f, 1900f })
                {
                    var point = at + way * reach;
                    if (MapMarks.Place(new Vec3(point.x, 0f, point.z), map.m_pixelSize, map.m_textureSize, (window.xMin, window.yMin, window.width, window.height), (area.width, area.height), out _))
                    {
                        Square(map, point, side, window, area);
                        laid++;
                    }
                }
            }
            p.Note($"{Numbers.Count(laid + 1)} squares of 512 m laid on the map's pin layer, one over your own ground; the picture shows how fog and the layer meet");
            yield return new Wait(0.5f);
            yield return Picture(p, "show on map checks, a 512 m square over your ground and one over fog");
        }

        /// <summary>A 512 m square of a see-through orange laid on the large map's pin layer at a world point.</summary>
        private static void Square(Minimap map, Vector3 point, float side, Rect window, Rect area)
        {
            if (!MapMarks.Place(new Vec3(point.x, 0f, point.z), map.m_pixelSize, map.m_textureSize, (window.xMin, window.yMin, window.width, window.height), (area.width, area.height), out var at)) return;
            var square = new GameObject("Scry map check square", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)square.transform;
            rect.SetParent(map.m_pinNameRootLarge != null ? map.m_pinNameRootLarge : map.m_pinRootLarge, false);
            // Anchored as the game's pins are, which the map's pin layer places from its lower left.
            var pin = map.m_pinPrefab != null ? map.m_pinPrefab.transform as RectTransform : null;
            rect.anchorMin = pin != null ? pin.anchorMin : Vector2.zero;
            rect.anchorMax = pin != null ? pin.anchorMax : Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(at.X, at.Y);
            rect.sizeDelta = new Vector2(side, side);
            var fill = square.GetComponent<Image>();
            fill.color = new Color(0.84f, 0.54f, 0.16f, 0.45f);
            fill.raycastTarget = false;
            MapCheckSquares.Add(square);
        }

        /// <summary>The squares taken off the map and the map closed, as before <see cref="MapChecks"/>.</summary>
        private static void MapChecksAsTheyWere()
        {
            foreach (var square in MapCheckSquares) if (square != null) Object.Destroy(square);
            MapCheckSquares.Clear();
            if (Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large) Minimap.instance.SetMapMode(Minimap.MapMode.Small);
        }
    }
}
