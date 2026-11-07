using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for kept marks that touch on the map: pips under them at rest, fanned out when pointed at.</summary>
    internal static partial class SelfTest
    {
        /// <summary>The map's zoom and the pointer before the part, put back after it.</summary>
        private static (float Zoom, Vector2 Pointer)? _stacksBefore;

        /// <summary>
        /// Two kinds kept 20 m apart, as remains of one find lie, and a third mark of one
        /// of them farther off: zoomed out, the two touch and show two pips under them, the lone one
        /// none; the pointer on them fans them out round their spot, naming both, one of them named
        /// alone when pointed at in the fan, and the fan closes as the pointer leaves it; zoomed in,
        /// they part and the pips go; on the minimap they show their pips too. Pictured at rest, fanned
        /// and on the minimap.
        /// </summary>
        private static IEnumerator StackedMarksTold(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            var player = Player.m_localPlayer;
            var map = Minimap.instance;
            const string copper = "rock4_copper", tin = "MineRock_Tin";
            if (book == null || player == null || map == null || Game.m_noMap || X.Find(copper) == null || X.Find(tin) == null)
            {
                p.Skip("there is no character, no map, or no copper or tin in this game");
                yield break;
            }
            KeepWhatIsKept(book);
            _stacksBefore = (map.LargeZoom, ZInput.pointerPosition);
            yield return MapCodeCompiled(p);
            var here = player.transform.position;
            var (hx, hz) = ((int)here.x, (int)here.z);
            var copperSpots = new List<(int X, int Z)> { (hx + 20, hz), (hx - 30, hz + 10) };
            var tinSpots = new List<(int X, int Z)> { (hx + 40, hz) };
            MapSpots.SpotsInsteadForTest(k => k == copper ? copperSpots : k == tin ? tinSpots : new List<(int X, int Z)>());
            ScryPanel.TakeAllOffMap();
            ScryPanel.KeepOnMap(copper, true);
            ScryPanel.KeepOnMap(tin, true);
            Session.Hide();
            MapSpots.Hide();
            Timing.ForgetBytesByPart();

            // Zoomed out so a mark spans some 35 m, by the touch the map's zoom gives, the two 20 m
            // apart touch: two pips under them, none under the lone one 50 m off. The pointer off
            // the map first, so it rests on no mark it would fan out.
            if (Cursor.lockState != CursorLockMode.Locked) ZInput.SetMousePosition(new Vector2(2f, 2f));
            map.SetMapMode(Minimap.MapMode.Large);
            map.LargeZoom = 0.06f;
            yield return Until(() => MapSpots.DotsShown == 3, 5);
            if (MapSpots.TouchForTest > 0f) map.LargeZoom = Mathf.Clamp(0.06f * 35f / MapSpots.TouchForTest, SeenOnMap.NearestZoom, SeenOnMap.FarthestZoom);
            yield return Until(() => MapSpots.DotsShown == 3 && MapSpots.PipsShown == 2, 5);
            p.Note($"zoomed out to {Numbers.Amount(map.LargeZoom, 3)}, marks touch within {Numbers.Amount(MapSpots.TouchForTest, 1)} m");
            p.Check(MapSpots.DotsShown == 3 && MapSpots.PipsShown == 2, "zoomed out, two kinds lying 20 m apart stand where they lie with two pips under them, the lone mark none",
                $"{Numbers.Count(MapSpots.DotsShown)} dots, {Numbers.Count(MapSpots.PipsShown)} pips");
            yield return Picture(p, "stacked marks at rest, zoomed out");

            // The pointer on them fans them out, naming both.
            var stacked = MapSpots.StackedDotForTest();
            if (stacked is Vector2 at)
            {
                PointerTo(at);
                yield return Until(() => MapSpots.FannedShown == 2, 3);
                var said = MapSpots.Pointed?.Words ?? "";
                p.Check(MapSpots.FannedShown == 2 && said.StartsWith("2 marks here", System.StringComparison.Ordinal) && said.Contains(MapKept.NameOf(copper)) && said.Contains(MapKept.NameOf(tin)),
                    "the pointer on them fans them out round their spot and names both kinds there", $"{Numbers.Count(MapSpots.FannedShown)} fanned: {said}");
                yield return Picture(p, "stacked marks pointed at, fanned out");

                // One of the fanned named alone, ringed.
                if (MapSpots.FanCenterForTest(0) is Vector2 one)
                {
                    PointerTo(one);
                    yield return Until(() => MapSpots.Pointed?.Ring != null, 3);
                    said = MapSpots.Pointed?.Words ?? "";
                    p.Check(MapSpots.Pointed?.Ring != null && MapSpots.FannedShown == 2 && !said.Contains("marks here"), "a mark pointed at in the fan is named alone and ringed, the fan staying open", said);
                }
                else p.Check(false, "a fanned mark stands on the screen to point at");

                // Away from the fan, it closes and the marks stand where they lie again.
                PointerTo(at + new Vector2(Screen.width / 4f, 0f));
                yield return Until(() => MapSpots.FannedShown == 0 && MapSpots.PipsShown == 2, 3);
                p.Check(MapSpots.FannedShown == 0 && MapSpots.PipsShown == 2 && MapSpots.DotsShown == 3, "the pointer gone from the fan, it closes, the marks and pips standing as before",
                    $"{Numbers.Count(MapSpots.FannedShown)} fanned, {Numbers.Count(MapSpots.DotsShown)} dots, {Numbers.Count(MapSpots.PipsShown)} pips");
            }
            else p.Check(false, "a stacked mark stands on the screen to point at");

            // Zoomed in, 20 m apart, they part and the pips go, where the nearest zoom parts them at all.
            map.LargeZoom = SeenOnMap.NearestZoom;
            yield return Until(() => MapSpots.PipsShown == 0 && MapSpots.DotsShown >= 2, 5);
            if (MapSpots.TouchForTest < 20f) p.Check(MapSpots.PipsShown == 0, "zoomed in, the two part and their pips go", $"{Numbers.Count(MapSpots.PipsShown)} pips, touching within {Numbers.Amount(MapSpots.TouchForTest, 1)} m");
            else p.Note($"at the nearest zoom marks still touch within {Numbers.Amount(MapSpots.TouchForTest, 1)} m, so two 20 m apart never part");
            p.Within(Timing.MostMs("spots stacked"), 2, "gathering the marks into stacks holds no frame long", $"{Numbers.Amount(Timing.MostMs("spots stacked"), 2)} ms at the most");
            // As many marks as the map keeps at most, gathered in the game's own runtime: the quickest of five.
            var random = new System.Random(7);
            var many = new (float X, float Z)[SeenOnMap.MostKept];
            for (var i = 0; i < many.Length; i++) many[i] = ((float)random.NextDouble() * 10000f, (float)random.NextDouble() * 10000f);
            var stacks = new MarkStacks();
            var quickest = double.MaxValue;
            for (var round = 0; round < 6; round++)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                stacks.Gather(many.Length, i => many[i], 90f);
                if (round > 0) quickest = System.Math.Min(quickest, watch.Elapsed.TotalMilliseconds);
            }
            p.Within(quickest, 3, $"gathering the {Numbers.Count(many.Length)} marks the map keeps at most holds no frame long", $"{Numbers.Amount(quickest, 2)} ms");

            // On the minimap, where its marks are small, a stack shows its pips by the minimap's own zoom.
            Settings.DungeonMarksOnMinimap = true;
            map.SetMapMode(Minimap.MapMode.Small);
            yield return Until(() => MiniKept.DotsShown == 3 && MiniKept.TouchForTest > 0f, 5);
            yield return null;
            var pips = MiniKept.TouchForTest > 20f ? 2 : 0;
            p.Check(MiniKept.PipsShown == pips, pips == 2 ? "on the minimap the two touch and show their pips too" : "on the minimap, zoomed near, the two stand apart with no pips",
                $"{Numbers.Count(MiniKept.DotsShown)} dots, {Numbers.Count(MiniKept.PipsShown)} pips, touching within {Numbers.Amount(MiniKept.TouchForTest, 1)} m");
            yield return Picture(p, "stacked marks on the minimap");
            StacksAsTheyWere();
        }

        /// <summary>The map's zoom, the pointer and what was kept put back, should the part have stopped before it did.</summary>
        private static void StacksAsTheyWere()
        {
            if (_stacksBefore is (float zoom, Vector2 pointer))
            {
                if (Minimap.instance != null) Minimap.instance.LargeZoom = zoom;
                if (Cursor.lockState != CursorLockMode.Locked) ZInput.SetMousePosition(pointer);
            }
            _stacksBefore = null;
            KeptOnMapAsItWas();
        }
    }
}
