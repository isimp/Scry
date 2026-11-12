using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for a spot crossed off or highlighted on the map, by a click or its menu.</summary>
    internal static partial class SelfTest
    {
        private static List<(int X, int Z)> _modeSpots;

        /// <summary>
        /// A kept deposit's spots clicked through the game's own handling of a click: one crossed, then
        /// highlighted, then plain, its cross and ring standing and its words saying what the next click
        /// does; a right click opening its menu, a line of it giving a mode; the chip counting those
        /// crossed; crossed spots leaving the map on Hidden; far out, crossed spots merged apart from
        /// plain ones and a highlighted one alone, a click on a merged mark changing every spot it stands for.
        /// </summary>
        private static IEnumerator SpotModesTold(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            var player = Player.m_localPlayer;
            var map = Minimap.instance;
            var world = CompendiumMap.World;
            const string copper = "rock4_copper";
            if (book == null || player == null || map == null || world == null || Game.m_noMap || X.Find(copper) == null)
            {
                p.Skip("there is no character, no map, or no copper in this game");
                yield break;
            }
            KeepWhatIsKept(book);
            NoteMapAsItWas(map);
            _crossedBefore = Settings.CrossedMarks;
            Settings.FarMarks = FarMarks.Full;
            Settings.CrossedMarks = CrossedMarks.Faded;
            yield return MapCodeCompiled(p);
            var clear = ClearGround(map, player.transform.position);
            var at = clear ?? new Vector2(player.transform.position.x, player.transform.position.z);
            var (cx, cz) = ((int)at.x, (int)at.y);
            _modeSpots = new List<(int X, int Z)> { (cx - 60, cz), (cx + 60, cz), (cx + 90, cz + 90), (cx + 100, cz + 90), (cx + 110, cz + 90), (cx + 120, cz + 90) };
            foreach (var (x, z) in _modeSpots) book.SetSpotMode(world, x, z, SpotMode.Plain);
            MapSpots.SpotsInsteadForTest(k => k == copper ? _modeSpots : new List<(int X, int Z)>());
            ScryPanel.TakeAllOffMap();
            ScryPanel.KeepOnMap(copper, true);
            Session.Hide();
            MapSpots.Hide();
            if (Cursor.lockState != CursorLockMode.Locked) ZInput.SetMousePosition(new Vector2(2f, 2f));
            map.SetMapMode(Minimap.MapMode.Large);
            map.LargeZoom = SeenOnMap.NearestZoom;
            CentreMapOn(map, at);
            yield return Until(() => MapSpots.DotsShown == _modeSpots.Count, 5);
            p.Check(MapSpots.DotsShown == _modeSpots.Count && MapSpots.ModesShownForTest == (0, 0), "every spot stands plain at first", Numbers.Count(MapSpots.DotsShown));

            var (sx, sz) = _modeSpots[0];
            if (MapSpots.DotCentreAtForTest(sx, sz) is Vector2 onFirst)
            {
                PointerTo(onFirst);
                yield return Until(() => (MapSpots.Pointed?.Words ?? "").Contains("right-click"), 3);
                p.Check((MapSpots.Pointed?.Words ?? "").Contains(SpotWords.ModeHint(SpotMode.Plain, 1)), "the pointer on a dot says a click crosses it off", MapSpots.Pointed?.Words ?? "");
                LeftClick(map, onFirst);
                yield return Until(() => MapSpots.ModesShownForTest.Crosses == 1, 3);
                p.Check(book.SpotModeAt(world, sx, sz) == SpotMode.Crossed && MapSpots.ModesShownForTest == (1, 0), "a click crosses that one spot off, under a cross", $"{book.SpotModeAt(world, sx, sz)}, {MapSpots.ModesShownForTest}");
                var chips = MapSpots.KindChips();
                p.Check(chips.Count > 0 && chips[0].Crossed == 1, "the kinds list counts it", chips.Count > 0 ? SpotWords.KindChip(chips[0].Name, chips[0].Seen, chips[0].Told, chips[0].Crossed) : "");
                yield return Picture(p, "a spot crossed off");
                yield return Until(() => (MapSpots.Pointed?.Words ?? "").Contains(SpotWords.ModeHint(SpotMode.Crossed, 1)), 3);
                LeftClick(map, onFirst);
                yield return Until(() => MapSpots.ModesShownForTest.Highlights == 1, 3);
                p.Check(book.SpotModeAt(world, sx, sz) == SpotMode.Highlighted && MapSpots.ModesShownForTest == (0, 1), "a second highlights it, bigger in a ring over the pins", $"{book.SpotModeAt(world, sx, sz)}, {MapSpots.ModesShownForTest}");
                yield return Picture(p, "a spot highlighted");
                LeftClick(map, onFirst);
                yield return Until(() => MapSpots.ModesShownForTest == (0, 0), 3);
                p.Check(book.SpotModeAt(world, sx, sz) == SpotMode.Plain, "a third puts it back");

                // A right click opens the menu, naming the modes it does not stand in, a line giving one.
                RightClick(map);
                yield return Until(() => MapSpots.MenuOpen, 3);
                p.Check(MapSpots.MenuOpen && MapSpots.MenuWordsForTest.Count >= 3 && MapSpots.MenuWordsForTest[MapSpots.MenuWordsForTest.Count - 1] == SpotWords.MenuWord(MapMenuChoice.OpenPage, 1) && MapSpots.MenuWordsForTest.Contains(SpotWords.MenuWord(MapMenuChoice.Highlight, 1)) && !MapSpots.MenuWordsForTest.Contains(SpotWords.MenuWord(MapMenuChoice.Plain, 1)), "a right click opens its menu, the other modes and its page", string.Join(", ", MapSpots.MenuWordsForTest));
                p.Check(MapSpots.PressTaken(), "while it stands, a press on the map only puts it away");
                yield return Picture(p, "a spot's menu");
                var highlightLine = MapSpots.MenuWordsForTest.IndexOf(SpotWords.MenuWord(MapMenuChoice.Highlight, 1));
                if (highlightLine >= 0) MapSpots.PickMenuForTest(highlightLine);
                yield return Until(() => MapSpots.ModesShownForTest.Highlights == 1, 3);
                p.Check(book.SpotModeAt(world, sx, sz) == SpotMode.Highlighted && !MapSpots.MenuOpen, "its line highlights it, the menu going", book.SpotModeAt(world, sx, sz).ToString());
                book.SetSpotMode(world, sx, sz, SpotMode.Plain);
                MapKept.MarksChanged();

                // Hidden: a crossed spot leaves the map, and comes back with the setting.
                book.SetSpotMode(world, sx, sz, SpotMode.Crossed);
                MapKept.MarksChanged();
                Settings.CrossedMarks = CrossedMarks.Hidden;
                yield return Until(() => MapSpots.DotsShown == _modeSpots.Count - 1, 3);
                p.Check(MapSpots.DotsShown == _modeSpots.Count - 1, "on Hidden, a crossed spot leaves the map", Numbers.Count(MapSpots.DotsShown));
                Settings.CrossedMarks = CrossedMarks.Faded;
                yield return Until(() => MapSpots.DotsShown == _modeSpots.Count, 3);
                p.Check(MapSpots.DotsShown == _modeSpots.Count, "on Faded it stands again", Numbers.Count(MapSpots.DotsShown));
                book.SetSpotMode(world, sx, sz, SpotMode.Plain);
                MapKept.MarksChanged();
            }
            else p.Check(false, "the first spot stands on the screen to point at");

            // Far out: four close together, one crossed and one highlighted, the other two merged.
            if (Cursor.lockState != CursorLockMode.Locked) ZInput.SetMousePosition(new Vector2(2f, 2f));
            book.SetSpotMode(world, _modeSpots[2].X, _modeSpots[2].Z, SpotMode.Crossed);
            book.SetSpotMode(world, _modeSpots[3].X, _modeSpots[3].Z, SpotMode.Highlighted);
            MapKept.MarksChanged();
            Settings.FarMarks = FarMarks.PlainMerged;
            map.LargeZoom = 0.06f;
            yield return Until(() => MapSpots.MergedShownForTest == 1, 5);
            p.Check(MapSpots.MergedShownForTest == 1 && MapSpots.ModesShownForTest.Highlights == 1, "far out, the crossed and the highlighted stand apart from those merged", $"{Numbers.Count(MapSpots.MergedShownForTest)} merged, {MapSpots.ModesShownForTest}");
            yield return Picture(p, "spots crossed and highlighted far out");
            var merged = MapSpots.DotCentreAtForTest(_modeSpots[4].X, _modeSpots[4].Z);
            if (merged is Vector2 onMerged && MapSpots.FannedShown == 0)
            {
                PointerTo(onMerged);
                yield return Until(() => (MapSpots.Pointed?.Words ?? "").Contains("all 2") || MapSpots.FannedShown > 0, 3);
                // Near others it spreads first, as any stack does: the merged mark is then pointed at in the spread.
                if (MapSpots.FannedShown > 0 && MapSpots.FannedDotOfSpotForTest(_modeSpots[4].X, _modeSpots[4].Z) is Vector2 inFan)
                {
                    PointerTo(inFan);
                    yield return Until(() => (MapSpots.Pointed?.Words ?? "").Contains("all 2"), 3);
                }
                p.Check((MapSpots.Pointed?.Words ?? "").Contains("all 2"), "the pointer on a merged mark says the click takes every spot", MapSpots.Pointed?.Words ?? "");
                LeftClick(map, (Vector2)ZInput.pointerPosition);
                yield return Until(() => book.SpotModeAt(world, _modeSpots[4].X, _modeSpots[4].Z) == SpotMode.Crossed, 3);
                p.Check(book.SpotModeAt(world, _modeSpots[4].X, _modeSpots[4].Z) == SpotMode.Crossed && book.SpotModeAt(world, _modeSpots[5].X, _modeSpots[5].Z) == SpotMode.Crossed, "a click on a merged mark crosses every spot it stands for");
            }
            else p.Check(false, "the merged mark stands on the screen to point at");
            SpotModesAsTheyWere();
        }

        private static CrossedMarks? _crossedBefore;

        /// <summary>A click on the map as the game hears one, through its own handling, pressed and let go at a point.</summary>
        private static void LeftClick(Minimap map, Vector2 at)
        {
            MapSpots.PressedForTest(at);
            MapMarkButtons.PressedForTest(at);
            map.OnMapLeftClick();
        }

        /// <summary>A right click on the map as the game hears one, through its own handling.</summary>
        private static void RightClick(Minimap map) => AccessTools.Method(typeof(Minimap), "RemovePinUnderPointer").Invoke(map, null);

        /// <summary>The spots' modes and the setting put back with the map, should the part have stopped before it did.</summary>
        private static void SpotModesAsTheyWere()
        {
            MapSpots.CloseMenu();
            if (_crossedBefore is CrossedMarks was) Settings.CrossedMarks = was;
            _crossedBefore = null;
            if (_modeSpots != null && CompendiumKeeper.Book is CompendiumBook book && CompendiumMap.World is string world)
            {
                foreach (var (x, z) in _modeSpots) book.SetSpotMode(world, x, z, SpotMode.Plain);
                MapKept.MarksChanged();
            }
            _modeSpots = null;
            CrowdedAsItWas();
        }
    }
}
