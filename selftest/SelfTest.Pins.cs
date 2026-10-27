using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for a ground's pin, marking the dungeons lying in it on Valheim's big map, and for With insides dimmed where nothing has insides.</summary>
    internal static partial class SelfTest
    {
        /// <summary>How the ground explored showed, and whether its pin was on, before the test chose otherwise; put back after it.</summary>
        private static (SourceLevel Shows, bool Pin)? _pinBefore;

        /// <summary>The lines of the character's book file that keep which grounds' pins are on.</summary>
        private static string KeptPins(string path) =>
            path != null && System.IO.File.Exists(path) ? string.Join("\n", System.IO.File.ReadAllLines(path).Where(l => l.StartsWith("marks\t", System.StringComparison.Ordinal))) : "";

        /// <summary>Whether a mark's words stand on a dark plate, the legend and the days told elsewhere.</summary>
        private static bool OnPlateAlone(string said) => said != null && said.StartsWith("<mark=", System.StringComparison.Ordinal) && !said.Contains("solid") && !said.Contains(" day ");

        /// <summary>
        /// The explored ground's pin at its row's end in the compendium's box, kept with the
        /// character as it is turned on, With insides dimmed on each row none of whose places has
        /// insides; with the row at Known, the game running the world names the dungeons lying in
        /// the ground, those whose rooms it keeps standing inside with their rooms, the others by
        /// their entrance, each in explored ground. Made-up dungeons where the player stands, one
        /// walked by you, one walked on this server and one known by its entrance alone: at Known,
        /// the one walked by you is marked filled and the others by their entrances, the one walked
        /// on this server saying With insides shows it, a click on it opening its own rooms tagged In
        /// your world; at With insides, that one hollow, saying how far it was walked. A click on the
        /// entrance whose area never loaded shows an example tagged as one, its note saying it
        /// stands in; on the hollow one its rooms on the stage. What the character chose is put back.
        /// </summary>
        private static IEnumerator DungeonPins(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            if (!p.Check(book != null, "the character's book is read")) yield break;
            if (book.Rule == CompendiumRule.Off)
            {
                p.Skip("the character plays with the compendium off, so no ground shows anything");
                yield break;
            }
            var entry = Crypt();
            var world = CompendiumMap.World;
            var player = Player.m_localPlayer;
            if (entry == null || world == null || player == null || Minimap.instance == null || Game.m_noMap)
            {
                p.Skip("there is no crypt, or no map in this world");
                yield break;
            }
            _pinBefore = (book.Shows(WorldSource.Explored), book.MarksDungeons(WorldSource.Explored));

            // The pin at the end of the explored ground's row, kept with the character as it is turned on at Known.
            Session.ShowKnownInstead(null);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            var pins = ScryPanel.Drawn(PanelPart.DungeonPins);
            var dimmed = ScryPanel.Drawn(PanelPart.InsidesDimmed);
            ScryPanel.HoldCompendiumBox(true);
            ScryPanel.ShowSource(WorldSource.Explored, SourceLevel.Known);
            var answers = DungeonsInGround.Answers;
            ScryPanel.MarkDungeons(WorldSource.Explored, true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.DungeonPins) > pins + 1 && KeptPins(CompendiumKeeper.FilePath).Contains("marks\tExplored"), 3);
            p.Check(ScryPanel.Drawn(PanelPart.DungeonPins) > pins, "the box draws a pin at the end of each ground's row");
            p.Check(KeptPins(CompendiumKeeper.FilePath).Contains("marks\tExplored"), "the explored ground's pin, turned on at Known, is kept in the character's book");

            // With insides dimmed on each row none of whose places found in this world has insides.
            var without = WorldSources.All.Where(s => book.Shows(s) != SourceLevel.WithInsides && DungeonKinds.All.Count > 0 && !WorldSources.HasInsides(book.Found(world, s), DungeonKinds.Is)).ToList();
            p.Note($"{Numbers.Count(DungeonKinds.All.Count)} kinds of dungeon lie inside; rows with none of them found: "
                + (without.Count == 0 ? "none" : string.Join(", ", without.Select(CompendiumWords.SourceName))));
            p.Check(without.Count > 0 == ScryPanel.Drawn(PanelPart.InsidesDimmed) > dimmed, "With insides is dimmed on exactly the rows none of whose places has insides",
                $"{Numbers.Count(without.Count)} such rows, dimmed drawn {Numbers.Count(ScryPanel.Drawn(PanelPart.InsidesDimmed) - dimmed)} times");
            yield return Picture(p, "compendium box pins");
            ScryPanel.HoldCompendiumBox(false);

            // The game running the world names the dungeons lying in the ground, with their rooms or by their entrance.
            yield return Until(() => DungeonsInGround.Answers > answers, 20);
            if (DungeonsInGround.Answers == answers) p.Note("no answer came on the dungeons in the explored ground: the server runs no Scry, or one older than this");
            else
            {
                var named = DungeonsInGround.Of(WorldSource.Explored).ToList();
                var grid = ExploredGrid.Unpack(CompendiumMap.PackedMap());
                var withRooms = named.Where(m => m.Map.Rooms.Count > 0).ToList();
                var walkedThere = withRooms.Count(m => m.Map.Walked.Count > 0);
                p.Note($"{Numbers.Count(named.Count)} dungeons lie in the explored ground, {Numbers.Count(withRooms.Count)} with their rooms kept, {Numbers.Count(walkedThere)} of them walked on this server: "
                    + string.Join(", ", named.GroupBy(m => m.Map.Location).OrderBy(g => g.Key, System.StringComparer.Ordinal).Select(g => $"{g.Key} {Numbers.Count(g.Count())}")));
                p.Check(grid != null && named.All(m => grid.Explored(m.Map.Entrance.X, m.Map.Entrance.Z)), "each one's entrance lies in explored ground");
                p.Check(withRooms.All(m => m.Map.Generator.Y > 3000f), "each whose rooms are kept stands inside with them");
                p.Check(named.All(m => DungeonKinds.Is(m.Map.Location) || m.Map.Rooms.Count > 0), "each known by its entrance alone is of a kind of dungeon lying inside");
            }

            // Made-up dungeons where the player stands: walked by you, walked on this server, and known by the entrance alone.
            var noCompendium = new CompendiumView(X.Entries);
            noCompendium.Tell(new CompendiumBook(), CompendiumRule.Off);
            Session.ShowKnownInstead(noCompendium);
            Select(entry);
            Stage.Inside = true;
            yield return Until(() => ReferenceEquals(X.Compendium, noCompendium) && ExampleLayouts.Of(entry) && ExampleLayouts.Example != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 150);
            var rolled = ExampleLayouts.Of(entry) ? ExampleLayouts.Example : null;
            if (!p.Check(rolled != null, "an example has been laid out")) yield break;
            var here = player.transform.position;
            var prefab = PlaceOf(entry).Prefab;
            var yours = new DungeonMaps();
            var yoursId = DungeonMaps.IdOf(world, 1L, 30u);
            yours.Keep(yoursId, prefab, SavedAsTheGameSaves(rolled), new Vec3(here.x, here.y, here.z), rolled.Site.Generator, rolled.Site.Turn);
            yours.Walk(yoursId, 0);
            var ground = new DungeonMaps();
            var serverId = DungeonMaps.IdOf(world, 1L, 31u);
            ground.Keep(serverId, prefab, SavedAsTheGameSaves(rolled), new Vec3(here.x + 200f, here.y, here.z), rolled.Site.Generator, rolled.Site.Turn);
            ground.Walk(serverId, 0);
            ground.Walk(serverId, 1);
            var entranceId = DungeonMaps.EntranceIdOf(world, here.x - 200f, here.z);
            ground.Keep(entranceId, prefab, new List<(int, Vec3, Vec3)>(), new Vec3(here.x - 200f, here.y, here.z), default, Quat.Identity);
            DungeonWalks.MapsInsteadForTest(yours);
            DungeonsInGround.InsteadForTest(WorldSource.Explored, ground);
            Session.Hide();
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            yield return Until(() => MapMarkButtons.MarksShown == 3, 5);
            p.Check(MapMarkButtons.MarksShown == 3 && MapMarkButtons.EntrancesShown == 2 && MapMarkButtons.HollowShown == 0,
                "at Known the large map marks the one walked by you filled, the others by their entrances",
                $"{Numbers.Count(MapMarkButtons.MarksShown)} marks, {Numbers.Count(MapMarkButtons.EntrancesShown)} entrances, {Numbers.Count(MapMarkButtons.HollowShown)} hollow");
            var saidBelow = MapMarkButtons.SaidForTest(1);
            p.Check(saidBelow != null && saidBelow.Contains("With insides shows what was walked"), "the one walked on this server says With insides shows it", saidBelow);
            var saidEntrance = MapMarkButtons.SaidForTest(2);
            p.Check(saidEntrance != null && saidEntrance.Contains("Nobody on this server walked it"), "the entrance alone says nobody on this server walked it", saidEntrance);

            // An entrance whose rooms the world keeps opens as itself, tagged as the world's own.
            var atKnown = MapMarkButtons.MarkCenterForTest(1);
            if (atKnown != null && MapMarkButtons.MarkAtForTest(atKnown.Value) == 1 && MapMarkButtons.ClickAtForTest(atKnown.Value))
            {
                yield return Until(() => ExampleLayouts.ShownMap == serverId && ScryPanel.TrailTaggedForTest, 60);
                var opened = MapMarkButtons.OpenedForTest;
                p.Check(ExampleLayouts.ShownMap == serverId && opened.Look == MarkLook.Entrance && opened.Rooms && ScryPanel.TrailTaggedForTest,
                    "an entrance whose rooms the world keeps opens its own rooms, tagged In your world", $"shown {ExampleLayouts.ShownMap ?? "an example"}, {opened.Look}");
                Session.Hide();
                Minimap.instance.SetMapMode(Minimap.MapMode.Large);
                yield return Until(() => MapMarkButtons.MarksShown == 3, 5);
            }
            else p.Note("the entrance whose rooms the world keeps stands under another mark at this zoom: its click is left out");

            ScryPanel.ShowSource(WorldSource.Explored, SourceLevel.WithInsides);
            yield return Until(() => MapMarkButtons.HollowShown == 1, 5);
            p.Check(MapMarkButtons.MarksShown == 3 && MapMarkButtons.HollowShown == 1 && MapMarkButtons.EntrancesShown == 1, "with insides the one walked on this server is marked hollow",
                $"{Numbers.Count(MapMarkButtons.MarksShown)} marks, {Numbers.Count(MapMarkButtons.EntrancesShown)} entrances, {Numbers.Count(MapMarkButtons.HollowShown)} hollow");
            var saidHollow = MapMarkButtons.SaidForTest(1);
            p.Check(saidHollow != null && saidHollow.Contains("Walked on this server: 2 of"), "its words say how far anyone on this server walked it", saidHollow);
            p.Check((MapMarkButtons.SaidForTest(0) ?? "").Contains("Walked by you: 1 of"), "the one walked by you says how far you walked it", MapMarkButtons.SaidForTest(0));
            p.Check(OnPlateAlone(MapMarkButtons.SaidForTest(0)), "its words stand on a dark plate, the name and how far it was walked alone", MapMarkButtons.SaidForTest(0));

            var hollowAt = MapMarkButtons.MarkCenterForTest(1);
            var entranceAt = MapMarkButtons.MarkCenterForTest(2);
            if (!p.Check(hollowAt != null && entranceAt != null, "the marks stand on the screen")) yield break;
            PointerTo(hollowAt.Value);
            bool Arrived() => ((Vector2)ZInput.pointerPosition - hollowAt.Value).sqrMagnitude < 4f;
            yield return Until(Arrived, 3);
            if (Arrived())
            {
                yield return Until(() => MapMarkButtons.NameShownForTest, 3);
                var shownAt = Time.unscaledTime;
                yield return Until(() => Time.unscaledTime - shownAt > 1.2f, 3);
                yield return Picture(p, $"the marks of {entry.Name} on the map, the hollow one's tip shown");
            }
            else p.Note("the game reads the pointer elsewhere, as while its window is not in front: the tip unpictured, the clicks made at the marks' middles");
            if (MapMarkButtons.MarkAtForTest(entranceAt.Value) != 2 || MapMarkButtons.MarkAtForTest(hollowAt.Value) != 1)
            {
                p.Note("the marks stand over one another at this zoom: the clicks are left out");
                PinsAsTheyWere();
                yield break;
            }

            // A click on the entrance whose area never loaded shows an example standing in, tagged as one.
            p.Check(MapMarkButtons.ClickAtForTest(entranceAt.Value), "a click on the map at the entrance goes to its mark");
            yield return Until(() => Session.IsOpen && X.Selected == entry && ExampleLayouts.Example != null && ScryPanel.ExampleTaggedForTest && (ScryPanel.DungeonNoteForTest ?? "").Contains("stands in for this one"), 30);
            p.Check(Minimap.instance.m_mode != Minimap.MapMode.Large && X.Selected == entry && ExampleLayouts.ShownMap == null && !MapMarkButtons.OpenedForTest.Rooms,
                "a click on the entrance whose area never loaded opens its page with an example", $"map {Minimap.instance.m_mode}, selected {X.Selected?.Name}, shown {ExampleLayouts.ShownMap ?? "an example"}");
            p.Check(ScryPanel.ExampleTaggedForTest, "the example is tagged as one");
            p.Check((ScryPanel.DungeonNoteForTest ?? "").Contains("An example stands in for this one of your world: its area never loaded"), "its note says it stands in, and why", ScryPanel.DungeonNoteForTest);
            yield return Picture(p, $"an example standing in for a {entry.Name} whose area never loaded");

            // A click on the hollow one shows its rooms on the stage.
            Session.Hide();
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            yield return Until(() => MapMarkButtons.MarksShown == 3, 5);
            var again = MapMarkButtons.MarkCenterForTest(1);
            p.Check(again != null && MapMarkButtons.ClickAtForTest(again.Value), "a click on the map at the hollow mark goes to it");
            yield return Until(() => ExampleLayouts.ShownMap == serverId && Stage.ExampleRoomsTotal == rolled.Rooms.Count, 60);
            p.Check(Minimap.instance.m_mode != Minimap.MapMode.Large && Session.IsOpen && X.Selected == entry && ExampleLayouts.ShownMap == serverId,
                "a click on it closes the map and shows that crypt's own rooms on the stage", $"map {Minimap.instance.m_mode}, selected {X.Selected?.Name}, shown {ExampleLayouts.ShownMap ?? "an example"}");
            PinsAsTheyWere();
        }

        /// <summary>
        /// A ground of hundreds of dungeons on Valheim's large map: made-up entrances all round the
        /// player, every one in view, all come to stand within a few seconds, and making them and
        /// telling their words holds no frame long, as making them all in one frame held one for
        /// most of a second.
        /// </summary>
        private static IEnumerator ManyMarks(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            var entry = Crypt();
            var world = CompendiumMap.World;
            var player = Player.m_localPlayer;
            if (book == null || book.Rule == CompendiumRule.Off)
            {
                p.Skip("the character plays with the compendium off, so no ground shows anything");
                yield break;
            }
            if (entry == null || world == null || player == null || Minimap.instance == null || Game.m_noMap)
            {
                p.Skip("there is no crypt, or no map in this world");
                yield break;
            }
            _pinBefore = (book.Shows(WorldSource.Explored), book.MarksDungeons(WorldSource.Explored));
            ScryPanel.ShowSource(WorldSource.Explored, SourceLevel.Known);
            ScryPanel.MarkDungeons(WorldSource.Explored, true);
            const int many = 400;
            var here = player.transform.position;
            var prefab = PlaceOf(entry).Prefab;
            var ground = new DungeonMaps();
            for (var i = 0; i < many; i++)
            {
                var (x, z) = (here.x + (i % 20 - 10) * 8f, here.z + (i / 20 - 10) * 8f);
                ground.Keep(DungeonMaps.EntranceIdOf(world, x, z), prefab, new List<(int, Vec3, Vec3)>(), new Vec3(x, here.y, z), default, Quat.Identity);
            }
            DungeonsInGround.InsteadForTest(WorldSource.Explored, ground);
            Session.Hide();
            var first = Timing.Measuring?.Frames ?? 0;
            Timing.ForgetBytesByPart();
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            var opened = Time.unscaledTime;
            yield return Until(() => MapMarkButtons.MarksShown == many, 20);
            var took = Time.unscaledTime - opened;
            // A few frames more, standing, for what placing them costs a frame once all are made.
            yield return new Wait(1);
            var frames = System.Math.Max(1, (Timing.Measuring?.Frames ?? 0) - first);
            var mostMs = Timing.MostMs("marks");
            var ownMs = Timing.OwnBytesByPartCopy().Where(part => part.Part == "marks").Sum(part => part.Ms);
            p.Check(MapMarkButtons.MarksShown == many, $"all {Numbers.Count(many)} entrances in view come to stand", $"{Numbers.Count(MapMarkButtons.MarksShown)} after {Numbers.Amount(took, 1)} s");
            p.Note($"they all stood after {Numbers.Amount(took, 1)} s; the marks took {Numbers.Fixed(ownMs / frames, 2)} ms a frame on average, {Numbers.Amount(mostMs, 1)} ms at the most");
            p.Check(mostMs < 16, "making them and telling their words holds no frame a sixtieth of a second", $"{Numbers.Amount(mostMs, 1)} ms at the most");
            PinsAsTheyWere();
        }

        /// <summary>Puts back how the explored ground showed and its pin, the made-up dungeons, the large map and the pointer, should the part have stopped before it did.</summary>
        private static void PinsAsTheyWere()
        {
            MarksAsTheyWere();
            ScryPanel.HoldCompendiumBox(false);
            if (_pinBefore is (SourceLevel shows, bool pin))
            {
                ScryPanel.ShowSource(WorldSource.Explored, shows);
                ScryPanel.MarkDungeons(WorldSource.Explored, pin);
            }
            _pinBefore = null;
            Session.ShowKnownInstead(null);
        }

        /// <summary>
        /// With the compendium on and a ground shown with insides, a dungeon's page names what its
        /// rooms walked on this server hold, though the character never met it: a made-up character
        /// knowing a crypt as lying in its explored ground, and walks on this server of one of its
        /// rooms that places a creature; that room's page names all it holds, another room's names
        /// only what was met, and the crypt's page names the creature.
        /// </summary>
        private static IEnumerator InsidesFromWalks(Probe p)
        {
            var entry = Crypt();
            var world = CompendiumMap.World;
            var label = entry != null ? Places.LabelOf(entry) : null;
            var rooms = label == null ? new List<Entry>() : X.Catalog.Where(e => e.Kind == Kind.Location && e.Components != null && System.Array.IndexOf(e.Components, "Room") >= 0 && e.FoundIn.Contains(label)).ToList();
            if (entry == null || world == null || rooms.Count < 2)
            {
                p.Skip("there is no crypt with rooms read, or no world");
                yield break;
            }
            // A room placing a creature, by what was read of it, and one placing none of the same.
            Entry Held(Entry room) => RoomContents.Of(room).Select(name => X.Find(name)).FirstOrDefault(e => e != null && e.Kind == Kind.Creature);
            var holding = rooms.FirstOrDefault(r => Held(r) != null);
            if (!p.Check(holding != null, "one of the crypt's rooms places a creature", Numbers.Count(rooms.Count) + " rooms")) yield break;
            var creature = Held(holding);
            var other = rooms.FirstOrDefault(r => r != holding && RoomContents.Of(r).All(name => name != creature.Name));

            var book = new CompendiumBook();
            book.FoundIn(world, WorldSource.Explored, new[] { PlaceOf(entry).Prefab });
            book.Show(WorldSource.Explored, SourceLevel.WithInsides);
            var map = new DungeonMap { Location = PlaceOf(entry).Prefab, Rooms = rooms.Select((r, i) => (r.Name.GetStableHashCode(), new Vec3(i, 0f, 0f), new Vec3(0f, 0f, 0f))).ToList() };
            map.Walked.Add(rooms.IndexOf(holding));
            var view = new CompendiumView(X.Entries);
            view.InWorld(world);
            view.RoomsHold(RoomContents.Of);
            view.WalkedOnServer(ServerWalks.Of(s => s == WorldSource.Explored ? new[] { map } : System.Array.Empty<DungeonMap>(), _ => null, name => name.GetStableHashCode()), 1);
            view.Tell(book, CompendiumRule.Loose);
            Session.ShowKnownInstead(view);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            yield return Until(() => ReferenceEquals(X.Compendium, view), 3);
            p.Check(X.LevelOf(creature) < KnownLevel.Seen, $"the made-up character never met {creature.Name}");

            X.Select(holding);
            p.Check(!X.LeavesOut(creature.Key) && !X.NotMet(creature.Key), $"the room walked on this server, {holding.Name}, names all it holds: {creature.Name}");
            if (other != null)
            {
                X.Select(other);
                p.Check(!view.HoldsNamed(other), $"a room nobody walked, {other.Name}, names only what was met");
            }
            X.Select(entry);
            yield return GoTo("facts");
            p.Check(!X.LeavesOut(creature.Key), $"the crypt's page names what its walked room holds: {creature.Name}");
            p.Check(!view.HoldsNamed(entry), "the crypt, not walked through whole, names only that, not all it holds");
            yield return PictureOf(p, "facts", "a crypt's page with insides from a walk on this server");
            Session.ShowKnownInstead(null);
        }
    }
}
