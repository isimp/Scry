using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Ground on the large map where a part's made-up marks can be pictured: explored, so they
    /// stand, and with no pin of the game's near, as pins and their names stand over Scry's marks
    /// and would hide what the picture is of.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>How far from any pin the ground lies, and how far round it is explored, in metres.</summary>
        private const float PinFree = 250f, ExploredRound = 100f;

        private static readonly AccessTools.FieldRef<Minimap, List<Minimap.PinData>> MapPins = AccessTools.FieldRefAccess<Minimap, List<Minimap.PinData>>("m_pins");
        private static readonly AccessTools.FieldRef<Minimap, Vector3> MapPanned = AccessTools.FieldRefAccess<Minimap, Vector3>("m_mapOffset");

        /// <summary>
        /// The nearest such ground to the character, looked for on rings from 300 m to 3 km out,
        /// sixteen ways round each, as world x and z; none where the explored map holds none.
        /// </summary>
        private static Vector2? ClearGround(Minimap map, Vector3 from)
        {
            var pins = MapPins(map);
            foreach (var ring in new[] { 300f, 500f, 800f, 1200f, 1700f, 2300f, 3000f })
            {
                for (var way = 0; way < 16; way++)
                {
                    var angle = way * Mathf.PI / 8f;
                    var (x, z) = (from.x + Mathf.Cos(angle) * ring, from.z + Mathf.Sin(angle) * ring);
                    if (Explored(map, x, z) && NoPinNear(pins, x, z)) return new Vector2(x, z);
                }
            }
            return null;
        }

        private static bool Explored(Minimap map, float x, float z)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    if (!CompendiumMap.ShownAt(map, x + dx * ExploredRound, z + dz * ExploredRound)) return false;
                }
            }
            return true;
        }

        private static bool NoPinNear(List<Minimap.PinData> pins, float x, float z)
        {
            if (pins == null) return true;
            foreach (var pin in pins)
            {
                if (pin == null) continue;
                var (dx, dz) = (pin.m_pos.x - x, pin.m_pos.z - z);
                if (dx * dx + dz * dz < PinFree * PinFree) return false;
            }
            return true;
        }

        /// <summary>
        /// Zoomed out until a square stands some 20 points across, too small for its count: the
        /// square holding three, shown alone on ground away from any pin so nothing hides it, shows
        /// three pips instead, pictured; then the squares round the character shown again.
        /// </summary>
        private static IEnumerator SquarePipsTold(Probe p, Minimap map, Vector3 here)
        {
            var clear = ClearGround(map, here);
            if (clear == null) p.Note("no explored ground lies away from the map's pins; the square stands by you, where pins may hide its pips");
            var at = clear ?? new Vector2(here.x, here.z);
            var (sx, sz) = (PlaceSquares.Index(at.x), PlaceSquares.Index(at.y));
            MapSpots.Show(MadeUpSpots, "Self-test places", 1, new List<(int X, int Z)>(), new List<(int X, int Z, int Left)> { (sx, sz, 3) });
            var area = map.m_mapImageLarge.rectTransform.rect.width;
            map.LargeZoom = Mathf.Clamp(PlaceSquares.Metres * area / (20f * map.m_textureSize * map.m_pixelSize), SeenOnMap.NearestZoom, SeenOnMap.FarthestZoom);
            CentreMapOn(map, new Vector2((sx + 0.5f) * PlaceSquares.Metres, (sz + 0.5f) * PlaceSquares.Metres));
            yield return Until(() => MapSquares.SquaresShown == 1 && MapSquares.PipsShown == 3 && MapSquares.CountsShown == 0, 5);
            p.Check(MapSquares.PipsShown == 3 && MapSquares.CountsShown == 0, "zoomed out, too small for its count, the square holding three shows three pips instead",
                $"{Numbers.Count(MapSquares.SquaresShown)} squares, {Numbers.Count(MapSquares.PipsShown)} pips, {Numbers.Count(MapSquares.CountsShown)} counts");
            yield return Picture(p, "show on map, a square zoomed out with pips");
            MapPanned(map) = Vector3.zero;
            map.LargeZoom = SpotsZoom;
        }

        /// <summary>The large map centred on a point of the world, as dragging it there would, the character where it stands.</summary>
        private static void CentreMapOn(Minimap map, Vector2 at)
        {
            var player = Player.m_localPlayer;
            if (player == null) return;
            MapPanned(map) = new Vector3(at.x - player.transform.position.x, 0f, at.y - player.transform.position.z);
        }
    }
}
