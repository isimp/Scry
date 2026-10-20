using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Scry
{
    /// <summary>The self-test's case for the world's server in the compendium's box.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// The compendium's box names the world's server first among those played with, by
        /// whether it runs Scry, and for a server without Scry, made up in place of the world's,
        /// its faint line says what that leaves out; where this game runs the world, no server is
        /// named.
        /// </summary>
        private static IEnumerator ServerNamed(Probe p)
        {
            var book = CompendiumKeeper.Book;
            if (book == null || book.Rule == CompendiumRule.Off)
            {
                p.Skip("the character plays with the compendium off, so the box names no one");
                yield break;
            }
            yield return Until(() => EventLink.Server != Scry.ServerScry.Waiting, 15);
            var real = EventLink.Server;
            var (row, _) = ScryPanel.PlayingWithForTest;
            p.Note($"the world's server: {real}; the row reads \"{row}\"");
            p.Check(real == Scry.ServerScry.Here ? row == null || !row.StartsWith("The server", System.StringComparison.Ordinal) : row != null && row.StartsWith("The server", System.StringComparison.Ordinal),
                real == Scry.ServerScry.Here ? "where this game runs the world, no server is named" : "the row names the server first", row);

            EventLink.ServerForTest(Scry.ServerScry.None);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            ScryPanel.HoldCompendiumBox(true);
            var (without, hint) = ScryPanel.PlayingWithForTest;
            p.Check(without != null && without.StartsWith("The server \u00b7 no Scry", System.StringComparison.Ordinal), "a server without Scry is named so", without);
            p.Check(hint != null && hint.StartsWith("The server runs no Scry: what your explored and shared maps show", System.StringComparison.Ordinal), "and the line under it says what that leaves out", hint);
            yield return Picture(p, "compendium box server without Scry");
            ServerAsItWas();
        }

        /// <summary>
        /// The character's walks of the world are kept by where each dungeon lies, whatever number
        /// the game gave its generator at the last load; listed to the server on joining, those it
        /// lacked were sent; and a list naming a made-up walk the server lacks is answered with it,
        /// one of the walks the server holds already with none, nothing sent after.
        /// </summary>
        private static IEnumerator WalksToServer(Probe p)
        {
            var world = CompendiumMap.World;
            if (world == null || !DungeonWalks.Ready)
            {
                p.Skip("no world, or the character's maps are not read yet");
                yield break;
            }
            var own = DungeonWalks.Maps.OfWorld(world).ToList();
            p.Note($"{Numbers.Count(own.Count)} walks of this world kept, {Numbers.Count(GroupMapsLink.SentWanted)} sent to the server this session as it wanted");
            p.Check(own.All(m => m.Id.StartsWith(world + ":at:", System.StringComparison.Ordinal)), "each walk of the world is kept by where its dungeon lies", string.Join(", ", own.Select(m => m.Id).Take(3)));
            if (ZNet.instance.IsServer())
            {
                p.Note("this game runs the world: its walks go into its records itself");
                yield break;
            }
            yield return Until(() => EventLink.Server != Scry.ServerScry.Waiting, 15);
            if (!ServerScries.Answers(EventLink.Server))
            {
                p.Skip("the server runs no Scry of this version, so it keeps no walks");
                yield break;
            }
            var madeUp = DungeonMaps.EntranceIdOf(world, 987654f, -987654f);
            var today = EnvMan.instance != null ? EnvMan.instance.GetDay() : -1;
            var listed = new List<WalkList.Walk> { new WalkList.Walk(madeUp, 3, today) };
            var held = WalkList.Of(DungeonWalks.Maps, world).FirstOrDefault();
            if (held.Id != null) listed.Add(held);
            GroupMapsLink.ListForTest(listed);
            yield return Until(() => GroupMapsLink.WantedForTest != null, 10);
            var wanted = GroupMapsLink.WantedForTest;
            if (!p.Check(wanted != null, "the server answers a list of walks")) yield break;
            p.Check(wanted.Contains(madeUp), "a walk it lacks is wanted", string.Join(", ", wanted));
            if (held.Id != null) p.Check(!wanted.Contains(held.Id), "one of the character's, sent on joining, is held already", string.Join(", ", wanted));
            else p.Note("the character walked no dungeon of this world, so none was listed on joining");
        }

        /// <summary>
        /// The server, asked to read what was left inside the world's dungeons, answers once done how
        /// many held traces, each uncovering a room at least, the way to them among the rooms; and in
        /// every record it sent, the rooms of traces alone count among the rooms walked, the line
        /// saying how many came of traces. An admin alone may ask.
        /// </summary>
        private static IEnumerator TracesReadOnServer(Probe p)
        {
            var world = CompendiumMap.World;
            if (world == null)
            {
                p.Skip("no world is played");
                yield break;
            }
            if (!ZNet.instance.IsServer())
            {
                yield return Until(() => EventLink.Server != Scry.ServerScry.Waiting, 15);
                if (!ServerScries.Answers(EventLink.Server))
                {
                    p.Skip("the server runs no Scry of this version, so it reads no traces");
                    yield break;
                }
            }
            GroupMapsLink.ReadTracesForTest();
            yield return Until(() => GroupMapsLink.TracesReadForTest != null, 300);
            var read = GroupMapsLink.TracesReadForTest;
            if (!p.Check(read != null, "the server answers once it read the traces")) yield break;
            var (dungeons, rooms, onTheWay) = read.Value;
            if (dungeons < 0)
            {
                p.Skip("this player is no admin of the server, which refused");
                yield break;
            }
            p.Note($"traces in {Numbers.Count(dungeons)} dungeons, {Numbers.Count(rooms)} rooms uncovered, {Numbers.Count(onTheWay)} of them on the way to a trace");
            p.Check(rooms >= dungeons && onTheWay >= 0 && onTheWay <= rooms, "each dungeon with traces uncovers a room at least, the way to them among the rooms", $"{Numbers.Count(dungeons)} / {Numbers.Count(rooms)} / {Numbers.Count(onTheWay)}");
            var sent = GroupMapsLink.Group.OfWorld(world).Where(m => m.Map.Tally != null).ToList();
            p.Note($"{Numbers.Count(sent.Count)} records sent so far hold traces");
            p.Check(sent.All(m => m.Map.Traced.All(m.Map.Walked.Contains)), "the rooms of traces alone count among the rooms walked", string.Join(", ", sent.Select(m => m.Id).Take(3)));
            foreach (var (id, map) in sent.Where(m => m.Map.Traced.Count > 0).Take(1))
            {
                var said = DungeonMapWords.Server(0, 1, map.Walked.Count, map.Rooms.Count, "Known", map.FirstDay, map.LastDay, map.Traced.Count);
                p.Check(said.Contains("from traces"), "its line says how many rooms came of traces", said);
                p.Check(DungeonMapWords.ServerTip(map.Tally, map.FirstDay, map.LastDay)?.StartsWith("Read from what was left inside", System.StringComparison.Ordinal) == true, "and its hover what was left inside", id);
            }
        }

        /// <summary>The world's server's Scry as the world's again, the box let go.</summary>
        private static void ServerAsItWas()
        {
            EventLink.ServerForTest(null);
            ScryPanel.HoldCompendiumBox(false);
        }
    }
}
