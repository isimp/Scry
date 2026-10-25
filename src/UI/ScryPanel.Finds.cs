using System.Collections.Generic;
using UnityEngine;

namespace Scry
{
    /// <summary>
    /// Under the strict rules, the chip on a known page's title row telling how much of what the
    /// page keeps back was found out (<see cref="PageFinds"/>), or why it shows whole.
    /// </summary>
    internal static partial class ScryPanel
    {
        /// <summary>The page the finds were gathered for, what the character knew then and when, and what they are.</summary>
        private static Entry _findsOf;
        private static int _findsStamp = -1;
        private static float _findsAt;
        private static List<(string Label, string Value)> _findsStill = new List<(string, string)>();
        private static PageFinds _finds = new PageFinds();

        /// <summary>The chip's words as last drawn, for the self-test; none where no chip was.</summary>
        [Diagnostic]
        public static string FindsChipForTest { get; private set; }

        /// <summary>What the page last gathered still keeps back, by what to do, for the self-test.</summary>
        [Diagnostic]
        public static IEnumerable<string> StillForTest => _findsStill.ConvertAll(s => s.Label);

        /// <summary>How much was found out of the page last gathered, of the numbers it keeps back, for the self-test.</summary>
        [Diagnostic]
        public static (int Found, int Of) FindsForTest => (_finds.Found, _finds.Of);

        /// <summary>
        /// What the page shown keeps back under the strict rules: what is still to find out, and how
        /// much of all it keeps back was found out; gathered again for another page, as what the
        /// character knows changes, and at most once a second.
        /// </summary>
        private static (List<(string Label, string Value)> Still, PageFinds Finds) FindsOf(Explorer explorer, Entry entry)
        {
            if (!ReferenceEquals(entry, _findsOf) || _findsStamp != explorer.KnowStamp || Time.unscaledTime - _findsAt > 1f)
            {
                _findsOf = entry;
                _findsStamp = explorer.KnowStamp;
                _findsAt = Time.unscaledTime;
                (_findsStill, _finds) = StillToFindOut(explorer, Facts.For(entry));
            }
            return (_findsStill, _finds);
        }

        /// <summary>Whether a page still has something to find out, by entry, as last gathered; told anew as what the character knows changes.</summary>
        private static readonly Dictionary<Entry, bool> LeftByPages = new Dictionary<Entry, bool>();
        private static int _leftStamp = -1;
        private static Explorer _leftIn;

        /// <summary>
        /// Whether a page still has something to find out, as its title chip would count it, for
        /// the search's <c>is:left</c> (<see cref="CompendiumView.LeftByPage"/>): the finds gathered
        /// under the rules as they stand on that page (<see cref="Explorer.AsPage{T}"/>), once for
        /// each page while what the character knows stays the same.
        /// </summary>
        private static bool LeftByPage(Explorer explorer, Entry entry)
        {
            if (entry == null) return false;
            if (!ReferenceEquals(_leftIn, explorer) || _leftStamp != explorer.KnowStamp)
            {
                _leftIn = explorer;
                _leftStamp = explorer.KnowStamp;
                LeftByPages.Clear();
            }
            if (LeftByPages.TryGetValue(entry, out var left)) return left;
            left = explorer.AsPage(entry, () =>
            {
                if (!explorer.LeavingOut) return false;
                var (_, finds) = StillToFindOut(explorer, Facts.For(entry));
                return finds.Of > finds.Found;
            });
            LeftByPages[entry] = left;
            return left;
        }

        /// <summary>Hands the view the page's finds for its search (<see cref="LeftByPage"/>), each frame, as the view may be a new one.</summary>
        private static void HandFinds(Explorer explorer)
        {
            var view = explorer?.Compendium;
            if (view != null && view.LeftByPage == null) view.LeftByPage = entry => LeftByPage(explorer, entry);
        }

        /// <summary>Lets go of the page the finds were gathered for, for another world.</summary>
        private static void ForgetFinds()
        {
            LeftByPages.Clear();
            _leftIn = null;
            _findsOf = null;
            _findsStill = new List<(string, string)>();
            _finds = new PageFinds();
        }

