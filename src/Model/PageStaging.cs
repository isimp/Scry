using System;

namespace Scry
{
    /// <summary>
    /// How far a page opened for the first time is laid out. Its parts, in the order the page
    /// draws them, are laid one after another over the frames after the click: each repaint lays
    /// at least one more and goes on while its share of the frame lasts, so no frame waits on all
    /// of them. Only a repaint lays more, so the events between two repaints (a click, a key) meet
    /// the page as it was last shown. A part laid is drawn whole from then on; one not yet laid is
    /// drawn as waiting, and every part after it waits too, so nothing moves up into its place.
    /// </summary>
    internal sealed class PageStaging
    {
        private readonly Func<double> _clock;
        private readonly double _budgetMs;
        private int _laid;
        private int _unit;
        private int _seen;
        private bool _repaint;
        private double _passStart;
        private int _laidThisPass;
        private bool _held;
        private double _spentMs;
        private bool _skip;
        private bool _laidLastRepaint;

        /// <param name="clock">The time now, in milliseconds.</param>
        /// <param name="budgetMs">How much of a repaint may go to laying parts beyond the first.</param>
        public PageStaging(Func<double> clock, double budgetMs)
        {
            _clock = clock;
            _budgetMs = budgetMs;
        }

        /// <summary>Whether parts of the page are still to be laid.</summary>
        public bool Active { get; private set; }

        /// <summary>Whether the part last asked about is the first held back in this drawing: the one being worked on next.</summary>
        public bool HeldFirst { get; private set; }

        /// <summary>Whether this drawing held back any part so far.</summary>
        public bool HeldAny => _held;

        /// <summary>How far it has got: the parts laid of those the last drawing went through; all once done.</summary>
        public double Share => !Active ? 1.0 : _seen == 0 ? 0.0 : Math.Min(1.0, (double)_laid / _seen);

        /// <summary>A page opened anew: nothing of it is laid yet.</summary>
        public void Begin()
        {
            Active = true;
            _laid = 0;
            _seen = 0;
            _laidLastRepaint = true;
        }

        /// <summary>Lays the rest at once: for a page whose parts are all at hand.</summary>
        public void Stop() => Active = false;

        /// <summary>
        /// A drawing of the page begins: a repaint, or an event between repaints. A repaint in a
        /// frame whose other work of Scry's already took the share (<paramref name="spentMs"/>, the
        /// stage's copy made and filmed) lays nothing new, unless the last repaint laid nothing
        /// either, so the page always gets on.
        /// </summary>
        public void StartPass(bool repaint, double spentMs = 0)
        {
            _unit = 0;
            _repaint = repaint;
            _spentMs = spentMs;
            _skip = repaint && Active && spentMs >= _budgetMs && _laidLastRepaint;
            _passStart = _clock();
            _laidThisPass = 0;
            _held = false;
            HeldFirst = false;
        }

        /// <summary>The page's next part: whether it is drawn whole, else as waiting.</summary>
        public bool Lay()
        {
            if (!Active) return true;
            var i = _unit++;
            if (i < _laid) return true;
            if (_repaint && !_skip && i == _laid && (_laidThisPass == 0 || _spentMs + _clock() - _passStart < _budgetMs))
            {
                _laid++;
                _laidThisPass++;
                return true;
            }
            HeldFirst = !_held;
            _held = true;
            return false;
        }

        /// <summary>The drawing ends: a repaint that held nothing back has laid the whole page.</summary>
        public void EndPass()
        {
            if (!Active) return;
            if (_repaint) _seen = _unit;
            if (_repaint) _laidLastRepaint = _laidThisPass > 0;
            if (_repaint && !_held) Active = false;
        }
    }
}
