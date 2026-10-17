namespace Scry
{
    /// <summary>
    /// Which room of a dungeon's example the mouse lights, in the plan box and on the stage
    /// alike: the one a way chip of the room card is hovered for, else the one under the mouse in
    /// the plan box or its trail, else the one under the mouse on the stage. That room alone is
    /// lit, not others of its name; the plan box frames it and names it, the way from the entrance
    /// is drawn to it, and the stage outlines it.
    /// </summary>
    internal static class RoomLight
    {
        /// <summary>The room lit, of those a card's way chip, the plan box and the stage have under the mouse; none.</summary>
        public static PlacedRoom Lit(PlacedRoom card, PlacedRoom box, PlacedRoom stage) => card ?? box ?? stage;

        /// <summary>Whether a room is the one lit: that very room, not another of its name.</summary>
        public static bool Lights(PlacedRoom lit, PlacedRoom room) => lit != null && ReferenceEquals(lit, room);

        /// <summary>The room the way from the entrance is drawn to: the one lit, else the one gone into.</summary>
        public static PlacedRoom RouteTo(PlacedRoom lit, PlacedRoom gone) => lit ?? gone;
    }
}
