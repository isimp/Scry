using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for Show on map showing every kind of a place at once.</summary>
    internal static partial class SelfTest
    {
        /// <summary>Whether the map showed all of a place's kinds before the part chose otherwise; put back after it.</summary>
        private static bool? _allKindsBefore;

        /// <summary>
        /// A place of several kinds, as the Burial Chambers, shown on the map from one kind's page:
        /// that kind's dots alone, named by it, the strip offering all its kinds; the strip's button
        /// lit, every kind's dots at once under the place's name, and off again the page's kind
        /// alone. Spots of the self-test's own, two for each kind, where the player stands.
        /// Pictured; put back as it was.
        /// </summary>
        private static IEnumerator ShowOnMapAllKinds(Probe p)
        {
            var player = Player.m_localPlayer;
            var family = X.Catalog.Where(e => e.Kind == Kind.Location && !e.IsRoom && e.Family != null && e.Family.Kinds.Count >= 2).Select(e => e.Family).FirstOrDefault();
            if (player == null || Minimap.instance == null || Game.m_noMap || family == null)
            {
                p.Skip("there is no place of several kinds, or no map in this world");
                yield break;
            }
            var kinds = family.Kinds;
            var here = player.transform.position;
            var spots = new Dictionary<string, IReadOnlyList<(int X, int Z)>>();
            for (var k = 0; k < kinds.Count; k++)
                spots[kinds[k].Key] = new List<(int X, int Z)> { ((int)here.x + 30 * (k + 1), (int)here.z), ((int)here.x, (int)here.z + 30 * (k + 1)) };
            MapSpots.SpotsInsteadForTest(kind => spots.TryGetValue(kind, out var at) ? at : new List<(int X, int Z)>());
            _allKindsBefore = MapSpots.AllKinds;
            MapSpots.ShowAllKinds(false);
            Session.Hide();
            var first = kinds[0];
            var buttons = ScryPanel.Drawn(PanelPart.MapAllKinds);
            MapSpots.Show(first, kinds, family.Name);
            yield return Until(() => MapSpots.DotsShown == 2 && ScryPanel.Drawn(PanelPart.MapAllKinds) > buttons, 10);
            p.Check(MapSpots.DotsShown == 2 && (MapSpots.Strip ?? "").StartsWith(first.ShownName + " · 2 you saw", System.StringComparison.Ordinal),
                "a kind's page shows that kind's dots alone, named by it", $"{Numbers.Count(MapSpots.DotsShown)} dots, {MapSpots.Strip}");
            p.Check(MapSpots.KindsOffered == kinds.Count && ScryPanel.Drawn(PanelPart.MapAllKinds) > buttons, $"the strip offers all {Numbers.Count(kinds.Count)} kinds of {family.Name}",
                $"{Numbers.Count(MapSpots.KindsOffered)} offered");

            MapSpots.ShowAllKinds(true);
            var all = kinds.Count * 2;
            yield return Until(() => MapSpots.DotsShown == all, 10);
            p.Check(MapSpots.DotsShown == all && (MapSpots.Strip ?? "").StartsWith(SpotWords.AllKindsName(family.Name, kinds.Count) + " · " + Numbers.Count(all) + " you saw", System.StringComparison.Ordinal),
                "with its button lit, every kind's dots at once, under the place's name", $"{Numbers.Count(MapSpots.DotsShown)} dots, {MapSpots.Strip}");
            yield return Picture(p, $"show on map, all kinds of {family.Name}");

            MapSpots.ShowAllKinds(false);
            yield return Until(() => MapSpots.DotsShown == 2, 10);
            p.Check(MapSpots.DotsShown == 2 && (MapSpots.Strip ?? "").StartsWith(first.ShownName, System.StringComparison.Ordinal), "off again, the page's kind alone", MapSpots.Strip);
            AllKindsAsTheyWere();
        }

        /// <summary>The sight's own spots and the choice of all kinds put back, the map shut, should the part have stopped before it did.</summary>
        private static void AllKindsAsTheyWere()
        {
            MapSpots.SpotsInsteadForTest(null);
            if (_allKindsBefore is bool all) MapSpots.ShowAllKinds(all);
            _allKindsBefore = null;
            ShowOnMapAsItWas();
        }
    }
}
