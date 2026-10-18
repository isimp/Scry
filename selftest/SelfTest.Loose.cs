using System.Collections;
using System.Linq;

namespace Scry
{
    internal static partial class SelfTest
    {
        /// <summary>
        /// A made-up character's pages read under the loose rules as a player sees them: a known
        /// windmill names neither the grain it grinds nor the flour before either was met, says
        /// faintly how many more were not met and keeps the grain's name back in text, its numbers
        /// whole; met, both are named. Stone's page names only the givers met. A known dungeon's
        /// example leaves the creatures not met off the stage, and the card of a room they were
        /// rolled in counts them. Each page is pictured.
        /// </summary>
        private static IEnumerator CompendiumLooseRead(Probe p)
        {
            var mill = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Piece && e.Name == "windmill");
            var barley = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == "Barley");
            var flour = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == "BarleyFlour");
            if (mill == null || barley == null || flour == null)
            {
                p.Skip("there is no windmill, barley or barley flour");
                yield break;
            }
            var book = new CompendiumBook();
            book.Raise(mill.Key, KnownLevel.Known);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Loose);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);

            // The windmill: what it grinds is left out and counted, its numbers stay.
            var grinds = Facts.For(mill).Rows.Any(r => r.Items.Any(i => i.Prefab == barley.Name));
            if (!grinds) p.Note("the windmill's page names no barley in a row: the data changed");
            X.Select(mill);
            var left = ScryPanel.Drawn(PanelPart.StrictLeftOut);
            yield return GoTo("does");
            yield return Until(() => ScryPanel.Drawn(PanelPart.StrictLeftOut) > left, 3);
            p.Check(X.NamesOnlyMet && X.LeavesOut(barley.Key) && X.LeavesOut(flour.Key) && !X.LeavingOut,
                "under the loose rules a known windmill names neither barley nor its flour, not yet met, and keeps its numbers");
            p.Check(!grinds || ScryPanel.Drawn(PanelPart.StrictLeftOut) > left, "and says faintly how many more were not yet met");
            var masked = ScryPanel.AsDrawn(X, $"Grinds {barley.ShownName} into flour");
            p.Check(!masked.Contains(barley.ShownName) && masked.Contains(CompendiumWords.Hidden), "a name not yet met reads ??? in a page's text", masked);
            yield return PictureOf(p, "does", "loose windmill, barley not yet met");

            book.Raise(barley.Key, KnownLevel.Seen);
            book.Raise(flour.Key, KnownLevel.Seen);
            view.Tell(book, CompendiumRule.Loose);
            yield return Until(() => !X.LeavesOut(barley.Key), 3);
            p.Check(!X.LeavesOut(barley.Key) && !X.LeavesOut(flour.Key), "seen, barley and its flour are named");
            yield return PictureOf(p, "does", "loose windmill, barley met");

            // Stone: only the givers met are named.
            var stone = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == "Stone");
            var givers = stone == null ? null : Facts.For(stone).Where.Where(s => !string.IsNullOrEmpty(s.Prefab) && X.Find(s.Prefab) != null).Select(s => s.Prefab).Distinct().ToList();
            if (givers != null && givers.Count > 1)
            {
                book.Raise(stone.Key, KnownLevel.Known);
                book.Raise(givers[0], KnownLevel.Seen);
                view.Tell(book, CompendiumRule.Loose);
                X.Select(stone);
                var stoneLeft = ScryPanel.Drawn(PanelPart.StrictLeftOut);
                yield return GoTo("from");
                yield return Until(() => ScryPanel.Drawn(PanelPart.StrictLeftOut) > stoneLeft, 3);
                var named = givers.Count(g => !X.LeavesOut(g));
                p.Note($"Stone: {Numbers.Count(givers.Count)} givers in the catalog, {Numbers.Count(named)} named with one met");
                p.Check(!X.LeavesOut(givers[0]) && X.LeavesOut(givers[1]) && ScryPanel.Drawn(PanelPart.StrictLeftOut) > stoneLeft,
                    "Stone's page names the giver met and counts the rest faintly");
                yield return PictureOf(p, "from", "loose Stone, where it comes from, one giver met");
            }
            else p.Note("no stone with two givers in the catalog");

            // A known dungeon: the creatures not met stand nowhere on its example, and a room's card counts them.
            var dungeon = ReferenceDungeons.Select(n => X.Catalog.FirstOrDefault(e => e.Name == n && PlaceOf(e) != null && !PlaceOf(e).IsRoom)).FirstOrDefault(e => e != null);
            if (dungeon == null)
            {
                p.Note("none of the reference dungeons is in the catalog");
                Session.ShowKnownInstead(null);
                yield break;
            }
            book.Raise(dungeon.Key, KnownLevel.Known);
            view.Tell(book, CompendiumRule.Loose);
            var wasInside = Stage.Inside;
            Stage.Inside = true;
            BigStage();
            var listsWere = ScryPanel.OpenCardListsForTest(true, true);
            Select(dungeon);
            yield return Until(() => CopyOf(dungeon) != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal && Stage.ExampleFloorsToRead == 0 && Stage.CreaturesWaiting == 0, 60);
            var ways = Stage.ExampleWays;
            var rolled = ways == null ? 0 : ways.Rooms.Sum(r => Stage.RoomCreatures(r).Count);
            p.Note($"{dungeon.Name}: {Numbers.Count(rolled)} creatures rolled, {Numbers.Count(Stage.CreaturesMade)} made, {Numbers.Count(Stage.CreaturesLeftOff)} left off");
            p.Check(rolled == 0 || Stage.CreaturesMade == 0 && Stage.CreaturesLeftOff > 0, "a known dungeon's example leaves the creatures not met off the stage");
            var room = ways?.Rooms.FirstOrDefault(r => Stage.RoomCreatures(r).Count > 0);
            if (room != null)
            {
                Stage.SetCutFromSide(false);
                ScryPanel.DoubleClickRoomForTest(X, room);
                var drawn = ScryPanel.Drawn(PanelPart.RoomCard);
                yield return Until(() => Stage.RoomGoneInto == room && ScryPanel.Drawn(PanelPart.RoomCard) > drawn + 1 && ScryPanel.RoomCardLines.Count > 0, 5);
                var lines = ScryPanel.RoomCardLines.ToList();
                p.Note($"{dungeon.Name}'s {room.Room.Name}'s card: " + string.Join(" | ", lines));
                var met = CompendiumWords.NotYetMet(null, Stage.RoomCreatures(room).Count);
                p.Check(lines.Contains(met), "and a room's card counts them as not yet met", met);
                yield return Picture(p, $"loose {dungeon.Name} room card, creatures not yet met");
            }
            else p.Note($"{dungeon.Name}: no room of this roll holds a creature");
            ScryPanel.OpenCardListsForTest(listsWere.Creatures, listsWere.Loot);
            Stage.Inside = wasInside;
            Session.ShowKnownInstead(null);
        }
    }
}
