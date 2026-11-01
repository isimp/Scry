using UnityEngine;
using static Scry.ScryPanel;

namespace Scry
{
    /// <summary>
    /// The self-test in the panel, drawn by the self-test where the panel leaves it room
    /// (<see cref="SelfTestHost"/>), with the panel's own strips and cards. While it runs, the
    /// strip shows it live: which part of how many, about how long is left, the newest line that
    /// needs a look, a segment a part along its foot coloured by how the part went, and Pause and
    /// Stop. Held, or once done, the card stands in the list's place: a tile a part by area and a
    /// feed of what needs a look (<see cref="SelfTestLive"/>); done, it also names every failed and
    /// skipped part and says what to do. The list is never covered while a part runs, as parts
    /// read what it draws. Between runs, the strip says what it recommends running next, and its
    /// card lists each with a button that runs it (<see cref="SelfTestRecommended"/>).
    /// </summary>
    internal static partial class SelfTestPanel
    {
        private static bool _shown;
        private static bool _details;
        private static bool _recommendedCard;
        private static bool _pauseCardHidden;
        private static string _recommendedAway;

        /// <summary>The card a paused run holds up put away, or let stand again as the run goes on or pauses anew.</summary>
        private static void HidePauseCard(bool hidden) => _pauseCardHidden = hidden;

        /// <summary>The last run's details put away.</summary>
        private static void DetailsAway() => _details = false;

        /// <summary>What is recommended shown in the list's place, the last run's details giving way to it, or put away.</summary>
        private static void ShowRecommended(bool shown)
        {
            _recommendedCard = shown;
            if (shown) _details = false;
        }

        /// <summary>What is recommended put away until what it says changes.</summary>
        private static void RecommendedAway(string line)
        {
            _recommendedAway = line;
            _recommendedCard = false;
        }

        /// <summary>Shows the strip again, as a run starts or ends.</summary>
        public static void ShowResult()
        {
            _shown = true;
            _details = false;
            _recommendedCard = false;
            _pauseCardHidden = false;
            ShowNewest();
        }

        /// <summary>Whether the strip shows: while a run goes on, once one has ended until it is put away, and while something is recommended until put away.</summary>
        public static bool StripShown => !HiddenForPicture && (SelfTest.Running || (_shown && SelfTest.LastHeadline != null) || RecommendedShown);

        /// <summary>Whether what is recommended has a strip: something to run, its line not put away.</summary>
        private static bool RecommendedShown => !SelfTest.Running && SelfTest.Recommended?.Line is string line && line != _recommendedAway;

        /// <summary>Whether the strip is put away for a clean picture, as the README's are taken.</summary>
        public static bool HiddenForPicture { get; set; }

        /// <summary>Whether a card stands in the list's place: while a run is held, or between runs the last run's details or what is recommended; never while a part runs.</summary>
        public static bool CardShown => SelfTest.Running ? SelfTest.Paused && !_pauseCardHidden : _details || _recommendedCard;

        /// <summary>The strip: the run live; once done, its headline with its details and a cross; between runs, what is recommended.</summary>
        [Diagnostic]
        public static void Strip(Rect rect)
        {
            if (SelfTest.Running)
            {
                LiveStrip(rect);
                return;
            }
            if (_shown && SelfTest.LastHeadline != null)
            {
                ResultStrip(rect);
                return;
            }
            RecommendedStrip(rect);
        }

        private static void ResultStrip(Rect rect)
        {
            var tone = SelfTest.LastFailed ? Skin.Warn : Skin.KindColor(Kind.StatusEffect);
            switch (NoticeStrip(rect, tone, SelfTest.LastHeadline, Skin.Label, false, "Details", _details, true))
            {
                case StripClick.Chip:
                    _details = !_details;
                    _recommendedCard = false;
                    CloseCards();
                    break;
                case StripClick.Cross:
                    _shown = false;
                    _details = false;
                    break;
            }
        }

        /// <summary>The card in the list's place: what is recommended, or the run held or done.</summary>
        public static void Card(Rect rect)
        {
            if (_recommendedCard && !SelfTest.Running) RecommendedCard(rect);
            else OutcomeCard(rect);
        }
    }
}
