using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// A history of views stepped between, as in a browser: going into a room of a dungeon's
    /// example, from room to room and back out are steps. Back returns to the view a step was
    /// taken from, as it was left, keeping the view it left to go forward to again; a new step lets
    /// go of the steps forward. Only the last <see cref="Limit"/> are kept.
    /// </summary>
    internal sealed class ViewSteps<T>
    {
        /// <summary>How many steps back are kept.</summary>
        public const int Limit = 50;

        private readonly List<T> _back = new List<T>();
        private readonly List<T> _forward = new List<T>();

        public bool CanGoBack => _back.Count > 0;
        public bool CanGoForward => _forward.Count > 0;

        /// <summary>A step taken from a view: kept to go back to, the steps forward let go of.</summary>
        public void Took(T from)
        {
            Keep(_back, from);
            _forward.Clear();
        }

        /// <summary>Back from the view now: the view the last step was taken from, the view now kept to go forward to again; false with none.</summary>
        public bool Back(T now, out T to) => Step(_back, _forward, now, out to);

        /// <summary>Forward from the view now: the view gone back from last, the view now kept to go back to again; false with none.</summary>
        public bool Forward(T now, out T to) => Step(_forward, _back, now, out to);

        /// <summary>Lets go of every step, as the views they are of are gone.</summary>
        public void Clear()
        {
            _back.Clear();
            _forward.Clear();
        }

        private static bool Step(List<T> from, List<T> into, T now, out T to)
        {
            to = default;
            if (from.Count == 0) return false;
            to = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            Keep(into, now);
            return true;
        }

        private static void Keep(List<T> into, T view)
        {
            into.Add(view);
            if (into.Count > Limit) into.RemoveAt(0);
        }
    }
}
