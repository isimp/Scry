using System;
using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The copy of a collection a save takes in the frame, its text written later on a worker
    /// thread. The array is kept and filled again by the next save once nothing reads the last
    /// one, so saving every few seconds makes next to no garbage; a save that may still be read
    /// gets a fresh one.
    /// </summary>
    internal sealed class SaveCopy<T>
    {
        private T[] _items = Array.Empty<T>();

        /// <summary>What <paramref name="from"/> holds now, in the kept array where <paramref name="mayReuse"/>, else in a fresh one, kept for the next.</summary>
        public ArraySegment<T> Of(IEnumerable<T> from, bool mayReuse)
        {
            var items = mayReuse ? _items : new T[_items.Length];
            var at = 0;
            foreach (var item in from)
            {
                if (at == items.Length)
                {
                    // A little room beyond, so a collection growing slowly does not take a new array every save.
                    var grown = new T[at + at / 4 + 16];
                    Array.Copy(items, grown, at);
                    items = grown;
                }
                items[at++] = item;
            }
            _items = items;
            return new ArraySegment<T>(items, 0, at);
        }
    }
}
