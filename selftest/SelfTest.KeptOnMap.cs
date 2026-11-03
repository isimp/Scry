using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for kinds kept on Valheim's map, their list, the words on what the pointer rests on, and the minimap.</summary>
    internal static partial class SelfTest
    {
        /// <summary>What the character kept on the map and whether the minimap showed it, before the part chose otherwise; put back after it.</summary>
        private static (List<string> Kept, bool Minimap)? _keptBefore;

        /// <summary>
        /// A kind kept on the map stands every time the large map opens, at the map's own
        /// brightness, nothing dimmed; the pointer resting on a dot names it, ringed; the title
        /// row's list holds it with how many stand, and its × takes it off the map; kept, it stands
        /// on the minimap too where the settings ask. Making and placing them holds no frame long.
        /// Spots of the self-test's own round the player for a kind of resource. Put back as it was.
        /// </summary>
        private static IEnumerator KeptOnMapShown(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            var player = Player.m_localPlayer;
            var kind = X.Find("rock4_copper") != null ? "rock4_copper" : null;
            if (book == null || player == null || Minimap.instance == null || Game.m_noMap || kind == null)
            {
                p.Skip("there is no character, no map, or no copper deposit in this game");
                yield break;
            }
            _keptBefore = (book.Kept.ToList(), Settings.DungeonMarksOnMinimap);
            // The kind's own page offers Keep on the map beside Show on map.
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            var keeps = ScryPanel.Drawn(PanelPart.PageKeep);
            Select(X.Find(kind));
            yield return Until(() => ScryPanel.Drawn(PanelPart.PageKeep) > keeps, 5);
            p.Check(ScryPanel.Drawn(PanelPart.PageKeep) > keeps, "the kind's page offers Keep on the map beside Show on map");
            var here = player.transform.position;
            var spots = new List<(int X, int Z)> { ((int)here.x + 20, (int)here.z), ((int)here.x - 25, (int)here.z + 10), ((int)here.x, (int)here.z - 30) };
            MapSpots.SpotsInsteadForTest(k => k == kind ? spots : new List<(int X, int Z)>());
            ScryPanel.TakeAllOffMap();
            ScryPanel.KeepOnMap(kind, true);
            p.Check(book.Keeps(kind) && !book.Keeps(CompendiumBook.Dungeons), "a kind kept, the rest taken off, is kept with the character", string.Join(", ", book.Kept));

            // Kept, it stands as the large map opens, nothing shown on demand, nothing dimmed.
            Session.Hide();
            MapSpots.Hide();
            Timing.ForgetBytesByPart();
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            yield return Until(() => MapSpots.DotsShown == spots.Count, 10);
            p.Check(MapSpots.DotsShown == spots.Count && MapSpots.Strip == null && !MapDim.Dimming, "the kept kind's dots stand as the map opens, no strip, the map and your pins as bright as ever",
                $"{Numbers.Count(MapSpots.DotsShown)} dots, dimming {MapDim.Dimming}");

            // Shown on demand as well, it stands once, the strip offering to keep it, lit as it is.
            var strips = ScryPanel.Drawn(PanelPart.MapKeep);
            MapSpots.Show(X.Find(kind));
            yield return Until(() => ScryPanel.Drawn(PanelPart.MapKeep) > strips + 1, 5);
            p.Check(MapSpots.DotsShown == spots.Count && ScryPanel.Drawn(PanelPart.MapKeep) > strips, "shown on demand too, each dot stands once, the strip's Keep on the map beside Hide",
                $"{Numbers.Count(MapSpots.DotsShown)} dots");
            MapSpots.Hide();
            yield return Until(() => MapSpots.DotsShown == spots.Count && MapSpots.Strip == null, 5);

            // The pointer on a dot names it, ringed.
            var first = MapSpots.DotCenterForTest(0);
            if (first is Vector2 at)
            {
                PointerTo(at);
                yield return Until(() => MapSpots.Pointed != null, 3);
                var said = MapSpots.Pointed?.Words ?? "";
                p.Check(said.StartsWith(MapKept.NameOf(kind) + "\n", System.StringComparison.Ordinal) && MapSpots.Pointed?.Ring != null, "the pointer on a dot names its kind, and how far and which way it is, the dot ringed", said);
                yield return Picture(p, "kept on the map, a dot pointed at");
            }
            else p.Note("no dot stands on the screen to point at");

            // The minimap shows what is kept, where the settings ask.
            Settings.DungeonMarksOnMinimap = true;
            Minimap.instance.SetMapMode(Minimap.MapMode.Small);
            yield return Until(() => MiniKept.DotsShown > 0, 5);
            p.Check(MiniKept.DotsShown > 0, "on the minimap the kept dots stand too", $"{Numbers.Count(MiniKept.DotsShown)} dots");
            yield return Picture(p, "kept on the minimap");
            p.Within(Timing.MostMs("minimap kept"), 2, "placing them on the minimap holds no frame long", $"{Numbers.Amount(Timing.MostMs("minimap kept"), 2)} ms at the most");
            p.Within(Timing.MostMs("map kept"), 8, "working out what is kept holds no frame long", $"{Numbers.Amount(Timing.MostMs("map kept"), 2)} ms at the most");
            p.Within(Timing.MostMs("spots"), 4, "the large map's dots and the pointer's words hold no frame long", $"{Numbers.Amount(Timing.MostMs("spots"), 2)} ms at the most");

            // The title row's list holds it with how many stand; its × takes it off.
            Minimap.instance.SetMapMode(Minimap.MapMode.Small);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            var lists = ScryPanel.Drawn(PanelPart.KeptList);
            ScryPanel.OpenKeptListForTest(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.KeptList) > lists + 1, 3);
            p.Check(ScryPanel.KeptListOpenForTest && ScryPanel.Drawn(PanelPart.KeptList) > lists && MapKept.CountOf(kind).Seen == spots.Count, "the title row's list holds the kind kept, counting the ones you saw",
                $"{Numbers.Count(MapKept.CountOf(kind).Seen)} counted");
            yield return Picture(p, "kept on the map, the list");
            ScryPanel.OpenKeptListForTest(false);
            ScryPanel.KeepOnMap(kind, false);
            Session.Hide();
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            yield return Until(() => MapSpots.DotsShown == 0, 5);
            p.Check(!book.Keeps(kind) && MapSpots.DotsShown == 0, "taken off, the kind's dots go from the map");
            KeptOnMapAsItWas();
        }

        /// <summary>What was kept and the minimap put back, the sight's own spots, the maps shut and the panel open, should the part have stopped before it did.</summary>
        private static void KeptOnMapAsItWas()
        {
            MapSpots.SpotsInsteadForTest(null);
            ScryPanel.OpenKeptListForTest(false);
            if (_keptBefore is (List<string> kept, bool minimap) && CompendiumKeeper.Book is CompendiumBook book)
            {
                ScryPanel.TakeAllOffMap();
                foreach (var each in kept) ScryPanel.KeepOnMap(each, true);
                Settings.DungeonMarksOnMinimap = minimap;
            }
            _keptBefore = null;
            if (Minimap.instance != null && Minimap.instance.m_mode == Minimap.MapMode.Large) Minimap.instance.SetMapMode(Minimap.MapMode.Small);
            if (!Session.IsOpen) Session.Show(null);
        }
    }
}
