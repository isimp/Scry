namespace Scry
{
    /// <summary>
    /// Where a kept kind's mark picker stands (<see cref="KeptMarks"/>): under what opened it, a
    /// line of the On the map list or the mark beside a Keep on the map, a gap apart; over it where
    /// the room ends first, and from the room's top where it fits neither way; its left at what
    /// opened it, moved left to stay inside the room, never left of the room's start.
    /// </summary>
    internal static class MarkPickerPlace
    {
        public static (float X, float Y) Of(float anchorX, float anchorTop, float anchorBottom, float width, float height, float roomX, float roomTop, float roomRight, float roomBottom, float gap)
        {
            var y = anchorBottom + gap;
            if (y + height > roomBottom)
            {
                var over = anchorTop - gap - height;
                y = over < roomTop ? roomTop : over;
            }
            var x = anchorX + width > roomRight ? roomRight - width : anchorX;
            if (x < roomX) x = roomX;
            return (x, y);
        }
    }
}