        /// <summary>
        /// The chip on a known page's title row under the strict rules, before copy and star: how
        /// many of the numbers it keeps back were found out, with a small bar, its tip what is
        /// still to find out and a click going to that in the You box; lit once all were; faint
        /// where the floor or a source of the world shows the page whole, its tip saying what does
        /// and how to see what is left; lit from the start on a page that keeps nothing back, so
        /// every known page under the rules carries it. Where the chip begins, else <paramref name="right"/>.
        /// </summary>
        private static float FindsChip(Explorer explorer, Entry entry, float right, float y)
        {
            FindsChipForTest = null;
            var whole = explorer.ShownWhole(out var source);
            if (!whole && !explorer.LeavingOut) return right;
            var (still, finds) = whole ? (null, null) : FindsOf(explorer, entry);
            var found = whole ? 0 : finds.Found;
            var of = whole ? 0 : finds.Of;
            CountDrawn(PanelPart.FindsChip);
            var height = U(24f);
            var icon = U(14f);
            var e = Event.current;
            if (whole)
            {
                var said = CompendiumWords.WholeBy(source);
                var at = new Rect(right - (Skin.Width(Skin.Small, said) + icon + U(22f)), y, Skin.Width(Skin.Small, said) + icon + U(22f), height);
                Skin.Box(at, Skin.Raised);
                // The floor's stairs, or the folded map of a source of the world, as the list's rows mark it.
                Skin.Icon(new Rect(at.x + U(8f), at.y + (height - icon) / 2f, icon, icon), source != null ? Skin.FoldedMap : Skin.Steps, Skin.Faint);
                Skin.LabelIn(new Rect(at.x + U(12f) + icon, at.y, at.width - icon - U(16f), height), said, Skin.Small, Skin.Faint);
                if (at.Contains(e.mousePosition)) AskTip("finds:whole", CompendiumWords.WholeTip(source));
                FindsChipForTest = said;
                return at.x;
            }
            if (found >= of)
            {
                var said = CompendiumWords.AllFoundOut;
                var width = Skin.Width(Skin.ChipOn, said) + icon + U(22f);
                var at = new Rect(right - width, y, width, height);
                GUI.Label(at, GUIContent.none, Skin.ChipOn);
                if (MarkPicture(MarkLook.Whole).OrNull()?.texture is Texture tick) GUI.DrawTexture(new Rect(at.x + U(7f), at.y + (height - icon) / 2f, icon, icon), tick);
                Skin.LabelIn(new Rect(at.x + U(11f) + icon, at.y, width - icon - U(15f), height), said, Skin.ChipOn, Skin.OnAccent);
                if (at.Contains(e.mousePosition)) AskTip("finds:all" + finds.Any, CompendiumWords.AllFoundOutTipFor(finds));
                FindsChipForTest = said;
                return at.x;
            }
            // Narrow where the name would keep less than a third of the row.
            var text = CompendiumWords.FoundOutCount(found, of, narrow: false);
            var bar = U(28f);
            var full = Skin.Width(Skin.Small, text) + bar + U(26f);
            if (right - full < right * 0.34f)
            {
                text = CompendiumWords.FoundOutCount(found, of, narrow: true);
                full = Skin.Width(Skin.Small, text) + bar + U(26f);
            }
            var chip = new Rect(right - full, y, full, height);
            var go = GUI.Button(chip, GUIContent.none, Skin.Chip);
            var track = new Rect(chip.x + U(10f), chip.y + (height - U(4f)) / 2f, bar, U(4f));
            Skin.Fill(track, Skin.Alpha(Skin.Accent, 0.25f));
            Skin.Fill(new Rect(track.x, track.y, track.width * found / Mathf.Max(1, of), track.height), Skin.Accent);
            Skin.LabelIn(new Rect(track.xMax + U(6f), chip.y, chip.xMax - track.xMax - U(10f), height), text, Skin.Small, Skin.Accent);
            if (chip.Contains(e.mousePosition)) AskTip("finds:" + text, CompendiumWords.FindsTip(still));
            if (go && still.Count > 0) ArriveAt(NewsArrival.AtYouLine(still[0].Label));
            FindsChipForTest = text;
            return chip.x;
        }
    }
}
