using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// The plan box of a dungeon's example inside. For each reference dungeon, each of its
        /// views, Above, Map and Tree, is drawn compact and grown, with a spot for every room;
        /// each room is found under the mouse at its spot (in the tree always, from above and on
        /// the map unless a room drawn over it stands there); grown, the stage is framed beside it.
        /// Dragging across the map turns it, and Least overlap turns it to the angle the model
        /// picks. A room lit from the box is outlined on the stage with no room gone into, and a
        /// click on one goes into it. The view and size chosen are remembered in the panel's file.
        /// The panel's font holds every sign the box and card draw. Each view is pictured.
        /// </summary>
        private static IEnumerator PlanBoxShows(Probe p)
        {
            var places = ReferenceDungeons.Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).Where(e => e != null).ToList();
            if (places.Count == 0) p.Skip("none of " + string.Join(", ", ReferenceDungeons) + " is in the catalog");
            var wasInside = Stage.Inside;
            var wasView = ScryPanel.PlanBoxView;
            var wasBig = ScryPanel.PlanBoxBig;
            var wasFolded = ScryPanel.PlanFolded;
            ScryPanel.PlanFolded = false;
            Stage.Inside = true;
            BigStage();

            var missing = string.Concat("×·›°–".Where(c => !Skin.FontHolds(c)));
            p.Check(missing.Length == 0, "the panel's font holds every sign the plan box and the card draw", $"{Skin.FontName} lacks {missing}");

            var unspotted = new List<string>();
            var unfound = new List<string>();
            var unframed = new List<string>();
            var unturned = new List<string>();
            var notLeast = new List<string>();
            var unlit = new List<string>();
            var notIn = new List<string>();
            foreach (var entry in places)
            {
                Select(entry);
                yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleFloorsToRead == 0, 60);
                var ways = Stage.ExampleWays;
                if (CopyOf(entry) == null || ways == null)
                {
                    p.Note($"{entry.Name}: no example stood on the stage");
                    continue;
                }
                Stage.SetCutFromSide(false);
                Stage.OpenLevel(Stage.FloorHeights.Count);
                Stage.ComeOut();

                foreach (var big in new[] { false, true })
                {
                    foreach (var view in new[] { PlanView.Above, PlanView.Map, PlanView.Tree })
                    {
                        ScryPanel.GrowPlanForTest(big);
                        ScryPanel.ShowPlanViewForTest(view);
                        var drawn = ScryPanel.Drawn(PanelPart.PlanBox);
                        yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawn + 1, 3);
                        var name = $"{entry.Name} {PlanBoxWords.ViewName(view)} {(big ? "grown" : "compact")}";
                        var spots = ScryPanel.PlanBoxSpotsForTest();
                        if (spots.Count != ways.Rooms.Count) unspotted.Add($"{name}: {Numbers.Count(spots.Count)} spots for {Numbers.Count(ways.Rooms.Count)} rooms");

                        // Each room under the mouse at its spot; from above and on the map one drawn over it may stand there.
                        var found = 0;
                        var covered = 0;
                        foreach (var (room, at) in spots)
                        {
                            var under = ScryPanel.PlanBoxRoomAtForTest(at);
                            if (under == room) found++;
                            else if (view != PlanView.Tree && under != null && spots.FindIndex(s => s.Room == under) > spots.FindIndex(s => s.Room == room)) covered++;
                            else unfound.Add($"{name}: {room.Room.Name} found as {(under != null ? under.Room.Name : "nothing")}");
                        }
                        p.Note($"{name}: {Numbers.Count(found)} of {Numbers.Count(spots.Count)} rooms found at their spots, {Numbers.Count(covered)} under a room drawn over them" + (view == PlanView.Map ? $", {Numbers.Count(ScryPanel.MapOverlapsShown)} overlapping at {PlanBoxWords.Angle(ScryPanel.MapAngleShown)}" : ""));

                        if (big != Stage.ViewShift > 0f) unframed.Add($"{name}: the stage framed {Numbers.Fixed(Stage.ViewShift, 2)} of its width aside");
                        yield return Picture(p, $"plan {name}");
                    }
                }

                // The map: dragged across, it turns; Least overlap turns it to the angle the model picks.
                ScryPanel.GrowPlanForTest(false);
                ScryPanel.ShowPlanViewForTest(PlanView.Map);
                ScryPanel.SetMapAngleForTest(10f);
                ScryPanel.DragMapForTest(25f);
                if (System.Math.Abs(ScryPanel.MapAngleShown - 30f) > 0.01f) unturned.Add($"{entry.Name}: at {Numbers.Fixed(ScryPanel.MapAngleShown, 1)} after a drag from 10");
                var rooms = ways.Rooms.ToList();
                var expected = PlanMap.LeastOverlap(rooms, ScryPanel.MapAngleShown);
                ScryPanel.LeastOverlapForTest();
                var drawnMap = ScryPanel.Drawn(PanelPart.PlanBox);
                yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnMap + 1, 3);
                var fewest = Enumerable.Range(0, 36).Min(i => PlanMap.Overlaps(rooms, i * PlanMap.Step));
                if (System.Math.Abs(ScryPanel.MapAngleShown - expected) > 0.01f || ScryPanel.MapOverlapsShown != fewest)
                    notLeast.Add($"{entry.Name}: {PlanBoxWords.Angle(ScryPanel.MapAngleShown)} with {Numbers.Count(ScryPanel.MapOverlapsShown)} overlapping, the model picks {PlanBoxWords.Angle(expected)} with {Numbers.Count(fewest)}");
                p.Note($"{entry.Name}: Least overlap turned the map to {PlanBoxWords.Angle(ScryPanel.MapAngleShown)}, {Numbers.Count(ScryPanel.MapOverlapsShown)} overlapping");
                yield return Picture(p, $"plan {entry.Name} Map least overlap");

                // A room lit from the box is outlined on the stage with no room gone into; a click goes into it.
                var deepest = ways.Deepest;
                ScryPanel.LightPlanRoomForTest(deepest);
                yield return Until(() => Stage.LitOutlineShown, 3);
                if (!Stage.LitOutlineShown) unlit.Add(entry.Name);
                else yield return Picture(p, $"plan {entry.Name} room lit");
                ScryPanel.LightPlanRoomForTest(null);
                ScryPanel.ClickPlanRoomForTest(deepest);
                yield return Until(() => Stage.RoomGoneInto == deepest, 3);
                if (Stage.RoomGoneInto != deepest) notIn.Add(entry.Name);
                ScryPanel.ShowPlanViewForTest(PlanView.Tree);
                var drawnTree = ScryPanel.Drawn(PanelPart.PlanBox);
                yield return Until(() => ScryPanel.Drawn(PanelPart.PlanBox) > drawnTree + 1 && !Stage.Gliding, 5);
                yield return Picture(p, $"plan {entry.Name} Tree deepest gone into");
                Stage.ComeOut();
            }

            // What was chosen is remembered in the panel's file.
            ScryPanel.ShowPlanViewForTest(PlanView.Tree);
            ScryPanel.GrowPlanForTest(true);
            var kept = ScryPanel.RememberedForTest();
            p.Check(kept.PlanView == PlanView.Tree && kept.PlanBig == true, "the plan box's view and size are remembered with the panel", $"{kept.PlanView}, grown {kept.PlanBig}");

            ScryPanel.LightPlanRoomForTest(null);
            ScryPanel.ShowPlanViewForTest(wasView);
            ScryPanel.GrowPlanForTest(wasBig);
            ScryPanel.PlanFolded = wasFolded;
            Stage.Inside = wasInside;
            StageAsItWas();
            p.Check(unspotted.Count == 0, "each view of the plan box has a spot for every room, compact and grown", string.Join("; ", unspotted.Take(6)));
            p.Check(unfound.Count == 0, "every room is found under the mouse at its spot, unless a room drawn over it stands there", string.Join("; ", unfound.Take(6)));
            p.Check(unframed.Count == 0, "grown, the stage is framed beside the plan box; compact, in its middle", string.Join("; ", unframed.Take(6)));
            p.Check(unturned.Count == 0, "dragging across the map turns it", string.Join("; ", unturned));
            p.Check(notLeast.Count == 0, "Least overlap turns the map to the angle with the fewest rooms over each other", string.Join("; ", notLeast));
            p.Check(unlit.Count == 0, "a room lit from the plan box is outlined on the stage with no room gone into", string.Join("; ", unlit));
            p.Check(notIn.Count == 0, "a click on a room in the plan box goes into it", string.Join("; ", notIn));
        }

        /// <summary>Puts the plan box back as the player had it, after <see cref="PlanBoxShows"/>.</summary>
        private static void PlanBoxAsItWas()
        {
            ScryPanel.LightPlanRoomForTest(null);
            ScryPanel.GrowPlanForTest(false);
            Stage.ComeOut();
        }
    }
}
