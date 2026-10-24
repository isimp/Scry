using UnityEngine;

namespace Scry
{
    /// <summary>
    /// A row of the learned list opens its page at what changed (<see cref="NewsArrival"/>): the
    /// line showing it is noted where the details draw it, the details scroll to it, and it is lit
    /// a moment in the look of the list's row picked, then fades.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>How long a page waits for a draw of its details to find the line arrived at, its facts read first.</summary>
        private const float ArrivalWait = 1.5f;

        /// <summary>Where the page shown opens, while a draw of its details has not found it; none.</summary>
        private static NewsArrival _arriving;

        private static float _arrivingSince;

        /// <summary>Where the draw going on found the line arrived at; none yet.</summary>
        private static float? _spotTop;

        /// <summary>The spot arrived at, lit a moment after <see cref="_litAt"/>; none.</summary>
        private static string _litSpot;

        private static float _litAt;

        /// <summary>The spot last arrived at, for the self-test; none.</summary>
        [Diagnostic]
        public static string ArrivedForTest { get; private set; }

        /// <summary>Whether a line arrived at is looked for or lit, so a spot is worth making for each line drawn.</summary>
        private static bool Spotting => _arriving != null || _litSpot != null;

        /// <summary>The attack a table's line plays, as the spot of its damage found out; none for a line of no attack.</summary>
        private static string AttackSpot(Facts.Row row, string link) => row.AttackCells != null && EntryKeys.PlaysAttack(link, out var item, out _) ? item : null;

        /// <summary>Another page is shown: from the learned list, it opens at what changed, In the game unfolded for it; else as it is.</summary>
        private static void ArriveAtNews(Explorer explorer)
        {
            _litSpot = null;
            ArrivedForTest = null;
            _arriving = explorer.LearnedOnly ? explorer.ArrivalOf(explorer.Selected) : null;
            _arrivingSince = Time.unscaledTime;
            if (_arriving != null && IsFolded("facts")) SetFolded("facts", false);
        }

        /// <summary>A new draw of the details looks for the line arrived at afresh.</summary>
        private static void StartSpotting() => _spotTop = null;

        /// <summary>A line standing for a spot is drawn at a height: noted while a page waits to arrive at it and the details' headings are noted.</summary>
        private static void NoteSpot(string spot, Rect rect)
        {
            if (_marking && _arriving != null && _spotTop == null && spot != null && spot == _arriving.Spot) _spotTop = rect.y;
        }

        /// <summary>Lights the line arrived at a moment, as the list lights the row picked: tinted, with the accent's bar at its left, fading.</summary>
        private static void LitSpot(string spot, Rect rect)
        {
            if (_litSpot == null || spot != _litSpot || Event.current.type != EventType.Repaint) return;
            var lit = NewsArrival.Lit(Time.unscaledTime - _litAt);
            if (lit <= 0f) return;
            var around = new Rect(rect.x - U(6f), rect.y - U(2f), rect.width + U(12f), rect.height + U(4f));
            Skin.Box(around, Skin.Alpha(Skin.AccentSoft, Skin.AccentSoft.a * lit));
            Skin.Fill(new Rect(around.x, around.y, U(3f), around.height), Skin.Alpha(Skin.Accent, lit));
        }

        /// <summary>
        /// Where to scroll the details to for the page arrived at, once a draw found its line, else
        /// the first of its topics the page has, lighting what it found; null while none is found
        /// or none waits. A page whose draws find neither in a moment opens at its top.
        /// </summary>
        private static float? ArrivedAtNews(float maxScroll)
        {
            if (_arriving == null) return null;
            string lit = null;
            float? top = null;
            if (_spotTop is float spot)
            {
                top = spot;
                lit = _arriving.Spot;
            }
            else
            {
                foreach (var topic in _arriving.Topics)
                {
                    if (!TopicTops.TryGetValue(topic, out var at)) continue;
                    top = at;
                    lit = NewsArrival.Topic(topic);
                    break;
                }
            }
            if (top == null)
            {
                if (Time.unscaledTime - _arrivingSince > ArrivalWait) _arriving = null;
                return null;
            }
            _arriving = null;
            _litSpot = lit;
            _litAt = Time.unscaledTime;
            ArrivedForTest = lit;
            return SectionLine.JumpTo(top.Value, U(40f), maxScroll);
        }
    }
}
