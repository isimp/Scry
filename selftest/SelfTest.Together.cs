using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>The self-test's case for events crossing between the players' games, and for who is told to play without Scry.</summary>
    internal static partial class SelfTest
    {
        /// <summary>
        /// Made-up events sent to the game running the world and echoed back from it as its own
        /// (<see cref="EventLink.AskEcho"/>), over the network where a dedicated server runs Scry,
        /// recorded into a book of the test's own by the one rule: one the character had a hand
        /// in, beyond sight, counts once though it came twice; one beyond sight it had no hand in
        /// records nothing; one within sight counts; one passed on as from a game that did not run
        /// it is passed over. The server's hello is heard first. In a world this game runs the
        /// echo goes through this game alone, and the network is not covered. Neither the
        /// character's book nor the play counts change.
        /// </summary>
        private static IEnumerator CompendiumEventsCross(Probe p)
        {
            var player = Player.m_localPlayer;
            var real = CompendiumKeeper.Book;
            if (!p.Check(player != null && real != null && ZNet.instance != null && ZRoutedRpc.instance != null, "a character is played in a world and its book read")) yield break;
            var local = ZNet.instance.IsServer();
            var world = EventLink.WorldSession;
            if (local) p.Note("this game runs the world: the events go through it alone, so the network is not covered");
            else
            {
                yield return Until(() => EventLink.HeardFrom(world), 5);
                if (!EventLink.HeardFrom(world))
                {
                    p.Skip("the server's game said no hello, so it runs no Scry: nothing crosses the network through it to test");
                    yield break;
                }
                if (!EventLink.Peers.Has(world))
                {
                    p.Skip("the server's Scry is of other messages: update it to test events crossing");
                    yield break;
                }
                p.Check(EventLink.Hellos > 0, "the server's Scry answered the character's hello");
            }

            var version = real.Version;
            var pairs = real.PairsVersion;
            var learned = real.Learned;
            var counted = CompendiumKeeper.Play.Told();
            var book = new CompendiumBook();
            CompendiumKeeper.RecordInto(book);
            EventLink.TakeOwnForTest = local;
            var me = new[] { ZNet.GetUID() };
            var made = TestEvents.Prefix + "Creature";
            var far = player.transform.position + Vector3.right * (Sighting.PersonReach * 2f);
            var near = player.transform.position + Vector3.right * 2f;
            // Numbers no game of the world reaches, so none of its own events is taken for these.
            var number = 1000000000 + Random.Range(0, 100000000);
            WorldEvent Death(long game, string thing, Vector3 at, long[] doers) =>
                new WorldEvent(game, number++, Happened.Died, thing, at.x, at.y, at.z, null, 1, doers);

            var taken = EventLink.Taken;
            // The one passed on first: the others are sent after it, so it has come once they have.
            EventLink.AskEcho(Death(world + 1, made + "Passed", far, me), 1);
            EventLink.AskEcho(Death(world, made + "Done", far, me), 2);
            EventLink.AskEcho(Death(world, made + "Unseen", far, null), 1);
            EventLink.AskEcho(Death(world, made + "Seen", near, null), 2);
            yield return Until(() => EventLink.Taken - taken >= 5, 10);
            var came = EventLink.Taken - taken;
            var fell = book.Tallied(Tally.Fell);
            if (came == 0 && !local) p.Skip("no echo came back: the server's Scry is older than this one, update it to test events crossing");
            else
            {
                p.Check(came == 5, (local ? "every event came back through this game" : "every event came back over the network") + ", the one passed on not taken", $"{Numbers.Count(came)} of 5 taken");
                p.Check(fell.Kills(made + "Done") == 1, "one the character had a hand in, beyond sight, counts once though it came twice");
                p.Check(fell.Kills(made + "Unseen") == 0, "one beyond sight it had no hand in records nothing");
                p.Check(fell.Kills(made + "Seen") == 1, "one within sight counts once, the character seeing it");
                p.Check(fell.Kills(made + "Passed") == 0, "one passed on as from a game that did not run it is passed over");
            }

            CompendiumKeeper.RecordInto(null);
            EventLink.TakeOwnForTest = false;
            p.Check(real.Version == version && real.PairsVersion == pairs && real.Learned == learned, "the character's book was left alone");
            p.Check(CompendiumKeeper.Play.Told() == counted, "and so were the play counts");
        }

        /// <summary>Puts the events' test switches back, should the part have stopped before it did.</summary>
        private static void EventsCrossAsItWas()
        {
            CompendiumKeeper.RecordInto(null);
            EventLink.TakeOwnForTest = false;
        }

        /// <summary>The player's notice of who plays without Scry, kept while the test uses one of its own.</summary>
        private static GapNotice _gapsBefore;

        /// <summary>
        /// Made-up players played with, one without Scry, one whose Scry needs updating and one with
        /// it: a notice under the header names the first two, its chip opens the compendium's box
        /// listing all three and the line under them, and put away it stays away until another
        /// player without Scry joins. Each is pictured; the world's players and the player's notice
        /// are put back.
        /// </summary>
        private static IEnumerator PlayingWithShown(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            if (!p.Check(book != null, "the character's book is read")) yield break;
            if (book.Rule == CompendiumRule.Off)
            {
                p.Skip("the character plays with the compendium off, so nothing tells who has Scry");
                yield break;
            }
            Session.ShowKnownInstead(null);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            ScryPanel.Compact = false;
            _gapsBefore = ScryPanel.GapNoticeForTest(new GapNotice());
            var players = new List<(string Name, PeerScry Scry)> { ("Ana", PeerScry.None), ("Bjorn", PeerScry.Outdated), ("Cara", PeerScry.Scry) };
            EventLink.PlayingWithForTest(players);

            var notice = ScryPanel.Drawn(PanelPart.PlayingWithNotice);
            yield return Until(() => ScryPanel.Drawn(PanelPart.PlayingWithNotice) > notice + 1, 3);
            p.Check(ScryPanel.Drawn(PanelPart.PlayingWithNotice) > notice
                && ScryPanel.PlayingWithLine == PlayingWithWords.Notice(players),
                "a notice under the header names the players whose games lack Scry", ScryPanel.PlayingWithLine);
            yield return Picture(p, "playing with notice");

            var row = ScryPanel.Drawn(PanelPart.PlayingWithRow);
            ScryPanel.ShowWhoHasScry(true);
            yield return Until(() => ScryPanel.Drawn(PanelPart.PlayingWithRow) > row + 1, 3);
            // The world's server, named first where it is not this game, then the players.
            p.Check(ScryPanel.Drawn(PanelPart.PlayingWithRow) > row && (ScryPanel.PlayingWithRow ?? "").EndsWith("Ana · no Scry, Bjorn · needs updating, Cara · Scry", System.StringComparison.Ordinal),
                "its chip opens the compendium's box, listing everyone played with and whether their game has Scry", ScryPanel.PlayingWithRow);
            yield return Picture(p, "compendium box playing with");

            // Put away, it stays away until another player without Scry joins.
            ScryPanel.PutPlayingWithAway();
            yield return null;
            yield return null;
            var away = ScryPanel.Drawn(PanelPart.PlayingWithNotice);
            for (var i = 0; i < 5; i++) yield return null;
            p.Check(ScryPanel.Drawn(PanelPart.PlayingWithNotice) == away, "put away, the notice stays away");
            EventLink.PlayingWithForTest(new List<(string Name, PeerScry Scry)>(players) { ("Dagny", PeerScry.None) });
            yield return Until(() => ScryPanel.Drawn(PanelPart.PlayingWithNotice) > away + 1, 3);
            p.Check(ScryPanel.Drawn(PanelPart.PlayingWithNotice) > away && (ScryPanel.PlayingWithLine?.Contains("Dagny") ?? false),
                "and comes back once another player without Scry joins", ScryPanel.PlayingWithLine);
            PlayingWithAsItWas();
        }

        /// <summary>Puts the world's players and the player's notice back, should the part have stopped before it did.</summary>
        private static void PlayingWithAsItWas()
        {
            EventLink.PlayingWithForTest(null);
            if (_gapsBefore != null) ScryPanel.GapNoticeForTest(_gapsBefore);
            _gapsBefore = null;
            ScryPanel.ShowWhoHasScry(false);
        }
    }
}
