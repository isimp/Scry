using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>The stage's heights against the usual one the overlaps are looked for at: low, usual and the tallest.</summary>
        private static readonly float[] StageHeights = { 0.6f, 1f, 2.4f };

        /// <summary>
        /// Nothing drawn over the stage covers anything else: the kind's tag, the chips, the ruler
        /// and the name of the floor opened, the view's buttons and the note under the picture,
        /// the plan box and the card of the room gone into. For a reference dungeon's inside, in
        /// the full view and the compact one, at a low, the usual and the tallest stage, with the
        /// plan box folded, compact and grown, with no room gone into and with its deepest gone
        /// into, the mouse on the stage so its buttons and note show; any two that overlap are
        /// told with where they stood, and the first few are pictured.
        /// </summary>
        private static IEnumerator NothingOverlapsOnStage(Probe p)
        {
            var entry = ReferenceDungeons.Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).FirstOrDefault(e => e != null);
            if (entry == null) p.Skip("none of " + string.Join(", ", ReferenceDungeons) + " is in the catalog");
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            var wasCompact = ScryPanel.Compact;
            var wasInside = Stage.Inside;
            var wasBig = ScryPanel.PlanBoxBig;
            var wasFolded = ScryPanel.PlanFolded;
            Stage.Inside = true;
            BigStage();

            Select(entry);
            yield return Until(() => CopyOf(entry) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleFloorsToRead == 0, 60);
            var ways = Stage.ExampleWays;
            if (CopyOf(entry) == null || ways == null) p.Skip($"{entry.Name}: no example stood on the stage");
            Stage.SetCutFromSide(false);
            Stage.ComeOut();
            ScryPanel.HoverStageForTest(new Vector2(0.5f, 0.5f));

            var overlaps = new List<string>();
            var looked = 0;
            var pictured = 0;
            var scrolled = 0;
            var lean = 0;
            var selfFolded = 0;
            var noWayBack = new List<string>();
            foreach (var goneInto in new[] { false, true })
            {
                if (goneInto)
                {
                    ScryPanel.ClickPlanRoomForTest(ways.Deepest);
                    yield return Until(() => Stage.RoomGoneInto == ways.Deepest && !Stage.Gliding, 5);
                }
                foreach (var compact in new[] { false, true })
                {
                    ScryPanel.Compact = compact;
                    ScryPanel.ListHidden = true;
                    foreach (var height in StageHeights)
                    {
                        ScryPanel.StageScale = height;
                        foreach (var box in new[] { "folded", "compact", "grown" })
                        {
                            ScryPanel.PlanFolded = box == "folded";
                            ScryPanel.GrowPlanForTest(box == "grown");
                            var repaints = ScryPanel.Repaints;
                            yield return Until(() => ScryPanel.Repaints >= repaints + 3, 3);
                            looked++;
                            var shape = ScryPanel.PlanBoxShapeShown;
                            if (box != "folded" && shape == BoxShape.Lean) lean++;
                            if (box != "folded" && shape == BoxShape.Folded) selfFolded++;
                            if (goneInto && ScryPanel.RoomCardScrolls) scrolled++;
                            // However the box stands, the breadcrumb ends at the room gone into.
                            var trail = ScryPanel.TrailForTest;
                            if (goneInto && (trail.Count < 2 || trail[trail.Count - 1].Part != TrailPart.Room) && noWayBack.Count < 6) noWayBack.Add($"{(compact ? "compact" : "full")} view, stage at {Numbers.Fixed(height, 1)}, plan box {box}, {shape}");
                            var name = $"{(compact ? "compact" : "full")} view, stage at {Numbers.Fixed(height, 1)}, plan box {box}{(box != "folded" && shape != BoxShape.Whole ? (shape == BoxShape.Lean ? " without its trail" : " folding itself") : "")}, {(goneInto ? "deepest room gone into" : "no room gone into")}";
                            var pieces = ScryPanel.StagePieces.ToList();
                            var found = false;
                            for (var a = 0; a < pieces.Count; a++)
                            {
                                for (var b = a + 1; b < pieces.Count; b++)
                                {
                                    if (pieces[a].Name == pieces[b].Name || !pieces[a].At.Overlaps(pieces[b].At)) continue;
                                    overlaps.Add($"{name}: {pieces[a].Name} {Where(pieces[a].At)} over {pieces[b].Name} {Where(pieces[b].At)}");
                                    found = true;
                                }
                            }
                            if (found && pictured < 4)
                            {
                                pictured++;
                                yield return Picture(p, $"overlap {entry.Name} {name}");
                            }
                        }
                    }
                }
            }
            p.Check(noWayBack.Count == 0, "with a room gone into, the breadcrumb ends at it in every shape the plan box takes", string.Join("; ", noWayBack));
            p.Note($"{entry.Name}: looked at {Numbers.Count(looked)} ways of showing it, {Numbers.Count(overlaps.Count)} overlaps found; the card scrolled in {Numbers.Count(scrolled)} of them, giving way to the plan box; the plan box dropped its trail and buttons in {Numbers.Count(lean)} and folded itself in {Numbers.Count(selfFolded)}, the stage too short");

            ScryPanel.HoverStageForTest(null);
            Stage.ComeOut();
            ScryPanel.Compact = wasCompact;
            ScryPanel.GrowPlanForTest(wasBig);
            ScryPanel.PlanFolded = wasFolded;
            Stage.Inside = wasInside;
            StageAsItWas();
            p.Check(overlaps.Count == 0, "nothing drawn over the stage covers anything else, in either view, at any stage height, with the plan box folded, compact or grown, with or without a room gone into", string.Join("; ", overlaps.Take(8)));
        }

        /// <summary>Where a piece stood on the panel and how big it was, in whole pixels: "(12, 340) (236, 180)".</summary>
        private static string Where(Rect at) => Figures.Point(at.position, 0) + " " + Figures.Point(at.size, 0);

        /// <summary>Lets go of the mouse on the stage and comes out of the room, after <see cref="NothingOverlapsOnStage"/>.</summary>
        private static void OverlapsAsTheyWere()
        {
            ScryPanel.HoverStageForTest(null);
            Stage.ComeOut();
        }
    }
}
