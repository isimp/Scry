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

        /// <summary>
        /// With the compendium on, a made-up character's list holds the world a player meets: no
        /// effect, sound, projectile, other part or mod is listed or has a tab, while peeking they
        /// are; places sharing a name say which of them each is; a known troll's page offers no
        /// spawn command, peeking it does; and its stars table waits as one faint line until one
        /// is killed. The troll's fight and the list are pictured.
        /// </summary>
        private static IEnumerator CompendiumPlayerView(Probe p)
        {
            var troll = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && e.Name == "Troll");
            if (troll == null)
            {
                p.Skip("there is no troll");
                yield break;
            }
            var book = new CompendiumBook();
            book.Raise(troll.Key, KnownLevel.Known);
            var view = new CompendiumView(X.Entries);
            view.Tell(book, CompendiumRule.Strict);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            X.Select(troll);
            yield return null;

            var listed = X.Results.Where(e => !Kinds.ForPlayers(e.Kind)).Select(e => e.Name).Take(5).ToList();
            var tabs = BuiltOf.Where(k => X.CountOf(k) > 0).Select(k => k.ToString()).ToList();
            p.Check(listed.Count == 0 && tabs.Count == 0, "with the compendium on, nothing the game is built of is listed or has a tab", string.Join(", ", listed.Concat(tabs)));
            var alike = X.Catalog.Where(e => e.Alike != null).Take(4).Select(e => $"{e.ShownName} {e.Alike} ({e.Name})").ToList();
            p.Note("alike names: " + string.Join("; ", alike));
            p.Check(alike.Count > 0, "entries of one kind sharing a name say which of them each is");

            var command = ScryPanel.Drawn(PanelPart.Command);
            yield return null;
            yield return null;
            p.Check(ScryPanel.Drawn(PanelPart.Command) == command, "a known troll's page offers no spawn command");
            var stars = Facts.For(troll).Rows.FirstOrDefault(r => r.Title == CombatWords.StarsTitle);
            p.Check(stars == null || X.KeepsBackWhole(stars.LineProofs, stars.Lines.Count) != null, "its stars table waits as one faint line until one is killed");
            yield return PictureOf(p, "fight", "player view Troll's fight, nothing found out");
            yield return Picture(p, "player view list and tabs");

            X.Peeking = true;
            command = ScryPanel.Drawn(PanelPart.Command);
            yield return Until(() => ScryPanel.Drawn(PanelPart.Command) > command, 3);
            p.Check(BuiltOf.Any(k => X.CountOf(k) > 0), "peeking, what the game is built of is listed again");
            p.Check(ScryPanel.Drawn(PanelPart.Command) > command, "and the troll's page offers its spawn command");
            X.Peeking = false;
            Session.ShowKnownInstead(null);
        }

        /// <summary>The kinds the game is built of, which the compendium lists only off or while peeking.</summary>
        private static readonly Kind[] BuiltOf = { Kind.Effect, Kind.Sound, Kind.Projectile, Kind.Other, Kind.Mod };

        /// <summary>
        /// Names as a player reads them: where Stone comes from is a row for each way, each giver a
        /// chip named by the game or by its prefab's words, rocks alike but for their biome one chip
        /// saying how many; a troll's attacks read as words. Stone's page is pictured.
        /// </summary>
        private static IEnumerator ReadableNames(Probe p)
        {
            var stone = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Item && e.Name == "Stone");
            var troll = X.Catalog.FirstOrDefault(e => e.Kind == Kind.Creature && e.Name == "Troll");
            if (stone == null || troll == null)
            {
                p.Skip("there is no stone or troll");
                yield break;
            }
            var givers = Facts.For(stone).Where.Count(s => SourceGroups.Groups(s.Record));
            var rows = ScryPanel.WhereRowsForTest(X, stone);
            var chips = rows.SelectMany(r => r.Items).ToList();
            p.Note($"Stone: {Numbers.Count(givers)} givers in {Numbers.Count(chips.Count)} chips: " + string.Join("; ", rows.Select(r => r.Title + ": " + string.Join(", ", r.Items.Select(i => i.Name)))));
            p.Check(chips.Count > 0 && chips.Count < givers, "Stone's givers alike but for their biome go into one chip", $"{Numbers.Count(chips.Count)} chips for {Numbers.Count(givers)} givers");
            // Each chip's name before its count, as the merging named it.
            var names = SourceGroups.Of(Facts.For(stone).Where.Select(s => s.Record)).Select(g => g.Name).ToList();
            var unread = names.Where(n => n.IndexOf('_') >= 0 || n.Any(char.IsDigit) || n.Length > 0 && char.IsLower(n[0])).ToList();
            p.Check(unread.Count == 0, "every giver of Stone reads as words, none as its prefab", string.Join(", ", unread));
            p.Check(rows.Select(r => r.Title).Distinct().Count() == rows.Count, "each way of giving it has one row");
            X.Select(stone);
            yield return PictureOf(p, "from", "readable Stone, where it comes from");

            var attacks = Facts.For(troll).Rows.Where(r => r.Title == CombatWords.AttacksTitle).SelectMany(r => r.Lines).Select(l => l.Cells[0]).ToList();
            p.Note("Troll's attacks: " + string.Join(", ", attacks));
            var raw = attacks.Where(a => a.IndexOf('_') >= 0 || a.Length > 0 && char.IsLower(a[0]) || a.Length > 1 && a.All(c => !char.IsLetter(c) || char.IsUpper(c))).ToList();
            p.Check(attacks.Count > 0 && raw.Count == 0, "a troll's attacks read as words", string.Join(", ", raw));
        }
    }
}
