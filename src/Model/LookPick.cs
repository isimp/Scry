using System.Collections.Generic;

namespace Scry
{
    /// <summary>
    /// The look the stage opens a page in where the one it would open in cannot show (under the
    /// compendium, a look of things not yet met or seen worn): for a creature whose looks are its
    /// gear, the first gear look that can show, as every creature that rolls a set spawns in one
    /// (<c>Humanoid.GiveDefaultItems</c>) and a Dvergr mage is never seen bare; the bare look only
    /// where no gear look can show; for other looks, the first that shows. And the order the look
    /// row offers them in (<see cref="InRow"/>).
    /// </summary>
    internal static class LookPick
    {
        /// <summary>The look to stand in, of those that can show (<paramref name="shown"/>), the one wanted where it can; 0 where none can.</summary>
        public static int Opening(IReadOnlyList<int> shown, int wanted, bool gear)
        {
            if (shown == null || shown.Count == 0) return 0;
            foreach (var look in shown) if (look == wanted) return wanted;
            if (gear) foreach (var look in shown) if (look > 0) return look;
            return shown[0];
        }

        /// <summary>The looks that can show in the order the look row offers them: a creature's in gear its sets first, the bare look after them; any other's as they come.</summary>
        public static List<int> InRow(IReadOnlyList<int> shown, bool gear)
        {
            var row = new List<int>(shown);
            if (gear && row.Remove(0)) row.Add(0);
            return row;
        }
    }
}
