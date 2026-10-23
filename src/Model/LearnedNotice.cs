using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Scry
{
    /// <summary>
    /// What the character learned since the panel last told of it, for the notice under its
    /// header. Each book (each character) keeps its own mark in its count of what was learned
    /// (<see cref="CompendiumBook.Learned"/>), from the start of the session until told. Only
    /// what the list lets show is named (seen or known), and nothing while the compendium is off
    /// or peeking. Worked out again only when the book, its mark or what is shown changed.
    /// </summary>
    internal sealed class LearnedNotice
    {
        private sealed class Mark
        {
            public int At;
        }

        private static readonly List<Entry> None = new List<Entry>();

        // A mark for each book while it is about: a made-up character's goes with it.
        private readonly ConditionalWeakTable<CompendiumBook, Mark> _marks = new ConditionalWeakTable<CompendiumBook, Mark>();
        private CompendiumBook _book;
        private List<Entry> _new = None;
        private int _bookVersion = -1;

        /// <summary>The book's count of what it learned when worked out last: news found out raises no level, so the version alone would miss it.</summary>
        private int _learned = -1;
        private int _stamp = -1;
        private int _mark = -1;

        /// <summary>What was learned since the mark, newest first, as far as the book keeps what it learned lately.</summary>
        public List<Entry> New(Explorer explorer)
        {
            var book = explorer.CompendiumOn ? explorer.Compendium.Book : null;
            if (book == null)
            {
                _book = null;
                return None;
            }
            var mark = _marks.GetValue(book, _ => new Mark()).At;
            if (ReferenceEquals(book, _book) && book.Version == _bookVersion && book.Learned == _learned && explorer.KnowStamp == _stamp && mark == _mark) return _new;

            _book = book;
            _bookVersion = book.Version;
            _learned = book.Learned;
            _stamp = explorer.KnowStamp;
            _mark = mark;
            _new = new List<Entry>();
            foreach (var key in explorer.Compendium.LearnedSince(mark))
            {
                var entry = explorer.Find(key);
                // Named as the list shows them: the world a player meets.
                if (entry != null && explorer.LevelOf(entry) >= KnownLevel.Seen && Kinds.ForPlayers(entry)) _new.Add(entry);
            }
            return _new;
        }

        /// <summary>Marks all the book shown last has learned as told.</summary>
        public void Told()
        {
            if (_book == null) return;
            _marks.GetValue(_book, _ => new Mark()).At = _book.Learned;
        }

        /// <summary>Lets go of the book shown last, as on leaving a world.</summary>
        public void Forget()
        {
            _book = null;
            _new = None;
        }
    }
}
