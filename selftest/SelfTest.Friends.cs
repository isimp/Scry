using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// The self-test's cases for what play together needs a friend's game for: the server's game
    /// runs made-up events as a friend's would, this game's own sending reaches the server, and
    /// made-up players come and go beside the real ones.
    /// </summary>
    internal static partial class SelfTest
    {
        /// <summary>Whether the world runs on a dedicated server whose Scry is of these messages, as the parts the server answers need; skipped otherwise, saying why.</summary>
        private static IEnumerator ServerScry(Probe p, System.Action<bool> ready)
        {
            ready(false);
            if (ZNet.instance == null || Player.m_localPlayer == null)
            {
                p.Check(false, "a character is played in a world");
                yield break;
            }
            if (ZNet.instance.IsServer())
            {
                p.Skip("this game runs the world: no other game is there to run events with");
                yield break;
            }
            var world = EventLink.WorldSession;
            yield return Until(() => EventLink.HeardFrom(world), 5);
            if (!EventLink.Peers.Has(world))
            {
                p.Skip(EventLink.HeardFrom(world) ? "the server's Scry is of other messages: update it" : "the server said no hello, so it runs no Scry");
                yield break;
            }
            ready(true);
        }

        /// <summary>
        /// Made-up events the server's game runs as its own, as a friend's game would
        /// (<see cref="EventLink.AskRun"/>): it chooses who gets each by the rules for every event,
        /// by where it has the players' characters, and sends it only to this game. One 2 m from
        /// the character is sent and counts as seen; one 35 m off is sent, within the margin, but
        /// counts nothing, beyond sight; one 50 m off is not sent; one 60 m off that the character
        /// did is sent and counts. Recorded into a book of the test's own while the character
        /// stands still; the character's book and the play counts are left alone.
        /// </summary>
        private static IEnumerator CompendiumEventsRunByWorld(Probe p)
        {
            var ready = false;
            yield return ServerScry(p, r => ready = r);
            if (!ready) yield break;
            var player = Player.m_localPlayer;
            var real = CompendiumKeeper.Book;
            if (!p.Check(real != null, "the character's book is read")) yield break;
            var version = real.Version;
            var counted = CompendiumKeeper.Play.Told();
            var book = new CompendiumBook();
            CompendiumKeeper.RecordInto(book);

            var at = player.transform.position;
            // A way the character sees 2 m along, so walls round it hide nothing the test counts as seen.
            var way = ClearWay(at, 2f);
            var near = TestEvents.Prefix + "CreatureNear";
            var margin = TestEvents.Prefix + "CreatureInMargin";
            var beyond = TestEvents.Prefix + "CreatureBeyond";
            var doneFar = TestEvents.Prefix + "CreatureDoneFar";
            var taken = EventLink.Taken;
            EventLink.AskRun(new List<(string Thing, Vector3 At, bool Done)>
            {
                (near, at + way * 2f, false),
                (margin, at + way * (Sighting.PersonReach + EventRecipients.Margin / 2f), false),
                (beyond, at + way * (Sighting.PersonReach + EventRecipients.Margin * 2f), false),
                (doneFar, at + way * (Sighting.PersonReach * 2f), true),
            });
            yield return Until(() => EventLink.RanForTest != null, 10);
            var ran = EventLink.RanForTest;
            if (ran == null)
            {
                CompendiumKeeper.RecordInto(null);
                p.Skip("the server ran none of them: its Scry is older than this one, update it");
                yield break;
            }
            if (EventLink.RanSawYouAt is Vector3 seen) p.Note($"the server has the character {Numbers.Amount(Vector3.Distance(seen, player.transform.position), 1)} m from where it stands");
            else p.Note("the server holds nothing of the character");
            bool ToYou(string thing) => ran.Any(r => r.Thing == thing && r.ToYou);
            var said = string.Join(", ", ran.Select(r => r.Thing.Substring(TestEvents.Prefix.Length) + (r.ToYou ? " sent" : " not sent")));
            p.Check(ran.Count == 4, "the server ran every one", said);
            p.Check(ToYou(near) && ToYou(margin) && !ToYou(beyond) && ToYou(doneFar),
                "it chose by the rules for every event: those within sight and the margin and the one the character did, not the one beyond", said);
            p.Check(EventLink.Taken - taken == 3, "the three sent came over the network", $"{Numbers.Count(EventLink.Taken - taken)} came");
            var fell = book.Tallied(Tally.Fell);
            p.Check(fell.Kills(near) == 1, "one 2 m away counts, seen");
            p.Check(fell.Kills(margin) == 0, "one 35 m away came but counts nothing, beyond sight");
            p.Check(fell.Kills(doneFar) == 1, "one the character did 60 m away counts");
            p.Check(fell.Kills(beyond) == 0, "and one 50 m away never came");

            CompendiumKeeper.RecordInto(null);
            p.Check(real.Version == version && CompendiumKeeper.Play.Told() == counted, "the character's book and the play counts were left alone");
        }

        /// <summary>
        /// This game's own sending, as it sends every event it runs (<see cref="EventLink.Send"/>):
        /// a made-up event the server's game did, high above the character so no player is near,
        /// goes to the server alone, and the server says it took it; one no one did goes nowhere,
        /// and the server says it never got it.
        /// </summary>
        private static IEnumerator CompendiumEventsSent(Probe p)
        {
            var ready = false;
            yield return ServerScry(p, r => ready = r);
            if (!ready) yield break;
            var world = EventLink.WorldSession;
            var high = Player.m_localPlayer.transform.position + Vector3.up * 1000f;

            var did = WorldEvents.Make(Happened.Died, TestEvents.Prefix + "CreatureServerDid", high, doers: new[] { world });
            var sent = EventLink.Send(did);
            p.Check(sent.Count == 1 && sent[0] == world, "this game sends an event the server's game did to it, and to no one else", $"{Numbers.Count(sent.Count)} sent");
            EventLink.AskTaken(did.Game, did.Number);
            yield return Until(() => EventLink.TakenForTest != null, 5);
            if (EventLink.TakenForTest == null)
            {
                p.Skip("the server did not answer: its Scry is older than this one, update it");
                yield break;
            }
            p.Check(EventLink.TakenForTest.Value.Taken, "and the server took it");

            var nobody = WorldEvents.Make(Happened.Died, TestEvents.Prefix + "CreatureNobodyDid", high);
            var none = EventLink.Send(nobody);
            p.Check(none.Count == 0, "one no one did, far from every player, goes nowhere", $"{Numbers.Count(none.Count)} sent");
            EventLink.AskTaken(nobody.Game, nobody.Number);
            yield return Until(() => EventLink.TakenForTest != null, 5);
            p.Check(EventLink.TakenForTest != null && !EventLink.TakenForTest.Value.Taken, "and the server never got it");
        }

        /// <summary>The player's notice of who plays without Scry, kept while the part uses one of its own.</summary>
        private static GapNotice _gapsBeforePlayers;

        /// <summary>Sessions no game is given, for the made-up players.</summary>
        private const long AnaSession = 0x5C7E5700A1L, BjornSession = 0x5C7E5700B2L;

        /// <summary>
        /// Who is played with as players come and go, through the same reading of who is in the
        /// world as play, made-up players listed beside the real ones (<see cref="EventLink.AlsoListedForTest"/>):
        /// Bjorn, whose Scry said hello, is played with at once and stays so while he dies and rises
        /// again, listed a while without a character; Ana, who said no hello, is named as playing
        /// without Scry only once 10 s in the world, the notice naming her where the compendium is
        /// on; Bjorn gone is forgotten, hello and all. The real players are left as they were.
        /// </summary>
        private static IEnumerator PlayersComeAndGo(Probe p)
        {
            if (!p.Check(ZNet.instance != null && Player.m_localPlayer != null, "a character is played in a world")) yield break;
            _gapsBeforePlayers = ScryPanel.GapNoticeForTest(new GapNotice());
            var ana = (AnaSession, "Ana");
            var bjorn = (BjornSession, "Bjorn");
            PeerScry? Of(string name)
            {
                foreach (var (who, scry) in EventLink.PlayingWith) if (who == name) return scry;
                return null;
            }
            string Told() => $"Ana {Of("Ana")?.ToString() ?? "not listed"}, Bjorn {Of("Bjorn")?.ToString() ?? "not listed"}";

            EventLink.Peers.Hello(BjornSession, ScryPeers.Messages);
            var since = Time.unscaledTime;
            EventLink.AlsoListedForTest(new List<(long Session, string Name)> { ana, bjorn }, 0);
            yield return Until(() => Of("Bjorn") == PeerScry.Scry, 3);
            p.Check(Of("Bjorn") == PeerScry.Scry && Of("Ana") == null, "a player whose Scry said hello is played with at once, one who said none not yet", Told());

            // Bjorn dies: listed without a character until he rises again.
            EventLink.AlsoListedForTest(new List<(long Session, string Name)> { ana }, 1);
            for (var i = 0; i < 3; i++) yield return null;
            p.Check(Of("Bjorn") == PeerScry.Scry && EventLink.Peers.Has(BjornSession), "one rising again after a death, listed without a character, is still played with, hello and all", Told());

            // Ten seconds in the world with no hello: Ana is named.
            yield return Until(() => Of("Ana") == PeerScry.None, (float)ScryPeers.HelloWait + 3f);
            var waited = Time.unscaledTime - since;
            p.Check(Of("Ana") == PeerScry.None && waited >= ScryPeers.HelloWait - 1.0, "one who said no hello is named as playing without Scry once 10 s in the world",
                $"after {Numbers.Amount(waited, 1)} s: {Told()}");
            var book = CompendiumKeeper.Book;
            if (book != null && book.Rule != CompendiumRule.Off && Session.IsOpen)
            {
                yield return Until(() => ScryPanel.PlayingWithLine?.Contains("Ana") ?? false, 3);
                p.Check(ScryPanel.PlayingWithLine?.Contains("Ana") ?? false, "and the notice names her", ScryPanel.PlayingWithLine);
            }
            else p.Note("the compendium is off or the panel closed, so no notice is drawn to read");

            // Bjorn rises again, then leaves.
            EventLink.AlsoListedForTest(new List<(long Session, string Name)> { ana, bjorn }, 0);
            for (var i = 0; i < 3; i++) yield return null;
            p.Check(Of("Bjorn") == PeerScry.Scry, "risen again, he is played with as before", Told());
            EventLink.AlsoListedForTest(new List<(long Session, string Name)> { ana }, 0);
            for (var i = 0; i < 3; i++) yield return null;
            p.Check(Of("Bjorn") == null && !EventLink.Peers.Has(BjornSession) && Of("Ana") == PeerScry.None, "gone, he is forgotten, hello and all", Told());
            PlayersAsTheyWere();
        }

        /// <summary>Takes the made-up players off the list and puts the player's notice back, should the part have stopped before it did.</summary>
        private static void PlayersAsTheyWere()
        {
            EventLink.AlsoListedForTest(null, 0);
            if (_gapsBeforePlayers != null) ScryPanel.GapNoticeForTest(_gapsBeforePlayers);
            _gapsBeforePlayers = null;
        }
    }
}
