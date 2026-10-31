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
        private bool _lastLaidNew;

        /// <summary>When each part of the page was laid, by the clock, for its fade; none for a page drawn whole at once.</summary>
        private readonly System.Collections.Generic.List<double> _laidAt = new System.Collections.Generic.List<double>();

        /// <summary>When the page was laid whole, by the clock; none for a page drawn whole at once.</summary>
        private double? _wholeAt;

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
            _laidAt.Clear();
            _wholeAt = null;
        }

        /// <summary>Lays the rest at once: for a page whose parts are all at hand.</summary>
        public void Stop()
        {
            Active = false;
            _laidAt.Clear();
            _wholeAt = null;
        }

        /// <summary>The part last asked about, by its place in the page's order.</summary>
        public int LastUnit { get; private set; }

        /// <summary>
        /// How far a part laid over frames has faded in, from nothing as it is laid to all once
        /// <paramref name="fadeMs"/> have passed; all for a page drawn whole at once, nothing for
        /// a part not laid yet, all for one first drawn once the page was whole, which never waited.
        /// </summary>
        public double Shown(int unit, double fadeMs)
        {
            if (!Active && _wholeAt == null) return 1.0;
            if (unit < 0) return 0.0;
            if (unit >= _laidAt.Count) return Active ? 0.0 : 1.0;
            return Math.Min(1.0, Math.Max(0.0, (_clock() - _laidAt[unit]) / fadeMs));
        }

        /// <summary>How far what waits for the whole page has faded in: nothing while it is laid out, all for a page drawn whole at once.</summary>
        public double WholeShown(double fadeMs)
        {
            if (Active) return 0.0;
            if (_wholeAt == null) return 1.0;
            return Math.Min(1.0, Math.Max(0.0, (_clock() - _wholeAt.Value) / fadeMs));
        }

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
            _lastLaidNew = false;
        }

        /// <summary>The page's next part: whether it is drawn whole, else as waiting.</summary>
        public bool Lay()
        {
            _lastLaidNew = false;
            LastUnit = _unit;
            if (!Active)
            {
                _unit++;
                return true;
            }
            var i = _unit++;
            if (i < _laid) return true;
            if (_repaint && !_skip && i == _laid && (_laidThisPass == 0 || HasRoom()))
            {
                _laid++;
                _laidThisPass++;
                _lastLaidNew = true;
                _laidAt.Add(_clock());
                return true;
            }
            HeldFirst = !_held;
            _held = true;
            return false;
        }

        /// <summary>Whether the frame's share has room left: Scry's other work of the frame and this drawing so far counted.</summary>
        public bool HasRoom() => _spentMs + _clock() - _passStart < _budgetMs;

        /// <summary>
        /// The part just laid anew is not done yet (its facts worked out over several frames): it
        /// waits after all, as does every part after it, and is asked again at the next repaint.
        /// False where it was laid before, which cannot be taken back.
        /// </summary>
        public bool NotYet()
        {
            if (!_lastLaidNew) return false;
            _lastLaidNew = false;
            _laid--;
            _laidAt.RemoveAt(_laidAt.Count - 1);
            HeldFirst = !_held;
            _held = true;
            return true;
        }

        /// <summary>The drawing ends: a repaint that held nothing back has laid the whole page.</summary>
        public void EndPass()
        {
            if (!Active) return;
            if (_repaint) _seen = _unit;
            if (_repaint) _laidLastRepaint = _laidThisPass > 0;
            if (_repaint && !_held)
            {
                Active = false;
                _wholeAt = _clock();
            }
        }
    }
}
