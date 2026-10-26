using System.Collections.Generic;

namespace Scry
{
    /// <summary>The list's families (<see cref="Family"/>): one row each with the compendium set, named and told as the family goes.</summary>
    internal sealed partial class Explorer
    {
        /// <summary>
        /// Whether the list shows a family (<see cref="Family"/>) as one row: with the compendium
        /// set, but in the lists of what was learned or looked at lately, each of whose rows tells
        /// of one member.
        /// </summary>
        public bool Folds => CompendiumSet && !_learnedOnly && !_recentOnly;

        /// <summary>The row an entry is listed by: its own, else, folded, its family's; -1 where none.</summary>
        private int RowOf(Entry entry)
        {
            if (entry == null) return -1;
            var row = _results.IndexOf(entry);
            if (row >= 0 || entry.Family == null || !Folds) return row;
            for (var i = 0; i < _results.Count; i++) if (_results[i].Family == entry.Family) return i;
            return -1;
        }

        /// <summary>The name a row shows: its family's where the list folds it (<see cref="Family.Name"/>), else its own.</summary>
        public string ShownAs(Entry entry) => Folds && entry?.Family != null ? entry.Family.Name : entry?.ShownName;

        /// <summary>
        /// What a family's row tells of its kinds where it folds: how many the character met and
        /// how many of those it knows (<see cref="CompendiumWords.FamilyNote"/>), never the kinds
        /// it has not met; none for fewer than two met, or for anything else.
        /// </summary>
        public string FamilyNote(Entry entry)
        {
            if (!Folds || entry?.Family == null) return null;
            if (_familyNoteStamp != KnowStamp)
            {
                _familyNoteStamp = KnowStamp;
                _familyNotes.Clear();
            }
            if (_familyNotes.TryGetValue(entry.Family, out var told)) return told;
            var met = 0;
            var known = 0;
            foreach (var kind in entry.Family.Kinds)
            {
                var level = _view.Of(kind);
                if (level >= KnownLevel.Seen) met++;
                if (level == KnownLevel.Known) known++;
            }
            told = met > 1 ? CompendiumWords.FamilyNote(met, known) : null;
            _familyNotes[entry.Family] = told;
            return told;
        }

        private readonly Dictionary<Family, string> _familyNotes = new Dictionary<Family, string>();
        private int _familyNoteStamp = -1;

        /// <summary>
        /// The members of an entry's family its page switches among, with the compendium set: those
        /// the character met, seen or known, in the family's order; none where it met fewer than
        /// two or the entry is in no family, nor with the compendium off, where each prefab is its
        /// own row.
        /// </summary>
        public IReadOnlyList<Entry> MembersMet(Entry entry)
        {
            if (!CompendiumSet || entry?.Family == null) return NoMembers;
            var met = entry.Family.Met(_view.Of);
            return met.Count > 1 ? met : NoMembers;
        }

        private static readonly Entry[] NoMembers = System.Array.Empty<Entry>();

        /// <summary>
        /// A member's words on its page's switch (<see cref="Family.Label"/>): a copy named by who
        /// carries it only once the character met every one of them, else by its prefab's own
        /// words (<see cref="Family.PlainLabel"/>).
        /// </summary>
        public string KindLabel(Entry member)
        {
            var family = member?.Family;
            if (family == null) return member?.ShownName ?? "";
            if (family.CopyOf(member) == null || !CompendiumOn) return family.Label(member);
            foreach (var link in member.Links)
                if (link.Group == LinkWords.CarriedBy && Find(link.Target) is Entry carrier && _view.Of(carrier) < KnownLevel.Seen) return family.PlainLabel(member);
            return family.Label(member);
        }

        /// <summary>Shows another member of the page's family met (<see cref="MembersMet"/>), on the same page: no step to go back from.</summary>
        public void SwitchTo(Entry member)
        {
            if (member == null || member == _selected) return;
            foreach (var met in MembersMet(_selected)) if (met == member) Select(member, false);
        }

        /// <summary>
        /// Whether a member's page keeps a value back that the proof shows, as that page would
        /// were it shown (<see cref="KeepsBack"/>): known under the strict rules, not whole, the
        /// proof not held for it.
        /// </summary>
        public bool KeepsBackOn(Entry page, string proof) =>
            page == _selected ? KeepsBack(proof) : proof != null && StrictOn(page) && !_view.Book.Proved(page.Key, proof);

        /// <summary>
        /// Whether a member's page leaves out the entry a key names, as that page would were it
        /// shown (<see cref="LeavesOut(string, bool, Tie?)"/>): what the character never met, and
        /// under the strict rules what it did not see together with that member.
        /// </summary>
        public bool LeavesOutOn(Entry page, string key, bool gameShown)
        {
            if (page == _selected) return LeavesOut(key, gameShown);
            if (!CompendiumOn || page == null || page.Kind == Kind.Mod || _view.Of(page) != KnownLevel.Known) return false;
            var other = Find(key);
            if (other == null || other == page || other.Kind == Kind.Mod) return false;
            if (_view.Of(other) < KnownLevel.Seen) return other.Kind == Kind.Skill || !(_view.HoldsNamed(page) || _view.HeldByWalks(page, other));
            return !gameShown && other.Kind != Kind.Skill && StrictOn(page) && !_view.Related(page, other);
        }

        /// <summary>Whether a page keeps back under the strict rules (<see cref="LeavingOut"/>), for any page, shown or not.</summary>
        private bool StrictOn(Entry page) =>
            CompendiumOn && _view.Rule == CompendiumRule.Strict && page != null && page.Kind != Kind.Mod && page.Kind != Kind.Skill
            && _view.Of(page) == KnownLevel.Known && !_view.Whole(page);
    }
}
