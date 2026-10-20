using System.Collections;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for a ground's pin, marking the dungeons it shows with insides on Valheim's big map.</summary>
    internal static partial class SelfTest
    {
        /// <summary>How the ground explored showed, and whether its pin was on, before the test chose otherwise; put back after it.</summary>
        private static (SourceLevel Shows, bool Pin)? _pinBefore;

        /// <summary>The lines of the character's book file that keep which grounds' pins are on.</summary>
        private static string KeptPins(string path) =>
            path != null && System.IO.File.Exists(path) ? string.Join("\n", System.IO.File.ReadAllLines(path).Where(l => l.StartsWith("marks\t", System.StringComparison.Ordinal))) : "";

        /// <summary>
        /// The explored ground's pin at its row's end in the compendium's box, kept with the
        /// character as it is turned on; with the row at With insides, the game running the world
        /// names the dungeons lying in the ground whose rooms it keeps, each standing inside with its
        /// rooms, its entrance in explored ground; and a made-up crypt shown so, never walked, stands
        /// on Valheim's big map as a hollow mark whose words say so, a click on it showing its own
        /// rooms on the stage. What the character chose is put back.
        /// </summary>
        private static IEnumerator DungeonPins(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            if (!p.Check(book != null, "the character's book is read")) yield break;
            if (book.Rule == CompendiumRule.Off)
            {
                p.Skip("the character plays with the compendium off, so no ground shows its insides");
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

            // The pin at the end of the explored ground's row, kept with the character as it is turned on.
            Session.ShowKnownInstead(null);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            var pins = ScryPanel.Drawn(PanelPart.DungeonPins);
            ScryPanel.HoldCompendiumBox(true);
            ScryPanel.ShowSource(WorldSource.Explored, SourceLevel.WithInsides);
            var answers = DungeonsInGround.Answers;
            ScryPanel.MarkDungeons(WorldSource.Explored, true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.DungeonPins) > pins + 1 && KeptPins(CompendiumKeeper.FilePath).Contains("marks\tExplored"), 3);
            p.Check(ScryPanel.Drawn(PanelPart.DungeonPins) > pins, "the box draws a pin at the end of each ground's row");
            p.Check(KeptPins(CompendiumKeeper.FilePath).Contains("marks\tExplored"), "the explored ground's pin, turned on, is kept in the character's book");
            yield return Picture(p, "compendium box pins");
            ScryPanel.HoldCompendiumBox(false);

            // The game running the world names the dungeons lying in the ground whose rooms it keeps.
            yield return Until(() => DungeonsInGround.Answers > answers, 20);
            if (DungeonsInGround.Answers == answers) p.Note("no answer came on the dungeons in the explored ground: the server runs no Scry, or one older than this");
            else
            {
                var named = DungeonsInGround.Of(WorldSource.Explored).ToList();
                var grid = ExploredGrid.Unpack(CompendiumMap.PackedMap());
                p.Note($"{Numbers.Count(named.Count)} dungeons lie in the explored ground with their rooms kept: "
                    + string.Join(", ", named.GroupBy(m => m.Map.Location).OrderBy(g => g.Key, System.StringComparer.Ordinal).Select(g => $"{g.Key} {Numbers.Count(g.Count())}")));
                p.Check(grid != null && named.All(m => m.Map.Rooms.Count > 0 && m.Map.Generator.Y > 3000f && grid.Explored(m.Map.Entrance.X, m.Map.Entrance.Z)),
                    "each stands inside with its rooms, its entrance in explored ground");
            }

            // A made-up crypt shown with insides, never walked, where the player stands.
            var noCompendium = new CompendiumView(X.Entries);
            noCompendium.Tell(new CompendiumBook(), CompendiumRule.Off);
            Session.ShowKnownInstead(noCompendium);
            Select(entry);
            Stage.Inside = true;
            yield return Until(() => ReferenceEquals(X.Compendium, noCompendium) && ExampleLayouts.Of(entry) && ExampleLayouts.Example != null && Stage.ExampleRoomsTotal > 0 && Stage.ExampleRoomsShown == Stage.ExampleRoomsTotal, 150);
            var rolled = ExampleLayouts.Of(entry) ? ExampleLayouts.Example : null;
            if (!p.Check(rolled != null, "an example has been laid out")) yield break;
            var here = player.transform.position;
            var made = new DungeonMaps();
            var id = DungeonMaps.IdOf(world, 1L, 31u);
            made.Keep(id, PlaceOf(entry).Prefab, SavedAsTheGameSaves(rolled), new Vec3(here.x, here.y, here.z), rolled.Site.Generator, rolled.Site.Turn);
            DungeonWalks.MapsInsteadForTest(new DungeonMaps());
            DungeonsInGround.ForTest(WorldSource.Explored, made);
            Session.Hide();
            Minimap.instance.SetMapMode(Minimap.MapMode.Large);
            yield return Until(() => MapMarkButtons.HollowShown > 0, 5);
            p.Check(MapMarkButtons.MarksShown == 1 && MapMarkButtons.HollowShown == 1, "the large map has a hollow mark for the crypt shown with insides",
                $"{Numbers.Count(MapMarkButtons.MarksShown)} marks, {Numbers.Count(MapMarkButtons.HollowShown)} hollow");
            var said = MapMarkButtons.SaidForTest(0);
            p.Check(said != null && said.Contains("not walked") && said.Contains(CompendiumWords.SourceName(WorldSource.Explored)),
                "its words say it was not walked and shows with insides from the explored ground", said);
            var center = MapMarkButtons.MarkCenterForTest(0);
            if (!p.Check(center != null, "the mark stands on the screen")) yield break;
            PointerTo(center.Value);
            bool Arrived() => ((Vector2)ZInput.pointerPosition - center.Value).sqrMagnitude < 4f;
            yield return Until(Arrived, 3);
            if (Arrived())
            {
                yield return Until(() => MapMarkButtons.NameShownForTest, 3);
                var shownAt = Time.unscaledTime;
                yield return Until(() => Time.unscaledTime - shownAt > 1.2f, 3);
                yield return Picture(p, $"the hollow mark of {entry.Name} on the map with its tip");
            }
            else p.Note("the game reads the pointer elsewhere, as while its window is not in front: the tip unpictured, the click made at the mark's middle");
            p.Check(MapMarkButtons.ClickAtForTest(center.Value), "a click on the map at the mark goes to the mark");
            yield return Until(() => ExampleLayouts.ShownMap == id && Stage.ExampleRoomsTotal == rolled.Rooms.Count, 60);
            p.Check(Minimap.instance.m_mode != Minimap.MapMode.Large && Session.IsOpen && X.Selected == entry && ExampleLayouts.ShownMap == id,
                "a click on it closes the map and shows that crypt's own rooms on the stage", $"map {Minimap.instance.m_mode}, selected {X.Selected?.Name}, shown {ExampleLayouts.ShownMap ?? "an example"}");
            PinsAsTheyWere();
        }

        /// <summary>Puts back how the explored ground showed and its pin, the made-up crypt, the large map and the pointer, should the part have stopped before it did.</summary>
        private static void PinsAsTheyWere()
        {
            DungeonsInGround.ForTest(WorldSource.Explored, null);
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
    }
}
