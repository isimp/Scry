using System.Collections;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>The self-test's case for the compact view's one notice strip.</summary>
    internal static partial class SelfTest
    {
        /// <summary>The player's notice of players without Scry, kept while the part stands one of its own in.</summary>
        private static GapNotice _gapsBeforeNotices;

        /// <summary>The view the panel was in before the part, put back after.</summary>
        private static bool? _compactBeforeNotices;

        /// <summary>
        /// In the compact view, peeking and a made-up player without Scry share one strip: the
        /// warning first, "1 of 2", the players' notice not drawn meanwhile; its step goes to the
        /// players' notice, "2 of 2", and round to the warning again; the peek stopped, the
        /// players' notice stands alone with no step; peeking again, the warning takes the strip
        /// at once. Pictured with both up. The players, the player's notice and the peek are put back.
        /// </summary>
        private static IEnumerator NoticesShareOneStrip(Probe p)
        {
            yield return Until(() => CompendiumKeeper.Book != null, 10);
            var book = CompendiumKeeper.Book;
            if (!p.Check(book != null, "the character's book is read")) yield break;
            if (book.Rule == CompendiumRule.Off)
            {
                p.Skip("the character plays with the compendium off, so nothing peeks and nothing tells who has Scry");
                yield break;
            }
            Session.ShowKnownInstead(null);
            if (!Session.IsOpen) Session.Show(null);
            KeepView();
            _compactBeforeNotices = ScryPanel.Compact;
            ScryPanel.Compact = true;
            _gapsBeforeNotices = ScryPanel.GapNoticeForTest(new GapNotice());
            EventLink.PlayingWithForTest(new List<(string Name, PeerScry Scry)> { ("Ana", PeerScry.None) });
            // The character's own view comes back at the session's next look, the view laid before
            // the part having the compendium off; peeking is refused until it is set.
            yield return Until(() => X.CompendiumSet, 5);
            X.Peeking = true;
            if (!p.Check(X.Peeking, "the character's compendium set again, peeking is on"))
            {
                NoticesAsTheyWere();
                yield break;
            }

            var lines = ScryPanel.Drawn(PanelPart.NoticeLine);
            yield return Until(() => ScryPanel.Drawn(PanelPart.NoticeLine) > lines + 2 && ScryPanel.NoticeLineForTest.Of == 2, 3);
            var players = ScryPanel.Drawn(PanelPart.PlayingWithNotice);
            var peeks = ScryPanel.Drawn(PanelPart.PeekStrip);
            for (var i = 0; i < 3; i++) yield return null;
            p.Check(ScryPanel.NoticeLineForTest == (Notice.Peeking, 1, 2) && ScryPanel.Drawn(PanelPart.PeekStrip) > peeks && ScryPanel.Drawn(PanelPart.PlayingWithNotice) == players,
                "peeking and a player without Scry share one strip, the warning first, the players' notice not drawn meanwhile", ScryPanel.NoticeLineForTest.ToString());
            p.Check(ScryPanel.PeekLineFitsForTest, "the peeking line fits the compact strip, sliding nowhere", CompendiumWords.PeekingLine(compact: true));
            yield return Picture(p, "compact notices one strip");

            ScryPanel.StepNoticeForTest();
            yield return Until(() => ScryPanel.NoticeLineForTest.Shown == Notice.PlayingWith && ScryPanel.Drawn(PanelPart.PlayingWithNotice) > players + 1, 3);
            p.Check(ScryPanel.NoticeLineForTest == (Notice.PlayingWith, 2, 2) && ScryPanel.Drawn(PanelPart.PlayingWithNotice) > players,
                "its step shows the players' notice, second of two", ScryPanel.NoticeLineForTest.ToString());
            ScryPanel.StepNoticeForTest();
            yield return Until(() => ScryPanel.NoticeLineForTest.Shown == Notice.Peeking, 3);
            p.Check(ScryPanel.NoticeLineForTest == (Notice.Peeking, 1, 2), "and round to the warning again", ScryPanel.NoticeLineForTest.ToString());

            X.Peeking = false;
            // The news of what was learned is put away while peeking and may come back with the peek's end: the players' notice stands first, the news after it where there is any.
            yield return Until(() => ScryPanel.NoticeLineForTest.Shown == Notice.PlayingWith, 3);
            var up = ScryPanel.NoticesUpForTest.Count;
            p.Check(ScryPanel.NoticeLineForTest == (Notice.PlayingWith, 1, up), up > 1 ? "the peek stopped, the players' notice stands first, the news after it" : "the peek stopped, the players' notice stands alone, with no step", ScryPanel.NoticeLineForTest.ToString());
            X.Peeking = true;
            yield return Until(() => ScryPanel.NoticeLineForTest.Shown == Notice.Peeking, 3);
            p.Check(ScryPanel.NoticeLineForTest == (Notice.Peeking, 1, 2), "peeking again, the warning takes the strip at once", ScryPanel.NoticeLineForTest.ToString());
            NoticesAsTheyWere();
        }

        /// <summary>Puts the peek, the world's players, the player's notice and the view back, should the part have stopped before it did.</summary>
        private static void NoticesAsTheyWere()
        {
            if (X != null) X.Peeking = false;
            EventLink.PlayingWithForTest(null);
            if (_gapsBeforeNotices != null) ScryPanel.GapNoticeForTest(_gapsBeforeNotices);
            _gapsBeforeNotices = null;
            if (_compactBeforeNotices.HasValue) ScryPanel.Compact = _compactBeforeNotices.Value;
            _compactBeforeNotices = null;
        }
    }
}
