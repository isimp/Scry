using System.Collections.Generic;

namespace Scry
{
    /// <summary>What the menu a right click opens on a mark offers.</summary>
    internal enum MapMenuChoice
    {
        Cross,
        Highlight,
        Plain,
        ZoomIn,
        OpenPage,
    }

    /// <summary>
    /// The small menu a right click opens on a mark on Valheim's large map, as a left click goes
    /// through its modes (<see cref="SpotModes"/>): the modes it does not stand in, zooming in on a
    /// merged mark, and last its page.
    /// </summary>
    internal static class MapMenu
    {
        /// <summary>What it offers on a mark of a mode standing for so many spots, in the order it stands them.</summary>
        public static List<MapMenuChoice> Of(SpotMode mode, int spots)
        {
            var choices = new List<MapMenuChoice>();
            if (mode != SpotMode.Crossed) choices.Add(MapMenuChoice.Cross);
            if (mode != SpotMode.Highlighted) choices.Add(MapMenuChoice.Highlight);
            if (mode != SpotMode.Plain) choices.Add(MapMenuChoice.Plain);
            if (spots > 1) choices.Add(MapMenuChoice.ZoomIn);
            choices.Add(MapMenuChoice.OpenPage);
            return choices;
        }

        /// <summary>The mode a choice gives; none for one that gives no mode.</summary>
        public static SpotMode? ModeOf(MapMenuChoice choice) =>
            choice == MapMenuChoice.Cross ? SpotMode.Crossed
            : choice == MapMenuChoice.Highlight ? SpotMode.Highlighted
            : choice == MapMenuChoice.Plain ? SpotMode.Plain
            : (SpotMode?)null;
    }
}
