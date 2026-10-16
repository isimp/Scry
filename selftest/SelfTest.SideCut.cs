using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// A dungeon's example that is mostly one stack of rooms opens cut from the side, as
        /// Mörkhalla's does, and a branching cave's from above. Cut from the side, a room gone into
        /// stands outlined in view with nothing put away and no cut held over it, and the creatures
        /// are cut with the rest rather than drawn again; switched to the cut from above and back,
        /// it follows. Pictured whole and gone into.
        /// </summary>
        private static IEnumerator SideCutShows(Probe p)
        {
            var stack = X.Catalog.FirstOrDefault(e => e.Name == "MorkBorg" && PlaceOf(e) != null && !PlaceOf(e).IsRoom);
            var cave = X.Catalog.FirstOrDefault(e => e.Name == "MountainCave02" && PlaceOf(e) != null && !PlaceOf(e).IsRoom);
            if (stack == null) p.Skip("MorkBorg is not in the catalog");
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            BigStage();

            if (cave != null)
            {
                Select(cave);
                yield return Until(() => CopyOf(cave) != null && Stage.ExampleShown != null && Stage.ExampleRoomsShown > 0, 60);
                p.Check(!Stage.CutFromSide, "a branching cave opens cut from above", cave.Name);
            }

            Select(stack);
            yield return Until(() => CopyOf(stack) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.CreaturesWaiting == 0, 60);
            if (!p.Check(Stage.ExampleShown != null && Stage.CutFromSide && Stage.SideCutting, "Mörkhalla, one stack of rooms, opens cut from the side", $"side {Stage.CutFromSide}, laid {Stage.SideCutting}"))
            {
                Stage.Inside = wasInside;
                StageAsItWas();
                yield break;
            }
            yield return Until(() => !Stage.Gliding, 5);
            yield return Picture(p, "side MorkBorg whole");

            var deepest = DungeonWays.Of(Stage.ExampleShown.Rooms).Deepest;
            ScryPanel.ClickRoomForTest(X, deepest);
            yield return Until(() => Stage.RoomGoneInto == deepest && Stage.ExampleRoomsKept && Stage.OutlineShown, 5);
            yield return Until(() => !Stage.Gliding, 5);
            var view = Stage.ExampleRoomInView(deepest);
            var inView = view is UnityEngine.Vector3 v && v.z > 0f && v.x > 0.05f && v.x < 0.95f && v.y > 0.05f && v.y < 0.95f;
            p.Check(Stage.RoomGoneInto == deepest && !Stage.Cutting && Stage.ExampleRoomsAway == 0,
                "from the side, a room gone into holds no cut over it and puts no room away", $"cut held {Stage.Cutting}, {Numbers.Count(Stage.ExampleRoomsAway)} away");
            p.Check(Stage.OutlineShown && inView, "it stands outlined in the camera's view", $"outlined {Stage.OutlineShown}, in view {inView}");
            p.Check(!Stage.CreaturesRedrawnOverCut, "creatures are cut with the rest, not drawn again over the side cut");
            yield return Picture(p, "side MorkBorg deepest room");

            // Switched to the cut from above, the room is cut at head height over its level; and back.
            Stage.SetCutFromSide(false);
            yield return Until(() => Stage.ExampleRoomsKept, 5);
            var above = Stage.Cutting && System.Math.Abs(Stage.CutAt - Stage.RoomLevelCut(deepest, Stage.RoomLevelOpen ?? 0f)) < 0.01f && !Stage.SideCutting;
            Stage.SetCutFromSide(true);
            yield return Until(() => Stage.ExampleRoomsKept, 5);
            p.Check(above && Stage.SideCutting && !Stage.Cutting, "switched from the side to above and back, the cut follows", $"above {above}, side {Stage.SideCutting}");

            Stage.ComeOut();
            Stage.Inside = wasInside;
            StageAsItWas();
        }

        /// <summary>Comes out of a room and puts the cut from above back, after <see cref="SideCutShows"/>.</summary>
        private static void SideCutAsItWas()
        {
            Stage.ComeOut();
            Stage.SetCutFromSide(false);
        }
    }
}
