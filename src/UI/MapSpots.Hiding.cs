using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// Kept kinds hidden on Valheim's map from the kinds list or a mark's menu
    /// (<see cref="CompendiumBook.Hides"/>): their dots, squares and dungeon marks left off the map
    /// and the minimap while they stay kept and go on being counted, so their chips stay on the
    /// list, grey, to be shown again. Only while the list is turned on (<see cref="KindsList.Hides"/>),
    /// where they are shown again. The kind a page shows on demand stands whatever is hidden. The
    /// spots crossed off and those the server told of are hidden by their settings, from the list's foot.
    /// </summary>
    internal static partial class MapSpots
    {
        /// <summary>What was hidden as the marks were last placed, for them to be placed again as it changes.</summary>
        private static (CompendiumBook Book, int Version, bool ListOn) _hiddenFor;

        /// <summary>Whether a kept kind is hidden on the map now: hidden in the book, while the list is turned on.</summary>
        public static bool HiddenKind(string kind) => kind != null && KindsList.Hides(Settings.KindsStrip, CompendiumKeeper.Book?.Hides(kind) ?? false);

        /// <summary>Whether anything is hidden on the map: kinds, the spots crossed off, or the places the server told of.</summary>
        public static bool AnyHidden =>
            KindsList.Hides(Settings.KindsStrip, CompendiumKeeper.Book?.HidesAny ?? false) || Settings.CrossedMarks == CrossedMarks.Hidden || !Settings.MapSquares;

        /// <summary>Whether a dot standing is left off as its kind is hidden; never one of the kind shown on demand.</summary>
        private static bool HiddenDot(int dot) => dot >= _demandDots && dot < StandingKinds.Count && HiddenKind(StandingKinds[dot]);

        /// <summary>Whether a kept kind's squares are left out: shown on demand, whose squares are its own already, or hidden.</summary>
        private static bool SquaresLeftOut(string kind) => KindsShownNow().Contains(kind) || HiddenKind(kind);

        /// <summary>The marks placed again where what is hidden changed, as a setting changed is followed.</summary>
        public static void FollowHiding()
        {
            var book = CompendiumKeeper.Book;
            var now = (book, book?.HiddenVersion ?? -1, Settings.KindsStrip);
            if (now == _hiddenFor) return;
            _hiddenFor = now;
            MapKept.MarksChanged();
        }

        /// <summary>The kinds under the list's chip holding a kind, and the chip's name; the kind alone, by the name given, where no chip holds it.</summary>
        public static (string[] Kinds, string Name) ChipOf(string kind, string name)
        {
            foreach (var chip in KindChips()) if (System.Array.IndexOf(chip.Kinds, kind) >= 0) return (chip.Kinds, chip.Name);
            return (new[] { kind }, name);
        }

        /// <summary>Every kind standing on the list, for showing one alone while all are hidden.</summary>
        public static List<string> KindsListed()
        {
            var kinds = new List<string>();
            foreach (var chip in KindChips()) kinds.AddRange(chip.Kinds);
            return kinds;
        }

        /// <summary>Kinds hidden or shown again from the list or a mark's menu, kept with the character at once.</summary>
        public static void HideKinds(IReadOnlyList<string> kinds, bool on)
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return;
            book.Hide(kinds, on, KindsListed());
            CompendiumKeeper.Save(now: true);
        }

        /// <summary>Every mark hidden or shown again, kept with the character at once.</summary>
        public static void HideAllKinds(bool on)
        {
            var book = CompendiumKeeper.Book;
            if (book == null) return;
            book.HideAll(on);
            CompendiumKeeper.Save(now: true);
        }

        /// <summary>Everything hidden shown again: the kinds, the spots crossed off and the places the server told of.</summary>
        public static void ShowEverything()
        {
            var book = CompendiumKeeper.Book;
            if (book != null && book.HidesAny)
            {
                book.ShowAll();
                CompendiumKeeper.Save(now: true);
            }
            if (Settings.CrossedMarks == CrossedMarks.Hidden) Settings.CrossedMarks = CrossedMarks.Faded;
            if (!Settings.MapSquares) Settings.MapSquares = true;
        }
    }
}
